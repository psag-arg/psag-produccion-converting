using System.Globalization;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Tarjeta "Producción por cámara" del Dashboard y Maestros → Cámaras: sin datos nunca es 0.</summary>
public class EficienciaUiTests
{
    public EficienciaUiTests() => CultureInfo.CurrentCulture = new CultureInfo("es-AR");

    [Fact]
    public void SinDatos_MuestraSinDatos_NoCero()
    {
        Assert.Equal("Sin datos", EficienciaUi.TextoContadas(new EficienciaTurnoDto { SinDatos = true, Contadas = 0 }));
    }

    [Fact]
    public void ConDatosEnCero_MuestraCero()
    {
        Assert.Equal("0", EficienciaUi.TextoContadas(new EficienciaTurnoDto { SinDatos = false, Contadas = 0 }));
        Assert.Equal("9.300", EficienciaUi.TextoContadas(new EficienciaTurnoDto { Contadas = 9300 }));
    }

    [Fact]
    public void Titulo_ConCanalYUnidad()
    {
        var c = new EficienciaCamaraPuntoDto { LineaNombre = "Tango 2", NumeroCanalGrabador = 9, Nombre = "x", Unidad = "rollos" };

        Assert.Equal("Tango 2 · Cámara 09 (rollos)", EficienciaUi.Titulo(c));
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
