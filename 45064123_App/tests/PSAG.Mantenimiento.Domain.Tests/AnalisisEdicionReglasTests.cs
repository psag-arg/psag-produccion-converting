using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-543/T-544: quién corrige un análisis cerrado, quién adjunta fotos y qué queda en el historial.
/// </summary>
public class AnalisisEdicionReglasTests
{
    private const string Operario = "OPERARIO";
    private const string Supervisor = "SUPERVISOR";
    private const string Mantenimiento = "MANTENIMIENTO";
    private const string Gerencia = "GERENCIA";
    private const string Administrador = "ADMINISTRADOR";

    private static Detencion Detencion(EstadoDetencion estado, string? responsableRol = Mantenimiento) => new()
    {
        DetencionId = 7,
        RequiereAnalisis = true,
        ResponsableRol = responsableRol,
        EstadoActual = estado,
        AccionesRealizadas = "Se cambió el rodillo",
        CausaFalla = "Desgaste",
        PlanAccion = null
    };

    [Theory]
    [InlineData(Administrador, true)]
    [InlineData(Supervisor, true)]
    [InlineData(Mantenimiento, false)]
    [InlineData(Operario, false)]
    [InlineData(Gerencia, false)]
    public void EditarCerrado_SoloAdministracionYSupervision(string rol, bool puede)
    {
        Assert.Equal(puede, AnalisisEdicionReglas.PuedeEditarCerrado([rol]));
    }

    [Fact]
    public void Adjuntar_EnBorrador_ElResponsablePuede()
    {
        var detencion = Detencion(EstadoDetencion.EN_SEGUIMIENTO);

        Assert.True(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "u1", [Mantenimiento]));
        Assert.False(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "u2", [Operario]));
    }

    [Fact]
    public void Adjuntar_Cerrado_SoloAdministracionYSupervision()
    {
        var detencion = Detencion(EstadoDetencion.CERRADA);

        Assert.True(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "u1", [Supervisor]));
        Assert.True(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "u1", [Administrador]));
        // El responsable original ya no puede tocarlo una vez cerrado.
        Assert.False(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "u1", [Mantenimiento]));
        Assert.False(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "u1", [Operario]));
    }

    [Fact]
    public void Adjuntar_PendienteDeJustificacion_NadiePuede()
    {
        var detencion = Detencion(EstadoDetencion.PENDIENTE_JUSTIFICACION);

        Assert.False(AnalisisEdicionReglas.PuedeAdjuntar(detencion, "admin", [Administrador]));
    }

    [Fact]
    public void Cambios_UnRegistroPorCampoModificadoConElValorAnterior()
    {
        var detencion = Detencion(EstadoDetencion.CERRADA);
        var fecha = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

        var cambios = AnalisisEdicionReglas.Cambios(
            detencion, "Se cambió el rodillo", "Rodamiento seco", "Lubricar semanal", "admin", fecha);

        Assert.Equal(2, cambios.Count);
        var causa = Assert.Single(cambios, c => c.Campo == AnalisisCambio.CampoCausa);
        Assert.Equal("Desgaste", causa.ValorAnterior);
        Assert.Equal("Rodamiento seco", causa.ValorNuevo);
        Assert.Equal("admin", causa.UsuarioId);
        Assert.Equal(fecha, causa.FechaHora);
        Assert.Equal(7, causa.DetencionId);

        var plan = Assert.Single(cambios, c => c.Campo == AnalisisCambio.CampoPlanAccion);
        Assert.Null(plan.ValorAnterior);
        Assert.Equal("Lubricar semanal", plan.ValorNuevo);
    }

    [Fact]
    public void Cambios_SinDiferencias_NoGeneraHistorial()
    {
        var detencion = Detencion(EstadoDetencion.CERRADA);

        var cambios = AnalisisEdicionReglas.Cambios(
            detencion, detencion.AccionesRealizadas, detencion.CausaFalla, null, "admin", DateTimeOffset.UtcNow);

        Assert.Empty(cambios);
    }
}
