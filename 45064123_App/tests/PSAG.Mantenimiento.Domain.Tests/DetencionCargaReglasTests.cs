using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class DetencionCargaReglasTests
{
    // Con horas manda la hora; si no, el turno del maquinista; un maquinista elegido sin turno ni horas no se resuelve.
    [Theory]
    [InlineData(true, true, false, FuenteTurno.HoraInicio)]
    [InlineData(false, true, false, null)]
    [InlineData(false, true, true, FuenteTurno.Maquinista)]
    [InlineData(false, false, false, FuenteTurno.HoraActual)]
    public void ElegirFuenteTurno_SegunHorasYMaquinista(bool conHoras, bool maquinistaElegido, bool maquinistaTieneTurno, FuenteTurno? esperado)
    {
        Assert.Equal(esperado, DetencionCargaReglas.ElegirFuenteTurno(conHoras, maquinistaElegido, maquinistaTieneTurno));
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
