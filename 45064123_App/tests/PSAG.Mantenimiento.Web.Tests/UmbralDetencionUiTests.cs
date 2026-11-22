using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Maestros → Cámaras: umbral de detención como número + unidad (min / segundos), un solo campo en segundos.</summary>
public class UmbralDetencionUiTests
{
    [Fact]
    public void AlEditar_MinutosEnterosEnMinutos_ElRestoEnSegundos_VacioEs5Min()
    {
        Assert.Equal((5, UmbralDetencionUi.Minutos), UmbralDetencionUi.Leer(new CamaraDto { UmbralDetencionSegundos = null }));
        Assert.Equal((5, UmbralDetencionUi.Minutos), UmbralDetencionUi.Leer(new CamaraDto { UmbralDetencionSegundos = 300 }));
        Assert.Equal((45, UmbralDetencionUi.Segundos), UmbralDetencionUi.Leer(new CamaraDto { UmbralDetencionSegundos = 45 }));
    }

    [Fact]
    public void Guardar_PasaASegundos()
    {
        var c = new CamaraDto();

        UmbralDetencionUi.Aplicar(c, 30, UmbralDetencionUi.Segundos);
        Assert.Equal(30, c.UmbralDetencionSegundos);

        UmbralDetencionUi.Aplicar(c, 5, UmbralDetencionUi.Minutos);
        Assert.Equal(300, c.UmbralDetencionSegundos);
    }

    [Fact]
    public void RangosDelInput_SegunLaUnidad()
    {
        Assert.Equal((1, 240), (UmbralDetencionUi.Minimo(UmbralDetencionUi.Minutos), UmbralDetencionUi.Maximo(UmbralDetencionUi.Minutos)));
        Assert.Equal((30, 14400), (UmbralDetencionUi.Minimo(UmbralDetencionUi.Segundos), UmbralDetencionUi.Maximo(UmbralDetencionUi.Segundos)));
        Assert.Contains("mínimo 30", UmbralDetencionUi.TextoAyuda);
    }
}
