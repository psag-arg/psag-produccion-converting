using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Permisos de las tareas de mantenimiento. Funciones puras: la API las usa para validar, no sólo la pantalla.
/// </summary>
public static class TareaMantenimientoReglas
{
    public const string MensajeSinPermisoCarga =
        "Las tareas de mantenimiento las cargan operarios, supervisores o el administrador.";
    public const string MensajeSinPermisoGestion =
        "Sólo mantenimiento, supervisión o el administrador pueden programar y cerrar tareas.";

    /// <summary>Roles que informan tareas: operario, supervisión y administrador.</summary>
    public static readonly IReadOnlyList<string> RolesCarga =
        [RolesSistema.Operario, RolesSistema.Supervisor, RolesSistema.Administrador];

    /// <summary>Roles que programan y cierran: mantenimiento, supervisión y administrador.</summary>
    public static readonly IReadOnlyList<string> RolesGestion =
        [RolesSistema.Mantenimiento, RolesSistema.Supervisor, RolesSistema.Administrador];

    public static bool PuedeCargar(IEnumerable<string> roles) => roles.Any(RolesCarga.Contains);

    public static bool PuedeProgramarYCerrar(IEnumerable<string> roles) => roles.Any(RolesGestion.Contains);

    /// <summary>
    /// Mismo criterio que la detención: el que es sólo operario carga únicamente en sus líneas.
    /// </summary>
    public static bool PuedeCargarEnLinea(IEnumerable<string> roles, bool lineaAsignada) =>
        DetencionCargaReglas.PuedeCargarEnLinea(roles, lineaAsignada);
}
