using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using PSAG.Mantenimiento.Domain.Common;

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

    /// <summary>
    /// Nombre para mostrar: el nombre completo o, si está vacío, el nombre de usuario. No se guarda.
    /// </summary>
    [NotMapped]
    public string NombreVisible => string.IsNullOrWhiteSpace(NombreCompleto) ? UserName ?? Id : NombreCompleto;
}

/// <summary>
/// Roles predefinidos del sistema.
/// </summary>
public static class Roles
{
    public const string Operario = RolesSistema.Operario;
    public const string Supervisor = RolesSistema.Supervisor;
    public const string Gerencia = RolesSistema.Gerencia;
    public const string Administrador = RolesSistema.Administrador;
    public const string Mantenimiento = RolesSistema.Mantenimiento;

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
