using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Maestro de la plantilla de Control de Proceso por línea (T-545): grupos y puntos de control.
/// La carga del maquinista sigue en RegistrosProcesoController. Sin DELETE: se desactiva.
/// </summary>
[Route("api/control-proceso")]
[ApiController]
[Authorize]
public class ControlProcesoController : ApiControllerBase
{
    private readonly IControlProcesoPlantillaService _plantillaService;

    public ControlProcesoController(IControlProcesoPlantillaService plantillaService)
    {
        _plantillaService = plantillaService;
    }

    /// <summary>
    /// Plantilla completa de la línea (grupos y puntos, activos o no).
    /// </summary>
    [HttpGet("plantilla/{lineaId:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(IReadOnlyList<GrupoParametroDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlantilla(int lineaId)
    {
        var result = await _plantillaService.ObtenerPlantillaAsync(lineaId);
        return result.IsSuccess ? Ok(result.Value) : Error(result, "Error al obtener la plantilla");
    }

    [HttpPost("grupos")]
    [Authorize(Policy = "CanManageCatalogs")]
    public async Task<IActionResult> CrearGrupo([FromBody] GuardarGrupoControlRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.CrearGrupoAsync(request, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : Error(result, "Error al crear el grupo");
    }

    [HttpPut("grupos/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    public async Task<IActionResult> ActualizarGrupo(int id, [FromBody] GuardarGrupoControlRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.ActualizarGrupoAsync(id, request, CurrentUserId);
        return result.IsSuccess ? NoContent() : Error(result, "Error al actualizar el grupo");
    }

    [HttpPost("puntos")]
    [Authorize(Policy = "CanManageCatalogs")]
    public async Task<IActionResult> CrearPunto([FromBody] GuardarPuntoControlRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.CrearPuntoAsync(request, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : Error(result, "Error al crear el punto de control");
    }

    [HttpPut("puntos/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    public async Task<IActionResult> ActualizarPunto(int id, [FromBody] GuardarPuntoControlRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.ActualizarPuntoAsync(id, request, CurrentUserId);
        return result.IsSuccess ? NoContent() : Error(result, "Error al actualizar el punto de control");
    }

    private IActionResult Error(Result result, string titulo)
    {
        var status = result.ErrorCode == "NOT_FOUND" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        return StatusCode(status, new ProblemDetails { Title = titulo, Detail = result.Error, Status = status });
    }
}
