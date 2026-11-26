using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class DetencionCargaReglasTests
{
    // T-798 etapa 3: el elegido a mano manda; si no, el operador del turno; si no, quien carga si está habilitado.
    [Theory]
    [InlineData("elegido", "turno", "carga", true, "elegido")]
    [InlineData(null, "turno", "carga", true, "turno")]
    [InlineData(null, null, "carga", true, "carga")]
    [InlineData(null, null, "carga", false, null)]
    public void ElegirOperador_ElegidoTurnoYQuienCarga(string? elegido, string? delTurno, string? carga, bool habilitado, string? esperado)
    {
        Assert.Equal(esperado, DetencionCargaReglas.ElegirOperador(elegido, delTurno, carga, habilitado));
    }

    [Fact]
    public void InstanteTurno_HoraDesdeOAhora()
    {
        var parada = new DateTimeOffset(2026, 11, 20, 2, 0, 0, TimeSpan.FromHours(-3));
        var ahora = parada.AddHours(5);
        Assert.Equal(parada, DetencionCargaReglas.InstanteTurno(parada, ahora));
        Assert.Equal(ahora, DetencionCargaReglas.InstanteTurno(null, ahora));
    }

    [Fact]
    public void FechaFutura_SeDetecta()
    {
        var hoy = new DateOnly(2026, 10, 11);
        Assert.True(DetencionCargaReglas.EsFechaFutura(hoy.AddDays(1), hoy));
        Assert.False(DetencionCargaReglas.EsFechaFutura(hoy, hoy));
    }

    [Theory]
    [InlineData(new[] { "OPERARIO" }, false, false)]
    [InlineData(new[] { "OPERARIO" }, true, true)]
    [InlineData(new[] { "SUPERVISOR" }, false, true)]
    [InlineData(new[] { "OPERARIO", "SUPERVISOR" }, false, true)]
    [InlineData(new[] { "ADMINISTRADOR" }, false, true)]
    public void OperarioSoloCargaEnSusLineas(string[] roles, bool lineaAsignada, bool esperado)
    {
        Assert.Equal(esperado, DetencionCargaReglas.PuedeCargarEnLinea(roles, lineaAsignada));
    }

    [Fact]
    public void OperarioSinLineas_SeDetectaSoloSiEsSoloOperario()
    {
        Assert.True(DetencionCargaReglas.OperarioSinLineas(["OPERARIO"], 0));
        Assert.False(DetencionCargaReglas.OperarioSinLineas(["OPERARIO"], 1));
        Assert.False(DetencionCargaReglas.OperarioSinLineas(["SUPERVISOR"], 0));
    }
}
