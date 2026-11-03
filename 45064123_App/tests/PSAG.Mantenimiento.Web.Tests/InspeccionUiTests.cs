using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Pantalla de Inspecciones: observación obligatoria en NO OK, filtros por frecuencia y quién realiza.</summary>
public class InspeccionUiTests
{
    private static EjecucionInspeccionDetalleDto Punto(int frecuencia, string quien, string? resultado = null, string? obs = null, bool venceHoy = true) =>
        new() { FrecuenciaDias = frecuencia, QuienRealiza = quien, Resultado = resultado, Observacion = obs, VenceHoy = venceHoy };

    [Fact]
    public void FaltaObservacion_SoloEnNoOkSinTexto()
    {
        Assert.True(InspeccionUi.FaltaObservacion(Punto(1, "PACKAGING", InspeccionUi.NoOk)));
        Assert.True(InspeccionUi.FaltaObservacion(Punto(1, "PACKAGING", InspeccionUi.NoOk, "  ")));
        Assert.False(InspeccionUi.FaltaObservacion(Punto(1, "PACKAGING", InspeccionUi.NoOk, "Correa cortada")));
        Assert.False(InspeccionUi.FaltaObservacion(Punto(1, "PACKAGING", InspeccionUi.Ok)));
        Assert.False(InspeccionUi.FaltaObservacion(Punto(1, "PACKAGING")));
    }

    [Fact]
    public void Coincide_FiltraPorFrecuenciaYQuienRealiza()
    {
        var diaria = Punto(1, "PACKAGING");
        var pente = Punto(10, "MECANICO");

        Assert.True(InspeccionUi.Coincide(diaria, null, null));
        Assert.True(InspeccionUi.Coincide(pente, 10, null));
        Assert.False(InspeccionUi.Coincide(diaria, 10, null));
        Assert.True(InspeccionUi.Coincide(pente, null, "MECANICO"));
        Assert.False(InspeccionUi.Coincide(pente, 1, "MECANICO"));
        Assert.False(InspeccionUi.Coincide(diaria, null, "MECANICO"));
    }

    [Fact]
    public void Frecuencias_DistintasYOrdenadas()
    {
        Assert.Equal(new[] { (1, "Diaria"), (10, "Cada 10 días") }, InspeccionUi.Frecuencias(new[] { 10, 1, 1, 10, 1 }));
    }

    [Fact]
    public void FaltanParaCerrar_CuentaSoloLosQueVencenHoySinResponder()
    {
        var detalles = new[]
        {
            Punto(1, "PACKAGING", InspeccionUi.Ok),
            Punto(1, "PACKAGING"),
            Punto(10, "MECANICO", venceHoy: false),
            new EjecucionInspeccionDetalleDto { FueraDePlantilla = true, VenceHoy = true }
        };
        Assert.Equal(1, InspeccionUi.FaltanParaCerrar(detalles));
    }

    [Fact]
    public void TextoVencimiento()
    {
        Assert.Equal("Vence hoy", InspeccionUi.TextoVencimiento(true, null));
        Assert.Equal("No vence hoy · próxima 13/11", InspeccionUi.TextoVencimiento(false, new DateOnly(2026, 11, 13)));
    }

    [Theory]
    [InlineData("PACKAGING", "Op. Packaging")]
    [InlineData("MECANICO", "Mecánico")]
    [InlineData(null, "Todos")]
    public void NombrePuesto(string? codigo, string esperado)
    {
        Assert.Equal(esperado, InspeccionUi.NombrePuesto(codigo));
    }
}
