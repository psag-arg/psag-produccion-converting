using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Nombres para mostrar de los usuarios que aparecen en un DTO (quién cargó, cerró, cambió...).
/// Application sólo conoce los ids; los nombres los completa la API, que tiene acceso a Identity.
/// </summary>
public static class NombresUsuario
{
    /// <summary>
    /// Nombre visible de cada id, con una sola consulta. Los ids que no existen no aparecen.
    /// </summary>
    public static async Task<Dictionary<string, string>> NombresVisiblesAsync(
        this UserManager<ApplicationUser> userManager,
        IEnumerable<string?> usuarioIds)
    {
        var ids = usuarioIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var usuarios = await userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new ApplicationUser { Id = u.Id, NombreCompleto = u.NombreCompleto, UserName = u.UserName })
            .ToListAsync();

        return usuarios.ToDictionary(u => u.Id, u => u.NombreVisible);
    }

    /// <summary>
    /// Nombre del usuario, o null si no hay id o el usuario no existe.
    /// </summary>
    public static string? NombreDe(this IReadOnlyDictionary<string, string> nombres, string? usuarioId) =>
        usuarioId != null && nombres.TryGetValue(usuarioId, out var nombre) ? nombre : null;
}
