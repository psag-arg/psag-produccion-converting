using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
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
/// T-798 etapa 2 · Turno de producción contra SQLite en memoria: un solo ABIERTO por línea, T3 que cruza medianoche,
/// SIN_APERTURA, AsegurarTurno (SISTEMA), relevos y cambio de producto con snapshot, cierre automático y producto no
/// habilitado. Turnos de planta T1 05–13, T2 13–21, T3 21–05; planta UTC-3; reloj fijo y movible.
/// </summary>
public class TurnoProduccionTests : IDisposable
{
    private const string Admin = "admin-test";
    private const string Oper1 = "oper-1";
    private const string Oper2 = "oper-2";
    private const string OperFutura = "oper-futura";
    private const string Supervisor = "sup-1";
    private static readonly string[] RolOperario = [RolesSistema.Operario];
    private static readonly string[] RolSupervisor = [RolesSistema.Supervisor];
    private static readonly int[] SinLineas = [];

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly TurnoProduccionService _servicio;
    private DateTimeOffset _reloj;

    private readonly Linea _tango2;
    private readonly Linea _futura2;
    private readonly Turno _t1;
    private readonly Turno _t2;
    private readonly Turno _t3;
    private readonly ProductoMaestro _sh30;
    private readonly ProductoMaestro _sh50;
    private readonly ProductoMaestro _noHabilitado;
    private readonly ProductoMaestro _deshabilitado;
    private readonly ParametroLineaProducto _parametroSh30;

    public TurnoProduccionTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _futura2 = new Linea { Codigo = "F2", Nombre = "Futura 2", Orden = 2, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _t1 = Turno("T1", "Turno 1", 5, 13, 1);
        _t2 = Turno("T2", "Turno 2", 13, 21, 2);
        _t3 = Turno("T3", "Turno 3", 21, 5, 3);
        _sh30 = Producto("HIGIENICO 30 MTS", 30, 1);
        _sh50 = Producto("HIGIENICO 50 MTS", 50, 2);
        _noHabilitado = Producto("SERVILLETA", null, 3);
        _deshabilitado = Producto("COCINA", null, 4);
        _context.AddRange(_tango2, _futura2, _t1, _t2, _t3, _sh30, _sh50, _noHabilitado, _deshabilitado);
        _context.SaveChanges();

        var desde = new DateOnly(2026, 11, 1);
        _parametroSh30 = new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh30.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 11200, BolsonesObjetivoTurno = 6500 };
        _context.AddRange(
            _parametroSh30,
            new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh50.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 6720, BolsonesObjetivoTurno = 4700 },
            new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _deshabilitado.ProductoMaestroId, VigenteDesde = desde, Habilitado = false },
            new ParametroLineaProducto { LineaId = _futura2.LineaId, ProductoMaestroId = _sh30.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 9000 });
        var asignada = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        _context.AddRange(
            new OperadorLinea { UsuarioId = Oper1, LineaId = _tango2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin },
            new OperadorLinea { UsuarioId = Oper2, LineaId = _tango2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin },
            new OperadorLinea { UsuarioId = OperFutura, LineaId = _futura2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 6, 0);
        _servicio = new TurnoProduccionService(new UnitOfWork(_context), ZonaPlanta, new TurnosProduccionOpciones(),
            NullLogger<TurnoProduccionService>.Instance, () => _reloj);
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
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

    private int[] LineasDe(string usuario) => usuario switch
    {
        Oper1 or Oper2 => [_tango2.LineaId],
        OperFutura => [_futura2.LineaId],
        _ => []
    };

    private async Task<AperturaTurnoProduccionDto> AbrirComoOperario(string usuario, int lineaId, int? productoId = null, bool relevar = false)
    {
        var r = await _servicio.AbrirAsync(lineaId, usuario, OrigenAperturaTurno.LOGIN, productoId, relevar, RolOperario, LineasDe(usuario));
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return r.Value!;
    }

    private async Task<List<TurnoProduccion>> Turnos()
    {
        _context.ChangeTracker.Clear();
        return await _context.TurnosProduccion.Include(t => t.Operadores).Include(t => t.Productos)
            .OrderBy(t => t.TurnoProduccionId).ToListAsync();
    }

    private static void AssertSinSuperposicion(TurnoProduccion t)
    {
        Assert.True(TurnoProduccionReglas.SinSuperposicion(TurnoProduccionReglas.Intervalos(t.Operadores)), "Operadores superpuestos");
        Assert.True(TurnoProduccionReglas.SinSuperposicion(TurnoProduccionReglas.Intervalos(t.Productos)), "Productos superpuestos");
    }

    // ===================== Un solo ABIERTO por línea =====================

    [Fact]
    public async Task AbrirElSiguiente_CierraElAnterior_YDosLineasPuedenTenerAbiertos()
    {
        _reloj = H(19, 6);
        var t1Tango = await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);
        var t1Futura = await AbrirComoOperario(OperFutura, _futura2.LineaId);
        Assert.Equal("ABIERTO", t1Tango.Turno!.Estado);
        Assert.Equal("ABIERTO", t1Futura.Turno!.Estado);

        _reloj = H(19, 13, 5);
        var t2Tango = await AbrirComoOperario(Oper2, _tango2.LineaId, _sh30.ProductoMaestroId);
        Assert.Equal(t1Tango.Turno.TurnoProduccionId, t2Tango.TurnoAnteriorCerradoId);
        Assert.Equal(_t2.TurnoId, t2Tango.Turno!.TurnoId);

        var turnos = await Turnos();
        var anterior = turnos.Single(t => t.TurnoProduccionId == t1Tango.Turno.TurnoProduccionId);
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, anterior.Estado);
        Assert.Equal(H(19, 13), anterior.FinReal);   // terminó a su hora aunque nadie lo cerró
        Assert.All(anterior.Operadores, o => Assert.Equal(H(19, 13), o.Hasta));
        Assert.All(anterior.Productos, p => Assert.Equal(H(19, 13), p.Hasta));
        Assert.Single(turnos, t => t.LineaId == _tango2.LineaId && t.Estado == EstadoTurnoProduccion.ABIERTO);
        // Futura 2 sigue con su T1 abierto: el índice es por línea.
        Assert.Single(turnos, t => t.LineaId == _futura2.LineaId && t.Estado == EstadoTurnoProduccion.ABIERTO);
    }

    [Fact]
    public async Task IndiceFiltrado_NoPermiteDosAbiertosEnLaMismaLinea()
    {
        TurnoProduccion Nuevo(Turno turno) => new()
        {
            LineaId = _tango2.LineaId, TurnoId = turno.TurnoId, FechaProduccion = new DateOnly(2026, 11, 19),
            InicioProgramado = H(19, 5), FinProgramado = H(19, 13), Estado = EstadoTurnoProduccion.ABIERTO,
            OrigenApertura = OrigenAperturaTurno.SISTEMA, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.TurnosProduccion.Add(Nuevo(_t1));
        await _context.SaveChangesAsync();
        _context.TurnosProduccion.Add(Nuevo(_t2));

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task AbrirDosVecesElMismoTurno_NoDuplica()
    {
        var a = await AbrirComoOperario(Oper1, _tango2.LineaId);
        _reloj = H(19, 7);
        var b = await AbrirComoOperario(Oper1, _tango2.LineaId);

        Assert.Equal(a.Turno!.TurnoProduccionId, b.Turno!.TurnoProduccionId);
        Assert.True(b.YaEraOperador);
        var t = Assert.Single(await Turnos());
        Assert.Single(t.Operadores);
    }

    // ===================== T3 que cruza medianoche =====================

    [Fact]
    public async Task T3_AbiertoALas23_ALas02EsElMismoTurnoYDelDiaDeLas21()
    {
        _reloj = H(19, 23);
        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId, _sh50.ProductoMaestroId);
        Assert.Equal(_t3.TurnoId, abierto.Turno!.TurnoId);
        Assert.Equal(new DateOnly(2026, 11, 19), abierto.Turno.FechaProduccion);
        Assert.Equal(H(19, 21), abierto.Turno.InicioProgramado);
        Assert.Equal(H(20, 5), abierto.Turno.FinProgramado);
        Assert.Equal(480, abierto.Turno.MinutosProgramados);

        _reloj = H(20, 2);
        var asegurado = await _servicio.AsegurarTurnoAsync(_tango2.LineaId, H(20, 2));
        Assert.Equal(abierto.Turno.TurnoProduccionId, asegurado!.TurnoProduccionId);
        var actual = await _servicio.ObtenerActualAsync(_tango2.LineaId);
        Assert.Equal(abierto.Turno.TurnoProduccionId, actual!.TurnoProduccionId);
        var relevo = await AbrirComoOperario(Oper2, _tango2.LineaId, relevar: true);
        Assert.Equal(abierto.Turno.TurnoProduccionId, relevo.Turno!.TurnoProduccionId);
        Assert.Equal(new DateOnly(2026, 11, 19), relevo.Turno.FechaProduccion);

        var t = Assert.Single(await Turnos());
        Assert.Equal(2, t.Operadores.Count);
        AssertSinSuperposicion(t);
    }

    // ===================== SIN_APERTURA =====================

    [Fact]
    public async Task SinApertura_SeMarcaYDespuesSeAbre()
    {
        // Futura 2 tiene una programación vacía desde el 01/11: no trabaja, no queda SIN_APERTURA.
        _context.Set<CalendarioLinea>().Add(new CalendarioLinea { LineaId = _futura2.LineaId, VigenteDesde = new DateOnly(2026, 11, 1), CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // 05:29: todavía dentro de la tolerancia del T1 del 19/11.
        Assert.Equal(2, await _servicio.MarcarSinAperturaAsync(H(19, 5, 29)));   // T2 y T3 del 18/11 de Tango 2
        Assert.Equal(1, await _servicio.MarcarSinAperturaAsync(H(19, 5, 30)));   // ahora sí el T1 del 19/11
        Assert.Equal(0, await _servicio.MarcarSinAperturaAsync(H(19, 5, 31)));   // no duplica

        var turnos = await Turnos();
        Assert.All(turnos, t => Assert.Equal(_tango2.LineaId, t.LineaId));
        var t1 = turnos.Single(t => t.TurnoId == _t1.TurnoId && t.FechaProduccion == new DateOnly(2026, 11, 19));
        Assert.Equal(EstadoTurnoProduccion.SIN_APERTURA, t1.Estado);
        Assert.Null(t1.InicioReal);
        Assert.Equal(480, t1.MinutosProgramados);

        _reloj = H(19, 6, 10);
        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);
        Assert.Equal(t1.TurnoProduccionId, abierto.Turno!.TurnoProduccionId);
        Assert.Equal("ABIERTO", abierto.Turno.Estado);
        Assert.Equal("LOGIN", abierto.Turno.OrigenApertura);
        Assert.Equal(H(19, 6, 10), abierto.Turno.InicioReal);
        Assert.Equal(3, (await Turnos()).Count);
    }

    // ===================== AsegurarTurno (SISTEMA) =====================

    [Fact]
    public async Task AsegurarTurno_CreaPorSistemaSinOperadorNiProducto_YNoDuplica()
    {
        _reloj = H(19, 9);
        var a = await _servicio.AsegurarTurnoAsync(_tango2.LineaId, H(19, 8, 59));
        var b = await _servicio.AsegurarTurnoAsync(_tango2.LineaId, H(19, 9));

        Assert.NotNull(a);
        Assert.Equal(a!.TurnoProduccionId, b!.TurnoProduccionId);
        var t = Assert.Single(await Turnos());
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, t.Estado);
        Assert.Equal(OrigenAperturaTurno.SISTEMA, t.OrigenApertura);
        Assert.Null(t.AbiertoPorUsuarioId);
        Assert.Empty(t.Operadores);
        Assert.Empty(t.Productos);   // "a completar"
        var dto = await _servicio.ObtenerActualAsync(_tango2.LineaId);
        Assert.True(dto!.ACompletar);

        // Después el operario lo toma: no se crea otro.
        var tomado = await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);
        Assert.Equal(t.TurnoProduccionId, tomado.Turno!.TurnoProduccionId);
        Assert.False(tomado.Turno.ACompletar);
    }

    [Fact]
    public async Task AsegurarTurno_DeUnTurnoPasado_LoCreaCerradoSinTocarElAbierto()
    {
        _reloj = H(19, 14);
        var actual = await AbrirComoOperario(Oper1, _tango2.LineaId);   // T2 del 19/11

        var pasado = await _servicio.AsegurarTurnoAsync(_tango2.LineaId, H(19, 2));   // T3 del 18/11
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, pasado!.Estado);
        Assert.Equal(new DateOnly(2026, 11, 18), pasado.FechaProduccion);
        Assert.Equal(H(19, 5), pasado.FinReal);

        var turnos = await Turnos();
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, turnos.Single(t => t.TurnoProduccionId == actual.Turno!.TurnoProduccionId).Estado);
    }

    // ===================== Relevo y cambio de producto =====================

    [Fact]
    public async Task Relevo_SinConfirmar_NoCambiaNada_YConfirmado_CierraAlOperadorVigente()
    {
        _reloj = H(19, 5, 10);
        await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);

        _reloj = H(19, 9);
        var sinConfirmar = await AbrirComoOperario(Oper2, _tango2.LineaId);
        Assert.True(sinConfirmar.RequiereConfirmarRelevo);
        Assert.Equal(Oper1, sinConfirmar.ReemplazadoUsuarioId);
        Assert.Single(Assert.Single(await Turnos()).Operadores);

        var relevo = await AbrirComoOperario(Oper2, _tango2.LineaId, relevar: true);
        Assert.False(relevo.RequiereConfirmarRelevo);
        Assert.Equal(Oper1, relevo.ReemplazadoUsuarioId);
        Assert.Equal(Oper2, relevo.Turno!.OperadorActual!.OperadorUsuarioId);

        var t = Assert.Single(await Turnos());
        var tramos = t.Operadores.OrderBy(o => o.Desde).ToList();
        Assert.Equal(2, tramos.Count);
        Assert.Equal((H(19, 5, 10), H(19, 9)), (tramos[0].Desde, tramos[0].Hasta!.Value));
        Assert.Equal(H(19, 9), tramos[1].Desde);
        Assert.Null(tramos[1].Hasta);
        AssertSinSuperposicion(t);
    }

    [Fact]
    public async Task CambioDeProducto_CierraElTramo_YGuardaSnapshotDelObjetivoVigente()
    {
        _reloj = H(19, 13, 2);
        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);
        var id = abierto.Turno!.TurnoProduccionId;
        Assert.Equal(11200, abierto.Turno.ProductoActual!.MangasObjetivoTurno);
        Assert.Equal(30m, abierto.Turno.ProductoActual.MetrosPorManga);
        Assert.Equal("LOGIN", abierto.Turno.ProductoActual.Origen);

        _reloj = H(19, 16, 30);
        var cambio = await _servicio.CambiarProductoAsync(id, new CambiarProductoTurnoRequest { ProductoMaestroId = _sh50.ProductoMaestroId, Desde = H(19, 16) },
            Oper1, RolOperario, LineasDe(Oper1));
        Assert.True(cambio.IsSuccess, cambio.ErrorMessage);
        Assert.Equal(6720, cambio.Value!.ProductoActual!.MangasObjetivoTurno);
        Assert.Equal(4700, cambio.Value.ProductoActual.BolsonesObjetivoTurno);
        Assert.Equal("CAMBIO", cambio.Value.ProductoActual.Origen);

        // Mañana cambia el objetivo del SH 30: el tramo histórico no cambia (snapshot, no JOIN).
        var parametro = await _context.ParametrosLineaProducto.SingleAsync(p => p.ParametroLineaProductoId == _parametroSh30.ParametroLineaProductoId);
        parametro.MangasObjetivoTurno = 99999;
        await _context.SaveChangesAsync();

        var t = Assert.Single(await Turnos());
        var tramos = t.Productos.OrderBy(p => p.Desde).ToList();
        Assert.Equal(2, tramos.Count);
        Assert.Equal(11200, tramos[0].MangasObjetivoTurno);
        Assert.Equal(_parametroSh30.ParametroLineaProductoId, tramos[0].ParametroLineaProductoId);
        Assert.Equal(480, tramos[0].MinutosTurnoReferencia);
        Assert.Equal(H(19, 16), tramos[0].Hasta);
        Assert.Equal(H(19, 16), tramos[1].Desde);
        AssertSinSuperposicion(t);

        // Un cambio anterior al inicio del tramo vigente se rechaza (se superpondría).
        var antes = await _servicio.CambiarProductoAsync(id, new CambiarProductoTurnoRequest { ProductoMaestroId = _sh30.ProductoMaestroId, Desde = H(19, 15) },
            Oper1, RolOperario, LineasDe(Oper1));
        Assert.True(antes.IsFailure);
    }

    [Fact]
    public async Task CorregirOperador_EsInsertOnly_ConAnuladoPorId()
    {
        _reloj = H(19, 6);
        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId);
        var tramoOriginal = abierto.Turno!.OperadorActual!.TurnoProduccionOperadorId;

        _reloj = H(19, 8);
        var r = await _servicio.CorregirOperadorAsync(abierto.Turno.TurnoProduccionId, new CorregirOperadorTurnoRequest
        {
            OperadorUsuarioId = Oper2, Desde = H(19, 6), Motivo = "Se logueó con el usuario del compañero", AnulaTramoId = tramoOriginal
        }, Supervisor, RolSupervisor, SinLineas);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(Oper2, r.Value!.OperadorActual!.OperadorUsuarioId);

        var t = Assert.Single(await Turnos());
        Assert.Equal(2, t.Operadores.Count);   // el original sigue, anulado
        var original = t.Operadores.Single(o => o.TurnoProduccionOperadorId == tramoOriginal);
        var nuevo = t.Operadores.Single(o => o.TurnoProduccionOperadorId != tramoOriginal);
        Assert.Equal(nuevo.TurnoProduccionOperadorId, original.AnuladoPorId);
        Assert.Equal(OrigenTramoOperador.SUPERVISOR, nuevo.Origen);
        AssertSinSuperposicion(t);

        // El operario no corrige.
        var operario = await _servicio.CorregirOperadorAsync(abierto.Turno.TurnoProduccionId, new CorregirOperadorTurnoRequest
        {
            OperadorUsuarioId = Oper1, Desde = H(19, 7), Motivo = "x"
        }, Oper1, RolOperario, LineasDe(Oper1));
        Assert.Equal("FORBIDDEN", operario.ErrorCode);
    }

    // ===================== Cierre automático =====================

    [Fact]
    public async Task CierreAutomatico_PasadoElFinMasLaTolerancia()
    {
        _reloj = H(19, 6);
        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);

        Assert.Equal(0, await _servicio.CerrarVencidosAsync(H(19, 13, 29)));
        Assert.Equal(1, await _servicio.CerrarVencidosAsync(H(19, 13, 30)));
        Assert.Equal(0, await _servicio.CerrarVencidosAsync(H(19, 13, 31)));

        var t = Assert.Single(await Turnos());
        Assert.Equal(abierto.Turno!.TurnoProduccionId, t.TurnoProduccionId);
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, t.Estado);
        Assert.Equal(H(19, 13), t.FinReal);
        Assert.Null(t.CerradoPorUsuarioId);
        Assert.All(t.Operadores, o => Assert.NotNull(o.Hasta));
        Assert.All(t.Productos, p => Assert.NotNull(p.Hasta));
    }

    [Fact]
    public async Task CierreManual_Cerrado_YNoSeReabreDesdeElLogin()
    {
        _reloj = H(19, 6);
        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId);
        _reloj = H(19, 12, 50);
        var cerrado = await _servicio.CerrarAsync(abierto.Turno!.TurnoProduccionId, "Fin de turno", Oper1, RolOperario, LineasDe(Oper1));
        Assert.True(cerrado.IsSuccess, cerrado.ErrorMessage);
        Assert.Equal("CERRADO", cerrado.Value!.Estado);
        Assert.Equal(H(19, 12, 50), cerrado.Value.FinReal);

        var reabrir = await _servicio.AbrirAsync(_tango2.LineaId, Oper2, OrigenAperturaTurno.LOGIN, null, true, RolOperario, LineasDe(Oper2));
        Assert.True(reabrir.IsFailure);
    }

    // ===================== Producto no habilitado =====================

    [Fact]
    public async Task ProductoNoHabilitadoEnLaLinea_SeRechaza()
    {
        // Sin parámetro en la línea.
        var sinParametro = await _servicio.AbrirAsync(_tango2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, _noHabilitado.ProductoMaestroId, false, RolOperario, LineasDe(Oper1));
        Assert.True(sinParametro.IsFailure);
        Assert.Contains("no está habilitado", sinParametro.ErrorMessage);
        // Con parámetro vigente pero deshabilitado.
        var deshabilitado = await _servicio.AbrirAsync(_tango2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, _deshabilitado.ProductoMaestroId, false, RolOperario, LineasDe(Oper1));
        Assert.True(deshabilitado.IsFailure);
        Assert.Empty(await Turnos());   // no se abrió nada

        var abierto = await AbrirComoOperario(Oper1, _tango2.LineaId);
        var cambio = await _servicio.CambiarProductoAsync(abierto.Turno!.TurnoProduccionId,
            new CambiarProductoTurnoRequest { ProductoMaestroId = _noHabilitado.ProductoMaestroId }, Oper1, RolOperario, LineasDe(Oper1));
        Assert.True(cambio.IsFailure);
        var correccion = await _servicio.CorregirProductoAsync(abierto.Turno.TurnoProduccionId,
            new CorregirProductoTurnoRequest { ProductoMaestroId = _deshabilitado.ProductoMaestroId, Desde = H(19, 6), Motivo = "x" }, Supervisor, RolSupervisor, SinLineas);
        Assert.True(correccion.IsFailure);
    }

    // ===================== Permisos y propuesta =====================

    [Fact]
    public async Task Operario_NoHabilitadoEnLaLinea_NoPuedeTomarla()
    {
        var r = await _servicio.AbrirAsync(_futura2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, null, false, RolOperario, LineasDe(Oper1));
        Assert.Equal("FORBIDDEN", r.ErrorCode);
    }

    [Fact]
    public async Task Propuesta_ListaSusLineas_ConProductosYObjetivos_OAvisaSiNoTieneNinguna()
    {
        var propuesta = await _servicio.ObtenerPropuestaAsync(Oper1, RolOperario);
        Assert.True(propuesta.Preguntar);
        Assert.Equal(_t1.TurnoId, propuesta.TurnoId);
        var linea = Assert.Single(propuesta.Lineas);
        Assert.Equal(_tango2.LineaId, linea.LineaId);
        Assert.Null(linea.Estado);
        Assert.True(linea.PuedeTomar);
        Assert.Equal([_sh30.ProductoMaestroId, _sh50.ProductoMaestroId], linea.Productos.Select(p => p.ProductoMaestroId));
        Assert.Equal(11200, linea.Productos[0].MangasObjetivoTurno);

        await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);
        var despues = await _servicio.ObtenerPropuestaAsync(Oper1, RolOperario);
        Assert.False(despues.Preguntar);   // ya es el operador y tiene producto: sigue de largo
        var otro = await _servicio.ObtenerPropuestaAsync(Oper2, RolOperario);
        Assert.True(otro.Preguntar);
        Assert.Equal(Oper1, otro.Lineas[0].OperadorActualUsuarioId);

        var sinLineas = await _servicio.ObtenerPropuestaAsync("oper-sin-lineas", RolOperario);
        Assert.True(sinLineas.Preguntar);
        Assert.Equal(TurnoProduccionReglas.MensajeSinLineas, sinLineas.Aviso);

        Assert.False((await _servicio.ObtenerPropuestaAsync(Supervisor, RolSupervisor)).Preguntar);
    }

    [Fact]
    public async Task ListadoDelDia_DevuelveLineaPorTurno_ConElRegistroSiExiste()
    {
        await AbrirComoOperario(Oper1, _tango2.LineaId, _sh30.ProductoMaestroId);

        var dia = await _servicio.ObtenerDiaAsync(new DateOnly(2026, 11, 19), null, RolSupervisor, SinLineas);
        Assert.Equal(6, dia.Filas.Count);   // 2 líneas x 3 turnos
        var fila = dia.Filas.Single(f => f.LineaId == _tango2.LineaId && f.TurnoId == _t1.TurnoId);
        Assert.True(fila.EnCurso);
        Assert.Equal("ABIERTO", fila.Turno!.Estado);
        Assert.Equal(2, fila.OperadoresHabilitados.Count);
        Assert.Null(dia.Filas.Single(f => f.LineaId == _tango2.LineaId && f.TurnoId == _t2.TurnoId).Turno);
    }
}

/// <summary>El script 34 de SQL Server crea las mismas restricciones e índices con nombre que el modelo de EF.</summary>
public class TurnoProduccionScriptTests
{
    [Fact]
    public void Script34_TieneLosChecksEIndicesDelModelo()
    {
        using var conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        using var context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(conexion).Options);
        var script = ScriptSql.Texto("34_t798_turnos_produccion.sql");
        var rollback = ScriptSql.Texto("34_t798_turnos_produccion_rollback.sql");

        var tablas = new[] { typeof(TurnoProduccion), typeof(TurnoProduccionOperador), typeof(TurnoProduccionProducto), typeof(ProduccionTurno) };
        foreach (var tipo in tablas)
        {
            var entidad = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.FindEntityType(tipo)!;
            var tabla = entidad.GetTableName()!;
            Assert.Contains($"CREATE TABLE [mnt].[{tabla}]", script);
            Assert.Contains($"DROP TABLE [mnt].[{tabla}]", rollback);
            foreach (var check in entidad.GetCheckConstraints())
                Assert.Contains(check.Name!, script);
            foreach (var columna in entidad.GetProperties())
                Assert.Contains($"[{columna.GetColumnName()}]", script);
        }
        Assert.Contains("UX_PRD_TurnosProduccion_LineaAbierto", script);
        Assert.Contains("WHERE [Estado] = N'ABIERTO'", script);
        Assert.Contains("UX_PRD_TurnosProduccion_Linea_Turno_Fecha", script);
    }
}
