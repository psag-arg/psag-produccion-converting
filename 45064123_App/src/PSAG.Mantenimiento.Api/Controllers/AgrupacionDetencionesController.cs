using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Agrupador de detenciones (reunión 17/11/2026): microcortes de cámara previos a una detención que el operario
/// declara con el mismo motivo. Sin DELETE: desagrupar desactiva (Activo = 0, quién y cuándo).
/// La restricción por línea asignada se valida acá. Agrupar NO cambia MinutosEfectivos de la detención.
/// </summary>
[Route("api/detenciones")]
[ApiController]
[Authorize]
public class AgrupacionDetencionesController : ApiControllerBase
{
    private readonly IAgrupacionDetencionService _servicio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;

    public AgrupacionDetencionesController(IAgrupacionDetencionService servicio, IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta)
    {
        _servicio = servicio;
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
    }

    /// <summary>Microcortes sin agrupar de la misma cámara/línea en los 10 min previos a la parada de la detención.</summary>
    [HttpGet("{id:long}/microcortes")]
    [ProducesResponseType(typeof(MicrocortesPreviosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMicrocortes(long id)
    {
        if (await ValidarDetencionAsync(id) is { } error)
            return error;
        var resultado = await _servicio.BuscarMicrocortesAsync(id);
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudieron buscar los microcortes", "Detención no encontrada") : Ok(resultado.Value);
    }

    /// <summary>
    /// Lo mismo para una detención que se está cargando (todavía no existe): línea, fecha y hora de inicio en hora
    /// de la planta, y opcionalmente la cámara.
    /// </summary>
    [HttpGet("microcortes")]
    [ProducesResponseType(typeof(MicrocortesPreviosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMicrocortesPorInicio(
        [FromQuery] int lineaId, [FromQuery] DateOnly fecha, [FromQuery] TimeOnly hora, [FromQuery] int? puntoControlCalidadId = null)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        if (!await PuedeOperarLineaAsync(lineaId))
            return LineaNoAsignada();

        var inicio = HoraPlanta.DesdeHoraDePared(fecha.ToDateTime(hora), _zonaPlanta);
        var resultado = await _servicio.BuscarMicrocortesAsync(lineaId, puntoControlCalidadId, inicio);
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudieron buscar los microcortes") : Ok(resultado.Value);
    }

    /// <summary>Microcortes agrupados activos y tiempo agrupado (inicio real, segundos totales; sólo informativo).</summary>
    [HttpGet("{id:long}/agrupacion")]
    [ProducesResponseType(typeof(AgrupacionDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgrupacion(long id)
    {
        var resultado = await _servicio.ObtenerAsync(id);
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudo leer la agrupación", "Detención no encontrada") : Ok(resultado.Value);
    }

    /// <summary>Agrupa microcortes bajo la detención (origen OPERADOR). Todo o nada.</summary>
    [HttpPost("{id:long}/agrupacion")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(AgrupacionDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Agrupar(long id, [FromBody] AgruparMicrocortesRequest request)
    {
        if (await ValidarDetencionAsync(id) is { } error)
            return error;
        var resultado = await _servicio.AgruparAsync(id, request.ParadaCamaraIds ?? [], CurrentUserId!);
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudieron agrupar los microcortes", "Detención no encontrada") : Ok(resultado.Value);
    }

    /// <summary>Desagrupa (Activo = 0 con quién y cuándo; no se borra nada). Todo o nada.</summary>
    [HttpPost("{id:long}/agrupacion/desagrupar")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(AgrupacionDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desagrupar(long id, [FromBody] AgruparMicrocortesRequest request)
    {
        if (await ValidarDetencionAsync(id) is { } error)
            return error;
        var resultado = await _servicio.DesagruparAsync(id, request.ParadaCamaraIds ?? [], CurrentUserId!);
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudieron desagrupar los microcortes", "Detención no encontrada") : Ok(resultado.Value);
    }

    /// <summary>Sesión, existencia de la detención y línea asignada (el operario sólo opera sus líneas).</summary>
    private async Task<IActionResult?> ValidarDetencionAsync(long id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
            return NotFound(new ProblemDetails { Title = "Detención no encontrada", Status = StatusCodes.Status404NotFound });
        return await PuedeOperarLineaAsync(detencion.LineaId) ? null : LineaNoAsignada();
    }

    private async Task<bool> PuedeOperarLineaAsync(int lineaId) =>
        DetencionCargaReglas.PuedeCargarEnLinea(Roles,
            await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(CurrentUserId!, lineaId) != null);

    private ObjectResult LineaNoAsignada() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
    {
        Title = "Línea no asignada",
        Detail = DetencionCargaReglas.MensajeLineaNoAsignada,
        Status = StatusCodes.Status403Forbidden
    });
}
