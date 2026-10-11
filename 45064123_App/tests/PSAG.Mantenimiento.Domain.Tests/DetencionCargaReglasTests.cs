using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class DetencionCargaReglasTests
{
    [Fact]
    public void ConHoras_ElTurnoSaleDeLaHoraAunqueElMaquinistaNoTengaTurno()
    {
        Assert.Equal(FuenteTurno.HoraInicio,
            DetencionCargaReglas.ElegirFuenteTurno(conHoras: true, maquinistaElegido: true, maquinistaTieneTurno: false));
    }

    [Fact]
    public void MaquinistaElegidoSinTurnoNiHoras_NoSePuedeResolver()
    {
        Assert.Null(DetencionCargaReglas.ElegirFuenteTurno(conHoras: false, maquinistaElegido: true, maquinistaTieneTurno: false));
    }

    [Fact]
    public void MaquinistaConTurno_UsaSuTurno()
    {
        Assert.Equal(FuenteTurno.Maquinista,
            DetencionCargaReglas.ElegirFuenteTurno(conHoras: false, maquinistaElegido: true, maquinistaTieneTurno: true));
    }

    [Fact]
    public void SinMaquinistaNiHoras_UsaLaHoraActual()
    {
        Assert.Equal(FuenteTurno.HoraActual,
            DetencionCargaReglas.ElegirFuenteTurno(conHoras: false, maquinistaElegido: false, maquinistaTieneTurno: false));
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
}
