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
    /// Lápiz de editar: detención abierta, rol que carga y, si es sólo operario, una de sus líneas.
    /// La API vuelve a validar lo mismo al guardar.
    /// </summary>
    public static bool PuedeEditar(DetencionDto det, Func<string, bool> tieneRol, IEnumerable<int> lineasAsignadas) =>
        !det.Eliminada
        && det.EstadoActual != "CERRADA"
        && PuedeCargar(tieneRol)
        && (!SoloOperario(tieneRol) || lineasAsignadas.Contains(det.LineaId));
}
