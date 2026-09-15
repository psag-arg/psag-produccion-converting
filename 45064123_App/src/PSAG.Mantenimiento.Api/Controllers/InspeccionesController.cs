using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Controller para inspecciones de máquina (Eje 3).
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InspeccionesController : ApiControllerBase
{
    private readonly IInspeccionService _inspeccionService;
    private readonly ILogger<InspeccionesController> _logger;

    public InspeccionesController(
        IInspeccionService inspeccionService,
        ILogger<InspeccionesController> logger)
    {
        _inspeccionService = inspeccionService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene planes de inspección para una línea y puesto.
    /// </summary>
    [HttpGet("planes")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanInspeccionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanes([FromQuery] int lineaId, [FromQuery] string puesto)
    {
        var result = await _inspeccionService.ObtenerPlanesAsync(lineaId, puesto);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener planes",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene planes pendientes de ejecución para hoy.
    /// R15: Según frecuencia y última ejecución.
    /// </summary>
    [HttpGet("pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanInspeccionConItemsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendientes([FromQuery] int lineaId, [FromQuery] string puesto)
    {
        var result = await _inspeccionService.ObtenerPendientesHoyAsync(lineaId, puesto);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener pendientes",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene un plan con todos sus ítems.
    /// </summary>
    [HttpGet("planes/{planId:int}")]
    [ProducesResponseType(typeof(PlanInspeccionConItemsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlan(int planId)
    {
        var result = await _inspeccionService.ObtenerPlanConItemsAsync(planId);

        if (!result.IsSuccess)
            return NotFound(new ProblemDetails
            {
                Title = "Plan no encontrado",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Inicia una nueva ejecución de inspección.
    /// </summary>
    [HttpPost("ejecuciones")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(IniciarEjecucionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IniciarEjecucion([FromBody] IniciarEjecucionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _inspeccionService.IniciarEjecucionAsync(request, CurrentUserId);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al iniciar ejecución",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene una ejecución completa con sus detalles.
    /// </summary>
    [HttpGet("ejecuciones/{id:long}")]
    [ProducesResponseType(typeof(EjecucionInspeccionCompletoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEjecucion(long id)
    {
        var result = await _inspeccionService.ObtenerEjecucionAsync(id);

        if (!result.IsSuccess)
            return NotFound(new ProblemDetails
            {
                Title = "Ejecución no encontrada",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Guarda resultados de inspección.
    /// R16: Si resultado es NO_OK, la observación es obligatoria.
    /// </summary>
    [HttpPost("ejecuciones/guardar")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarResultados([FromBody] GuardarResultadosInspeccionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _inspeccionService.GuardarResultadosAsync(request, CurrentUserId);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "CONCURRENCY")
                return Conflict(new ProblemDetails
                {
                    Title = "Conflicto de concurrencia",
                    Detail = result.Error,
                    Status = StatusCodes.Status409Conflict
                });

            return BadRequest(new ProblemDetails
            {
                Title = "Error al guardar resultados",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Obtiene ejecuciones paginadas con filtros.
    /// </summary>
    [HttpGet("ejecuciones")]
    [ProducesResponseType(typeof(PagedResult<EjecucionInspeccionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEjecuciones([FromQuery] EjecucionInspeccionFiltroRequest filtro)
    {
        var result = await _inspeccionService.ObtenerEjecucionesPaginadoAsync(filtro);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener ejecuciones",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene hallazgos NO OK pendientes.
    /// Panel para supervisores/mantenimiento.
    /// </summary>
    [HttpGet("no-ok")]
    [ProducesResponseType(typeof(IReadOnlyList<HallazgoNoOkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHallazgosPendientes([FromQuery] int? lineaId = null)
    {
        var result = await _inspeccionService.ObtenerHallazgosPendientesAsync(lineaId);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener hallazgos",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene hallazgos paginados con filtros.
    /// </summary>
    [HttpGet("no-ok/paginado")]
    [ProducesResponseType(typeof(PagedResult<HallazgoNoOkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHallazgosPaginado([FromQuery] HallazgoNoOkFiltroRequest filtro)
    {
        var result = await _inspeccionService.ObtenerHallazgosPaginadoAsync(filtro);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener hallazgos",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Resuelve un hallazgo NO OK.
    /// R17: No se borra, se marca como resuelto.
    /// </summary>
    [HttpPost("no-ok/{id:long}/resolver")]
    [Authorize(Policy = "CanCloseDetenciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolverHallazgo(long id, [FromBody] ResolverHallazgoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        request.HallazgoNoOkId = id;
        var result = await _inspeccionService.ResolverHallazgoAsync(request, CurrentUserId);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND")
                return NotFound(new ProblemDetails
                {
                    Title = "Hallazgo no encontrado",
                    Detail = result.Error,
                    Status = StatusCodes.Status404NotFound
                });

            return BadRequest(new ProblemDetails
            {
                Title = "Error al resolver hallazgo",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return NoContent();
    }
}
