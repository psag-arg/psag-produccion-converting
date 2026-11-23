using System.Reflection;
using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>Qué roles habilita la matriz migrada (T-802) para el [Permiso] de una acción de controller.</summary>
public static class MatrizMigrada
{
    public static readonly string[] Roles =
        [RolesSistema.Administrador, RolesSistema.Mantenimiento, RolesSistema.Supervisor, RolesSistema.Operario, RolesSistema.Gerencia];

    public static IReadOnlyList<string> RolesConPermiso(MethodInfo accion)
    {
        var permiso = accion.GetCustomAttribute<PermisoAttribute>()
                      ?? throw new InvalidOperationException($"{accion.DeclaringType?.Name}.{accion.Name} no tiene [Permiso].");
        return Roles.Where(r => permiso.Codigos.Any(c => CatalogoPermisos.Buscar(c)!.De(r).HasFlag(permiso.Accion))).ToList();
    }
}
