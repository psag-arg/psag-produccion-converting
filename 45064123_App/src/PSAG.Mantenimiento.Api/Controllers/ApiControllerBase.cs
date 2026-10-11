using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
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
