using System.Globalization;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Tarjeta "Eficiencia (cámara)" del Dashboard: sin objetivo nunca muestra 0 %.</summary>
public class EficienciaUiTests
{
    public EficienciaUiTests() => CultureInfo.CurrentCulture = new CultureInfo("es-AR");

    [Fact]
    public void SinObjetivo_MuestraFaltaCargar_NoCeroPorCiento()
    {
        var t = new EficienciaTurnoDto { Estado = "SIN_OBJETIVO", Contadas = 500 };

        Assert.Equal("Falta cargar el objetivo", EficienciaUi.TextoPorcentaje(t));
    }

    [Fact]
    public void NoProgramado_NoMuestraPorcentaje()
    {
        var t = new EficienciaTurnoDto { Estado = "NO_PROGRAMADO", Objetivo = 0 };

        Assert.Equal("Turno no programado", EficienciaUi.TextoPorcentaje(t));
    }

    [Fact]
    public void ConObjetivo_MuestraElPorcentajeConComa()
    {
        var t = new EficienciaTurnoDto { Estado = "OK", Objetivo = 8000, Porcentaje = 87.5m };

        Assert.Equal("87,5 %", EficienciaUi.TextoPorcentaje(t));
    }

    [Theory]
    [InlineData(95.0, "text-success")]
    [InlineData(90.0, "text-success")]
    [InlineData(75.0, "text-warning")]
    [InlineData(40.0, "text-danger")]
    [InlineData(null, "text-muted")]
    public void ColorPorcentaje(double? porcentaje, string clase)
    {
        Assert.Equal(clase, EficienciaUi.ColorPorcentaje(porcentaje is { } p ? (decimal)p : null));
    }

    [Theory]
    [InlineData(130.0, "100%")]
    [InlineData(42.5, "42.5%")]
    [InlineData(null, "0%")]
    public void AnchoBarra_EntreCeroYCien(double? porcentaje, string ancho)
    {
        Assert.Equal(ancho, EficienciaUi.AnchoBarra(porcentaje is { } p ? (decimal)p : null));
    }

    [Fact]
    public void Titulo_ConNumeroDeCamaraYUnidad()
    {
        var c = new EficienciaCamaraPuntoDto { LineaNombre = "Tango 2", NumeroCamara = 9, Nombre = "x", Unidad = "rollos" };

        Assert.Equal("Tango 2 · Cámara 09 (rollos)", EficienciaUi.Titulo(c));
    }
}
