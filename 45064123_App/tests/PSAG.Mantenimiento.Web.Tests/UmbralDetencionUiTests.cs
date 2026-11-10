using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Maestros → Calidad: umbral de detención como número + unidad (min / segundos), reunión 10/11.</summary>
public class UmbralDetencionUiTests
{
    [Fact]
    public void AlEditar_MuestraLaUnidadConQueSeGuardo()
    {
        Assert.Equal((5, UmbralDetencionUi.Minutos), UmbralDetencionUi.Leer(new PuntoControlCalidadDto { MinutosUmbralDetencion = 5 }));
        Assert.Equal((45, UmbralDetencionUi.Segundos),
            UmbralDetencionUi.Leer(new PuntoControlCalidadDto { MinutosUmbralDetencion = 5, SegundosUmbralDetencion = 45 }));
    }

    [Fact]
    public void GuardarEnSegundos_CargaLosSegundosYDejaLosMinutos()
    {
        var p = new PuntoControlCalidadDto { MinutosUmbralDetencion = 5 };

        UmbralDetencionUi.Aplicar(p, 30, UmbralDetencionUi.Segundos);

        Assert.Equal(30, p.SegundosUmbralDetencion);
        Assert.Equal(5, p.MinutosUmbralDetencion);
    }

    [Fact]
    public void GuardarEnMinutos_BorraLosSegundos()
    {
        var p = new PuntoControlCalidadDto { MinutosUmbralDetencion = 5, SegundosUmbralDetencion = 30 };

        UmbralDetencionUi.Aplicar(p, 5, UmbralDetencionUi.Minutos);

        Assert.Null(p.SegundosUmbralDetencion);
        Assert.Equal(5, p.MinutosUmbralDetencion);
    }

    [Fact]
    public void RangosDelInput_SegunLaUnidad()
    {
        Assert.Equal((1, 240), (UmbralDetencionUi.Minimo(UmbralDetencionUi.Minutos), UmbralDetencionUi.Maximo(UmbralDetencionUi.Minutos)));
        Assert.Equal((30, 14400), (UmbralDetencionUi.Minimo(UmbralDetencionUi.Segundos), UmbralDetencionUi.Maximo(UmbralDetencionUi.Segundos)));
        Assert.Contains("mínimo 30", UmbralDetencionUi.TextoAyuda);
    }
}
