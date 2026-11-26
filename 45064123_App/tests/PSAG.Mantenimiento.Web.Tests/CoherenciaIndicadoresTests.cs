using System.Globalization;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Services;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// T-805 · Coherencia entre pantallas: un solo semáforo de eficiencia (rojo &lt; 65, amarillo 65–85, verde &gt; 85) en
/// el dashboard (tarjetas y tabla de presupuesto vs. real), la barra de turno y el tablero (medidor, barras, tramos);
/// y la barra de turno usa los indicadores que publica el tablero del mismo turno.
/// </summary>
public class CoherenciaIndicadoresTests
{
    public CoherenciaIndicadoresTests() => CultureInfo.CurrentCulture = new CultureInfo("es-AR");

    private static ComparacionProduccionDto Ok(decimal p) => new() { Porcentaje = p, Estado = "OK" };

    [Theory]
    [InlineData(84.9, "AMARILLO", "text-warning-emphasis")]
    [InlineData(64.9, "ROJO", "text-danger")]
    [InlineData(85.1, "VERDE", "text-success")]
    [InlineData(65.0, "AMARILLO", "text-warning-emphasis")]
    [InlineData(85.0, "AMARILLO", "text-warning-emphasis")]
    public void Semaforo_IgualEnDashboardBarraYTablero(double porcentaje, string nivelApi, string claseTexto)
    {
        var p = (decimal)porcentaje;
        var esperado = GraficosUi.NivelDeApi(nivelApi);

        // Tablero: medidor y barras (nivel de la API y el de la Web coinciden), tabla de tramos
        Assert.Equal(esperado, GraficosUi.NivelEficiencia(p));
        Assert.Equal(esperado, TableroUi.NivelEficiencia(Ok(p)));
        // Dashboard (tarjeta y tabla) y barra de turno: la misma clase de color
        Assert.Equal(claseTexto, EficienciaUi.ClasePorcentaje(Ok(p)));
        Assert.Equal(claseTexto, EficienciaUi.ClaseNivel(esperado));
    }

    [Fact]
    public void Semaforo_SinDato_Gris()
    {
        Assert.Equal("text-muted", EficienciaUi.ClasePorcentaje(new ComparacionProduccionDto { Estado = "SIN_DATOS" }));
        Assert.Equal("text-muted", EficienciaUi.ClasePorcentaje(new ComparacionProduccionDto { Estado = "SIN_OBJETIVO", Porcentaje = 50 }));
    }

    [Fact]
    public void Barra_ConTableroAbierto_UsaLosIndicadoresDelTablero()
    {
        var enPantalla = new IndicadoresEnPantalla();
        IndicadoresTurnoDto? avisado = null;
        enPantalla.Publicado += i => avisado = i;

        // Sin tablero: la barra pide los suyos (ParaBarra = null)
        Assert.Null(enPantalla.ParaBarra(7));

        var fuente = enPantalla.AnotarFuente(7);
        var t1 = new IndicadoresTurnoDto { TurnoProduccionId = 7, CalculadoEn = new DateTimeOffset(2026, 11, 23, 14, 9, 32, TimeSpan.FromHours(-3)) };
        enPantalla.Publicar(t1);
        Assert.Same(t1, avisado);
        Assert.Same(t1, enPantalla.ParaBarra(7));
        Assert.Null(enPantalla.ParaBarra(8));

        // Un cálculo más viejo (la barra que había pedido antes) no pisa al del tablero
        enPantalla.Publicar(new IndicadoresTurnoDto { TurnoProduccionId = 7, CalculadoEn = t1.CalculadoEn.AddMinutes(-1) });
        Assert.Same(t1, enPantalla.ParaBarra(7));

        // Al salir del tablero la barra vuelve a pedir los suyos
        fuente.Dispose();
        fuente.Dispose();
        Assert.Null(enPantalla.ParaBarra(7));
        Assert.Same(t1, enPantalla.Ultimo(7));
    }

    [Fact]
    public void Dashboard_TurnoEnCurso_MuestraTambienElObjetivoDelTurno()
    {
        Assert.Equal("del turno 11.200", EficienciaUi.TextoObjetivoDelTurno(new ComparacionProduccionDto { Objetivo = 1622.4m, ObjetivoTurno = 11200m }));
        Assert.Null(EficienciaUi.TextoObjetivoDelTurno(new ComparacionProduccionDto { Objetivo = 11200m, ObjetivoTurno = 11200m }));
        Assert.Null(EficienciaUi.TextoObjetivoDelTurno(new ComparacionProduccionDto { Objetivo = null, ObjetivoTurno = null }));
    }
}
