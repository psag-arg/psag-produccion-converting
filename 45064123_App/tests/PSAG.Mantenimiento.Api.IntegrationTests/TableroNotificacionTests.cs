using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.TiempoReal;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-770 · Actualización en línea del tablero: al guardar, el UnitOfWork avisa qué turnos de producción cambiaron
/// (INotificadorTablero, acá un fake). La ingesta de cámara avisa CONTEO y PARADAS del turno de los minutos; la declaración
/// de una detención avisa DETENCIONES; un guardado que no toca turnos no avisa; si el aviso falla, el guardado sigue.
/// Y el notificador de SignalR manda el evento al grupo del turno.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class TableroNotificacionTests : CalidadTestBase
{
    private sealed class NotificadorFalso : INotificadorTablero
    {
        public List<CambioTablero> Recibidos { get; } = [];
        public bool Fallar { get; set; }

        public Task NotificarAsync(IReadOnlyCollection<CambioTablero> cambios, CancellationToken cancellationToken = default)
        {
            if (Fallar)
                throw new InvalidOperationException("se cayó SignalR");
            Recibidos.AddRange(cambios);
            return Task.CompletedTask;
        }
    }

    private readonly NotificadorFalso _notificador = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly ConteoCamaraService _conteo;
    private readonly DateTimeOffset _hora;   // 10:00 de planta hace 2 días (turno mañana)

    public TableroNotificacionTests()
    {
        _unitOfWork = new UnitOfWork(Context, _notificador);
        _conteo = new ConteoCamaraService(_unitOfWork, NuevoTurnosProduccion(_unitOfWork), ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        _hora = new DateTimeOffset(hoy.AddDays(-2).AddHours(10), TimeSpan.FromHours(-3));

        var camara = Context.Camaras.Single(c => c.CamaraId == CamTango.CamaraId);
        camara.CreaDetenciones = true;
        camara.UmbralDetencionSegundos = 300;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private long TurnoDeLaHora() =>
        Context.TurnosProduccion.AsNoTracking()
            .Single(t => t.LineaId == Tango2.LineaId && t.TurnoId == TurnoManana.TurnoId && t.FechaProduccion == DateOnly.FromDateTime(_hora.DateTime))
            .TurnoProduccionId;

    [Fact]
    public async Task IngestaDeConteo_AvisaAlTurnoDeLosMinutos()
    {
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Minutos = [new MinutoCamaraIngesta { Minuto = _hora, Carril = 1, Cantidad = 10, SegundosMarcha = 60, Estado = "MARCHA" }],
            Paradas = [new ParadaCamaraIngesta { IdentificadorExterno = "TEST-T770-1", Inicio = _hora.AddMinutes(5), Fin = _hora.AddMinutes(5).AddSeconds(40), Tipo = "PARADA", Carriles = [1] }]
        });

        Assert.True(r.IsSuccess, r.ErrorMessage);
        var turno = TurnoDeLaHora();
        Assert.Contains(new CambioTablero(turno, MotivoCambioTablero.Conteo), _notificador.Recibidos);
        Assert.Contains(new CambioTablero(turno, MotivoCambioTablero.Paradas), _notificador.Recibidos);
        Assert.All(_notificador.Recibidos, c => Assert.Equal(turno, c.TurnoProduccionId));
    }

    [Fact]
    public async Task DeclararUnaDetencion_AvisaDetenciones_YSiElAvisoFallaElGuardadoSigue()
    {
        // La parada de 7 min crea la detención automática (pendiente de declaración)
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Paradas = [new ParadaCamaraIngesta { IdentificadorExterno = "TEST-T770-2", Inicio = _hora, Fin = _hora.AddMinutes(7), Tipo = "PARADA", Carriles = [1] }]
        });
        Assert.Equal(1, r.Value!.DetencionesCreadas);
        var turno = TurnoDeLaHora();
        Assert.Contains(new CambioTablero(turno, MotivoCambioTablero.Detenciones), _notificador.Recibidos);

        _notificador.Recibidos.Clear();
        _notificador.Fallar = true;
        var d = await Context.Detenciones.AsNoTracking().SingleAsync(x => x.OrigenRegistro == OrigenRegistroDetencion.CAMARA);
        var declaracion = new DeclaracionDetencionService(_unitOfWork, new DetencionService(_unitOfWork, NullLogger<DetencionService>.Instance),
            ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance);
        var ok = await declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Se cortó el papel", HoraArranque = new TimeOnly(10, 7), RowVersion = d.RowVersion
        }, Operario, null);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
        Context.ChangeTracker.Clear();
        Assert.Equal(EstadoDetencion.REGISTRADA, (await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId)).EstadoActual);

        // Sin falla, la declaración avisa DETENCIONES del turno
        _notificador.Fallar = false;
        var d2 = await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId);
        d2.Descripcion = "Se cortó el papel (corregido)";
        await _unitOfWork.SaveChangesAsync();
        Assert.Equal([new CambioTablero(turno, MotivoCambioTablero.Detenciones)], _notificador.Recibidos);
    }

    [Fact]
    public async Task GuardadoQueNoTocaTurnos_NoAvisa()
    {
        var linea = await Context.Lineas.SingleAsync(l => l.LineaId == Futura2.LineaId);
        linea.Nombre = "Futura 2 bis";
        await _unitOfWork.SaveChangesAsync();
        Assert.Empty(_notificador.Recibidos);
    }

    // ---------- Notificador de SignalR con un IHubContext falso ----------

    private sealed class ProxyFalso : IClientProxy
    {
        public List<(string Metodo, object?[] Args)> Enviados { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Enviados.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class ClientesFalsos : IHubClients
    {
        public Dictionary<string, ProxyFalso> Grupos { get; } = [];
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => Grupos.TryGetValue(groupName, out var p) ? p : Grupos[groupName] = new ProxyFalso();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class HubFalso : IHubContext<TableroHub>
    {
        public ClientesFalsos Falsos { get; } = new();
        public IHubClients Clients => Falsos;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    [Fact]
    public async Task NotificadorSignalR_MandaUnEventoPorTurnoAlGrupoDelTurno()
    {
        var hub = new HubFalso();
        var notificador = new NotificadorTableroSignalR(hub, NullLogger<NotificadorTableroSignalR>.Instance);

        await notificador.NotificarAsync([
            new CambioTablero(7, MotivoCambioTablero.Conteo),
            new CambioTablero(7, MotivoCambioTablero.Paradas),
            new CambioTablero(9, MotivoCambioTablero.Detenciones)
        ]);

        Assert.Equal(["turno-7", "turno-9"], hub.Falsos.Grupos.Keys.OrderBy(k => k));
        var (metodo, args) = Assert.Single(hub.Falsos.Grupos["turno-7"].Enviados);
        Assert.Equal(TableroHub.Evento, metodo);
        Assert.Equal(7L, args[0]);
        Assert.Equal("CONTEO,PARADAS", args[1]);
        Assert.Equal(9L, Assert.Single(hub.Falsos.Grupos["turno-9"].Enviados).Args[0]);
    }
}
