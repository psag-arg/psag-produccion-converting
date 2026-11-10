using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// Auditoría I y N: la pendiente de declaración ofrece Declarar (no Editar), y el aviso de fin de turno dice bien
/// cuántas son de este turno, se descarta por turno + fecha y no se muestra a Gerencia.
/// </summary>
public class RolesCargaTests
{
    private static Func<string, bool> Roles(params string[] roles) => roles.Contains;

    private static DetencionDto Det(string estado, int linea = 1) => new() { LineaId = linea, EstadoActual = estado };

    [Theory]
    [InlineData("REGISTRADA", true, false)]
    [InlineData("EN_SEGUIMIENTO", true, false)]
    [InlineData("PENDIENTE_JUSTIFICACION", false, true)]
    [InlineData("CERRADA", false, false)]
    public void PendienteDeDeclaracion_SeDeclaraNoSeEdita(string estado, bool editar, bool declarar)
    {
        var operario = Roles("OPERARIO");

        Assert.Equal(editar, RolesCarga.PuedeEditar(Det(estado), operario, [1]));
        Assert.Equal(declarar, RolesCarga.PuedeDeclarar(Det(estado), operario, [1]));
    }

    [Fact]
    public void Declarar_OperarioDeOtraLineaOGerencia_No()
    {
        Assert.False(RolesCarga.PuedeDeclarar(Det("PENDIENTE_JUSTIFICACION", linea: 2), Roles("OPERARIO"), [1]));
        Assert.False(RolesCarga.PuedeDeclarar(Det("PENDIENTE_JUSTIFICACION"), Roles("GERENCIA"), []));
        Assert.True(RolesCarga.PuedeDeclarar(Det("PENDIENTE_JUSTIFICACION", linea: 2), Roles("SUPERVISOR"), []));
    }

    [Theory]
    [InlineData(1, 1, "tenés 1 detención de este turno sin declarar")]
    [InlineData(3, 3, "tenés 3 detenciones de este turno sin declarar")]
    [InlineData(2, 0, "tenés 2 detenciones de turnos anteriores sin declarar")]
    [InlineData(3, 1, "tenés 3 detenciones sin declarar (1 de este turno y 2 de turnos anteriores)")]
    public void TextoDelAviso_DistingueEsteTurnoDeLosAnteriores(int pendientes, int delTurno, string esperado)
    {
        Assert.Equal(esperado, RolesCarga.TextoPendientesAviso(new AvisoFinTurnoDto { Pendientes = pendientes, PendientesTurnoActual = delTurno }));
    }

    [Fact]
    public void DescartarElAviso_EsPorTurnoYFecha_LaNocheSiguienteVuelveAAparecer()
    {
        var lunes = new AvisoFinTurnoDto { TurnoId = 3, TurnoNombre = "Noche", FechaInicioTurno = new DateOnly(2026, 11, 9) };
        var martes = new AvisoFinTurnoDto { TurnoId = 3, TurnoNombre = "Noche", FechaInicioTurno = new DateOnly(2026, 11, 10) };

        Assert.NotEqual(RolesCarga.ClaveTurnoAviso(lunes), RolesCarga.ClaveTurnoAviso(martes));
        Assert.Equal(RolesCarga.ClaveTurnoAviso(lunes), RolesCarga.ClaveTurnoAviso(new AvisoFinTurnoDto { TurnoId = 3, FechaInicioTurno = new DateOnly(2026, 11, 9) }));
    }

    [Theory]
    [InlineData("GERENCIA", false)]
    [InlineData("MANTENIMIENTO", false)]
    [InlineData("OPERARIO", true)]
    [InlineData("SUPERVISOR", true)]
    [InlineData("ADMINISTRADOR", true)]
    public void AvisoFinDeTurno_SoloParaQuienDeclara(string rol, bool ve)
    {
        Assert.Equal(ve, RolesCarga.VeDeclaracion(Roles(rol)));
    }
}
