using System.Globalization;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Tarjeta "Presupuesto vs. real" del Dashboard (T-798 etapa 5) y Maestros → Cámaras: sin datos nunca es 0.</summary>
public class EficienciaUiTests
{
    public EficienciaUiTests() => CultureInfo.CurrentCulture = new CultureInfo("es-AR");

    [Fact]
    public void SinDatos_MuestraSinDatos_NoCero()
    {
        var c = new ComparacionProduccionDto { Objetivo = 11200, Real = null, Estado = "SIN_DATOS" };

        Assert.Equal("Sin datos", EficienciaUi.TextoReal(c));
        Assert.Equal("Sin datos", EficienciaUi.TextoPorcentaje(c));
        Assert.Equal("text-muted", EficienciaUi.ClasePorcentaje(c));
    }

    [Fact]
    public void SinObjetivo_NoEsCeroPorCiento()
    {
        var c = new ComparacionProduccionDto { Objetivo = null, Real = 500, Estado = "SIN_OBJETIVO" };

        Assert.Equal("Sin objetivo", EficienciaUi.TextoObjetivo(c));
        Assert.Equal("Sin objetivo", EficienciaUi.TextoPorcentaje(c));
    }

    [Fact]
    public void ConDatos_PorcentajeConUnDecimal_YProvisorioMarcado()
    {
        Assert.Equal("83,0 %", EficienciaUi.TextoPorcentaje(new ComparacionProduccionDto { Porcentaje = 83.0m, Estado = "OK" }));
        Assert.Equal("92,3 % *", EficienciaUi.TextoPorcentaje(new ComparacionProduccionDto { Porcentaje = 92.3m, Estado = "OK", Provisorio = true }));
        Assert.Equal("0", EficienciaUi.TextoReal(new ComparacionProduccionDto { Real = 0, Estado = "OK", Porcentaje = 0 }));
        Assert.Equal("9.300", EficienciaUi.TextoReal(new ComparacionProduccionDto { Real = 9300 }));
        Assert.Equal("text-warning-emphasis", EficienciaUi.ClasePorcentaje(new ComparacionProduccionDto { Porcentaje = 83.0m, Estado = "OK" }));
        Assert.Equal("670 m/min", EficienciaUi.TextoVelocidad(669.9m));
        Assert.Equal("-", EficienciaUi.TextoVelocidad(null));
    }

    [Theory]
    [InlineData("VERDE", "bg-success")]
    [InlineData("AMARILLO", "bg-warning text-dark")]
    [InlineData("ROJO", "bg-danger")]
    [InlineData("SIN_DATOS", "bg-secondary")]
    public void UltimoDato_Color(string estado, string clase) => Assert.Equal(clase, CamarasUi.ClaseUltimoDato(estado));

    [Theory]
    [InlineData(null, "nunca")]
    [InlineData(0, "hace menos de 1 min")]
    [InlineData(7, "hace 7 min")]
    [InlineData(125, "hace 2 h 05 min")]
    public void UltimoDato_Texto(int? minutos, string texto) => Assert.Equal(texto, CamarasUi.TextoUltimoDato(minutos));
}
