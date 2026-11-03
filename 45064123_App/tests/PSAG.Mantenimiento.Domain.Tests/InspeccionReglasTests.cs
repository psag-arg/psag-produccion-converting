using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>Inspecciones (reunión 03/11 [78:17]): estado OK / NO OK, frecuencia por punto y quién realiza.</summary>
public class InspeccionReglasTests
{
    [Theory]
    [InlineData("OK", ResultadoInspeccion.OK)]
    [InlineData("ok", ResultadoInspeccion.OK)]
    [InlineData("NO OK", ResultadoInspeccion.NO_OK)]
    [InlineData("NO_OK", ResultadoInspeccion.NO_OK)]
    [InlineData("nook", ResultadoInspeccion.NO_OK)]
    public void Normalizar_AceptaLasFormasDeOkYNoOk(string valor, ResultadoInspeccion esperado)
    {
        Assert.Equal(esperado, InspeccionReglas.Normalizar(valor));
    }

    [Theory]
    [InlineData("SI")]
    [InlineData("bien")]
    [InlineData("N/A")]
    public void ValidarRespuesta_RechazaLoQueNoEsOkNiNoOk(string valor)
    {
        Assert.False(InspeccionReglas.ValidarRespuesta(valor, null).EsValido);
    }

    [Fact]
    public void ValidarRespuesta_NoOkPideObservacion()
    {
        Assert.Contains("obligatoria", InspeccionReglas.ValidarRespuesta("NO OK", "  ").Error);

        var r = InspeccionReglas.ValidarRespuesta("NO OK", "  Correa cortada ");
        Assert.True(r.EsValido);
        Assert.Equal((ResultadoInspeccion.NO_OK, "Correa cortada"), (r.Resultado, r.Observacion));
    }

    [Fact]
    public void ValidarRespuesta_OkConObservacionOpcional_YVacioEsSinValor()
    {
        Assert.Equal(ResultadoInspeccion.OK, InspeccionReglas.ValidarRespuesta("OK", null).Resultado);
        Assert.Equal("Algo de juego", InspeccionReglas.ValidarRespuesta("OK", "Algo de juego").Observacion);
        Assert.True(InspeccionReglas.ValidarRespuesta(null, null).SinValor);
        Assert.False(InspeccionReglas.ValidarRespuesta("NO OK", new string('x', 501)).EsValido);
    }

    [Fact]
    public void VenceHoy_DiariaYCadaDiezDias()
    {
        var hoy = new DateOnly(2026, 11, 3);
        Assert.True(InspeccionReglas.VenceHoy(null, 10, hoy));                    // nunca se hizo
        Assert.True(InspeccionReglas.VenceHoy(hoy.AddDays(-1), 1, hoy));          // diaria de ayer
        Assert.False(InspeccionReglas.VenceHoy(hoy, 1, hoy));                     // diaria de hoy
        Assert.False(InspeccionReglas.VenceHoy(hoy.AddDays(-9), 10, hoy));        // cada 10, hace 9
        Assert.True(InspeccionReglas.VenceHoy(hoy.AddDays(-10), 10, hoy));        // cada 10, hace 10
        Assert.Equal(new DateOnly(2026, 11, 13), InspeccionReglas.ProximoVencimiento(hoy, 10));
        Assert.Null(InspeccionReglas.ProximoVencimiento(null, 10));
    }

    [Theory]
    [InlineData(1, "Diaria")]
    [InlineData(7, "Semanal")]
    [InlineData(10, "Cada 10 días")]
    public void DescribirFrecuencia(int dias, string esperado)
    {
        Assert.Equal(esperado, InspeccionReglas.DescribirFrecuencia(dias));
    }

    [Theory]
    [InlineData("OP. PACKAGING", TipoPuesto.PACKAGING)]
    [InlineData("Operador Packaging", TipoPuesto.PACKAGING)]
    [InlineData("PACKAGING", TipoPuesto.PACKAGING)]
    [InlineData("MECÁNICO", TipoPuesto.MECANICO)]
    [InlineData("mecanico", TipoPuesto.MECANICO)]
    [InlineData("4", TipoPuesto.MECANICO)]
    public void TryLeerPuesto_LeeLaColumnaDeLaPlanilla(string valor, TipoPuesto esperado)
    {
        Assert.True(InspeccionReglas.TryLeerPuesto(valor, out var p));
        Assert.Equal(esperado, p);
    }

    [Fact]
    public void TryLeerPuesto_VacioEsTodos_YDesconocidoFalla()
    {
        Assert.True(InspeccionReglas.TryLeerPuesto(null, out var todos));
        Assert.Null(todos);
        Assert.True(InspeccionReglas.TryLeerPuesto("Todos", out _));
        Assert.False(InspeccionReglas.TryLeerPuesto("GERENTE", out _));
        Assert.False(InspeccionReglas.TryLeerPuesto("9", out _));
    }

    [Fact]
    public void AplicaAPuesto()
    {
        Assert.True(InspeccionReglas.AplicaAPuesto(TipoPuesto.MECANICO, null));
        Assert.True(InspeccionReglas.AplicaAPuesto(TipoPuesto.MECANICO, TipoPuesto.MECANICO));
        Assert.False(InspeccionReglas.AplicaAPuesto(TipoPuesto.MECANICO, TipoPuesto.PACKAGING));
    }

    [Fact]
    public void MotivoDefinicionInvalida_PideParteTareasEstandarYFrecuenciaValida()
    {
        Assert.Null(InspeccionReglas.MotivoDefinicionInvalida("Carro pente", "Inspeccionar el estado", "Barras ajustadas", "Quitar carros", "T2-EMP-13", TipoPuesto.MECANICO, 10));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida(" ", "x", "x", null, null, TipoPuesto.PACKAGING, 1));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida("x", "", "x", null, null, TipoPuesto.PACKAGING, 1));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida("x", "x", null, null, null, TipoPuesto.PACKAGING, 1));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida("x", "x", "x", null, null, TipoPuesto.PACKAGING, 0));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida("x", "x", "x", null, null, TipoPuesto.PACKAGING, 366));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida("x", new string('t', 301), "x", null, null, TipoPuesto.PACKAGING, 1));
        Assert.NotNull(InspeccionReglas.MotivoDefinicionInvalida("x", "x", "x", null, null, (TipoPuesto)9, 1));
    }

    [Fact]
    public void Mecanico_TieneDescripcion()
    {
        Assert.Equal("Mecánico", TipoPuesto.MECANICO.ObtenerDescripcion());
    }
}
