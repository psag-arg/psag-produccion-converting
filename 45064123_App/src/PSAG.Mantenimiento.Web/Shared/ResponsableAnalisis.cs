using System.Security.Claims;

namespace PSAG.Mantenimiento.Web.Shared;

public static class ResponsableAnalisis
{
    /// <summary>
    /// El análisis le toca al usuario si lo asignaron a él o a uno de sus roles.
    /// </summary>
    public static bool EsResponsable(this ClaimsPrincipal? usuario, string? responsableUsuarioId, string? responsableRol) =>
        usuario != null
        && ((!string.IsNullOrEmpty(responsableUsuarioId) && responsableUsuarioId == usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value)
            || (!string.IsNullOrEmpty(responsableRol) && usuario.IsInRole(responsableRol)));

    /// <summary>"MANTENIMIENTO" → "Mantenimiento".</summary>
    public static string NombreRol(string rol) =>
        rol.Length == 0 ? rol : char.ToUpperInvariant(rol[0]) + rol[1..].ToLowerInvariant();
}
