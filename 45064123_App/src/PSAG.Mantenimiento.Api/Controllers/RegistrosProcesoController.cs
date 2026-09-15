using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Controller para registros de variables de proceso (Eje 2).
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class RegistrosProcesoController : ApiControllerBase
{
    private readonly IRegistroProcesoService _registroProcesoService;
    private readonly ILogger<RegistrosProcesoController> _logger;

    public RegistrosProcesoController(
        IRegistroProcesoService registroProcesoService,
        ILogger<RegistrosProcesoController> logger)
    {
        _registroProcesoService = registroProcesoService;
        _logger = logger;
    }

    /// <summary>
    /// Inicia o retoma un registro de proceso para línea+producto.
    /// Si ya existe uno para el turno actual, lo retoma.
    /// </summary>
    [HttpPost("iniciar")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(IniciarRegistroProcesoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IniciarRegistro([FromBody] IniciarRegistroProcesoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _registroProcesoService.IniciarRegistroAsync(request, CurrentUserId);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al iniciar registro",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene un registro completo con todos sus grupos y valores.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(RegistroProcesoCompletoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRegistro(long id)
    {
        var result = await _registroProcesoService.ObtenerCompletoAsync(id);

        if (!result.IsSuccess)
            return NotFound(new ProblemDetails
            {
                Title = "Registro no encontrado",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Guarda valores de parámetros. Calcula desvíos automáticamente.
    /// </summary>
    [HttpPost("guardar-valores")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarValores([FromBody] GuardarValoresRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var result = await _registroProcesoService.GuardarValoresAsync(request, CurrentUserId);

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
                Title = "Error al guardar valores",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Cierra un registro (no editable después).
    /// </summary>
    [HttpPost("{id:long}/cerrar")]
    [Authorize(Policy = "CanCloseDetenciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CerrarRegistro(long id, [FromBody] CerrarRegistroProcesoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        request.RegistroProcesoId = id;
        var result = await _registroProcesoService.CerrarRegistroAsync(request, CurrentUserId);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al cerrar registro",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return NoContent();
    }

    /// <summary>
    /// Obtiene registros paginados con filtros.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RegistroProcesoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegistros([FromQuery] RegistroProcesoFiltroRequest filtro)
    {
        var result = await _registroProcesoService.ObtenerPaginadoAsync(filtro);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener registros",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene los grupos de parámetros con estándares para línea+producto.
    /// Útil para mostrar el formulario de captura.
    /// </summary>
    [HttpGet("grupos/{lineaId:int}/{productoId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<GrupoParametroDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGruposConEstandares(int lineaId, int productoId)
    {
        var result = await _registroProcesoService.ObtenerGruposConEstandaresAsync(lineaId, productoId);

        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Title = "Error al obtener grupos",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Value);
    }
}
