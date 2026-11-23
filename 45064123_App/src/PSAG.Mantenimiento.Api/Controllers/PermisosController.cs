using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Roles y permisos configurables (T-802). "mios" lo usa la Web para armar el menú (cualquier usuario logueado);
/// el configurador (Configuración → Roles y permisos) lee y guarda la matriz de cada rol.
/// </summary>
[Route("api/permisos")]
[ApiController]
[Authorize]
public class PermisosController : ApiControllerBase
{
    private readonly IPermisosService _permisos;

    public PermisosController(IPermisosService permisos) => _permisos = permisos;

    /// <summary>Opciones que ve el usuario (unión de sus roles) con sus acciones y si está obligado a declarar turno.</summary>
    [HttpGet("mios")]
    [ProducesResponseType(typeof(PermisosMiosDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMios(CancellationToken cancellationToken) =>
        Ok(await _permisos.ObtenerMiosAsync(Roles, cancellationToken));

    /// <summary>Catálogo de opciones y matriz de todos los roles.</summary>
    [HttpGet("roles")]
    [Permiso(Opciones.ConfigRoles, AccionPermiso.Ver)]
    [ProducesResponseType(typeof(ConfiguracionPermisosDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfiguracion(CancellationToken cancellationToken) =>
        Ok(await _permisos.ObtenerConfiguracionAsync(cancellationToken));

    /// <summary>
    /// Guarda la matriz completa de un rol (las opciones que no vienen quedan sin permisos) y su tilde "Obligado a declarar
    /// turno". Se aplica en la siguiente request (invalida la caché). El ADMINISTRADOR no puede quedarse sin Roles y permisos.
    /// </summary>
    [HttpPut("roles/{roleId}")]
    [Permiso(Opciones.ConfigRoles, AccionPermiso.Modificar)]
    [ProducesResponseType(typeof(RolPermisosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GuardarRol(string roleId, [FromBody] GuardarPermisosRolRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _permisos.GuardarRolAsync(roleId, request, CurrentUserId, cancellationToken);
        return resultado.IsFailure
            ? ProblemaDe(resultado, "No se pudieron guardar los permisos", "Rol no encontrado")
            : Ok(resultado.Value);
    }
}
