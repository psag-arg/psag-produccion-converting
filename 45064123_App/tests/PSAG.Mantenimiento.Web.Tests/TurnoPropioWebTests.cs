using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Services;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>T-802 en la Web: obligado a turno sin turno = bloqueado, y la única pantalla que ve es "Inicio de turno".</summary>
public class TurnoPropioWebTests
{
    [Fact]
    public void Bloqueado_SoloSiEstaObligadoYNoTieneTurno()
    {
        Assert.True(new EstadoTurnoPropio(true, null).Bloqueado);
        Assert.False(new EstadoTurnoPropio(true, new TurnoProduccionDto { TurnoProduccionId = 1 }).Bloqueado);
        Assert.False(new EstadoTurnoPropio(false, null).Bloqueado);
        Assert.False(EstadoTurnoPropio.Libre.Bloqueado);
    }

    [Theory]
    [InlineData("/produccion/inicio-turno", true)]
    [InlineData("produccion/inicio-turno?volver=%2Fdetenciones", true)]
    [InlineData("/Produccion/Inicio-Turno/", true)]
    [InlineData("/detenciones", false)]
    [InlineData("/produccion/turnos", false)]
    [InlineData("/", false)]
    public void EsInicioTurno(string ruta, bool esperado) => Assert.Equal(esperado, TurnoPropioService.EsInicioTurno(ruta));
}
