using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Turno automático (pedido de Emiliano, 24/11; docs/diseno/T-798 "Turno de máquina vs. sesión del operario"), contra
/// SQLite o SQL Server, con reloj fijo. El turno es de la MÁQUINA: el job lo abre a la hora de inicio si la línea está
/// programada (sin operario, producto heredado supuesto) y lo cierra a la hora de fin exacta; el operario tiene su
/// tolerancia para declarar lo suyo; "lo pisa" el que entra al turno siguiente; las paradas que cruzan el cambio de turno
/// se cortan en dos detenciones vinculadas; el microcorte se decide por la duración total.
/// Turnos T1 05–13, T2 13–21, T3 21–05; planta UTC-3. Tango 2 con SH 30 y SH 50; Tango 1 sin productos con objetivo;
/// Futura 2 sin programación (no trabaja).
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class TurnoAutomaticoTests : IDisposable
{
    private const string Admin = "admin-test";
    private const string Oper1 = "oper-1";
    private const string Oper2 = "oper-2";
    private const string OperTango1 = "oper-tango1";
    private const string Supervisor = "sup-1";
    private static readonly string[] RolOperario = [RolesSistema.Operario];
    private static readonly string[] RolSupervisor = [RolesSistema.Supervisor];

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly BaseDePrueba _base;
    private readonly MantenimientoDbContext _context;
    private readonly TurnoProduccionService _turnos;
    private readonly ConteoCamaraService _conteo;
    private readonly DeclaracionDetencionService _declaracion;
    private readonly IndicadoresService _indicadores;
    private readonly TableroService _tablero;
    private DateTimeOffset _reloj;

    private readonly Linea _tango2;
    private readonly Linea _tango1;
    private readonly Linea _futura2;
    private readonly Turno _t1;
    private readonly Turno _t2;
    private readonly Turno _t3;
    private readonly ProductoMaestro _sh30;
    private readonly ProductoMaestro _sh50;
    private readonly Camara _camara;

    public TurnoAutomaticoTests()
    {
        _base = BaseDePrueba.Crear();
        _context = _base.Context;

        _tango2 = new Linea { Codigo = "T2", Nombre = "TANGO 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _tango1 = new Linea { Codigo = "T1", Nombre = "TANGO 1", Orden = 2, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _futura2 = new Linea { Codigo = "F2", Nombre = "FUTURA 2", Orden = 3, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _t1 = Turno("T1", "Turno 1", 5, 13, 1);
        _t2 = Turno("T2", "Turno 2", 13, 21, 2);
        _t3 = Turno("T3", "Turno 3", 21, 5, 3);
        _sh30 = Producto("HIGIENICO 30 MTS", 30, 1);
        _sh50 = Producto("HIGIENICO 50 MTS", 50, 2);
        _context.AddRange(_tango2, _tango1, _futura2, _t1, _t2, _t3, _sh30, _sh50);
        _context.SaveChanges();

        var desde = new DateOnly(2026, 11, 1);
        var asignada = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        _camara = new Camara
        {
            LineaId = _tango2.LineaId, Codigo = "TEST-CAM-AUTO", Nombre = "Cámara Tango 2", Funcion = FuncionCamara.Conteo,
            Magnitud = MagnitudProduccion.Mangas, MetodoDeteccion = MetodoDeteccionCamara.Pasos, CantidadCarriles = 1,
            DetectaParadas = true, CreaDetenciones = true, UmbralDetencionSegundos = 300, Habilitada = true,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.AddRange(
            new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh30.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 11200, BolsonesObjetivoTurno = 6500 },
            new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh50.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 6720, BolsonesObjetivoTurno = 4700 },
            new OperadorLinea { UsuarioId = Oper1, LineaId = _tango2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin },
            new OperadorLinea { UsuarioId = Oper2, LineaId = _tango2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin },
            new OperadorLinea { UsuarioId = OperTango1, LineaId = _tango1.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin },
            // Futura 2: programación vacía desde el 01/11 (no trabaja: no se abre sola).
            new CalendarioLinea { LineaId = _futura2.LineaId, VigenteDesde = desde, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin },
            _camara);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 6);
        var unitOfWork = new UnitOfWork(_context);
        _turnos = new TurnoProduccionService(unitOfWork, ZonaPlanta, new TurnosProduccionOpciones(), NullLogger<TurnoProduccionService>.Instance, () => _reloj);
        _conteo = new ConteoCamaraService(unitOfWork, _turnos, ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        _declaracion = new DeclaracionDetencionService(unitOfWork, new DetencionService(unitOfWork, NullLogger<DetencionService>.Instance),
            ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance, () => _reloj);
        _indicadores = new IndicadoresService(unitOfWork, ZonaPlanta, () => _reloj);
        _tablero = new TableroService(unitOfWork, _indicadores, ZonaPlanta, () => _reloj);
    }

    public void Dispose()
    {
        _context.Dispose();
        _base.Dispose();
    }

    private static Turno Turno(string codigo, string nombre, int desde, int hasta, int orden) => new()
    {
        Codigo = codigo, Nombre = nombre, HoraInicio = new TimeOnly(desde, 0), HoraFin = new TimeOnly(hasta, 0), Orden = orden,
        CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    private static ProductoMaestro Producto(string codigo, decimal? metros, int orden) => new()
    {
        Codigo = codigo, Nombre = codigo, MetrosPorManga = metros, Orden = orden, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    /// <summary>Hora de pared de la planta (UTC-3) de noviembre de 2026.</summary>
    private static DateTimeOffset H(int dia, int hora, int minuto = 0) => new(2026, 11, dia, hora, minuto, 0, TimeSpan.FromHours(-3));

    private int[] LineasDe(string usuario) => usuario == OperTango1 ? [_tango1.LineaId] : [_tango2.LineaId];

    private async Task<TurnoProduccion> TurnoAsync(int lineaId, Turno turno, int dia)
    {
        _context.ChangeTracker.Clear();
        return await _context.TurnosProduccion.Include(t => t.Operadores).Include(t => t.Productos)
            .SingleAsync(t => t.LineaId == lineaId && t.TurnoId == turno.TurnoId && t.FechaProduccion == new DateOnly(2026, 11, dia));
    }

    private async Task<Application.Common.Result<AperturaTurnoProduccionDto>> EntrarAsync(string usuario, int? productoId, int lineaId = 0, bool relevar = true) =>
        await _turnos.AbrirAsync(lineaId == 0 ? _tango2.LineaId : lineaId, usuario, OrigenAperturaTurno.LOGIN, productoId, relevar, RolOperario, LineasDe(usuario));

    private async Task<List<Detencion>> DetencionesAsync()
    {
        _context.ChangeTracker.Clear();
        return (await _context.Detenciones.IgnoreQueryFilters().Include(d => d.TurnoProduccion).ToListAsync())
            .OrderBy(d => d.FechaHoraParada).ToList();
    }

    private static string IdParada(DateTimeOffset inicio) => $"TEST-CAM-AUTO-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z";

    private Task<Application.Common.Result<IngestaConteoCamaraRespuesta>> ParadaCerradaAsync(DateTimeOffset inicio, DateTimeOffset fin) =>
        _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            Camara = _camara.Codigo,
            Paradas = [new ParadaCamaraIngesta { IdentificadorExterno = IdParada(inicio), Inicio = inicio, Fin = fin, Tipo = "PARADA", Carriles = [1] }]
        });

    private Task<Application.Common.Result<IngestaConteoCamaraRespuesta>> ParadaEnCursoAsync(DateTimeOffset inicio, DateTimeOffset hasta) =>
        _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            Camara = _camara.Codigo,
            ParadasEnCurso = [new ParadaEnCursoIngesta { IdentificadorExterno = IdParada(inicio), Inicio = inicio, Hasta = hasta, Tipo = "SIN_MANGAS" }]
        });

    // ===================== A. Job: apertura automática =====================

    [Fact]
    public async Task Job_AbreALas5SinOperario_ConProductoHeredadoSupuesto_YNoAbreLaLineaNoProgramada()
    {
        // T3 del 18/11 con SH 50 (lo tomó el operario a las 21:10)
        _reloj = H(18, 21, 10);
        Assert.True((await EntrarAsync(Oper1, _sh50.ProductoMaestroId)).IsSuccess);

        _reloj = H(19, 5, 0);
        var r = await _turnos.ProcesarAsync(_reloj);
        Assert.Equal(1, r.Cerrados);   // el T3 del 18/11, a las 05:00 exactas

        var t1 = await TurnoAsync(_tango2.LineaId, _t1, 19);
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, t1.Estado);
        Assert.Equal(OrigenAperturaTurno.SISTEMA, t1.OrigenApertura);
        Assert.Equal(H(19, 5), t1.InicioReal);
        Assert.Empty(t1.Operadores);                                     // "sin operador"
        var heredado = Assert.Single(t1.Productos);
        Assert.Equal(OrigenTramoProducto.HEREDADO, heredado.Origen);
        Assert.True(heredado.SinConfirmar);
        Assert.Equal(_sh50.ProductoMaestroId, heredado.ProductoMaestroId);
        Assert.Equal(6720, heredado.MangasObjetivoTurno);                // snapshot del parámetro vigente al 19/11
        Assert.Equal(H(19, 5), heredado.Desde);
        Assert.Null(heredado.Hasta);

        var t3 = await TurnoAsync(_tango2.LineaId, _t3, 18);
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, t3.Estado);
        Assert.Equal(H(19, 5), t3.FinReal);

        // Tango 1 (programada, sin productos con objetivo): se abre igual, sin producto y con alerta.
        var tango1 = await TurnoAsync(_tango1.LineaId, _t1, 19);
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, tango1.Estado);
        Assert.Empty(tango1.Productos);
        // Futura 2 (no programada): no se abre sola.
        Assert.False(await _context.TurnosProduccion.AnyAsync(t => t.LineaId == _futura2.LineaId));

        // Idempotente
        var otraVez = await _turnos.ProcesarAsync(H(19, 5, 1));
        Assert.Equal((0, 0, 0), (otraVez.Cerrados, otraVez.Abiertos, otraVez.CreadosTerminados));
        Assert.Single(_context.TurnosProduccion.Where(t => t.LineaId == _tango2.LineaId && t.Estado == EstadoTurnoProduccion.ABIERTO));

        var dto = (await _turnos.ObtenerActualAsync(_tango2.LineaId))!;
        Assert.True(dto.SinOperador);
        Assert.True(dto.ProductoSinConfirmar);
    }

    [Fact]
    public async Task Job_CierraA13ExactasYAbreElT2_QueHeredaElProductoDelT1()
    {
        _reloj = H(19, 6);
        Assert.True((await EntrarAsync(Oper1, _sh30.ProductoMaestroId)).IsSuccess);

        _reloj = H(19, 12, 59);
        Assert.Equal(0, (await _turnos.ProcesarAsync(_reloj)).Cerrados);
        _reloj = H(19, 13, 0);
        var r = await _turnos.ProcesarAsync(_reloj);
        Assert.Equal(2, r.Cerrados);   // el T1 de Tango 2 y el de Tango 1 (que el job abrió a las 12:59)
        Assert.Equal(2, r.Abiertos);   // los T2 de las dos

        var t1 = await TurnoAsync(_tango2.LineaId, _t1, 19);
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, t1.Estado);
        Assert.Equal(H(19, 13), t1.FinReal);
        Assert.Null(Assert.Single(t1.Operadores).Hasta);                  // la sesión del operario sigue (tolerancia)
        var t2 = await TurnoAsync(_tango2.LineaId, _t2, 19);
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, t2.Estado);
        Assert.Equal(H(19, 13), t2.InicioReal);
        var heredado = Assert.Single(t2.Productos);
        Assert.Equal((OrigenTramoProducto.HEREDADO, _sh30.ProductoMaestroId), (heredado.Origen, heredado.ProductoMaestroId));

        // A las 13:30 vence la tolerancia (30 min por defecto): el job cierra la sesión en las 13:00.
        _reloj = H(19, 13, 30);
        Assert.Equal(1, (await _turnos.ProcesarAsync(_reloj)).SesionesVencidas);
        Assert.Equal(H(19, 13), Assert.Single((await TurnoAsync(_tango2.LineaId, _t1, 19)).Operadores).Hasta);
    }

    [Fact]
    public async Task Job_ParadoYRetomado_AbreSoloElVigente_YLosQueFaltanSeCreanTerminados()
    {
        _reloj = H(19, 6);
        Assert.True((await EntrarAsync(Oper1, _sh30.ProductoMaestroId)).IsSuccess);

        // El job no corrió de 06:00 a 22:00
        _reloj = H(19, 22);
        var r = await _turnos.ProcesarAsync(_reloj);
        Assert.Equal(1, r.Cerrados);              // el T1 de Tango 2
        Assert.Equal(2, r.Abiertos);              // los T3 de Tango 2 y Tango 1 (sólo el vigente)
        Assert.True(r.CreadosTerminados >= 2);    // los T2 de las dos (y lo que faltaba de Tango 1), ya terminados

        Assert.Equal(H(19, 13), (await TurnoAsync(_tango2.LineaId, _t1, 19)).FinReal);
        var t2 = await TurnoAsync(_tango2.LineaId, _t2, 19);
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, t2.Estado);
        Assert.Equal((H(19, 13), H(19, 21)), (t2.InicioReal!.Value, t2.FinReal!.Value));
        Assert.Equal(H(19, 21), Assert.Single(t2.Productos).Hasta);       // heredado del T1, cerrado con el turno
        var t3 = await TurnoAsync(_tango2.LineaId, _t3, 19);
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, t3.Estado);
        Assert.Equal(H(19, 21), t3.InicioReal);
        Assert.Equal(_sh30.ProductoMaestroId, Assert.Single(t3.Productos).ProductoMaestroId);
        Assert.Single(_context.TurnosProduccion.Where(t => t.LineaId == _tango2.LineaId && t.Estado == EstadoTurnoProduccion.ABIERTO));
    }

    [Fact]
    public async Task Heredado_SinParametroVigente_NoHereda()
    {
        _reloj = H(18, 21, 10);
        Assert.True((await EntrarAsync(Oper1, _sh50.ProductoMaestroId)).IsSuccess);
        // Desde el 19/11 el SH 50 no está habilitado en Tango 2
        _context.Add(new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh50.ProductoMaestroId, VigenteDesde = new DateOnly(2026, 11, 19), Habilitado = false });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 5);
        await _turnos.ProcesarAsync(_reloj);
        Assert.Empty((await TurnoAsync(_tango2.LineaId, _t1, 19)).Productos);
    }

    // ===================== C. Operario: producto obligatorio =====================

    [Fact]
    public async Task Operario_ConfirmaElHeredado_SinTramoNuevo_OCambia_ConTramoNuevo()
    {
        _reloj = H(18, 21, 10);
        await EntrarAsync(Oper1, _sh50.ProductoMaestroId);
        _reloj = H(19, 5);
        await _turnos.ProcesarAsync(_reloj);

        // Sin producto: 400 PRODUCTO_REQUERIDO
        _reloj = H(19, 5, 10);
        var sin = await EntrarAsync(Oper2, null);
        Assert.Equal(TurnoProduccionService.CodigoProductoRequerido, sin.ErrorCode);

        // La propuesta le sugiere el heredado (sin confirmar)
        var propuesta = await _turnos.ObtenerPropuestaAsync(Oper2, RolOperario, true);
        var opcion = propuesta.Lineas.Single(l => l.LineaId == _tango2.LineaId);
        Assert.Equal(_sh50.ProductoMaestroId, opcion.ProductoActualId);
        Assert.True(opcion.ProductoSinConfirmar);
        Assert.True(opcion.SinOperador);

        var confirma = await EntrarAsync(Oper2, _sh50.ProductoMaestroId);
        Assert.True(confirma.IsSuccess, confirma.ErrorMessage);
        Assert.True(confirma.Value!.ProductoConfirmado);
        var t1 = await TurnoAsync(_tango2.LineaId, _t1, 19);
        var tramo = Assert.Single(t1.Productos);
        Assert.False(tramo.SinConfirmar);
        Assert.Equal(Oper2, tramo.ConfirmadoPorUsuarioId);

        // Otro operario lo releva y cambia: tramo nuevo
        _reloj = H(19, 9);
        var cambia = await EntrarAsync(Oper1, _sh30.ProductoMaestroId);
        Assert.True(cambia.IsSuccess, cambia.ErrorMessage);
        var tramos = (await TurnoAsync(_tango2.LineaId, _t1, 19)).Productos.OrderBy(p => p.Desde).ToList();
        Assert.Equal(2, tramos.Count);
        Assert.Equal(H(19, 9), tramos[0].Hasta);
        Assert.Equal((OrigenTramoProducto.CAMBIO, H(19, 9)), (tramos[1].Origen, tramos[1].Desde));
    }

    [Fact]
    public async Task Operario_LineaSinObjetivos_EntraIgualConAlerta()
    {
        _reloj = H(19, 6);
        var r = await EntrarAsync(OperTango1, null, _tango1.LineaId);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Contains("no tiene objetivos cargados", r.Value!.Alerta);
        Assert.True(r.Value.Turno!.SinObjetivos);
        Assert.Contains(r.Value.Turno.Alertas, a => a.Contains("TANGO 1 no tiene objetivos cargados"));
    }

    // ===================== B. Cierre del turno de máquina vs. tolerancia del operario =====================

    [Fact]
    public async Task LoPisa_ElT1VencidoSeCierraALas13_YElOperarioDelT1ConservaSuTolerancia()
    {
        _reloj = H(19, 6);
        await EntrarAsync(Oper1, _sh30.ProductoMaestroId);

        // El job no corrió: a las 13:05 entra el del T2
        _reloj = H(19, 13, 5);
        var t2 = await EntrarAsync(Oper2, _sh30.ProductoMaestroId);
        Assert.True(t2.IsSuccess, t2.ErrorMessage);
        Assert.NotNull(t2.Value!.TurnoAnteriorCerradoId);
        var t1 = await TurnoAsync(_tango2.LineaId, _t1, 19);
        Assert.Equal((EstadoTurnoProduccion.CERRADO_AUTOMATICO, H(19, 13)), (t1.Estado, t1.FinReal!.Value));
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, (await TurnoAsync(_tango2.LineaId, _t2, 19)).Estado);

        // El del T1 sigue en su tolerancia (vence por tiempo, no por el logueo del otro)
        var sesion = await _turnos.ObtenerSesionEnToleranciaAsync(Oper1);
        Assert.Equal(t1.TurnoProduccionId, sesion!.TurnoProduccionId);
        var dto = await _turnos.ObtenerSesionEnToleranciaDtoAsync(Oper1);
        Assert.Equal(H(19, 13, 30), dto!.ToleranciaHasta);
        Assert.Null(await _turnos.ObtenerMioAsync(Oper1));                 // no tiene turno abierto
        _reloj = H(19, 13, 31);
        Assert.Null(await _turnos.ObtenerSesionEnToleranciaAsync(Oper1));
    }

    [Fact]
    public async Task TerminarMiTurno_AntesDeHora_DejaElTurnoAbiertoSinOperador()
    {
        _reloj = H(19, 6);
        var abierto = (await EntrarAsync(Oper1, _sh30.ProductoMaestroId)).Value!.Turno!;
        _reloj = H(19, 11);
        var r = await _turnos.TerminarMiTurnoAsync(abierto.TurnoProduccionId, Oper1);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal("ABIERTO", r.Value!.Estado);
        Assert.True(r.Value.SinOperador);
        Assert.Equal(H(19, 11), Assert.Single(r.Value.Operadores).Hasta);
        Assert.Null(await _turnos.ObtenerMioAsync(Oper1));
        Assert.Null(await _turnos.ObtenerSesionEnToleranciaAsync(Oper1));   // terminó antes: no queda en tolerancia

        // Otro no lo termina por él
        Assert.Equal("VALIDATION", (await _turnos.TerminarMiTurnoAsync(abierto.TurnoProduccionId, Oper2)).ErrorCode);
    }

    [Fact]
    public async Task ToleranciaPorTurno_SeConfiguraEnABM_Turnos()
    {
        var t1 = await _context.Turnos.SingleAsync(t => t.TurnoId == _t1.TurnoId);
        t1.ToleranciaCierreOperadorMinutos = 10;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 6);
        await EntrarAsync(Oper1, _sh30.ProductoMaestroId);
        _reloj = H(19, 13);
        await _turnos.ProcesarAsync(_reloj);
        _reloj = H(19, 13, 9);
        Assert.NotNull(await _turnos.ObtenerSesionEnToleranciaAsync(Oper1));
        _reloj = H(19, 13, 10);
        Assert.Null(await _turnos.ObtenerSesionEnToleranciaAsync(Oper1));
    }

    // ===================== D. Detenciones que cruzan de turno =====================

    [Fact]
    public async Task ParadaQueAbarcaDosTurnos_LlegaTarde_DosPartesVinculadas_10Y20Min_PendientesPorSeparado()
    {
        _reloj = H(19, 13, 30);
        var r = await ParadaCerradaAsync(H(19, 12, 50), H(19, 13, 20));
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(2, r.Value!.DetencionesCreadas);

        var partes = await DetencionesAsync();
        Assert.Equal(2, partes.Count);
        var (p1, p2) = (partes[0], partes[1]);
        Assert.Equal(_t1.TurnoId, p1.TurnoProduccion.TurnoId);
        Assert.Equal(_t2.TurnoId, p2.TurnoProduccion.TurnoId);
        Assert.Equal(p1.DetencionId, p2.DetencionAnteriorId);
        Assert.True(p2.VieneDelTurnoAnterior);
        Assert.Equal((H(19, 12, 50), H(19, 13)), (p1.FechaHoraParada!.Value, p1.FechaHoraArranqueDetectada!.Value));
        Assert.Equal((H(19, 13), H(19, 13, 20)), (p2.FechaHoraParada!.Value, p2.FechaHoraArranqueDetectada!.Value));
        Assert.All(partes, p => Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, p.EstadoActual));
        Assert.All(partes, p => Assert.Equal(0, p.MinutosEfectivos));      // pendiente = 0 efectivos (trampa 2)
        Assert.Equal(600, DetencionCamaraReglas.SegundosDetectados(p1, p1.TurnoProduccion, _reloj));
        Assert.Equal(1200, DetencionCamaraReglas.SegundosDetectados(p2, p2.TurnoProduccion, _reloj));

        // Reenvío: no duplica
        Assert.Equal(0, (await ParadaCerradaAsync(H(19, 12, 50), H(19, 13, 20))).Value!.DetencionesCreadas);
        Assert.Equal(2, (await DetencionesAsync()).Count);

        // Cada parte en su tablero, con la ventana de cada una
        var tablero1 = (await _tablero.ObtenerAsync(p1.TurnoProduccionId, false)).Value!;
        var tablero2 = (await _tablero.ObtenerAsync(p2.TurnoProduccionId, false)).Value!;
        Assert.Equal((1, 10), (tablero1.CifrasDetenciones.Pendientes.Cantidad, tablero1.CifrasDetenciones.Pendientes.Minutos));
        Assert.Equal((1, 20), (tablero2.CifrasDetenciones.Pendientes.Cantidad, tablero2.CifrasDetenciones.Pendientes.Minutos));
        Assert.True(Assert.Single(tablero2.PendientesDeclarar).VieneDelTurnoAnterior);

        // Se declaran por separado. La del T1, hasta el corte (13:00)
        var decl = (await _declaracion.ObtenerAsync(p1.DetencionId)).Value!;
        Assert.Equal(p2.DetencionId, decl.ParteSiguienteId);
        Assert.Equal(H(19, 13), decl.DeclararHasta);
        var pasada = await _declaracion.DeclararAsync(p1.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Atasco", HoraArranque = new TimeOnly(13, 20), RowVersion = p1.RowVersion
        }, Oper1, null);
        Assert.Equal("VALIDATION", pasada.ErrorCode);
        Assert.Contains("turno siguiente", pasada.ErrorMessage);
        var ok1 = await _declaracion.DeclararAsync(p1.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Atasco", HoraArranque = new TimeOnly(13, 0), RowVersion = p1.RowVersion
        }, Oper1, null);
        Assert.True(ok1.IsSuccess, ok1.ErrorMessage);
        Assert.Equal(10, ok1.Value!.MinutosEfectivos);
        var ok2 = await _declaracion.DeclararAsync(p2.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Siguió el atasco", HoraArranque = new TimeOnly(13, 20), RowVersion = p2.RowVersion
        }, Oper2, null);
        Assert.True(ok2.IsSuccess, ok2.ErrorMessage);
        Assert.Equal(20, ok2.Value!.MinutosEfectivos);

        // Indicadores por turno: cada turno sus minutos
        var ind1 = (await _indicadores.ObtenerTurnoAsync(p1.TurnoProduccionId)).Value!;
        var ind2 = (await _indicadores.ObtenerTurnoAsync(p2.TurnoProduccionId)).Value!;
        Assert.Equal(10, ind1.MinutosParadaQueCuentan);
        Assert.Equal(20, ind2.MinutosParadaQueCuentan);
    }

    [Fact]
    public async Task ParadaCortaQueCruzaElCambioDeTurno_EsUnMicrocorte_QueCuentaUnaVez()
    {
        _reloj = H(19, 13, 10);
        var r = await ParadaCerradaAsync(H(19, 12, 58), H(19, 13, 2));   // 4 min, umbral 5
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(0, r.Value!.DetencionesCreadas);
        Assert.Empty(await DetencionesAsync());

        var t2 = await _turnos.AsegurarTurnoAsync(_tango2.LineaId, H(19, 13, 5));
        var t1 = await TurnoAsync(_tango2.LineaId, _t1, 19);
        var micro1 = (await _tablero.ObtenerAsync(t1.TurnoProduccionId, false)).Value!.Microcortes;
        var micro2 = (await _tablero.ObtenerAsync(t2!.TurnoProduccionId, false)).Value!.Microcortes;
        Assert.Equal((1, 120), (micro1.Cantidad, micro1.Segundos));
        Assert.Equal((0, 120), (micro2.Cantidad, micro2.Segundos));

        // Presupuesto vs. real por turno: cada turno sus segundos; la cantidad, 1 en el T1 y 0 en el T2; el día, 1 (no 2).
        var porTurno = (await _indicadores.ObtenerProduccionAsync(new IndicadoresProduccionFiltro
        {
            Desde = new DateOnly(2026, 11, 19), Hasta = new DateOnly(2026, 11, 19), LineaId = _tango2.LineaId, Agrupar = "turno"
        })).Value!;
        var f1 = porTurno.Filas.Single(f => f.TurnoProduccionId == t1.TurnoProduccionId);
        var f2 = porTurno.Filas.Single(f => f.TurnoProduccionId == t2.TurnoProduccionId);
        Assert.Equal((1, 120), (f1.Microcortes!.Value, f1.SegundosMicrocortes!.Value));
        Assert.Equal((0, 120), (f2.Microcortes!.Value, f2.SegundosMicrocortes!.Value));
        Assert.Equal((1, 240), (porTurno.Total.Microcortes!.Value, porTurno.Total.SegundosMicrocortes!.Value));
    }

    [Fact]
    public async Task ParadaAbiertaAlCierre_ElJobLaCorta_ElTableroDelT2LaMuestra_YElArranqueVaALaParteDelT2()
    {
        _reloj = H(19, 6);
        await EntrarAsync(Oper1, _sh30.ProductoMaestroId);
        _reloj = H(19, 12, 56);
        Assert.Equal(1, (await ParadaEnCursoAsync(H(19, 12, 50), H(19, 12, 56))).Value!.DetencionesCreadas);
        _context.Camaras.Single(c => c.CamaraId == _camara.CamaraId).UltimoDatoRecibido = H(19, 12, 59);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 13, 0);
        await _turnos.ProcesarAsync(_reloj);

        var partes = await DetencionesAsync();
        Assert.Equal(2, partes.Count);
        Assert.Equal(H(19, 13), partes[0].FechaHoraArranqueDetectada);
        Assert.Null(partes[1].FechaHoraArranqueDetectada);                // sigue parada en el T2
        Assert.Equal(partes[0].DetencionId, partes[1].DetencionAnteriorId);

        // Desde el arranque del T2, su tablero muestra que la máquina está detenida ("viene del turno anterior")
        _reloj = H(19, 13, 4);
        var tablero = (await _tablero.ObtenerAsync(partes[1].TurnoProduccionId, false)).Value!;
        var pendiente = Assert.Single(tablero.PendientesDeclarar);
        Assert.True(pendiente.VieneDelTurnoAnterior);
        Assert.True(pendiente.ParadaEnCurso);
        Assert.Equal(240, pendiente.SegundosDetectados);
        Assert.Equal(4, tablero.CifrasDetenciones.Pendientes.Minutos);
        Assert.True(Assert.Single(tablero.Detenciones).VieneDelTurnoAnterior);

        // Reenvío de la parada en curso y después el arranque: van a la parte del T2 (no se duplica)
        _reloj = H(19, 13, 25);
        await ParadaEnCursoAsync(H(19, 12, 50), H(19, 13, 10));
        await ParadaCerradaAsync(H(19, 12, 50), H(19, 13, 20));
        partes = await DetencionesAsync();
        Assert.Equal(2, partes.Count);
        Assert.Equal(H(19, 13), partes[0].FechaHoraArranqueDetectada);
        Assert.Equal(H(19, 13, 20), partes[1].FechaHoraArranqueDetectada);
    }

    [Fact]
    public async Task ParadaAbiertaAlCierre_ConLaCamaraSinDatos_NoSeArrastraAlTurnoSiguiente()
    {
        _reloj = H(19, 6);
        await EntrarAsync(Oper1, _sh30.ProductoMaestroId);
        _reloj = H(19, 12, 0);
        await ParadaEnCursoAsync(H(19, 11, 50), H(19, 12, 0));
        _context.Camaras.Single(c => c.CamaraId == _camara.CamaraId).UltimoDatoRecibido = H(19, 12, 0);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 13, 0);
        await _turnos.ProcesarAsync(_reloj);
        Assert.Single(await DetencionesAsync());
    }

    // ===================== C.11 Alertas del supervisor =====================

    [Fact]
    public async Task Alertas_SinProducto_SupuestoSinConfirmar_SinObjetivos_YLaCorreccionConfirma()
    {
        _reloj = H(18, 21, 10);
        await EntrarAsync(Oper1, _sh50.ProductoMaestroId);
        _reloj = H(19, 5);
        await _turnos.ProcesarAsync(_reloj);
        // Un turno sin producto: lo abre el sistema por un dato en Futura 2 (no programada, turno extra)
        await _turnos.AsegurarTurnoAsync(_futura2.LineaId, H(19, 5, 30));

        var dia = await _turnos.ObtenerDiaAsync(new DateOnly(2026, 11, 19), null, RolSupervisor, []);
        var tango2 = dia.Filas.Single(f => f.LineaId == _tango2.LineaId && f.TurnoId == _t1.TurnoId).Turno!;
        Assert.True(tango2.ProductoSinConfirmar);
        Assert.Contains(tango2.Alertas, a => a.StartsWith("Producto supuesto sin confirmar"));
        var tango1 = dia.Filas.Single(f => f.LineaId == _tango1.LineaId && f.TurnoId == _t1.TurnoId).Turno!;
        Assert.Contains(tango1.Alertas, a => a.Contains("TANGO 1 no tiene objetivos cargados: Maestros → Parámetros línea + producto"));
        var futura = dia.Filas.Single(f => f.LineaId == _futura2.LineaId && f.TurnoId == _t1.TurnoId).Turno!;
        Assert.Contains(futura.Alertas, a => a.StartsWith("Turno sin producto"));

        // Tablero gerencial
        var tablero = (await _tablero.ObtenerAsync(tango2.TurnoProduccionId, true)).Value!;
        Assert.True(tablero.SinOperador);
        Assert.True(tablero.ProductoSinConfirmar);
        Assert.Contains(tablero.Alertas, a => a.StartsWith("Producto supuesto sin confirmar"));

        // El supervisor corrige el heredado (turno abierto sin operador): queda confirmado (lo reemplaza un tramo de supervisor)
        _reloj = H(19, 7);
        var r = await _turnos.CorregirProductoAsync(tango2.TurnoProduccionId, new CorregirProductoTurnoRequest
        {
            ProductoMaestroId = _sh30.ProductoMaestroId, Desde = H(19, 5), Motivo = "Arrancó con SH 30", AnulaTramoId = tango2.ProductoActual!.TurnoProduccionProductoId
        }, Supervisor, RolSupervisor, []);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.False(r.Value!.ProductoSinConfirmar);
        Assert.DoesNotContain(r.Value.Alertas, a => a.StartsWith("Producto supuesto"));

        // Corregir sobre un turno cerrado también funciona
        _reloj = H(19, 13, 1);
        await _turnos.ProcesarAsync(_reloj);
        var cerrado = await _turnos.CorregirProductoAsync(tango2.TurnoProduccionId, new CorregirProductoTurnoRequest
        {
            ProductoMaestroId = _sh50.ProductoMaestroId, Desde = H(19, 10), Hasta = H(19, 13), Motivo = "Cambio no declarado",
            AnulaTramoId = r.Value.ProductoActual?.TurnoProduccionProductoId
        }, Supervisor, RolSupervisor, []);
        Assert.True(cerrado.IsSuccess, cerrado.ErrorMessage);
    }
}
