using System.Globalization;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>T-798 etapa 6: barra de turno, estado de la producción oficial y microcortes. Sin datos nunca es 0.</summary>
public class ProduccionUiTests
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);

    public ProduccionUiTests() => CultureInfo.CurrentCulture = new CultureInfo("es-AR");

    private static DateTimeOffset Hora(int h, int m = 0, int dia = 24) => new(2026, 11, dia, h, m, 0, Planta);

    // ===================== Formato "sin datos" =====================

    [Fact]
    public void TextoCantidad_SinDatoEsSinDatos_YCeroEsCero()
    {
        Assert.Equal("Sin datos", ProduccionUi.TextoCantidad(null));
        Assert.Equal("0", ProduccionUi.TextoCantidad(0));
        Assert.Equal("4.100", ProduccionUi.TextoCantidad(4100));
    }

    [Fact]
    public void Barra_SinDatosNiObjetivo_NoMuestraCero()
    {
        var sinDatos = new ComparacionProduccionDto { Objetivo = 5060, Real = null, Estado = "SIN_DATOS" };
        var sinObjetivo = new ComparacionProduccionDto { Objetivo = null, Real = 300, Estado = "SIN_OBJETIVO" };

        Assert.Equal("Eficiencia parcial: sin datos", ProduccionUi.TextoEficienciaBarra(sinDatos, abierto: true));
        Assert.Equal("(mangas sin datos / obj. a la hora 5.060)", ProduccionUi.TextoMangasBarra(sinDatos, abierto: true));
        Assert.Equal("Eficiencia: sin objetivo", ProduccionUi.TextoEficienciaBarra(sinObjetivo, abierto: false));
        Assert.Equal("(mangas 300 / obj. sin objetivo)", ProduccionUi.TextoMangasBarra(sinObjetivo, abierto: false));
    }

    // ===================== Colores de estado =====================

    [Theory]
    [InlineData("VALIDADA", "text-bg-success", "Validada")]
    [InlineData("PENDIENTE", "text-bg-warning", "Pendiente de validar")]
    [InlineData("PARCIAL", "text-bg-info", "Parcial (en curso)")]
    [InlineData("SIN_DATOS", "text-bg-secondary", "Sin datos")]
    [InlineData(null, "text-bg-secondary", "Sin datos")]
    public void EstadoReal_ColorYTexto(string? estado, string clase, string texto)
    {
        Assert.Equal(clase, ProduccionUi.ClaseEstadoReal(estado));
        Assert.Equal(texto, ProduccionUi.TextoEstadoReal(estado));
    }

    [Fact]
    public void Acciones_SoloConTramoCerrado_ValidarSoloSiHayCamaraPendiente()
    {
        var pendiente = new MagnitudTramoDto { Magnitud = "MANGAS", EstadoReal = "PENDIENTE", Vigente = new ProduccionTurnoDto { CantidadCamara = 9300 } };
        var bolsonesSinCamara = new MagnitudTramoDto { Magnitud = "BOLSONES", EstadoReal = "SIN_DATOS" };

        Assert.Equal((false, false, false), ProduccionUi.AccionesProduccion(tramoAbierto: true, pendiente));
        Assert.Equal((true, true, true), ProduccionUi.AccionesProduccion(tramoAbierto: false, pendiente));
        Assert.Equal((false, false, true), ProduccionUi.AccionesProduccion(tramoAbierto: false, bolsonesSinCamara));
    }

    // ===================== Objetivo a la hora =====================

    [Fact]
    public void ObjetivoALaHora_Prorrateado()
    {
        // 11.200 mangas en 480 min; a las 3 h 37 min (217 min) del turno: 5.063,3.
        Assert.Equal(5063.3m, ProduccionUi.ObjetivoALaHora(11200, 480, 217));
        Assert.Equal(11200m, ProduccionUi.ObjetivoALaHora(11200, 480, 480));
        Assert.Equal(0m, ProduccionUi.ObjetivoALaHora(11200, 480, -5));
        Assert.Null(ProduccionUi.ObjetivoALaHora(null, 480, 217));
        Assert.Null(ProduccionUi.ObjetivoALaHora(11200, 0, 217));
    }

    [Fact]
    public void Barra_ComoEnLaDemo()
    {
        var turno = new TurnoProduccionDto
        {
            LineaId = 2, LineaNombre = "Tango 2", TurnoCodigo = "T2", Estado = "ABIERTO",
            InicioProgramado = Hora(13), FinProgramado = Hora(21),
            ProductoActual = new TramoProductoDto { ProductoNombre = "SH 30", Desde = Hora(13, 5) },
            OperadorActual = new TramoOperadorDto { OperadorUsuarioId = "u1", OperadorNombre = "Juan" }
        };
        var ind = new IndicadoresTurnoDto
        {
            Eficiencia = new ComparacionProduccionDto { Porcentaje = 81.0m, Estado = "OK", Provisorio = true },
            Mangas = new ComparacionProduccionDto { Objetivo = 5060, Real = 4100, Estado = "OK" }
        };

        var partes = ProduccionUi.PartesBarra(turno, ind);

        Assert.Equal("Tango 2 · T2 (13–21) · SH 30 desde 13:05 · Op. Juan · Eficiencia parcial 81 % (mangas 4.100 / obj. a la hora 5.060)",
            string.Join(" · ", partes));
    }

    [Fact]
    public void Barra_SinIndicadoresNiProducto_NoSeRompe()
    {
        var turno = new TurnoProduccionDto
        {
            LineaNombre = "Tango 2", TurnoCodigo = "T3", Estado = "ABIERTO",
            InicioProgramado = Hora(21), FinProgramado = Hora(5, 0, 25)
        };

        Assert.Equal(["Tango 2", "T3 (21–05)", "Producto a completar", "Sin operador"], ProduccionUi.PartesBarra(turno, null));
        Assert.Equal("T1 (05:30–13)", ProduccionUi.TextoTurno("T1", Hora(5, 30), Hora(13)));
    }

    [Fact]
    public void PuedeOperarTurno_OperarioSoloEnSuLinea_SupervisionEnTodas()
    {
        Assert.True(ProduccionUi.PuedeOperarTurno(["OPERARIO"], [2], 2));
        Assert.False(ProduccionUi.PuedeOperarTurno(["OPERARIO"], [3], 2));
        Assert.True(ProduccionUi.PuedeOperarTurno(["SUPERVISOR"], [], 2));
        Assert.False(ProduccionUi.PuedeOperarTurno(["GERENCIA"], [2], 2));
    }

    // ===================== Microcortes =====================

    private static ParadaCamaraDto Parada(long? turno, int camara, int segundos, bool supera, int minuto = 0) => new()
    {
        TurnoProduccionId = turno, TurnoNombre = turno == null ? null : "T2", CamaraId = camara, CamaraCodigo = $"CAM-{camara:00}",
        LineaNombre = "Tango 2", DuracionSegundos = segundos, SuperaUmbral = supera, FechaHoraInicio = Hora(14, minuto)
    };

    [Fact]
    public void Microcortes_AgrupaPorTurnoYCamara()
    {
        var filas = ProduccionUi.MicrocortesPorTurno(
        [
            Parada(10, 9, 40, false), Parada(10, 9, 50, false, 5), Parada(10, 9, 600, true, 10),
            Parada(10, 10, 30, false), Parada(null, 9, 20, false)
        ]);

        Assert.Equal(3, filas.Count);
        var cam9 = filas[0];
        Assert.Equal((10L, 9, 2, 90, 1, 600), (cam9.TurnoProduccionId!.Value, cam9.CamaraId, cam9.Microcortes, cam9.SegundosMicrocortes, cam9.Paradas, cam9.SegundosParadas));
        Assert.Equal(2, cam9.MinutosMicrocortes);
        Assert.Equal(10, cam9.MinutosParadas);
        Assert.Equal(10, filas[1].CamaraId);
        Assert.Equal("Sin turno", filas[2].TurnoNombre);
    }
}
