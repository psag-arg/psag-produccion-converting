using Microsoft.AspNetCore.Identity;

namespace PSAG.Mantenimiento.Infrastructure.Identity;

/// <summary>
/// Usuario de la aplicación extendiendo IdentityUser.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public bool RequiereCambioPassword { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? UltimoAcceso { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiration { get; set; }
}

/// <summary>
/// Roles predefinidos del sistema.
/// </summary>
public static class Roles
{
    public const string Operario = "OPERARIO";
    public const string Supervisor = "SUPERVISOR";
    public const string Gerencia = "GERENCIA";
    public const string Administrador = "ADMINISTRADOR";
    public const string Mantenimiento = "MANTENIMIENTO";

    public static readonly string[] TodosLosRoles = { Operario, Supervisor, Gerencia, Administrador, Mantenimiento };

    /// <summary>
    /// Roles que tienen acceso global a todas las líneas.
    /// </summary>
    public static readonly string[] RolesGlobales = { Administrador, Mantenimiento, Gerencia };

    /// <summary>
    /// Roles que pueden modificar eventos.
    /// </summary>
    public static readonly string[] RolesModificacion = { Operario, Supervisor, Mantenimiento, Administrador };

    /// <summary>
    /// Roles de solo lectura.
    /// </summary>
    public static readonly string[] RolesSoloLectura = { Gerencia };
}
