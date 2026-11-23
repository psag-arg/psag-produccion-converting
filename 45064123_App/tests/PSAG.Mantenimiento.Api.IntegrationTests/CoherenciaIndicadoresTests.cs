using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-805 · Coherencia de indicadores entre pantallas, contra SQLite o SQL Server con reloj fijo (planta UTC-3).
/// Turno 2 del 23/11 (13–21) en Tango 2, SH 30 (11.200 mangas / 6.500 bolsones por turno) declarado a las 13:05.
/// El objetivo del turno, el de a la hora, la eficiencia, mangas, bolsones, velocidad, indisponibilidad y microcortes son
/// los mismos en la fila del turno de api/indicadores/produccion, en api/indicadores/turno/{id} y en api/tablero/turno
/// (operador y gerencial): salen del mismo cálculo (IndicadoresService) con el mismo "ahora".
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class CoherenciaIndicadoresTests : IDisposable
{
    private const string Admin = "admin-t805";
    private const string Oper1 = "oper-t805";
    private static readonly string[] RolOperario = [RolesSistema.Operario];
    private static readonly DateOnly Dia = new(2026, 11, 23);

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly BaseDePrueba _base;
    private readonly MantenimientoDbContext _context;
    private readonly TurnoProduccionService _turnos;
    private readonly IndicadoresService _indicadores;
    private readonly TableroService _tablero;
    private DateTimeOffset _reloj;

    private readonly Linea _tango2;
    private readonly ProductoMaestro _sh30;
    private readonly ProductoMaestro _sh50;
    private readonly Camara _camaraMangas;
    private readonly TipoFalla _proceso;

    public CoherenciaIndicadoresTests()
    {
        _base = BaseDePrueba.Crear();
        _context = _base.Context;

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _sh30 = Producto("HIGIENICO SH 30 MTS X 4", 30, 1);
        _sh50 = Producto("HIGIENICO SH 50 MTS X 4", 50, 2);
        _proceso = new TipoFalla { Codigo = "FP", Nombre = "FALLA PROCESO", Orden = 1, CuentaParaIndicadores = true, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.AddRange(_tango2, Turno("T1", "Turno 1", 5, 13, 1), Turno("T2", "Turno 2", 13, 21, 2), Turno("T3", "Turno 3", 21, 5, 3),
            _sh30, _sh50, _proceso);
        _context.SaveChanges();

        var desde = new DateOnly(2026, 11, 1);
        _camaraMangas = new Camara
        {
            LineaId = _tango2.LineaId, Codigo = "T805-CAM-10", Nombre = "Cámara 10 mangas", Funcion = FuncionCamara.Conteo,
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

        var unitOfWork = new UnitOfWork(_context);
        _turnos = new TurnoProduccionService(unitOfWork, ZonaPlanta, new TurnosProduccionOpciones(),
            NullLogger<TurnoProduccionService>.Instance, () => _reloj);
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

    private static ProductoMaestro Producto(string nombre, decimal metros, int orden) => new()
    {
        Codigo = nombre[..15], Nombre = nombre, MetrosPorManga = metros, Orden = orden, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    private static DateTimeOffset H(int hora, int minuto = 0, int segundo = 0) => new(2026, 11, 23, hora, minuto, segundo, TimeSpan.FromHours(-3));

    private void Minutos(DateTimeOffset desde, int cantidadMinutos, decimal cantidad)
    {
        for (var i = 0; i < cantidadMinutos; i++)
        {
            _context.ConteosCamaraMinuto.Add(new ConteoCamaraMinuto
            {
                Minuto = desde.AddMinutes(i), CamaraId = _camaraMangas.CamaraId, Carril = 1, Cantidad = cantidad,
                SegundosMarcha = 60, SegundosParada = 0, Estado = EstadoCarrilCamara.MARCHA, FechaHoraCreacion = desde
            });
        }
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    /// <summary>Turno abierto a las 13:05 con SH 30 (producto declarado tarde), cámara, una detención y un microcorte.</summary>
    private async Task<long> TurnoDeclaradoTardeAsync()
    {
        Minutos(H(13, 5), 64, 19);    // 13:05–14:09: 19 mangas/min
        _reloj = H(13, 5);
        var abierto = await _turnos.AbrirAsync(_tango2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, _sh30.ProductoMaestroId, true, RolOperario, [_tango2.LineaId]);
        Assert.True(abierto.IsSuccess, abierto.ErrorMessage);
        var turno = abierto.Value!.Turno!.TurnoProduccionId;
        _context.ChangeTracker.Clear();

        _context.Detenciones.Add(new Detencion
        {
            Codigo = "DET-T805-1", LineaId = _tango2.LineaId, TurnoProduccionId = turno, TipoFallaId = _proceso.TipoFallaId,
            MinutosDeclarados = 12, MinutosEfectivos = 12, OrigenTiempo = OrigenTiempo.DECLARADO, Descripcion = "Prueba T-805",
            EstadoActual = EstadoDetencion.REGISTRADA, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        _context.ParadasCamara.Add(new ParadaCamara
        {
            CamaraId = _camaraMangas.CamaraId, TurnoProduccionId = turno, FechaHoraInicio = H(13, 30), FechaHoraFin = H(13, 30, 40),
            DuracionSegundos = 40, Tipo = TipoParadaCamara.PARADA, Carriles = "1", IdentificadorExterno = "T805-MC1",
            UmbralSegundos = 300, SuperaUmbral = false, FechaHoraCreacion = H(13, 30)
        });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return turno;
    }

    private async Task<(FilaPresupuestoRealDto Fila, IndicadoresTurnoDto Turno, TableroTurnoDto Operador, TableroTurnoDto Gerencial)> PantallasAsync(long turno)
    {
        _context.ChangeTracker.Clear();
        var produccion = await _indicadores.ObtenerProduccionAsync(new IndicadoresProduccionFiltro { Desde = Dia, Hasta = Dia, LineaId = _tango2.LineaId });
        Assert.True(produccion.IsSuccess, produccion.ErrorMessage);
        _context.ChangeTracker.Clear();
        var detalle = await _indicadores.ObtenerTurnoAsync(turno);
        Assert.True(detalle.IsSuccess, detalle.ErrorMessage);
        _context.ChangeTracker.Clear();
        var operador = await _tablero.ObtenerAsync(turno, soloLectura: false);
        Assert.True(operador.IsSuccess, operador.ErrorMessage);
        _context.ChangeTracker.Clear();
        var gerencial = await _tablero.ObtenerAsync(turno, soloLectura: true);
        Assert.True(gerencial.IsSuccess, gerencial.ErrorMessage);
        return (produccion.Value!.Filas.Single(f => f.TurnoProduccionId == turno), detalle.Value!, operador.Value!, gerencial.Value!);
    }

    /// <summary>Todos los números repetidos entre pantallas son iguales.</summary>
    private static void TodoIgual(FilaPresupuestoRealDto fila, IndicadoresTurnoDto turno, params TableroTurnoDto[] tableros)
    {
        Assert.Equal(fila.Mangas.Objetivo, turno.Mangas.Objetivo);
        Assert.Equal(fila.Mangas.ObjetivoTurno, turno.Mangas.ObjetivoTurno);
        Assert.Equal(fila.Mangas.Real, turno.Mangas.Real);
        Assert.Equal(fila.Bolsones.Objetivo, turno.Bolsones.Objetivo);
        Assert.Equal(fila.Bolsones.ObjetivoTurno, turno.Bolsones.ObjetivoTurno);
        Assert.Equal(fila.Bolsones.Real, turno.Bolsones.Real);
        Assert.Equal(fila.Eficiencia.Porcentaje, turno.Eficiencia.Porcentaje);
        Assert.Equal(fila.VelocidadMetrosMinuto, turno.VelocidadMetrosMinuto);
        Assert.Equal(fila.Indisponibilidad, turno.Indisponibilidad);
        Assert.Equal(fila.MinutosParadaQueCuentan, turno.MinutosParadaQueCuentan);
        foreach (var t in tableros)
        {
            Assert.Equal(turno.CalculadoEn, t.Indicadores.CalculadoEn);
            Assert.Equal(turno.CalculadoEn, t.GeneradoEn);
            Assert.Equal(turno.Mangas.ObjetivoTurno, t.Eficiencia.ObjetivoTurno);
            Assert.Equal(turno.Mangas.ObjetivoTurno, t.Produccion.Mangas.ObjetivoTurno);
            Assert.Equal(turno.Bolsones.ObjetivoTurno, t.Produccion.Bolsones.ObjetivoTurno);
            Assert.Equal(turno.Mangas.Objetivo, t.Eficiencia.ObjetivoALaHora);
            Assert.Equal(turno.Mangas.Objetivo, t.Produccion.Mangas.ObjetivoALaHora);
            Assert.Equal(turno.Bolsones.Objetivo, t.Produccion.Bolsones.ObjetivoALaHora);
            Assert.Equal(turno.Eficiencia.Porcentaje, t.Eficiencia.Porcentaje);
            Assert.Equal(turno.Mangas.Real, t.Eficiencia.Real);
            Assert.Equal(turno.Mangas.Real, t.Produccion.Mangas.Real);
            Assert.Equal(turno.Bolsones.Real, t.Produccion.Bolsones.Real);
            Assert.Equal(turno.VelocidadMetrosMinuto, t.Velocidad.Promedio);
            Assert.Equal(turno.Indisponibilidad, t.Indicadores.Indisponibilidad);
            Assert.Equal(fila.Microcortes, t.CifrasDetenciones.Microcortes.Cantidad);
            Assert.Equal(fila.MinutosMicrocortes, t.CifrasDetenciones.Microcortes.Minutos);
        }
    }

    [Fact]
    public async Task DeclaracionTardia_ObjetivoDelTurnoEntero_YMismosNumerosEnTodasLasPantallas()
    {
        var turno = await TurnoDeclaradoTardeAsync();
        _reloj = H(14, 9, 32);

        var (fila, detalle, operador, gerencial) = await PantallasAsync(turno);
        TodoIgual(fila, detalle, operador, gerencial);

        // Objetivo del turno: 11.200 / 6.500 aunque el producto se declaró a las 13:05 (antes: 11.083 / 6.432)
        Assert.Equal(11200m, detalle.Mangas.ObjetivoTurno);
        Assert.Equal(6500m, detalle.Bolsones.ObjetivoTurno);
        // A la hora 14:09:32: 69 min 32 s del turno → 11.200 × 69,53 / 480
        Assert.Equal(1622.4m, detalle.Mangas.Objetivo);
        Assert.Equal(941.6m, detalle.Bolsones.Objetivo);
        Assert.Equal(1216m, detalle.Mangas.Real);                       // 64 min × 19
        Assert.Equal(74.9m, detalle.Eficiencia.Porcentaje);             // 1.216 ÷ 1.622,44
        Assert.Equal("AMARILLO", operador.Eficiencia.Nivel);
        Assert.Equal(570m, detalle.VelocidadMetrosMinuto);              // 19 × 30
        Assert.Equal(2.5m, detalle.Indisponibilidad);                   // 12 ÷ 480
        Assert.Equal(1, fila.Microcortes);

        // Objetivo de la hora 13–14 del tablero: la hora entera (1.400), los 5 min sin producto incluidos
        Assert.Equal(1400m, operador.MangasPorHora.First().Objetivo);

        // Barra de turno (lee api/indicadores/turno o lo que publica el tablero): mismo % y mismo objetivo a la hora
        Assert.Equal(operador.Eficiencia.Porcentaje, detalle.Eficiencia.Porcentaje);
        Assert.Equal(operador.Eficiencia.ObjetivoALaHora, detalle.Mangas.Objetivo);
    }

    [Fact]
    public async Task TurnoCerrado_ElObjetivoDelTurnoEsElDeALaHora_EnTodasLasPantallas()
    {
        var turno = await TurnoDeclaradoTardeAsync();
        _reloj = H(21);
        var cierre = await _turnos.CerrarAsync(turno, null, Oper1, RolOperario, [_tango2.LineaId]);
        Assert.True(cierre.IsSuccess, cierre.ErrorMessage);
        _reloj = H(22);

        var (fila, detalle, operador, gerencial) = await PantallasAsync(turno);
        TodoIgual(fila, detalle, operador, gerencial);
        Assert.Equal(11200m, fila.Mangas.Objetivo);
        Assert.Equal(11200m, fila.Mangas.ObjetivoTurno);
        Assert.Equal(6500m, fila.Bolsones.Objetivo);
    }

    [Fact]
    public async Task CambioDeProducto_ConHuecoSinProducto_ObjetivoSegunLaRegla()
    {
        var turno = await TurnoDeclaradoTardeAsync();
        _reloj = H(16);
        var cambio = await _turnos.CambiarProductoAsync(turno, new CambiarProductoTurnoRequest { ProductoMaestroId = _sh50.ProductoMaestroId },
            Oper1, RolOperario, [_tango2.LineaId]);
        Assert.True(cambio.IsSuccess, cambio.ErrorMessage);
        _context.ChangeTracker.Clear();
        // Hueco sin producto 15:40–16:00 (el SH 30 terminó a las 15:40)
        var sh30 = _context.TurnosProduccionProductos.Single(p => p.TurnoProduccionId == turno && p.ProductoMaestroId == _sh30.ProductoMaestroId);
        sh30.Hasta = H(15, 40);
        _context.SaveChanges();
        _reloj = H(17);

        var (fila, detalle, operador, gerencial) = await PantallasAsync(turno);
        TodoIgual(fila, detalle, operador, gerencial);

        // SH 30: 13:00–15:50 (5 min del principio + 155 propios + 10 del hueco) = 170 min; SH 50: 15:50–21:00 = 310 min
        Assert.Equal([170m, 310m], detalle.Tramos.Select(t => t.MinutosObjetivoTurno));
        Assert.Equal(8306.7m, detalle.Mangas.ObjetivoTurno);            // 11.200 × 170/480 + 6.720 × 310/480
        // A las 17: SH 30 entero (170) + SH 50 de 15:50 a 17:00 (70) = 3.966,7 + 980
        Assert.Equal([170m, 70m], detalle.Tramos.Select(t => t.MinutosObjetivo));
        Assert.Equal(4946.7m, detalle.Mangas.Objetivo);
    }
}
