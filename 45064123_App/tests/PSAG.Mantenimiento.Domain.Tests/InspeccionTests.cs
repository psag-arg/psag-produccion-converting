using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Tests;

public class InspeccionTests
{
    [Fact]
    public void EjecucionInspeccion_CreacionInicial_NoEstaCompletada()
    {
        // Arrange & Act
        var ejecucion = new EjecucionInspeccion();

        // Assert
        Assert.False(ejecucion.Completada);
    }

    [Fact]
    public void EjecucionInspeccion_CantidadesIniciales_SonCero()
    {
        // Arrange & Act
        var ejecucion = new EjecucionInspeccion();

        // Assert
        Assert.Equal(0, ejecucion.CantidadOk);
        Assert.Equal(0, ejecucion.CantidadNoOk);
    }

    [Fact]
    public void EjecucionInspeccion_DetallesIniciales_ColeccionVacia()
    {
        // Arrange & Act
        var ejecucion = new EjecucionInspeccion();

        // Assert
        Assert.NotNull(ejecucion.Detalles);
        Assert.Empty(ejecucion.Detalles);
    }

    [Fact]
    public void EjecucionInspeccionDetalle_Resultado_ValorPorDefecto()
    {
        // Arrange & Act
        var detalle = new EjecucionInspeccionDetalle();

        // Assert - El enum tiene un valor por defecto (el primer valor)
        Assert.Equal(default(ResultadoInspeccion), detalle.Resultado);
    }

    [Theory]
    [InlineData(ResultadoInspeccion.OK)]
    [InlineData(ResultadoInspeccion.NO_OK)]
    public void EjecucionInspeccionDetalle_Resultado_AceptaValoresEnum(ResultadoInspeccion resultado)
    {
        // Arrange
        var detalle = new EjecucionInspeccionDetalle();

        // Act
        detalle.Resultado = resultado;

        // Assert
        Assert.Equal(resultado, detalle.Resultado);
    }

    [Fact]
    public void PlanInspeccion_CreacionInicial_EstaActivo()
    {
        // Arrange & Act
        var plan = new PlanInspeccion();

        // Assert
        Assert.True(plan.Activo);
    }

    [Fact]
    public void PlanInspeccion_FrecuenciaDias_PorDefectoEsUno()
    {
        // Arrange & Act
        var plan = new PlanInspeccion();

        // Assert
        Assert.Equal(1, plan.FrecuenciaDias);
    }

    [Fact]
    public void PlanInspeccion_ItemsIniciales_ColeccionVacia()
    {
        // Arrange & Act
        var plan = new PlanInspeccion();

        // Assert
        Assert.NotNull(plan.Items);
        Assert.Empty(plan.Items);
    }

    [Theory]
    [InlineData(TipoPuesto.MAQUINISTA)]
    [InlineData(TipoPuesto.AYUDANTE)]
    [InlineData(TipoPuesto.PACKAGING)]
    public void PlanInspeccion_PuestoResponsable_AceptaTodosTipos(TipoPuesto puesto)
    {
        // Arrange
        var plan = new PlanInspeccion();

        // Act
        plan.PuestoResponsable = puesto;

        // Assert
        Assert.Equal(puesto, plan.PuestoResponsable);
    }

    [Fact]
    public void ItemPlanInspeccion_CreacionInicial_EstaActivo()
    {
        // Arrange & Act
        var item = new ItemPlanInspeccion();

        // Assert
        Assert.True(item.Activo);
    }

    [Theory]
    [InlineData(MetodoInspeccion.VISUAL)]
    [InlineData(MetodoInspeccion.TACTO)]
    [InlineData(MetodoInspeccion.MEDICION)]
    public void ItemPlanInspeccion_Metodo_AceptaTodosMetodos(MetodoInspeccion metodo)
    {
        // Arrange
        var item = new ItemPlanInspeccion();

        // Act
        item.Metodo = metodo;

        // Assert
        Assert.Equal(metodo, item.Metodo);
    }

    [Fact]
    public void HallazgoNoOk_CreacionInicial_NoEstaResuelto()
    {
        // Arrange & Act
        var hallazgo = new HallazgoNoOk();

        // Assert
        Assert.False(hallazgo.Resuelto);
        Assert.Null(hallazgo.FechaHoraResolucion);
    }

    [Fact]
    public void HallazgoNoOk_RequiereAccionCorrectiva_PorDefectoFalse()
    {
        // Arrange & Act
        var hallazgo = new HallazgoNoOk();

        // Assert
        Assert.False(hallazgo.RequiereAccionCorrectiva);
    }
}
