using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Dashboard;
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
/// T-798 etapa 5 · Producción oficial e indicadores contra SQLite en memoria, con los números del diseño 2.10 (turnos de
/// 8 h): la fila de la cámara al cerrar el tramo (cambio de producto, cierre manual, al abrir el siguiente, vencido),
/// cobertura, validación / corrección / carga manual insert-only, parcial al vuelo, presupuesto vs. real por turno, día,
/// producto y línea, velocidad e indisponibilidad desde los turnos. Planta UTC-3; reloj movible.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class IndicadoresTests : IDisposable
{
    private const string Admin = "admin-t798-5";
    private const string Oper1 = "oper-1";
    private const string Supervisor = "sup-1";
    private static readonly string[] RolOperario = [RolesSistema.Operario];
    private static readonly string[] RolSupervisor = [RolesSistema.Supervisor];

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly BaseDePrueba _base;
    private readonly MantenimientoDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly TurnoProduccionService _turnos;
    private readonly IndicadoresService _indicadores;
    private DateTimeOffset _reloj;

    private readonly Linea _tango2;
    private readonly Turno _t1;
    private readonly Turno _t2;
    private readonly Turno _t3;
    private readonly ProductoMaestro _sh30;
    private readonly ProductoMaestro _sh50;
    private readonly Camara _camaraMangas;

    public IndicadoresTests()
    {
        _base = BaseDePrueba.Crear();
        _context = _base.Context;

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _t1 = Turno("T1", "Turno 1", 5, 13, 1);
        _t2 = Turno("T2", "Turno 2", 13, 21, 2);
        _t3 = Turno("T3", "Turno 3", 21, 5, 3);
        _sh30 = Producto("HIGIENICO 30 MTS", 30, 1);
        _sh50 = Producto("HIGIENICO 50 MTS", 50, 2);
        _context.AddRange(_tango2, _t1, _t2, _t3, _sh30, _sh50);
        _context.SaveChanges();

        var desde = new DateOnly(2026, 11, 1);
        _camaraMangas = new Camara
        {
            LineaId = _tango2.LineaId, Codigo = "TEST-CAM-10", Nombre = "Cámara 10 mangas", Funcion = FuncionCamara.Conteo,
            Magnitud = MagnitudProduccion.Mangas, MetodoDeteccion = MetodoDeteccionCamara.Pasos, CantidadCarriles = 1,
            CuentaProduccionOficial = true, Habilitada = true, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.AddRange(
            _camaraMangas,
            new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh30.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 11200, BolsonesObjetivoTurno = 6500 },
            new ParametroLineaProducto { LineaId = _tango2.LineaId, ProductoMaestroId = _sh50.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = 6720, BolsonesObjetivoTurno = 4700 },
            new OperadorLinea { UsuarioId = Oper1, LineaId = _tango2.LineaId, FechaAsignacion = desde.ToDateTime(TimeOnly.MinValue), AsignadoPorUsuarioId = Admin });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        _reloj = H(19, 5);
        _unitOfWork = new UnitOfWork(_context);
        _turnos = new TurnoProduccionService(_unitOfWork, ZonaPlanta, new TurnosProduccionOpciones(),
            NullLogger<TurnoProduccionService>.Instance, () => _reloj);
        _indicadores = new IndicadoresService(_unitOfWork, ZonaPlanta, () => _reloj);
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

    private static ProductoMaestro Producto(string codigo, decimal metros, int orden) => new()
    {
        Codigo = codigo, Nombre = codigo, MetrosPorManga = metros, Orden = orden, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    /// <summary>Hora de pared de la planta (UTC-3) de noviembre de 2026.</summary>
    private static DateTimeOffset H(int dia, int hora, int minuto = 0) => new(2026, 11, dia, hora, minuto, 0, TimeSpan.FromHours(-3));

    /// <summary>Minutos de la cámara de mangas desde <paramref name="desde"/>, todos con la misma cantidad.</summary>
    private void Minutos(DateTimeOffset desde, int cantidadMinutos, decimal cantidad, short segundosMarcha = 60, Camara? camara = null)
    {
        for (var i = 0; i < cantidadMinutos; i++)
        {
            _context.ConteosCamaraMinuto.Add(new ConteoCamaraMinuto
            {
                Minuto = desde.AddMinutes(i), CamaraId = (camara ?? _camaraMangas).CamaraId, Carril = 1, Cantidad = cantidad,
                SegundosMarcha = segundosMarcha, SegundosParada = (short)(60 - segundosMarcha),
                Estado = segundosMarcha > 0 ? EstadoCarrilCamara.MARCHA : EstadoCarrilCamara.PARADA, FechaHoraCreacion = desde
            });
        }
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private async Task<TurnoProduccionDto> AbrirAsync(DateTimeOffset hora, ProductoMaestro producto)
    {
        _reloj = hora;
        var r = await _turnos.AbrirAsync(_tango2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, producto.ProductoMaestroId, true, RolOperario, [_tango2.LineaId]);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        _context.ChangeTracker.Clear();
        return r.Value!.Turno!;
    }

    private async Task CambiarProductoAsync(long turnoId, DateTimeOffset hora, ProductoMaestro producto)
    {
        _reloj = hora;
        var r = await _turnos.CambiarProductoAsync(turnoId, new CambiarProductoTurnoRequest { ProductoMaestroId = producto.ProductoMaestroId },
            Oper1, RolOperario, [_tango2.LineaId]);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        _context.ChangeTracker.Clear();
    }

    private async Task CerrarAsync(long turnoId, DateTimeOffset hora)
    {
        _reloj = hora;
        var r = await _turnos.CerrarAsync(turnoId, null, Oper1, RolOperario, [_tango2.LineaId]);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        _context.ChangeTracker.Clear();
    }

    private async Task<IndicadoresTurnoDto> IndicadoresAsync(long turnoId)
    {
        _context.ChangeTracker.Clear();
        var r = await _indicadores.ObtenerTurnoAsync(turnoId);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return r.Value!;
    }

    private async Task<List<ProduccionTurno>> ProduccionesAsync()
    {
        _context.ChangeTracker.Clear();
        return await _context.ProduccionesTurno.AsNoTracking().OrderBy(p => p.ProduccionTurnoId).ToListAsync();
    }

    /// <summary>
    /// El día 19 completo (2.10): T1 SH 30 real 9.300; T2 SH 30 3 h (3.900) + SH 50 5 h (3.850); T3 21–05 SH 50 real 6.200.
    /// Cada turno lo cierra la apertura del siguiente; el T3 se cierra por vencido.
    /// </summary>
    private async Task<(long T1, long T2, long T3)> DiaCompletoAsync()
    {
        Minutos(H(19, 5), 465, 20);            // 9.300 en 465 min
        Minutos(H(19, 12, 45), 15, 0, 0);      // 15 min parados con fila (cobertura completa)
        Minutos(H(19, 13), 120, 20);           // SH 30: 2.400
        Minutos(H(19, 15), 60, 25);            //        1.500 → 3.900
        Minutos(H(19, 16), 250, 13);           // SH 50: 3.250
        Minutos(H(19, 20, 10), 50, 12);        //          600 → 3.850
        Minutos(H(19, 21), 400, 13);           // T3:    5.200
        Minutos(H(20, 3, 40), 80, 12.5m);      //        1.000 → 6.200

        var t1 = await AbrirAsync(H(19, 5), _sh30);
        var t2 = await AbrirAsync(H(19, 13), _sh30);
        await CambiarProductoAsync(t2.TurnoProduccionId, H(19, 16), _sh50);
        var t3 = await AbrirAsync(H(19, 21), _sh50);
        _reloj = H(20, 5, 31);
        Assert.Equal(1, await _turnos.CerrarVencidosAsync(_reloj));
        _context.ChangeTracker.Clear();
        return (t1.TurnoProduccionId, t2.TurnoProduccionId, t3.TurnoProduccionId);
    }

    // ===================== Producción oficial al cerrar el tramo =====================

    [Fact]
    public async Task CambioDeProductoYCierre_CreanLaFilaDeLaCamara_PendienteConCobertura()
    {
        Minutos(H(19, 13), 120, 20);
        Minutos(H(19, 15), 60, 25);
        Minutos(H(19, 16), 250, 13);
        Minutos(H(19, 20, 10), 50, 12);
        var t2 = await AbrirAsync(H(19, 13), _sh30);

        await CambiarProductoAsync(t2.TurnoProduccionId, H(19, 16), _sh50);
        var trasCambio = Assert.Single(await ProduccionesAsync());
        await CerrarAsync(t2.TurnoProduccionId, H(19, 21));
        var filas = await ProduccionesAsync();

        Assert.Equal(3900m, trasCambio.CantidadCamara);
        Assert.Equal(180, trasCambio.MinutosConDatos);
        Assert.Equal(3900m, trasCambio.CantidadOficial);
        Assert.Equal(EstadoValidacionProduccion.PENDIENTE, trasCambio.EstadoValidacion);
        Assert.Equal(OrigenProduccionTurno.CAMARA, trasCambio.Origen);
        Assert.Equal(2, filas.Count);
        Assert.Equal((3850m, 300), (filas[1].CantidadCamara!.Value, filas[1].MinutosConDatos!.Value));
        Assert.All(filas, f => Assert.Equal(MagnitudProduccion.Mangas, f.Magnitud));   // sin cámara de bolsones: no hay fila
        Assert.All(filas, f => Assert.Equal(TurnoProduccionService.UsuarioSistema, f.RegistradoPorUsuarioId));
    }

    [Fact]
    public async Task T2ConCambio_Objetivo8400_Real7750_Eficiencia92_3_YPorProducto()
    {
        var (_, t2, _) = await DiaCompletoAsync();

        var ind = await IndicadoresAsync(t2);

        Assert.Equal(8400m, ind.Mangas.Objetivo);
        Assert.Equal(7750m, ind.Mangas.Real);
        Assert.Equal(92.3m, ind.Eficiencia.Porcentaje);
        Assert.True(ind.Eficiencia.Provisorio);   // pendientes de validar
        Assert.Equal([92.9m, 91.7m], ind.Tramos.Select(t => t.Eficiencia.Porcentaje!.Value));
        Assert.Equal([4200m, 4200m], ind.Tramos.Select(t => t.Mangas.Objetivo!.Value));
        Assert.Equal(480, ind.MinutosProgramados);
        // Bolsones sin cámara: "sin datos", no 0 %; el objetivo prorrateado igual se informa.
        Assert.Equal(EstadoIndicador.SinDatos, ind.Bolsones.Estado);
        Assert.Null(ind.Bolsones.Real);
        Assert.Null(ind.Bolsones.Porcentaje);
        Assert.Equal(5375m, ind.Bolsones.Objetivo);   // 6.500 × 180/480 + 4.700 × 300/480
        Assert.All(ind.Tramos, t => Assert.False(t.Bolsones.TieneCamaraOficial));
    }

    [Fact]
    public async Task T1_83_YT3QueCruzaMedianoche_92_3_EnUnSoloTurno()
    {
        var (t1, _, t3) = await DiaCompletoAsync();

        var ind1 = await IndicadoresAsync(t1);
        var ind3 = await IndicadoresAsync(t3);

        Assert.Equal((11200m, 9300m, 83.0m), (ind1.Mangas.Objetivo!.Value, ind1.Mangas.Real!.Value, ind1.Eficiencia.Porcentaje!.Value));
        Assert.Equal(new DateOnly(2026, 11, 19), ind3.FechaProduccion);
        Assert.Equal((6720m, 6200m, 92.3m), (ind3.Mangas.Objetivo!.Value, ind3.Mangas.Real!.Value, ind3.Eficiencia.Porcentaje!.Value));
        Assert.Equal("CERRADO_AUTOMATICO", ind3.Estado);
        Assert.Equal(480m, Assert.Single(ind3.Tramos).MinutosTramo);
    }

    [Fact]
    public async Task CoberturaMenorAl90_QuedaSinDatos_YElParcialDelTramoAbiertoEsProvisorio()
    {
        Minutos(H(19, 5), 400, 20);   // 400 de 480 min = 83 %
        var t1 = await AbrirAsync(H(19, 5), _sh30);

        _reloj = H(19, 9);
        var parcial = await IndicadoresAsync(t1.TurnoProduccionId);
        await CerrarAsync(t1.TurnoProduccionId, H(19, 13));
        var fila = Assert.Single(await ProduccionesAsync());
        var cerrado = await IndicadoresAsync(t1.TurnoProduccionId);

        Assert.True(parcial.Abierto);
        Assert.Equal(EstadoRealProduccion.Parcial, parcial.Tramos[0].Mangas.EstadoReal);
        Assert.Equal(4800m, parcial.Mangas.Real);        // 240 min × 20, al vuelo
        Assert.Equal(5600m, parcial.Mangas.Objetivo);    // 4 h de 8
        Assert.True(parcial.Eficiencia.Provisorio);
        Assert.Equal(EstadoValidacionProduccion.SIN_DATOS, fila.EstadoValidacion);
        Assert.Null(fila.CantidadOficial);
        Assert.Equal(8000m, fila.CantidadCamara);
        Assert.Equal(EstadoIndicador.SinDatos, cerrado.Eficiencia.Estado);
        Assert.Null(cerrado.Eficiencia.Porcentaje);
    }

    [Fact]
    public async Task ProduccionCeroConCoberturaCompleta_EsCeroPorCiento()
    {
        Minutos(H(19, 5), 480, 0, 0);
        var t1 = await AbrirAsync(H(19, 5), _sh30);
        await CerrarAsync(t1.TurnoProduccionId, H(19, 13));

        var ind = await IndicadoresAsync(t1.TurnoProduccionId);

        Assert.Equal(EstadoIndicador.Ok, ind.Eficiencia.Estado);
        Assert.Equal(0m, ind.Eficiencia.Porcentaje);
        Assert.Equal(0m, ind.Mangas.Real);
        Assert.Null(ind.VelocidadMetrosMinuto);   // no hubo minutos en marcha
    }

    [Fact]
    public async Task TurnoCortadoA6HorasPorExcepcion_Objetivo8400()
    {
        _context.Add(new TiempoProgramadoDia
        {
            LineaId = _tango2.LineaId, Fecha = new DateOnly(2026, 11, 21), Horas = 18, Origen = "EXCEPCION", Motivo = "Día corto",
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        _context.SaveChanges();
        Minutos(H(21, 5), 400, 14.5m);
        Minutos(H(21, 11, 40), 80, 15);   // 7.000
        var t1 = await AbrirAsync(H(21, 5), _sh30);
        _reloj = H(21, 13, 31);
        await _turnos.CerrarVencidosAsync(_reloj);

        var ind = await IndicadoresAsync(t1.TurnoProduccionId);

        Assert.Equal(360, ind.MinutosProgramados);
        Assert.Equal(480m, ind.Tramos[0].MinutosTramo);
        Assert.Equal(360m, ind.Tramos[0].MinutosObjetivo);
        Assert.Equal(8400m, ind.Mangas.Objetivo);
        Assert.Equal(83.3m, ind.Eficiencia.Porcentaje);
    }

    [Fact]
    public async Task TurnoSinProducto_EsSinObjetivo()
    {
        Minutos(H(19, 5), 60, 20);
        _reloj = H(19, 6);
        var tp = await _turnos.AsegurarTurnoAsync(_tango2.LineaId, H(19, 5, 30));

        var ind = await IndicadoresAsync(tp!.TurnoProduccionId);

        Assert.True(ind.ACompletar);
        Assert.Equal(EstadoIndicador.SinObjetivo, ind.Eficiencia.Estado);
        Assert.Null(ind.Eficiencia.Porcentaje);
        Assert.Empty(await ProduccionesAsync());
    }

    // ===================== Supervisor: validar, corregir, manual (insert-only) =====================

    [Fact]
    public async Task Supervisor_ValidaCorrigeYCargaAMano_InsertOnly()
    {
        var (_, t2, _) = await DiaCompletoAsync();
        var tramos = (await IndicadoresAsync(t2)).Tramos;
        var sh30 = tramos[0].TurnoProduccionProductoId;
        var sh50 = tramos[1].TurnoProduccionProductoId;
        _reloj = H(20, 8);

        Task<Application.Common.Result<ProduccionTurnoDto>> Registrar(long tramo, string magnitud, string accion, decimal? cantidad = null, string? motivo = null) =>
            _turnos.RegistrarProduccionAsync(t2, new RegistrarProduccionTurnoRequest
            {
                TurnoProduccionProductoId = tramo, Magnitud = magnitud, Accion = accion, Cantidad = cantidad, Motivo = motivo
            }, Supervisor, RolSupervisor, []);

        var validada = await Registrar(sh30, "mangas", "validar");
        var sinMotivo = await Registrar(sh50, "MANGAS", "CORREGIR", 3800);
        var corregida = await Registrar(sh50, "MANGAS", "CORREGIR", 3800, "Contador de la máquina");
        var operario = await _turnos.RegistrarProduccionAsync(t2, new RegistrarProduccionTurnoRequest
        {
            TurnoProduccionProductoId = sh30, Magnitud = "BOLSONES", Accion = "MANUAL", Cantidad = 2400
        }, Oper1, RolOperario, [_tango2.LineaId]);
        var manual = await Registrar(sh30, "BOLSONES", "MANUAL", 2400);
        var corregirSinCamara = await Registrar(sh50, "BOLSONES", "CORREGIR", 10, "x");
        var revalidar = await Registrar(sh30, "MANGAS", "VALIDAR");

        Assert.True(validada.IsSuccess, validada.ErrorMessage);
        Assert.Equal((3900m, "CAMARA", "VALIDADA"), (validada.Value!.CantidadOficial!.Value, validada.Value.Origen, validada.Value.EstadoValidacion));
        Assert.Equal("VALIDATION", sinMotivo.ErrorCode);
        Assert.True(corregida.IsSuccess, corregida.ErrorMessage);
        Assert.Equal(("CAMARA_CORREGIDA", 3850m, 3800m), (corregida.Value!.Origen, corregida.Value.CantidadCamara!.Value, corregida.Value.CantidadOficial!.Value));
        Assert.Equal("FORBIDDEN", operario.ErrorCode);
        Assert.True(manual.IsSuccess, manual.ErrorMessage);
        Assert.Equal("VALIDATION", corregirSinCamara.ErrorCode);
        Assert.Equal("VALIDATION", revalidar.ErrorCode);   // ya validada

        var filas = await ProduccionesAsync();
        var tramoIds = new[] { sh30, sh50 };
        var delT2 = filas.Where(f => tramoIds.Contains(f.TurnoProduccionProductoId)).ToList();
        Assert.Equal(5, delT2.Count);   // 2 de cámara + validada + corregida + manual: nada se borra
        Assert.Equal(2, delT2.Count(f => f.AnuladoPorId != null));
        Assert.Equal("Contador de la máquina", delT2.Single(f => f.Origen == OrigenProduccionTurno.CAMARA_CORREGIDA).Motivo);

        var ind = await IndicadoresAsync(t2);
        Assert.Equal(7700m, ind.Mangas.Real);
        Assert.Equal(91.7m, ind.Eficiencia.Porcentaje);
        Assert.False(ind.Eficiencia.Provisorio);
        Assert.Equal(2400m, ind.Bolsones.Real);
        Assert.Equal(98.5m, ind.Bolsones.Porcentaje);   // 2.400 / 2.437,5; el tramo SH 50 sin bolsones no entra en el %
        Assert.Equal(1, ind.Bolsones.TramosSinDatos);
    }

    [Fact]
    public async Task Supervisor_NoRegistraUnTramoAbierto()
    {
        var t1 = await AbrirAsync(H(19, 5), _sh30);
        _reloj = H(19, 8);
        var tramo = (await IndicadoresAsync(t1.TurnoProduccionId)).Tramos[0].TurnoProduccionProductoId;

        var r = await _turnos.RegistrarProduccionAsync(t1.TurnoProduccionId, new RegistrarProduccionTurnoRequest
        {
            TurnoProduccionProductoId = tramo, Magnitud = "BOLSONES", Accion = "MANUAL", Cantidad = 100
        }, Supervisor, RolSupervisor, []);

        Assert.Equal("VALIDATION", r.ErrorCode);
        Assert.Contains("abierto", r.ErrorMessage);
    }

    // ===================== Presupuesto vs. real =====================

    private async Task<PresupuestoRealDto> ProduccionAsync(string agrupar, int? productoId = null)
    {
        _context.ChangeTracker.Clear();
        var r = await _indicadores.ObtenerProduccionAsync(new IndicadoresProduccionFiltro
        {
            Desde = new DateOnly(2026, 11, 19), Hasta = new DateOnly(2026, 11, 19), LineaId = _tango2.LineaId,
            ProductoMaestroId = productoId, Agrupar = agrupar
        });
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return r.Value!;
    }

    [Fact]
    public async Task PresupuestoVsReal_PorDiaYPorProducto_SumaNoPromedia()
    {
        await DiaCompletoAsync();

        var dia = await ProduccionAsync("dia");
        var productos = await ProduccionAsync("producto");
        var turnos = await ProduccionAsync("turno");
        var linea = await ProduccionAsync("linea");
        var soloSh50 = await ProduccionAsync("turno", _sh50.ProductoMaestroId);

        var fila = Assert.Single(dia.Filas);
        Assert.Equal((26320m, 23250m, 88.3m), (fila.Mangas.Objetivo!.Value, fila.Mangas.Real!.Value, fila.Eficiencia.Porcentaje!.Value));
        Assert.Equal(3, fila.Turnos);
        Assert.Equal(1440, fila.MinutosProgramados);
        Assert.Equal([85.7m, 92.0m], productos.Filas.Select(f => f.Eficiencia.Porcentaje!.Value));
        Assert.Equal(["HIGIENICO 30 MTS", "HIGIENICO 50 MTS"], productos.Filas.Select(f => f.ProductoNombre!));
        Assert.All(productos.Filas, f => Assert.Null(f.Indisponibilidad));
        Assert.Equal([83.0m, 92.3m, 92.3m], turnos.Filas.Select(f => f.Eficiencia.Porcentaje!.Value));
        Assert.Equal(88.3m, turnos.Total.Eficiencia.Porcentaje);
        Assert.Equal(88.3m, Assert.Single(linea.Filas).Eficiencia.Porcentaje);
        // Sólo SH 50: el T2 aporta su tramo de 5 h y el T3 entero; el T1 no aparece.
        Assert.Equal(2, soloSh50.Filas.Count);
        Assert.Equal((10920m, 10050m, 92.0m), (soloSh50.Total.Mangas.Objetivo!.Value, soloSh50.Total.Mangas.Real!.Value, soloSh50.Total.Eficiencia.Porcentaje!.Value));
    }

    [Fact]
    public async Task PresupuestoVsReal_ValidaElFiltro()
    {
        var r = await _indicadores.ObtenerProduccionAsync(new IndicadoresProduccionFiltro
        {
            Desde = new DateOnly(2026, 11, 19), Hasta = new DateOnly(2026, 11, 18), Agrupar = "turno"
        });
        var agrupar = await _indicadores.ObtenerProduccionAsync(new IndicadoresProduccionFiltro
        {
            Desde = new DateOnly(2026, 11, 19), Hasta = new DateOnly(2026, 11, 19), Agrupar = "semana"
        });

        Assert.Equal("VALIDATION", r.ErrorCode);
        Assert.Equal("VALIDATION", agrupar.ErrorCode);
    }

    // ===================== Velocidad =====================

    [Fact]
    public async Task Velocidad_PorTramo_EnLosMinutosEnMarcha()
    {
        // Cantidad es DECIMAL(9,1) (como manda la cámara, un decimal): con 22,33 SQL Server guarda 22,3 y SQLite 22,33.
        Minutos(H(19, 5), 60, 22.3m);          // 22,3 mangas/min × 30 m = 669 m/min
        Minutos(H(19, 6), 30, 0, 0);           // parada: no entra
        var t1 = await AbrirAsync(H(19, 5), _sh30);
        await CerrarAsync(t1.TurnoProduccionId, H(19, 6, 30));

        var ind = await IndicadoresAsync(t1.TurnoProduccionId);

        Assert.Equal(60, ind.Tramos[0].MinutosMarcha);
        Assert.InRange(ind.VelocidadMetrosMinuto!.Value, 668.5m, 669.5m);
    }

    // ===================== Indisponibilidad desde los turnos =====================

    private void Detencion(long turnoId, string codigo, int minutos, TipoFalla? tipo)
    {
        _context.Add(new Detencion
        {
            Codigo = codigo, LineaId = _tango2.LineaId, TurnoProduccionId = turnoId, TipoFallaId = tipo?.TipoFallaId,
            OrigenTiempo = OrigenTiempo.DECLARADO, MinutosDeclarados = minutos, MinutosEfectivos = minutos, Descripcion = "Prueba",
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Indisponibilidad_ConUnTipoQueNoCuenta_YTurnoSinSnapshotRecalculado()
    {
        var mecanica = new TipoFalla { Codigo = "MEC", Nombre = "Mecánica", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        var programada = new TipoFalla { Codigo = "PROG", Nombre = "Preventivo", CuentaParaIndicadores = false, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.AddRange(mecanica, programada);
        _context.SaveChanges();
        var t1 = await AbrirAsync(H(19, 5), _sh30);
        await CerrarAsync(t1.TurnoProduccionId, H(19, 13));
        // Turno convertido (opción B): sin snapshot de minutos programados (0) → se recalcula desde la programación.
        var convertido = _context.TurnoProduccionId(_tango2.LineaId, _t2.TurnoId, new DateOnly(2026, 11, 10));
        Detencion(t1.TurnoProduccionId, "DET-1", 30, mecanica);
        Detencion(t1.TurnoProduccionId, "DET-2", 60, programada);   // no cuenta
        Detencion(t1.TurnoProduccionId, "DET-3", 18, null);         // sin tipo: cuenta
        Detencion(convertido, "DET-4", 48, mecanica);
        _context.ChangeTracker.Clear();

        var ind = await IndicadoresAsync(t1.TurnoProduccionId);
        var indConvertido = await IndicadoresAsync(convertido);
        var dashboard = await new DashboardService(_unitOfWork, ZonaPlanta).ObtenerIndisponibilidadAsync(new ReporteFiltroRequest
        {
            LineaId = _tango2.LineaId, Anio = 2026, MesDesde = 11, MesHasta = 11
        });

        Assert.Equal((48, 480, 10.0m), (ind.MinutosParadaQueCuentan, ind.MinutosProgramados, ind.Indisponibilidad!.Value));
        Assert.Equal((480, 10.0m), (indConvertido.MinutosProgramados, indConvertido.Indisponibilidad!.Value));
        Assert.True(dashboard.IsSuccess);
        var mes = Assert.Single(dashboard.Value!.Meses);
        Assert.Equal(960, mes.MinutosProgramados);       // los 2 turnos de producción, no los 30 días del mes
        Assert.Equal(96, mes.MinutosTotalesParada);
        Assert.Equal(10.0, mes.IndisponibilidadTotal);
    }
}
