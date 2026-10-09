using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Tests;

public class DetencionTests
{
    [Fact]
    public void Detencion_CreacionInicial_EstadoEsRegistrada()
    {
        // Arrange & Act
        var detencion = new Detencion();

        // Assert
        Assert.Equal(EstadoDetencion.REGISTRADA, detencion.EstadoActual);
    }

    [Fact]
    public void Detencion_EstaCerrada_RetornaFalse_CuandoEstadoEsRegistrada()
    {
        // Arrange
        var detencion = new Detencion { EstadoActual = EstadoDetencion.REGISTRADA };

        // Assert
        Assert.False(detencion.EstaCerrada);
    }

    [Fact]
    public void Detencion_EstaCerrada_RetornaTrue_CuandoEstadoEsCerrada()
    {
        // Arrange
        var detencion = new Detencion { EstadoActual = EstadoDetencion.CERRADA };

        // Assert
        Assert.True(detencion.EstaCerrada);
    }

    [Fact]
    public void Detencion_PuedeSerEditada_RetornaTrue_CuandoNoEstaCerrada()
    {
        // Arrange
        var detencion = new Detencion { EstadoActual = EstadoDetencion.REGISTRADA };

        // Assert
        Assert.True(detencion.PuedeSerEditada);
    }

    [Fact]
    public void Detencion_PuedeSerEditada_RetornaFalse_CuandoEstaCerrada()
    {
        // Arrange
        var detencion = new Detencion { EstadoActual = EstadoDetencion.CERRADA };

        // Assert
        Assert.False(detencion.PuedeSerEditada);
    }

    [Theory]
    [InlineData(EstadoDetencion.REGISTRADA, EstadoDetencion.EN_SEGUIMIENTO)]
    [InlineData(EstadoDetencion.EN_SEGUIMIENTO, EstadoDetencion.CERRADA)]
    [InlineData(EstadoDetencion.REGISTRADA, EstadoDetencion.CERRADA)]
    public void Detencion_PuedeTransicionarA_RetornaTrue_ParaTransicionesValidas(
        EstadoDetencion estadoActual,
        EstadoDetencion estadoNuevo)
    {
        // Arrange
        var detencion = new Detencion { EstadoActual = estadoActual };

        // Act
        var resultado = detencion.PuedeTransicionarA(estadoNuevo);

        // Assert
        Assert.True(resultado);
    }

    [Fact]
    public void Detencion_PuedeTransicionarA_RetornaFalse_DesdeCerrada()
    {
        // Arrange
        var detencion = new Detencion { EstadoActual = EstadoDetencion.CERRADA };

        // Act
        var resultado = detencion.PuedeTransicionarA(EstadoDetencion.REGISTRADA);

        // Assert
        Assert.False(resultado);
    }

    [Fact]
    public void EstadoEnSeguimiento_SeDescribeComoEnAnalisis()
    {
        Assert.Equal("En análisis", EstadoDetencion.EN_SEGUIMIENTO.ObtenerDescripcion());
    }
}
