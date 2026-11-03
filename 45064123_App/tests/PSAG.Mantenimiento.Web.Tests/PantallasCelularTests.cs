using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Dictado por voz: cómo se suma lo dictado al campo.</summary>
public class DictadoTextoTests
{
    [Theory]
    [InlineData(null, "se cortó el papel", "Se cortó el papel")]
    [InlineData("", "  cambio de cuchilla ", "Cambio de cuchilla")]
    [InlineData("Se cortó el papel", "y se reenhebró", "Se cortó el papel y se reenhebró")]
    [InlineData("Se cortó el papel.", "después se ajustó", "Se cortó el papel. Después se ajustó")]
    [InlineData("Línea 1\n", "segunda línea", "Línea 1\nSegunda línea")]
    [InlineData("Hola ", "", "Hola ")]
    public void Agregar_SumaAlFinal(string? actual, string dictado, string esperado)
    {
        Assert.Equal(esperado, DictadoTexto.Agregar(actual, dictado));
    }

    [Fact]
    public void Agregar_RespetaElLargoMaximo()
    {
        var r = DictadoTexto.Agregar("12345", "678901", largoMaximo: 8);
        Assert.Equal(8, r.Length);
        Assert.Equal("12345 67", r);
    }

    [Theory]
    [InlineData("not-allowed", "micrófono")]
    [InlineData("no-speech", "No se escuchó")]
    [InlineData("sin-https", "HTTPS")]
    [InlineData("sin-api", "Chrome o Edge")]
    [InlineData("cualquier-cosa", "No se pudo usar el dictado")]
    public void MensajeError_EsClaro(string codigo, string contiene)
    {
        Assert.Contains(contiene, DictadoTexto.MensajeError(codigo));
    }
}

/// <summary>Control de proceso en el celular: números con coma y estado del punto.</summary>
public class ControlProcesoUiTests
{
    [Theory]
    [InlineData("152", 152)]
    [InlineData("0,7", 0.7)]
    [InlineData("0.7", 0.7)]
    [InlineData("-0,7", -0.7)]
    [InlineData(",5", 0.5)]
    [InlineData("-,3", -0.3)]
    [InlineData(" 98,5 ", 98.5)]
    [InlineData("−0,5", -0.5)]
    public void TryLeerNumero_AceptaComaYPunto(string texto, double esperado)
    {
        Assert.True(ControlProcesoUi.TryLeerNumero(texto, out var v));
        Assert.Equal((decimal)esperado, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.234,5")]
    [InlineData("1,2,3")]
    [InlineData("5-")]
    public void TryLeerNumero_RechazaLoAmbiguo(string texto)
    {
        Assert.False(ControlProcesoUi.TryLeerNumero(texto, out _));
    }

    [Theory]
    [InlineData(0.7, "0,7")]
    [InlineData(-200, "-200")]
    [InlineData(98.50, "98,5")]
    public void MostrarNumero_ConComa(double valor, string esperado)
    {
        Assert.Equal(esperado, ControlProcesoUi.MostrarNumero((decimal)valor));
    }

    private static RegistroProcesoDetalleDto Numerico(decimal? valor, decimal? min = null, decimal? max = null, string? estandar = null) =>
        new() { TipoDato = ControlProcesoCatalogo.Numerico, ValorReal = valor, ValorMin = min, ValorMax = max, ValorEstandarSnapshot = estandar };

    [Fact]
    public void EvaluarLocal_CasosDeLaDemo_SonFaltaEstandar()
    {
        Assert.Equal(ControlProcesoUi.SinEstandar, ControlProcesoUi.EvaluarLocal(Numerico(5, estandar: "-0,7")));
        Assert.Equal(ControlProcesoUi.SinEstandar, ControlProcesoUi.EvaluarLocal(Numerico(45, estandar: "0")));
        Assert.Equal(ControlProcesoUi.SinEstandar, ControlProcesoUi.EvaluarLocal(Numerico(350)));
    }

    [Fact]
    public void EvaluarLocal_ConRango()
    {
        Assert.Equal(ControlProcesoUi.Ok, ControlProcesoUi.EvaluarLocal(Numerico(152, 152, 153)));
        Assert.Equal(ControlProcesoUi.FueraDeRango, ControlProcesoUi.EvaluarLocal(Numerico(160, 152, 153)));
        Assert.Equal(ControlProcesoUi.FueraDeRango, ControlProcesoUi.EvaluarLocal(Numerico(9.9m, 10)));
        Assert.Equal(ControlProcesoUi.SinValor, ControlProcesoUi.EvaluarLocal(Numerico(null, 152, 153)));
    }

    [Fact]
    public void EvaluarLocal_OkNoOkYTexto()
    {
        Assert.Equal(ControlProcesoUi.FueraDeRango, ControlProcesoUi.EvaluarLocal(new() { TipoDato = ControlProcesoCatalogo.OkNoOk, ValorRealTexto = "NO OK" }));
        Assert.Equal(ControlProcesoUi.Ok, ControlProcesoUi.EvaluarLocal(new() { TipoDato = ControlProcesoCatalogo.OkNoOk, ValorRealTexto = "OK" }));
        Assert.Equal(ControlProcesoUi.Informativo, ControlProcesoUi.EvaluarLocal(new() { TipoDato = ControlProcesoCatalogo.Texto, ValorRealTexto = "papel húmedo" }));
    }

    [Fact]
    public void FaltaObservacion_SoloFueraDeRangoSinObservacion()
    {
        var fuera = Numerico(160, 152, 153);
        Assert.True(ControlProcesoUi.FaltaObservacion(fuera));
        fuera.Observacion = "cuchilla gastada";
        Assert.False(ControlProcesoUi.FaltaObservacion(fuera));
        Assert.False(ControlProcesoUi.FaltaObservacion(Numerico(5, estandar: "-0,7")));
    }

    [Theory]
    [InlineData("-0,7", null, null, true)]
    [InlineData("-200", null, null, true)]
    [InlineData("152 - 153", 152.0, 153.0, false)]
    [InlineData(null, -5.0, 5.0, true)]
    [InlineData("0", null, null, false)]
    public void AdmiteNegativo(string? estandar, double? min, double? max, bool esperado)
    {
        var d = Numerico(null, (decimal?)min, (decimal?)max, estandar);
        Assert.Equal(esperado, ControlProcesoUi.AdmiteNegativo(d));
    }
}

/// <summary>Buscadores con tipeo: sin acentos y por palabras.</summary>
public class BusquedaTextoTests
{
    [Theory]
    [InlineData("Higiénico 30 mts", "hig 30", true)]
    [InlineData("Higiénico 30 mts", "HIGIENICO", true)]
    [InlineData("Falla Eléctrica", "electrica", true)]
    [InlineData("Higiénico 30 mts", "30 hig", true)]
    [InlineData("Higiénico 30 mts", "45", false)]
    [InlineData("Rebobinadora", "", true)]
    [InlineData(null, "x", false)]
    public void Coincide(string? texto, string busqueda, bool esperado)
    {
        Assert.Equal(esperado, BusquedaTexto.Coincide(texto, busqueda));
    }
}
