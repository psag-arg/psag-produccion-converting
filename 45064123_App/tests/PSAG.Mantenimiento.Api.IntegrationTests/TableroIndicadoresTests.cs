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
/// T-770 iteración 2 · Los 4 bloques del tablero (TableroService contra SQLite o SQL Server, reloj movible, planta UTC-3).
/// Turno 2 del 19/11 (13–21) en Tango 2: SH 30 (30 m/manga, 11.200 mangas y 6.500 bolsones por turno) de 13 a 16 y SH 50
/// (50 m/manga, 6.720 y 4.700) desde las 16; "ahora" 17:00.
/// <list type="bullet">
/// <item>Eficiencia: aguja contra el objetivo a la hora (5.040) con los cortes 65/85; objetivo del turno completo 8.400.</item>
/// <item>Producción: mangas contra 8.400; bolsones sin cámara = "Sin datos" (nunca 0 %).</item>
/// <item>Velocidad: 600 m/min con SH 30, parada en 0, 650 m/min con SH 50, minutos sin fila = null.</item>
/// <item>Detenciones: por tipo del maestro, programadas aparte, pendientes con los minutos de la parada de cámara.</item>
/// </list>
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class TableroIndicadoresTests : IDisposable
{
    private const string Admin = "admin-t770-2";
    private const string Oper1 = "oper-t770-2";
    private static readonly string[] RolOperario = [RolesSistema.Operario];

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly BaseDePrueba _base;
    private readonly MantenimientoDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly TurnoProduccionService _turnos;
    private readonly TableroService _tablero;
    private DateTimeOffset _reloj;

    private readonly Linea _tango2;
    private readonly ProductoMaestro _sh30;
    private readonly ProductoMaestro _sh50;
    private readonly Camara _camaraMangas;
    private readonly TipoFalla _proceso;
    private readonly TipoFalla _equipo;
    private readonly TipoFalla _programada;

    public TableroIndicadoresTests()
    {
        _base = BaseDePrueba.Crear();
        _context = _base.Context;

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _sh30 = Producto("HIGIENICO SH 30 MTS X 4", 30, 1);
        _sh50 = Producto("HIGIENICO SH 50 MTS X 4", 50, 2);
        _proceso = Tipo("FP", "FALLA PROCESO", 1, true);
        _equipo = Tipo("FE", "FALLA EQUIPO", 2, true);
        _programada = Tipo("PP", "PARADA PROGRAMADA", 3, false);
        _context.AddRange(_tango2, Turno("T1", "Turno 1", 5, 13, 1), Turno("T2", "Turno 2", 13, 21, 2), Turno("T3", "Turno 3", 21, 5, 3),
            _sh30, _sh50, _proceso, _equipo, _programada, new TipoFalla { Codigo = "VJ", Nombre = "VIEJO", Orden = 9, Activo = false, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin });
        _context.SaveChanges();

        var desde = new DateOnly(2026, 11, 1);
        _camaraMangas = new Camara
        {
            LineaId = _tango2.LineaId, Codigo = "T770-CAM-10", Nombre = "Cámara 10 mangas", Funcion = FuncionCamara.Conteo,
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

        _reloj = H(13);
        _unitOfWork = new UnitOfWork(_context);
        _turnos = new TurnoProduccionService(_unitOfWork, ZonaPlanta, new TurnosProduccionOpciones(),
            NullLogger<TurnoProduccionService>.Instance, () => _reloj);
        _tablero = new TableroService(_unitOfWork, new IndicadoresService(_unitOfWork, ZonaPlanta, () => _reloj), ZonaPlanta, () => _reloj);
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

    private static ProductoMaestro Producto(string nombre, decimal metros, int orden) => new()
    {
        Codigo = nombre[..15], Nombre = nombre, MetrosPorManga = metros, Orden = orden, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    private static TipoFalla Tipo(string codigo, string nombre, int orden, bool cuenta) => new()
    {
        Codigo = codigo, Nombre = nombre, Orden = orden, CuentaParaIndicadores = cuenta, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    private static DateTimeOffset H(int hora, int minuto = 0) => new(2026, 11, 19, hora, minuto, 0, TimeSpan.FromHours(-3));

    private void Minutos(DateTimeOffset desde, int cantidadMinutos, decimal cantidad, short segundosMarcha = 60)
    {
        for (var i = 0; i < cantidadMinutos; i++)
        {
            _context.ConteosCamaraMinuto.Add(new ConteoCamaraMinuto
            {
                Minuto = desde.AddMinutes(i), CamaraId = _camaraMangas.CamaraId, Carril = 1, Cantidad = cantidad,
                SegundosMarcha = segundosMarcha, SegundosParada = (short)(60 - segundosMarcha),
                Estado = segundosMarcha > 0 ? EstadoCarrilCamara.MARCHA : EstadoCarrilCamara.PARADA, FechaHoraCreacion = desde
            });
        }
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private Detencion Det(string codigo, long turno, TipoFalla? tipo, int minutos) => new()
    {
        Codigo = codigo, LineaId = _tango2.LineaId, TurnoProduccionId = turno, TipoFallaId = tipo?.TipoFallaId,
        MinutosDeclarados = minutos, MinutosEfectivos = minutos, OrigenTiempo = OrigenTiempo.DECLARADO, Descripcion = "Prueba T-770",
        EstadoActual = EstadoDetencion.REGISTRADA, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    private Detencion Pendiente(string codigo, long turno, DateTimeOffset parada, DateTimeOffset? arranque) => new()
    {
        Codigo = codigo, LineaId = _tango2.LineaId, TurnoProduccionId = turno, FechaHoraParada = parada, FechaHoraArranqueDetectada = arranque,
        OrigenTiempo = OrigenTiempo.CALCULADO, MinutosEfectivos = 0, Descripcion = string.Empty,
        EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION, OrigenRegistro = OrigenRegistroDetencion.CAMARA,
        IdentificadorParadaCamara = codigo, CamaraId = _camaraMangas.CamaraId, CreadoPorUsuarioId = "SISTEMA", ModificadoPorUsuarioId = "SISTEMA"
    };

    /// <summary>Turno 2 con SH 30 de 13 a 16 y SH 50 desde las 16; cámara, detenciones; reloj 17:00.</summary>
    private async Task<long> TurnoConDatosAsync()
    {
        Minutos(H(13), 60, 20);          // 13:00–14:00 SH 30: 20 mangas/min = 600 m/min
        Minutos(H(14), 10, 0, 0);        // 14:00–14:10 parada (filas en 0)
        Minutos(H(14, 10), 110, 20);     // 14:10–16:00
        Minutos(H(16), 30, 13);          // 16:00–16:30 SH 50: 13 mangas/min = 650 m/min
        // 16:30–16:35 sin filas (cámara sin datos)
        Minutos(H(16, 35), 25, 13);      // 16:35–17:00

        _reloj = H(13);
        var abierto = await _turnos.AbrirAsync(_tango2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, _sh30.ProductoMaestroId, true, RolOperario, [_tango2.LineaId]);
        Assert.True(abierto.IsSuccess, abierto.ErrorMessage);
        var turno = abierto.Value!.Turno!.TurnoProduccionId;
        _reloj = H(16);
        var cambio = await _turnos.CambiarProductoAsync(turno, new CambiarProductoTurnoRequest { ProductoMaestroId = _sh50.ProductoMaestroId },
            Oper1, RolOperario, [_tango2.LineaId]);
        Assert.True(cambio.IsSuccess, cambio.ErrorMessage);
        _context.ChangeTracker.Clear();

        _context.Detenciones.AddRange(
            Det("DET-T770-P1", turno, _proceso, 12),
            Det("DET-T770-P2", turno, _proceso, 8),
            Det("DET-T770-E1", turno, _equipo, 5),
            Det("DET-T770-PP", turno, _programada, 30),
            Pendiente("DET-T770-PEN1", turno, H(14), H(14, 10)),   // la cámara la vio arrancar: 10 min
            Pendiente("DET-T770-PEN2", turno, H(16, 50), null));   // sigue parada: hasta ahora
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        _reloj = H(17);
        return turno;
    }

    private async Task<TableroTurnoDto> TableroAsync(long turno)
    {
        _context.ChangeTracker.Clear();
        var r = await _tablero.ObtenerAsync(turno, soloLectura: false);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return r.Value!;
    }

    [Fact]
    public async Task Eficiencia_AgujaContraElObjetivoALaHora_ConLosCortesNuevos_YElObjetivoDelTurno()
    {
        var t = await TableroAsync(await TurnoConDatosAsync());
        var e = t.Eficiencia;

        // Objetivo a la hora: SH 30 3 h (4.200) + SH 50 1 h (840); del turno completo: 4.200 + SH 50 5 h (4.200)
        Assert.Equal(5040m, e.ObjetivoALaHora);
        Assert.Equal(8400m, e.ObjetivoTurno);
        Assert.Equal(t.Indicadores.Mangas.Real, e.Real);
        Assert.Equal(t.Indicadores.Eficiencia.Porcentaje, e.Porcentaje);
        Assert.Equal("OK", e.Estado);
        Assert.Equal(65m, e.UmbralRojo);
        Assert.Equal(85m, e.UmbralVerde);
        Assert.Equal(TableroIndicadoresReglas.NivelEficiencia(e.Porcentaje), e.Nivel);
        Assert.InRange(e.Porcentaje!.Value, 65m, 85m);            // ~81 %: con los cortes viejos (95/85) era rojo
        Assert.Equal("AMARILLO", e.Nivel);
        Assert.Equal(TableroIndicadoresReglas.Porcentaje(e.Real, 8400m), e.PorcentajeTurno);
    }

    [Fact]
    public async Task Produccion_MangasContraElObjetivoDelTurno_YBolsonesSinCamaraSinDatos()
    {
        var t = await TableroAsync(await TurnoConDatosAsync());
        var p = t.Produccion;

        Assert.Equal(_sh50.Nombre, p.ProductoNombre);
        Assert.Equal([_sh30.Nombre, _sh50.Nombre], p.Productos);

        Assert.Equal("MANGAS", p.Mangas.Magnitud);
        Assert.Equal(8400m, p.Mangas.ObjetivoTurno);
        Assert.Equal(5040m, p.Mangas.ObjetivoALaHora);
        Assert.Equal("OK", p.Mangas.Estado);
        Assert.NotNull(p.Mangas.Real);
        Assert.Equal(TableroIndicadoresReglas.Porcentaje(p.Mangas.Real, 8400m), p.Mangas.PorcentajeTurno);
        Assert.Equal("AMARILLO", p.Mangas.Nivel);

        // Bolsones: objetivo 6.500 × 3/8 + 4.700 × 5/8 = 5.375; sin cámara ni carga manual: "Sin datos", nunca 0 %
        Assert.Equal(5375m, p.Bolsones.ObjetivoTurno);
        Assert.Null(p.Bolsones.Real);
        Assert.Null(p.Bolsones.PorcentajeTurno);
        Assert.Equal("SIN_DATOS", p.Bolsones.Estado);
        Assert.Equal("SIN_DATO", p.Bolsones.Nivel);
    }

    [Fact]
    public async Task Velocidad_PorMinuto_DosProductosDeDistintoMetraje_ParadaEnCero_YHuecoSinDatos()
    {
        var t = await TableroAsync(await TurnoConDatosAsync());
        var v = t.Velocidad;

        Assert.Equal(H(13), v.Desde);
        Assert.Equal(H(13), v.EjeDesde);
        Assert.Equal(H(21), v.EjeHasta);
        Assert.Equal(240, v.MetrosPorMinuto.Count);               // 13:00 a 17:00
        Assert.Equal(600m, v.MetrosPorMinuto[0]);                 // SH 30: 20 × 30
        Assert.All(v.MetrosPorMinuto.Skip(60).Take(10), x => Assert.Equal(0m, x));   // parada 14:00–14:10 en 0
        Assert.Equal(600m, v.MetrosPorMinuto[70]);
        Assert.Equal(650m, v.MetrosPorMinuto[180]);               // 16:00 ya es SH 50: 13 × 50
        Assert.All(v.MetrosPorMinuto.Skip(210).Take(5), x => Assert.Null(x));        // 16:30–16:35 sin datos
        Assert.Equal(650m, v.MetrosPorMinuto[239]);
        Assert.Equal(650m, v.Actual);
        Assert.Equal(670m, v.Referencia);
        Assert.False(v.FaltaMetraje);
        Assert.Equal([30m, 50m], v.Tramos.Select(x => x.MetrosPorManga!.Value));
        Assert.Equal(t.Indicadores.VelocidadMetrosMinuto, v.Promedio);
    }

    [Fact]
    public async Task Detenciones_PorTipo_TotalYPendientesDesdeLaParadaDeCamara()
    {
        var turno = await TurnoConDatosAsync();
        var d = (await TableroAsync(turno)).DetencionesPorTipo;

        Assert.Equal(["FALLA PROCESO|20|2", "FALLA EQUIPO|5|1", "PARADA PROGRAMADA|30|1", "Sin declarar|20|2"],
            d.Barras.Select(b => $"{b.Nombre}|{b.Minutos}|{b.Cantidad}"));
        Assert.True(d.Barras.Single(b => b.Nombre == "Sin declarar").SinDeclarar);
        Assert.False(d.Barras.Single(b => b.Nombre == "PARADA PROGRAMADA").Cuenta);
        // Pendientes: 14:00→14:10 (arranque detectado) + 16:50→17:00 (sigue parada, hasta ahora)
        Assert.Equal(20, d.MinutosPendientes);
        Assert.Equal(2, d.CantidadPendientes);
        Assert.Equal(45, d.MinutosTotal);          // 20 + 5 + 20 pendientes; la programada (30) va aparte
        Assert.Equal(30, d.MinutosNoCuentan);

        // Una hora después sigue parada: los pendientes crecen con el reloj
        _reloj = H(18);
        Assert.Equal(80, (await TableroAsync(turno)).DetencionesPorTipo.MinutosPendientes);

        // Turno automático: el turno de máquina cierra a las 21 (no antes). La que sigue parada se corta ahí: su parte en este
        // turno va hasta las 21 (16:50→21:00 = 250) y lo que sigue es otra detención, en el T3.
        _reloj = H(21, 5);
        Assert.Equal(1, await _turnos.CerrarVencidosAsync(H(21, 5)));
        _reloj = H(22);
        var cerrado = await TableroAsync(turno);
        Assert.False(cerrado.Abierto);
        Assert.Equal(260, cerrado.DetencionesPorTipo.MinutosPendientes);
    }

    [Fact]
    public async Task Detenciones_CuatroCifras_ConDatosMixtos()
    {
        var turno = await TurnoConDatosAsync();
        // Una detención manual declarada sin tipo todavía (cuenta) y paradas de cámara: 1 real y 3 microcortes (40 + 70 + 50 s)
        _context.Detenciones.Add(Det("DET-T770-MAN", turno, null, 4));
        _context.ParadasCamara.AddRange(
            Parada(turno, H(14), 600, "T770C-REAL", supera: true),
            Parada(turno, H(15), 40, "T770C-MC1", supera: false),
            Parada(turno, H(15, 30), 70, "T770C-MC2", supera: false),
            Parada(turno, H(16, 10), 50, "T770C-MC3", supera: false));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        var t = await TableroAsync(turno);
        var c = t.CifrasDetenciones;
        Assert.Equal((2, 20), (c.Pendientes.Cantidad, c.Pendientes.Minutos));       // 14:00→14:10 + 16:50→17:00 (desde la parada de cámara)
        Assert.Equal((4, 29), (c.Declaradas.Cantidad, c.Declaradas.Minutos));       // 12 + 8 + 5 + 4 (sin tipo); la programada no
        Assert.Equal((6, 49), (c.Totales.Cantidad, c.Totales.Minutos));             // declaradas + pendientes
        Assert.Equal((3, 3), (c.Microcortes.Cantidad, c.Microcortes.Minutos));      // 160 s; no suman en totales
        Assert.Equal((1, 30), (c.Programadas.Cantidad, c.Programadas.Minutos));     // aparte
        Assert.Equal(t.DetencionesPorTipo.MinutosTotal, c.Totales.Minutos);
        Assert.Equal(t.Microcortes.Cantidad, c.Microcortes.Cantidad);
        Assert.Equal(t.PendientesDeclarar.Count, c.Pendientes.Cantidad);

        // Al declarar una pendiente pasa de Pendientes a Declaradas; Totales igual en cantidad
        var pen = _context.Detenciones.Single(d => d.Codigo == "DET-T770-PEN1");
        pen.EstadoActual = EstadoDetencion.REGISTRADA;
        pen.TipoFallaId = _equipo.TipoFallaId;
        pen.MinutosDeclarados = pen.MinutosEfectivos = 11;
        pen.OrigenTiempo = OrigenTiempo.DECLARADO;
        pen.Descripcion = "Declarada en la prueba";
        _context.SaveChanges();
        var despues = (await TableroAsync(turno)).CifrasDetenciones;
        Assert.Equal((1, 10), (despues.Pendientes.Cantidad, despues.Pendientes.Minutos));
        Assert.Equal((5, 40), (despues.Declaradas.Cantidad, despues.Declaradas.Minutos));
        Assert.Equal((6, 50), (despues.Totales.Cantidad, despues.Totales.Minutos));
    }

    private ParadaCamara Parada(long turno, DateTimeOffset inicio, int segundos, string id, bool supera) => new()
    {
        CamaraId = _camaraMangas.CamaraId, TurnoProduccionId = turno, FechaHoraInicio = inicio, FechaHoraFin = inicio.AddSeconds(segundos),
        DuracionSegundos = segundos, Tipo = TipoParadaCamara.PARADA, Carriles = "1", IdentificadorExterno = id,
        UmbralSegundos = 300, SuperaUmbral = supera, FechaHoraCreacion = inicio
    };
}
