using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-783: el operario confirma en el login que es el maquinista del turno; el supervisor lo corrige. Las detenciones
/// automáticas toman al confirmado y las pendientes del turno se reasignan. Reloj fijo: hace 2 días a las 10:50
/// (turno mañana de 06:00 a 14:00).
/// </summary>
public class MaquinistaTurnoTests : CalidadTestBase
{
    private const string OperarioRotativo = "oper-2";
    private const string OperarioTarde = "oper-3";
    private static readonly string[] RolOperario = [RolesSistema.Operario];
    private static readonly string[] RolSupervisor = [RolesSistema.Supervisor];

    private readonly MaquinistaTurnoService _servicio;
    private readonly ConteoCamaraService _conteo;
    private readonly DateTimeOffset _ahora;
    private readonly DateOnly _dia;
    private UsuarioLinea _manana = null!;     // oper-1: Tango 2, turno mañana
    private UsuarioLinea _rotativo = null!;   // oper-2: Tango 2, sin turno fijo
    private UsuarioLinea _tarde = null!;      // oper-3: Tango 2, turno tarde
    private UsuarioLinea _futura = null!;     // oper-futura: Futura 2, turno mañana

    public MaquinistaTurnoTests()
    {
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        _ahora = new DateTimeOffset(hoy.AddDays(-2).AddHours(10).AddMinutes(50), TimeSpan.FromHours(-3));
        _dia = DateOnly.FromDateTime(_ahora.DateTime);
        var unitOfWork = new UnitOfWork(Context);
        _servicio = new MaquinistaTurnoService(unitOfWork, ZonaPlanta, NullLogger<MaquinistaTurnoService>.Instance, () => _ahora);
        _conteo = new ConteoCamaraService(unitOfWork, ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);

        var camara = Context.PuntosControlCalidad.Single(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        camara.CreaDetenciones = true;
        camara.MinutosUmbralDetencion = 5;
        var asignada = DateTimeOffset.UtcNow.AddDays(-30);
        _manana = new UsuarioLinea { UsuarioId = Operario, LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin };
        _rotativo = new UsuarioLinea { UsuarioId = OperarioRotativo, LineaId = Tango2.LineaId, TurnoId = null, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin };
        _tarde = new UsuarioLinea { UsuarioId = OperarioTarde, LineaId = Tango2.LineaId, TurnoId = TurnoTarde.TurnoId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin };
        _futura = new UsuarioLinea { UsuarioId = "oper-futura", LineaId = Futura2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin };
        Context.AddRange(_manana, _rotativo, _tarde, _futura);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private DateTimeOffset Hora(int hora, int minuto) =>
        new(_ahora.Date.AddHours(hora).AddMinutes(minuto), TimeSpan.FromHours(-3));

    private Task<Application.Common.Result<IngestaConteoCamaraRespuesta>> ParadaDe6Minutos(DateTimeOffset inicio) =>
        _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            ParadasEnCurso = [new ParadaEnCursoIngesta { IdentificadorExterno = $"TEST-CAM-T2-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z", Inicio = inicio, Hasta = inicio.AddMinutes(6), Tipo = "PARADA" }]
        });

    private async Task<List<Detencion>> Automaticas()
    {
        Context.ChangeTracker.Clear();
        return await Context.Detenciones.IgnoreQueryFilters().Include(d => d.HistorialEstados)
            .Where(d => d.OrigenRegistro == OrigenRegistroDetencion.CAMARA).OrderBy(d => d.DetencionId).ToListAsync();
    }

    private async Task<List<MaquinistaTurno>> Registros()
    {
        Context.ChangeTracker.Clear();
        return await Context.MaquinistasTurno.OrderBy(m => m.MaquinistaTurnoId).ToListAsync();
    }

    [Fact]
    public async Task Propuesta_OperarioConAsignacionDelTurno_LePreguntaPorSuLinea()
    {
        var p = await _servicio.ObtenerPropuestaAsync(Operario, RolOperario);

        Assert.True(p.Preguntar);
        Assert.Null(p.Aviso);
        Assert.Equal(TurnoManana.TurnoId, p.TurnoId);
        Assert.Equal(_dia, p.FechaTurno);
        var o = Assert.Single(p.Opciones);
        Assert.Equal(Tango2.LineaId, o.LineaId);
        Assert.Equal(_manana.UsuarioLineaId, o.UsuarioLineaId);
        Assert.False(o.YaConfirmado);
        Assert.Null(o.MaquinistaActualUsuarioId);
    }

    [Fact]
    public async Task Propuesta_QuienNoEsOperario_NoSeLePregunta()
    {
        var p = await _servicio.ObtenerPropuestaAsync(Operario, RolSupervisor);
        Assert.False(p.Preguntar);
        Assert.Empty(p.Opciones);
    }

    [Fact]
    public async Task Propuesta_SinRelacionParaElTurno_AvisaYNoOfreceLineas()
    {
        var p = await _servicio.ObtenerPropuestaAsync(OperarioTarde, RolOperario);   // asignado a la tarde; son las 10:50

        Assert.True(p.Preguntar);
        Assert.Empty(p.Opciones);
        Assert.Contains("no quedás como maquinista", p.Aviso);
    }

    [Fact]
    public async Task Confirmar_GuardaElRegistroDelTurnoYUnaSegundaVezNoLoDuplica()
    {
        var r = await _servicio.ConfirmarAsync(Operario, "Operario Uno", Tango2.LineaId);
        var otra = await _servicio.ConfirmarAsync(Operario, "Operario Uno", Tango2.LineaId);

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.False(r.Value!.YaEstaba);
        Assert.True(otra.Value!.YaEstaba);
        var m = Assert.Single(await Registros());
        Assert.Equal(Tango2.LineaId, m.LineaId);
        Assert.Equal(TurnoManana.TurnoId, m.TurnoId);
        Assert.Equal(_dia, m.FechaTurno);
        Assert.Equal(_manana.UsuarioLineaId, m.UsuarioLineaId);
        Assert.Equal(Operario, m.UsuarioId);
        Assert.Equal(OrigenMaquinistaTurno.LOGIN, m.Origen);
        Assert.Equal(Operario, m.RegistradoPorUsuarioId);
        // Ya confirmado: no se le vuelve a preguntar.
        Assert.False((await _servicio.ObtenerPropuestaAsync(Operario, RolOperario)).Preguntar);
    }

    [Fact]
    public async Task Confirmar_SinRelacion_NoLoAsignaYExplicaQuienLoCorrige()
    {
        var r = await _servicio.ConfirmarAsync(OperarioTarde, "Operario Tarde", Tango2.LineaId);
        var otraLinea = await _servicio.ConfirmarAsync(Operario, "Operario Uno", Futura2.LineaId);

        Assert.False(r.IsSuccess);
        Assert.Contains("supervisor", r.ErrorMessage);
        Assert.False(otraLinea.IsSuccess);
        Assert.Empty(await Registros());
    }

    [Fact]
    public async Task Confirmar_ConOtroYaConfirmado_LoReemplazaYQuedaElHistorial()
    {
        await _servicio.ConfirmarAsync(Operario, "Operario Uno", Tango2.LineaId);

        var antes = await _servicio.ObtenerPropuestaAsync(OperarioRotativo, RolOperario);
        var r = await _servicio.ConfirmarAsync(OperarioRotativo, "Rotativo", Tango2.LineaId);

        var o = Assert.Single(antes.Opciones);
        Assert.True(o.SinTurnoFijo);
        Assert.Equal(Operario, o.MaquinistaActualUsuarioId);
        Assert.Equal("LOGIN", o.MaquinistaActualOrigen);
        Assert.Equal(Operario, r.Value!.ReemplazadoUsuarioId);
        Assert.Equal([Operario, OperarioRotativo], (await Registros()).Select(m => m.UsuarioId));
        // Al primero se le muestra quién quedó.
        Assert.Equal(OperarioRotativo, Assert.Single((await _servicio.ObtenerPropuestaAsync(Operario, RolOperario)).Opciones).MaquinistaActualUsuarioId);
    }

    [Fact]
    public async Task DetencionAutomatica_TomaAlConfirmadoAunqueLaAsignacionDigaOtro()
    {
        await _servicio.ConfirmarAsync(OperarioRotativo, "Rotativo", Tango2.LineaId);

        var r = await ParadaDe6Minutos(Hora(10, 43));

        Assert.Equal(1, r.Value!.DetencionesCreadas);
        var d = Assert.Single(await Automaticas());
        Assert.Equal(_rotativo.UsuarioLineaId, d.UsuarioLineaId);   // por asignación hubiera sido oper-1 (turno mañana)
        Assert.Contains("login", Assert.Single(d.HistorialEstados).Observacion);
    }

    [Fact]
    public async Task Confirmar_ReasignaLasPendientesDelTurno_NoLasDeclaradasNiLasDeOtroTurno()
    {
        await ParadaDe6Minutos(Hora(10, 43));   // mañana: por asignación, oper-1
        await ParadaDe6Minutos(Hora(15, 0));    // tarde: por asignación, oper-3
        var declarada = new Detencion
        {
            Codigo = "DET-TEST-DECLARADA", LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, UsuarioLineaId = _manana.UsuarioLineaId,
            Fecha = _dia, FechaHoraParada = Hora(9, 0), OrigenTiempo = OrigenTiempo.DECLARADO, MinutosEfectivos = 10,
            Descripcion = "Ya declarada", EstadoActual = EstadoDetencion.REGISTRADA, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        Context.Add(declarada);
        Context.SaveChanges();

        var r = await _servicio.ConfirmarAsync(OperarioRotativo, "Rotativo", Tango2.LineaId);

        Assert.Equal(1, r.Value!.DetencionesReasignadas);
        var detenciones = await Automaticas();
        var manana = detenciones.Single(d => d.TurnoId == TurnoManana.TurnoId);
        Assert.Equal(_rotativo.UsuarioLineaId, manana.UsuarioLineaId);
        var h = manana.HistorialEstados.OrderBy(x => x.DetencionEstadoHistorialId).Last();
        Assert.Equal(MaquinistaTurnoService.OrigenAccionHistorial, h.OrigenAccion);
        Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, h.EstadoNuevo);
        Assert.Contains("Rotativo", h.Observacion);
        Assert.Equal(_tarde.UsuarioLineaId, detenciones.Single(d => d.TurnoId == TurnoTarde.TurnoId).UsuarioLineaId);
        Assert.Equal(_manana.UsuarioLineaId, (await Context.Detenciones.AsNoTracking().SingleAsync(d => d.Codigo == "DET-TEST-DECLARADA")).UsuarioLineaId);
    }

    [Fact]
    public async Task Corregir_ElSupervisorCambiaElMaquinistaConMotivoYReasignaLasPendientes()
    {
        await _servicio.ConfirmarAsync(Operario, "Operario Uno", Tango2.LineaId);
        await ParadaDe6Minutos(Hora(10, 43));

        var r = await _servicio.CorregirAsync(new CorregirMaquinistaTurnoRequest
        {
            LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaTurno = _dia,
            UsuarioLineaId = _rotativo.UsuarioLineaId, Observacion = "  Oper-1 confirmó por error, cubre el rotativo  "
        }, Supervisor, RolSupervisor, [], "Rotativo");

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(1, r.Value!.DetencionesReasignadas);
        var vigente = (await Registros()).Last();
        Assert.Equal(OrigenMaquinistaTurno.SUPERVISOR, vigente.Origen);
        Assert.Equal(Supervisor, vigente.RegistradoPorUsuarioId);
        Assert.Equal(OperarioRotativo, vigente.UsuarioId);
        Assert.Equal("Oper-1 confirmó por error, cubre el rotativo", vigente.Observacion);
        Assert.Equal(_rotativo.UsuarioLineaId, Assert.Single(await Automaticas()).UsuarioLineaId);
        Assert.Equal(2, (await Registros()).Count);   // el registro del login sigue ahí
    }

    [Fact]
    public async Task Corregir_Validaciones()
    {
        CorregirMaquinistaTurnoRequest Pedido(int usuarioLineaId, string? motivo = "Cubre", int? lineaId = null, DateOnly? fecha = null) => new()
        {
            LineaId = lineaId ?? Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaTurno = fecha ?? _dia,
            UsuarioLineaId = usuarioLineaId, Observacion = motivo
        };

        var sinMotivo = await _servicio.CorregirAsync(Pedido(_rotativo.UsuarioLineaId, " "), Supervisor, RolSupervisor, [], "x");
        var otraLinea = await _servicio.CorregirAsync(Pedido(_futura.UsuarioLineaId), Supervisor, RolSupervisor, [], "x");
        var operario = await _servicio.CorregirAsync(Pedido(_rotativo.UsuarioLineaId), Operario, RolOperario, [Tango2.LineaId], "x");
        var lineaAjena = await _servicio.CorregirAsync(Pedido(_rotativo.UsuarioLineaId), Supervisor, RolSupervisor, [Futura2.LineaId], "x");
        var futuro = await _servicio.CorregirAsync(Pedido(_rotativo.UsuarioLineaId, fecha: DiaPlanta(-1)), Supervisor, RolSupervisor, [], "x");

        Assert.Contains("motivo", sinMotivo.ErrorMessage);
        Assert.Contains("no está asignado a esa línea", otraLinea.ErrorMessage);
        Assert.Equal("FORBIDDEN", operario.ErrorCode);
        Assert.Equal("FORBIDDEN", lineaAjena.ErrorCode);
        Assert.Equal("VALIDATION", futuro.ErrorCode);
        Assert.Empty(await Registros());
    }

    [Fact]
    public async Task ObtenerDia_UnaFilaPorLineaYTurno_ConVigenteHistorialYLoQueDiceLaAsignacion()
    {
        await _servicio.ConfirmarAsync(Operario, "Operario Uno", Tango2.LineaId);

        var dia = await _servicio.ObtenerDiaAsync(_dia, RolSupervisor, []);

        Assert.Equal(4, dia.Filas.Count);   // 2 líneas x 2 turnos
        var tangoManana = dia.Filas.Single(f => f.LineaId == Tango2.LineaId && f.TurnoId == TurnoManana.TurnoId);
        Assert.True(tangoManana.EnCurso);
        Assert.Equal(Operario, tangoManana.Vigente!.UsuarioId);
        Assert.Equal("LOGIN", tangoManana.Vigente.Origen);
        Assert.Single(tangoManana.Historial);
        Assert.Equal(3, tangoManana.Asignaciones.Count);
        Assert.Equal(_manana.UsuarioLineaId, tangoManana.Asignaciones[0].UsuarioLineaId);   // primero las del turno
        var tangoTarde = dia.Filas.Single(f => f.LineaId == Tango2.LineaId && f.TurnoId == TurnoTarde.TurnoId);
        Assert.Null(tangoTarde.Vigente);
        Assert.False(tangoTarde.EnCurso);
        Assert.Equal(OperarioTarde, tangoTarde.SegunAsignacionUsuarioId);
        // Supervisor de Futura: sólo ve su línea.
        Assert.All((await _servicio.ObtenerDiaAsync(_dia, RolSupervisor, [Futura2.LineaId])).Filas, f => Assert.Equal(Futura2.LineaId, f.LineaId));
    }
}
