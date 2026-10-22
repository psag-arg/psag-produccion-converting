using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using System.Security.Claims;

namespace PSAG.Mantenimiento.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected string? CurrentUserName => User.FindFirstValue(ClaimTypes.Name);

    protected string? CorrelationId => HttpContext.Items["CorrelationId"]?.ToString();

    protected IReadOnlyCollection<string> Roles => User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    /// <summary>
    /// Traduce el ErrorCode de un Result de Application a ProblemDetails: NOT_FOUND 404, FORBIDDEN 403,
    /// CONCURRENCY 409 y el resto (VALIDATION) 400. Cada controller pasa sus títulos; un código sin título
    /// propio usa <paramref name="titulo"/>.
    /// </summary>
    protected IActionResult ProblemaDe(
        Result resultado,
        string titulo,
        string? tituloNoEncontrado = null,
        string? tituloSinPermiso = null,
        string? tituloConcurrencia = null)
    {
        var (status, tituloFinal) = resultado.ErrorCode switch
        {
            "NOT_FOUND" => (StatusCodes.Status404NotFound, tituloNoEncontrado ?? titulo),
            "FORBIDDEN" => (StatusCodes.Status403Forbidden, tituloSinPermiso ?? titulo),
            "CONCURRENCY" => (StatusCodes.Status409Conflict, tituloConcurrencia ?? titulo),
            _ => (StatusCodes.Status400BadRequest, titulo)
        };

        return StatusCode(status, new ProblemDetails
        {
            Title = tituloFinal,
            Detail = resultado.ErrorMessage,
            Status = status,
            Instance = HttpContext.Request.Path
        });
    }

    /// <summary>
    /// Líneas asignadas al usuario actual (ninguna si no hay sesión).
    /// </summary>
    protected async Task<int[]> LineasAsignadasAsync(IUsuarioLineaRepository usuariosLineas) =>
        string.IsNullOrEmpty(CurrentUserId)
            ? Array.Empty<int>()
            : await usuariosLineas.GetLineaIdsAsync(CurrentUserId);

    /// <summary>
    /// Calendario base y excepciones de horas programadas: el administrador edita todas las líneas,
    /// el supervisor sólo las suyas (CalendarioLineaReglas.PuedeEditar).
    /// </summary>
    protected async Task<bool> PuedeEditarCalendarioAsync(IUsuarioLineaRepository usuariosLineas, int lineaId) =>
        CalendarioLineaReglas.PuedeEditar(Roles, await LineasAsignadasAsync(usuariosLineas), lineaId);

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Value is null ? NoContent() : Ok(result.Value);
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Error",
            Detail = result.Error,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path
        });
    }

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Error",
            Detail = result.Error,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path
        });
    }

    protected ActionResult<T> HandleResultWithValue<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Error",
            Detail = result.Error,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path
        });
    }

    protected IActionResult CreatedAtActionResult<T>(string actionName, object routeValues, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValues, result.Value);
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Error",
            Detail = result.Error,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path
        });
    }
}
