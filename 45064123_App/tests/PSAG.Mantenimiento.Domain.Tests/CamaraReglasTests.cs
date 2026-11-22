using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-798 etapa 4: reglas del maestro de cámaras (los CHECK por función de CAM_Camaras).</summary>
public class CamaraReglasTests
{
    private static Camara Conteo() => new()
    {
        Codigo = "T2-CAM10", Nombre = "Cámara 10", LineaId = 1, Funcion = FuncionCamara.Conteo,
        Magnitud = MagnitudProduccion.Mangas, MetodoDeteccion = MetodoDeteccionCamara.Pasos, CantidadCarriles = 1,
        DetectaParadas = true, CreaDetenciones = true, UmbralDetencionSegundos = 300
    };

    [Fact]
    public void Conteo_Completa_EsValida() => Assert.Null(CamaraReglas.MotivoInvalida(Conteo()));

    [Fact]
    public void Conteo_SinMagnitud_NoEsValida()
    {
        var c = Conteo();
        c.Magnitud = null;
        Assert.Contains("magnitud", CamaraReglas.MotivoInvalida(c));
    }

    [Fact]
    public void Conteo_CreaDetencionesSinDetectarParadas_NoEsValida()
    {
        var c = Conteo();
        c.DetectaParadas = false;
        Assert.NotNull(CamaraReglas.MotivoInvalida(c));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(14401)]
    public void UmbralFueraDeRango_NoEsValido(int segundos)
    {
        var c = Conteo();
        c.UmbralDetencionSegundos = segundos;
        Assert.NotNull(CamaraReglas.MotivoInvalida(c));
    }

    [Fact]
    public void Calidad_ConDatosDeConteo_NoEsValida_SinEllos_Si()
    {
        var c = new Camara { Codigo = "T2-CAM3", Nombre = "Cámara 3", LineaId = 1, Funcion = FuncionCamara.Calidad, Magnitud = MagnitudProduccion.Bolsones };
        Assert.NotNull(CamaraReglas.MotivoInvalida(c));
        c.Magnitud = null;
        Assert.Null(CamaraReglas.MotivoInvalida(c));
        c.CuentaProduccionOficial = true;
        Assert.NotNull(CamaraReglas.MotivoInvalida(c));
    }

    [Theory]
    [InlineData("T2 CAM10")]
    [InlineData("UN-CODIGO-DE-MAS-DE-TREINTA-CARACTERES")]
    public void Codigo_ConEspaciosOLargo_NoEsValido(string codigo)
    {
        var c = Conteo();
        c.Codigo = codigo;
        Assert.NotNull(CamaraReglas.MotivoInvalida(c));
    }

    [Theory]
    [InlineData(null, "SIN_DATOS")]
    [InlineData(3, "VERDE")]
    [InlineData(5, "VERDE")]
    [InlineData(20, "AMARILLO")]
    [InlineData(31, "ROJO")]
    public void EstadoUltimoDato(int? minutosAtras, string esperado)
    {
        var ahora = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(-3));
        Assert.Equal(esperado, CamaraReglas.EstadoUltimoDato(minutosAtras is { } m ? ahora.AddMinutes(-m) : null, ahora));
    }
}
