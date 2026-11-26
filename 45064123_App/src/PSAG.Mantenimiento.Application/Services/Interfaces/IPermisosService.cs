using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Roles y permisos configurables (T-802): matriz rol × opción × VER/ALTA/MODIFICACION/BAJA en las tablas CFG_.
/// Lee con caché corta (se invalida al guardar desde el configurador).
/// </summary>
public interface IPermisosService
{
    /// <summary>Unión de los permisos de los roles (por nombre) y si alguno está obligado a declarar turno.</summary>
    Task<PermisosUsuario> ObtenerDeRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default);

    Task<PermisosMiosDto> ObtenerMiosAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default);

    Task<ConfiguracionPermisosDto> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default);

    Task<Result<RolPermisosDto>> GuardarRolAsync(
        string roleId, GuardarPermisosRolRequest request, string usuarioId, CancellationToken cancellationToken = default);

    void InvalidarCache();
}

/// <summary>
/// Permisos del usuario de la request actual, para los chequeos que los servicios hacían por rol. Devuelve null
/// cuando no hay usuario de una request (jobs, tests de servicio): el servicio usa entonces su regla por rol de siempre,
/// que es equivalente a la matriz migrada.
/// </summary>
public interface IPermisosUsuarioActual
{
    Task<bool?> TieneAsync(string codigoOpcion, AccionPermiso accion, CancellationToken cancellationToken = default);
}

public static class PermisosUsuarioActualExtensions
{
    /// <summary>Permiso configurado si hay usuario de request; si no, la regla por rol anterior a T-802.</summary>
    public static async Task<bool> PermiteAsync(
        this IPermisosUsuarioActual? permisos, string codigoOpcion, AccionPermiso accion, Func<bool> reglaPorRol,
        CancellationToken cancellationToken = default)
    {
        if (permisos == null)
            return reglaPorRol();
        return await permisos.TieneAsync(codigoOpcion, accion, cancellationToken) ?? reglaPorRol();
    }
}
