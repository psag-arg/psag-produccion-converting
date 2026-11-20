using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Maquinista de un turno concreto (T-783, definición de Emiliano del 20/11): al loguearse, el operario confirma si
/// es el maquinista del turno en curso de su máquina (línea). Funciones puras: las usan el login, la corrección del
/// supervisor y la elección del maquinista de las detenciones.
/// </summary>
public static class MaquinistaTurnoReglas
{
    public const int ObservacionMaxima = 500;

    public const string MensajeSinRelacion =
        "No tenés asignación como maquinista para este turno en ninguna línea, así que no quedás como maquinista. " +
        "Pedile a tu supervisor que te cargue en \"Maquinistas del turno\" (o al administrador que te asigne en Maestros → Maquinistas).";

    public const string MensajeSinTurno = "No hay un turno activo a esta hora: no hay maquinista que confirmar.";

    /// <summary>
    /// ¿La asignación habilita a ser maquinista de ese turno? Sí si es del turno, o si no tiene turno fijo (cubre
    /// cualquiera, como en <see cref="DetencionCamaraReglas.ElegirMaquinista"/>).
    /// </summary>
    public static bool Corresponde(UsuarioLinea asignacion, int turnoId) =>
        asignacion.TurnoId == null || asignacion.TurnoId == turnoId;

    /// <summary>Asignaciones del usuario con las que puede confirmarse maquinista del turno: primero las del turno.</summary>
    public static IReadOnlyList<UsuarioLinea> Candidatas(IEnumerable<UsuarioLinea> asignacionesDelUsuario, int turnoId) =>
        asignacionesDelUsuario
            .Where(a => Corresponde(a, turnoId))
            .OrderBy(a => a.TurnoId == null ? 1 : 0)
            .ThenBy(a => a.LineaId)
            .ToList();

    /// <summary>El vigente es el registro más reciente (INSERT-ONLY: nunca se edita ni se borra uno anterior).</summary>
    public static MaquinistaTurno? Vigente(IEnumerable<MaquinistaTurno> registrosDelTurno) =>
        registrosDelTurno
            .OrderByDescending(r => r.FechaHora)
            .ThenByDescending(r => r.MaquinistaTurnoId)
            .FirstOrDefault();

    /// <summary>
    /// Quién corrige el maquinista de un turno: administrador y mantenimiento, todas las líneas; supervisor, las
    /// suyas (sin líneas asignadas, todas, como en Mis pendientes). El operario no corrige: confirma en el login.
    /// </summary>
    public static bool PuedeCorregir(IEnumerable<string> roles, IReadOnlyCollection<int> lineasAsignadas, int lineaId)
    {
        var lista = roles.ToList();
        if (lista.Contains(RolesSistema.Administrador) || lista.Contains(RolesSistema.Mantenimiento))
            return true;
        return lista.Contains(RolesSistema.Supervisor) && (lineasAsignadas.Count == 0 || lineasAsignadas.Contains(lineaId));
    }

    /// <summary>Día del turno que contiene la hora de pared (un nocturno después de medianoche es del día anterior).</summary>
    public static DateOnly FechaTurno(Turno turno, DateTime horaPared) =>
        DateOnly.FromDateTime(DetencionCamaraReglas.InicioTurnoEnCurso(turno, horaPared));

    /// <summary>Inicio y fin, en hora de pared, del turno que empezó ese día.</summary>
    public static (DateTime Inicio, DateTime Fin) VentanaTurno(DateOnly fechaTurno, Turno turno) =>
        EficienciaCamaraCalculador.VentanaTurno(fechaTurno, turno);

    /// <summary>Motivo por el que no se acepta la corrección del supervisor, o null.</summary>
    public static string? MotivoCorreccionInvalida(UsuarioLinea? asignacion, int lineaId, string? observacion)
    {
        if (asignacion == null)
            return "La asignación elegida no existe.";
        if (asignacion.LineaId != lineaId)
            return "El maquinista elegido no está asignado a esa línea (Maestros → Maquinistas).";
        if (string.IsNullOrWhiteSpace(observacion))
            return "Escribí el motivo de la corrección.";
        if (observacion.Trim().Length > ObservacionMaxima)
            return $"El motivo no puede pasar de {ObservacionMaxima} caracteres.";
        return null;
    }

    /// <summary>Texto que queda en el historial de cada detención pendiente que pasa a tomar al maquinista del turno.</summary>
    public static string ObservacionReasignacion(string nombreMaquinista, bool porSupervisor) =>
        porSupervisor
            ? $"Maquinista del turno corregido por el supervisor: {nombreMaquinista}."
            : $"Maquinista del turno confirmado en el login: {nombreMaquinista}.";
}
