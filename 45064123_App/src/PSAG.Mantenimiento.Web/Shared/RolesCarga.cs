namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Mismo criterio que DetencionCargaReglas en la API: el operario sólo carga en sus líneas,
/// salvo que además tenga otro rol que carga.
/// </summary>
public static class RolesCarga
{
    public static bool SoloOperario(Func<string, bool> tieneRol) =>
        tieneRol("OPERARIO") && !new[] { "ADMINISTRADOR", "SUPERVISOR", "MANTENIMIENTO" }.Any(tieneRol);
}
