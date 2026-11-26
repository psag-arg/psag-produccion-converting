using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.Autorizacion;

/// <summary>Resuelve <see cref="PermisoRequirement"/> con los permisos del usuario (unión de sus roles, con caché corta).</summary>
public sealed class PermisoHandler : AuthorizationHandler<PermisoRequirement>
{
    private readonly IHttpContextAccessor _http;

    public PermisoHandler(IHttpContextAccessor http) => _http = http;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;
        var permisos = await PermisosDeRequest.ObtenerAsync(_http.HttpContext, context.User);
        // Sin permiso no se llama a Fail(): así el requisito queda en FailedRequirements y PermisoResultHandler arma el 403.
        if (permisos.TieneAlguna(requirement.Codigos, requirement.Accion))
            context.Succeed(requirement);
    }
}

/// <summary>Permisos del usuario de la request, cargados una sola vez por request (HttpContext.Items).</summary>
public static class PermisosDeRequest
{
    private const string Clave = "T802.Permisos";

    public static async Task<PermisosUsuario> ObtenerAsync(HttpContext? http, ClaimsPrincipal usuario)
    {
        if (http == null)
            return PermisosUsuario.Ninguno;
        if (http.Items.TryGetValue(Clave, out var guardado) && guardado is PermisosUsuario p)
            return p;
        var servicio = http.RequestServices.GetRequiredService<IPermisosService>();
        var roles = usuario.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var permisos = await servicio.ObtenerDeRolesAsync(roles, http.RequestAborted);
        http.Items[Clave] = permisos;
        return permisos;
    }

    public static string Mensaje(PermisoRequirement r)
    {
        var nombres = r.Codigos.Select(c => CatalogoPermisos.Buscar(c)?.Nombre ?? c).Select(n => $"\"{n}\"");
        return $"Tus roles no tienen permiso para {AccionPermisoTexto.Verbo(r.Accion)} en {string.Join(" / ", nombres)}. "
               + "Si lo necesitás, pedíselo al administrador (Configuración → Roles y permisos).";
    }
}

/// <summary>Un 403 por falta de permiso sale con ProblemDetails en castellano (el resto, como siempre).</summary>
public sealed class PermisoResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _porDefecto = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<PermisoRequirement>().FirstOrDefault() is { } requisito)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Sin permiso",
                Detail = PermisosDeRequest.Mensaje(requisito),
                Status = StatusCodes.Status403Forbidden,
                Instance = context.Request.Path
            }, options: null, contentType: "application/problem+json");
            return;
        }
        await _porDefecto.HandleAsync(next, context, policy, authorizeResult);
    }
}

/// <summary>Para los servicios de Application que antes chequeaban por rol (ver <see cref="IPermisosUsuarioActual"/>).</summary>
public sealed class PermisosUsuarioActualHttp : IPermisosUsuarioActual
{
    private readonly IHttpContextAccessor _http;

    public PermisosUsuarioActualHttp(IHttpContextAccessor http) => _http = http;

    public async Task<bool?> TieneAsync(string codigoOpcion, AccionPermiso accion, CancellationToken cancellationToken = default)
    {
        var http = _http.HttpContext;
        if (http?.User.Identity?.IsAuthenticated != true)
            return null;
        var permisos = await PermisosDeRequest.ObtenerAsync(http, http.User);
        return permisos.Tiene(codigoOpcion, accion);
    }
}
