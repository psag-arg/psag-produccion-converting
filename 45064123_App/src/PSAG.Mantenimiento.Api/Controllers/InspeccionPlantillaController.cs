using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Maestro de planes de inspección por línea (uno por equipo) y sus puntos, igual que el de Control de Proceso.
/// La carga del operador sigue en InspeccionesController. Sin DELETE: se desactiva.
/// </summary>
[Route("api/inspecciones/maestro")]
[ApiController]
[Authorize(Policy = "CanManageCatalogs")]
public class InspeccionPlantillaController : ApiControllerBase
{
    private readonly IInspeccionPlantillaService _plantillaService;

    public InspeccionPlantillaController(IInspeccionPlantillaService plantillaService)
    {
        _plantillaService = plantillaService;
    }

    /// <summary>Planes de la línea con todos sus puntos (activos o no).</summary>
    [HttpGet("plantilla/{lineaId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanInspeccionConItemsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlantilla(int lineaId)
    {
        var result = await _plantillaService.ObtenerPlantillaAsync(lineaId);
        return result.IsSuccess ? Ok(result.Value) : ProblemaDe(result, "Error al obtener los planes de inspección");
    }

    [HttpPost("planes")]
    public async Task<IActionResult> CrearPlan([FromBody] GuardarPlanInspeccionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.CrearPlanAsync(request, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : ProblemaDe(result, "Error al crear el plan");
    }

    [HttpPut("planes/{id:int}")]
    public async Task<IActionResult> ActualizarPlan(int id, [FromBody] GuardarPlanInspeccionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.ActualizarPlanAsync(id, request, CurrentUserId);
        return result.IsSuccess ? NoContent() : ProblemaDe(result, "Error al actualizar el plan");
    }

    [HttpPost("puntos")]
    public async Task<IActionResult> CrearItem([FromBody] GuardarItemInspeccionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.CrearItemAsync(request, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : ProblemaDe(result, "Error al crear el punto de inspección");
    }

    [HttpPut("puntos/{id:int}")]
    public async Task<IActionResult> ActualizarItem(int id, [FromBody] GuardarItemInspeccionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _plantillaService.ActualizarItemAsync(id, request, CurrentUserId);
        return result.IsSuccess ? NoContent() : ProblemaDe(result, "Error al actualizar el punto de inspección");
    }
}
