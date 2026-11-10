using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Mismo criterio que DetencionCargaReglas en la API: el operario sólo carga en sus líneas,
/// salvo que además tenga otro rol que carga.
/// </summary>
public static class RolesCarga
{
    /// <summary>Mismos roles que la política CanCreateDetenciones.</summary>
    public static bool PuedeCargar(Func<string, bool> tieneRol) =>
        new[] { "ADMINISTRADOR", "MANTENIMIENTO", "SUPERVISOR", "OPERARIO" }.Any(tieneRol);

    public static bool SoloOperario(Func<string, bool> tieneRol) =>
        tieneRol("OPERARIO") && !new[] { "ADMINISTRADOR", "SUPERVISOR", "MANTENIMIENTO" }.Any(tieneRol);

    /// <summary>
    /// Líneas en las que el usuario puede cargar: sus líneas asignadas; sin asignaciones, ninguna si es
    /// sólo operario (la API lo rechaza) y todas para el resto.
    /// </summary>
    public static List<LineaDto> LineasDisponibles(List<LineaDto> lineas, IEnumerable<MaquinistaDto> asignaciones, bool esSoloOperario)
    {
        var ids = asignaciones.Select(a => a.LineaId).ToHashSet();
        if (ids.Count > 0)
            return lineas.Where(l => ids.Contains(l.LineaId)).ToList();

        return esSoloOperario ? new() : lineas;
    }

    public const string PendienteDeclaracion = "PENDIENTE_JUSTIFICACION";

    /// <summary>
    /// Lápiz de editar: detención abierta y ya declarada, rol que carga y, si es sólo operario, una de sus líneas.
    /// La pendiente de declaración no se edita: se declara (la API la rechaza). La API vuelve a validar al guardar.
    /// </summary>
    public static bool PuedeEditar(DetencionDto det, Func<string, bool> tieneRol, IEnumerable<int> lineasAsignadas) =>
        det.EstadoActual is not ("CERRADA" or PendienteDeclaracion)
        && PuedeCargarDetencion(det, tieneRol, lineasAsignadas);

    /// <summary>Botón Declarar: pendiente de declaración, mismos roles y líneas que para cargar.</summary>
    public static bool PuedeDeclarar(DetencionDto det, Func<string, bool> tieneRol, IEnumerable<int> lineasAsignadas) =>
        det.EstadoActual == PendienteDeclaracion
        && PuedeCargarDetencion(det, tieneRol, lineasAsignadas);

    private static bool PuedeCargarDetencion(DetencionDto det, Func<string, bool> tieneRol, IEnumerable<int> lineasAsignadas) =>
        !det.Eliminada
        && PuedeCargar(tieneRol)
        && (!SoloOperario(tieneRol) || lineasAsignadas.Contains(det.LineaId));

    /// <summary>
    /// Clave con la que se descarta el aviso de fin de turno: turno + día en que empezó. Así, cerrar el aviso de
    /// la noche del lunes no lo oculta la noche del martes.
    /// </summary>
    public static string? ClaveTurnoAviso(AvisoFinTurnoDto? aviso) =>
        aviso?.TurnoId is { } turnoId ? $"{turnoId}|{aviso.FechaInicioTurno:yyyy-MM-dd}" : null;

    /// <summary>Texto del aviso de fin de turno: cuántas son de este turno y cuántas de turnos anteriores.</summary>
    public static string TextoPendientesAviso(AvisoFinTurnoDto aviso)
    {
        static string Detenciones(int n) => n == 1 ? "1 detención" : $"{n} detenciones";
        var anteriores = aviso.Pendientes - aviso.PendientesTurnoActual;
        if (anteriores <= 0)
            return $"tenés {Detenciones(aviso.Pendientes)} de este turno sin declarar";
        if (aviso.PendientesTurnoActual == 0)
            return $"tenés {Detenciones(anteriores)} de turnos anteriores sin declarar";
        return $"tenés {Detenciones(aviso.Pendientes)} sin declarar ({aviso.PendientesTurnoActual} de este turno y {anteriores} de turnos anteriores)";
    }

    /// <summary>Roles que declaran (ven el aviso de fin de turno): operario, supervisor y administrador. Gerencia no.</summary>
    public static bool VeDeclaracion(Func<string, bool> tieneRol) =>
        new[] { "OPERARIO", "SUPERVISOR", "ADMINISTRADOR" }.Any(tieneRol);
}
