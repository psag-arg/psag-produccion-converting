using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Tests;

public class RegistroProcesoTests
{
    [Fact]
    public void RegistroProceso_CreacionInicial_EstadoEsParcial()
    {
        // Arrange & Act
        var registro = new RegistroProceso();

        // Assert
        Assert.Equal(EstadoRegistroProceso.PARCIAL, registro.Estado);
    }

    [Fact]
    public void RegistroProceso_PuedeSerEditado_RetornaTrue_CuandoEstadoEsParcial()
    {
        // Arrange
        var registro = new RegistroProceso { Estado = EstadoRegistroProceso.PARCIAL };

        // Assert
        Assert.True(registro.PuedeSerEditado);
    }

    [Fact]
    public void RegistroProceso_PuedeSerEditado_RetornaTrue_CuandoEstadoEsCompleto()
    {
        // Arrange
        var registro = new RegistroProceso { Estado = EstadoRegistroProceso.COMPLETO };

        // Assert
        Assert.True(registro.PuedeSerEditado);
    }

    [Fact]
    public void RegistroProceso_PuedeSerEditado_RetornaFalse_CuandoEstadoEsCerrado()
    {
        // Arrange
        var registro = new RegistroProceso { Estado = EstadoRegistroProceso.CERRADO };

        // Assert
        Assert.False(registro.PuedeSerEditado);
    }

    [Fact]
    public void RegistroProceso_DetallesIniciales_ColeccionVacia()
    {
        // Arrange & Act
        var registro = new RegistroProceso();

        // Assert
        Assert.NotNull(registro.Detalles);
        Assert.Empty(registro.Detalles);
    }

    [Fact]
    public void RegistroProceso_CantidadDesvios_EsCero_SinDetalles()
    {
        // Arrange
        var registro = new RegistroProceso();

        // Assert
        Assert.Equal(0, registro.CantidadDesvios);
    }

    [Fact]
    public void RegistroProceso_CantidadDesvios_CuentaCorrectamente()
    {
        // Arrange
        var registro = new RegistroProceso();
        registro.Detalles.Add(new RegistroProcesoDetalle { FueraDeEstandar = true });
        registro.Detalles.Add(new RegistroProcesoDetalle { FueraDeEstandar = false });
        registro.Detalles.Add(new RegistroProcesoDetalle { FueraDeEstandar = true });

        // Assert
        Assert.Equal(2, registro.CantidadDesvios);
    }

    [Fact]
    public void RegistroProceso_CantidadCompletados_CuentaValoresNumericos()
    {
        // Arrange
        var registro = new RegistroProceso();
        registro.Detalles.Add(new RegistroProcesoDetalle { ValorReal = 10.5m });
        registro.Detalles.Add(new RegistroProcesoDetalle { ValorReal = null });
        registro.Detalles.Add(new RegistroProcesoDetalle { ValorReal = 20.0m });

        // Assert
        Assert.Equal(2, registro.CantidadCompletados);
    }

    [Fact]
    public void RegistroProceso_CantidadCompletados_CuentaValoresTexto()
    {
        // Arrange
        var registro = new RegistroProceso();
        registro.Detalles.Add(new RegistroProcesoDetalle { ValorRealTexto = "OK" });
        registro.Detalles.Add(new RegistroProcesoDetalle { ValorRealTexto = null });
        registro.Detalles.Add(new RegistroProcesoDetalle { ValorRealTexto = "Correcto" });

        // Assert
        Assert.Equal(2, registro.CantidadCompletados);
    }

    [Fact]
    public void RegistroProceso_RowVersion_InicialVacio()
    {
        // Arrange & Act
        var registro = new RegistroProceso();

        // Assert
        Assert.NotNull(registro.RowVersion);
        Assert.Empty(registro.RowVersion);
    }
}

public class RegistroProcesoDetalleTests
{
    [Fact]
    public void RegistroProcesoDetalle_ValorReal_PuedeSerNulo()
    {
        // Arrange & Act
        var detalle = new RegistroProcesoDetalle();

        // Assert
        Assert.Null(detalle.ValorReal);
    }

    [Fact]
    public void RegistroProcesoDetalle_FueraDeEstandar_PorDefectoFalse()
    {
        // Arrange & Act
        var detalle = new RegistroProcesoDetalle();

        // Assert
        Assert.False(detalle.FueraDeEstandar);
    }

    [Fact]
    public void RegistroProcesoDetalle_ValorEstandarSnapshot_PuedeSerNulo()
    {
        // Arrange & Act
        var detalle = new RegistroProcesoDetalle();

        // Assert
        Assert.Null(detalle.ValorEstandarSnapshot);
    }

    [Fact]
    public void RegistroProcesoDetalle_FechaHoraRegistro_TieneValorPorDefecto()
    {
        // Arrange & Act
        var detalle = new RegistroProcesoDetalle();

        // Assert
        Assert.NotEqual(default, detalle.FechaHoraRegistro);
    }
}

public class EstadoRegistroProcesoExtensionsTests
{
    [Theory]
    [InlineData(EstadoRegistroProceso.PARCIAL, "Parcial")]
    [InlineData(EstadoRegistroProceso.COMPLETO, "Completo")]
    [InlineData(EstadoRegistroProceso.CERRADO, "Cerrado")]
    public void ObtenerDescripcion_RetornaDescripcionCorrecta(
        EstadoRegistroProceso estado,
        string descripcionEsperada)
    {
        // Act
        var descripcion = estado.ObtenerDescripcion();

        // Assert
        Assert.Equal(descripcionEsperada, descripcion);
    }

    [Theory]
    [InlineData(EstadoRegistroProceso.PARCIAL, true)]
    [InlineData(EstadoRegistroProceso.COMPLETO, true)]
    [InlineData(EstadoRegistroProceso.CERRADO, false)]
    public void PuedeSerEditado_RetornaValorCorrecto(
        EstadoRegistroProceso estado,
        bool esperado)
    {
        // Act
        var resultado = estado.PuedeSerEditado();

        // Assert
        Assert.Equal(esperado, resultado);
    }
}
