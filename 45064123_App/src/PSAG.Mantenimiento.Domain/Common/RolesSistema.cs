namespace PSAG.Mantenimiento.Domain.Common;

/// <summary>
/// Nombres de los roles del sistema. Domain y Application los usan directamente;
/// Infrastructure los reexpone en Roles para Identity.
/// </summary>
public static class RolesSistema
{
    public const string Operario = "OPERARIO";
    public const string Supervisor = "SUPERVISOR";
    public const string Gerencia = "GERENCIA";
    public const string Administrador = "ADMINISTRADOR";
    public const string Mantenimiento = "MANTENIMIENTO";
}
