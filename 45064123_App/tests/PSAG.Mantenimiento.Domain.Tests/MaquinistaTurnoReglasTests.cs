using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-783: maquinista de un turno concreto (confirmación en el login y corrección del supervisor).</summary>
public class MaquinistaTurnoReglasTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(-3));

    private static readonly Turno Noche = new() { TurnoId = 3, HoraInicio = new TimeOnly(22, 0), HoraFin = new TimeOnly(6, 0), Activo = true };
    private static readonly Turno Manana = new() { TurnoId = 1, HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0), Activo = true };

    private static UsuarioLinea Asignacion(int id, int lineaId, int? turnoId) => new()
    {
        UsuarioLineaId = id, UsuarioId = "oper", LineaId = lineaId, TurnoId = turnoId, FechaAsignacion = T0, AsignadoPorUsuarioId = "admin"
    };

    [Theory]
    [InlineData(1, 1, true)]       // asignado a ese turno
    [InlineData(null, 1, true)]    // sin turno fijo: cubre cualquiera
    [InlineData(2, 1, false)]      // asignado a otro turno: no corresponde
    public void Corresponde_TurnoDeLaAsignacionOSinTurnoFijo(int? turnoAsignacion, int turnoEnCurso, bool esperado) =>
        Assert.Equal(esperado, MaquinistaTurnoReglas.Corresponde(Asignacion(1, 1, turnoAsignacion), turnoEnCurso));

    [Fact]
    public void Candidatas_PrimeroLasDelTurnoYSinLasDeOtroTurno()
    {
        var c = MaquinistaTurnoReglas.Candidatas([Asignacion(1, 1, null), Asignacion(2, 2, 1), Asignacion(3, 3, 2)], turnoId: 1);
        Assert.Equal([2, 1], c.Select(a => a.UsuarioLineaId));
    }

    [Fact]
    public void Vigente_ElRegistroMasReciente()
    {
        var registros = new[]
        {
            new MaquinistaTurno { MaquinistaTurnoId = 1, UsuarioLineaId = 10, FechaHora = T0 },
            new MaquinistaTurno { MaquinistaTurnoId = 3, UsuarioLineaId = 30, FechaHora = T0.AddMinutes(5) },
            new MaquinistaTurno { MaquinistaTurnoId = 2, UsuarioLineaId = 20, FechaHora = T0.AddMinutes(5) }
        };
        Assert.Equal(30, MaquinistaTurnoReglas.Vigente(registros)!.UsuarioLineaId);   // misma hora: el último id
        Assert.Null(MaquinistaTurnoReglas.Vigente([]));
    }

    [Theory]
    [InlineData(RolesSistema.Administrador, new int[0], 5, true)]
    [InlineData(RolesSistema.Mantenimiento, new[] { 1 }, 5, true)]
    [InlineData(RolesSistema.Supervisor, new[] { 1, 2 }, 2, true)]
    [InlineData(RolesSistema.Supervisor, new[] { 1, 2 }, 5, false)]   // supervisor: sólo sus líneas
    [InlineData(RolesSistema.Supervisor, new int[0], 5, true)]        // supervisor sin líneas: todas (como Mis pendientes)
    [InlineData(RolesSistema.Operario, new[] { 5 }, 5, false)]        // el operario confirma, no corrige
    [InlineData(RolesSistema.Gerencia, new int[0], 5, false)]
    public void PuedeCorregir_PorRolYLinea(string rol, int[] lineas, int lineaId, bool esperado) =>
        Assert.Equal(esperado, MaquinistaTurnoReglas.PuedeCorregir([rol], lineas, lineaId));

    [Fact]
    public void FechaTurno_ElNocturnoDespuesDeMedianocheEsDelDiaAnterior()
    {
        Assert.Equal(new DateOnly(2026, 11, 19), MaquinistaTurnoReglas.FechaTurno(Noche, new DateTime(2026, 11, 20, 2, 30, 0)));
        Assert.Equal(new DateOnly(2026, 11, 20), MaquinistaTurnoReglas.FechaTurno(Noche, new DateTime(2026, 11, 20, 23, 0, 0)));
        Assert.Equal(new DateOnly(2026, 11, 20), MaquinistaTurnoReglas.FechaTurno(Manana, new DateTime(2026, 11, 20, 10, 0, 0)));
    }

    [Fact]
    public void MotivoCorreccionInvalida_AsignacionDeLaLineaYMotivoObligatorio()
    {
        Assert.Null(MaquinistaTurnoReglas.MotivoCorreccionInvalida(Asignacion(1, 7, 1), 7, "Cubrió el turno"));
        Assert.NotNull(MaquinistaTurnoReglas.MotivoCorreccionInvalida(null, 7, "x"));
        Assert.Contains("no está asignado a esa línea", MaquinistaTurnoReglas.MotivoCorreccionInvalida(Asignacion(1, 8, 1), 7, "x"));
        Assert.Contains("motivo", MaquinistaTurnoReglas.MotivoCorreccionInvalida(Asignacion(1, 7, 1), 7, "  "));
        Assert.Contains("500", MaquinistaTurnoReglas.MotivoCorreccionInvalida(Asignacion(1, 7, 1), 7, new string('x', 501)));
    }
}
