using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Conteo de producción y paradas detectadas por cámara (T-610). Consultas con sesión; todos los usuarios ven
/// (como Calidad). El servicio de conteo entra por <see cref="ConteoCamaraIngestaController"/>. Sin DELETE ni edición.
/// </summary>
[Route("api/conteo-camara")]
[ApiController]
[Authorize]
public class ConteoCamaraController : ApiControllerBase
{
    private readonly IConteoCamaraService _servicio;

    public ConteoCamaraController(IConteoCamaraService servicio) => _servicio = servicio;

    /// <summary>
    /// Paradas detectadas por la cámara con su cruce con las detenciones cargadas
    /// (CON_DETENCION / POSIBLE / SIN_DETENCION). soloSinDetencion=true deja sólo las que nadie cargó.
    /// </summary>
    [HttpGet("paradas")]
    [ProducesResponseType(typeof(ParadasCamaraResultado), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetParadas([FromQuery] ParadasCamaraFiltroRequest filtro)
    {
        var resultado = await _servicio.ObtenerParadasAsync(filtro);
        return resultado.IsFailure ? ErrorResult(resultado) : Ok(resultado.Value);
    }

    /// <summary>Rollos por hora y carril de un día de planta.</summary>
    [HttpGet("produccion")]
    [ProducesResponseType(typeof(ProduccionCamaraDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProduccion([FromQuery] DateOnly fecha, [FromQuery] int? lineaId)
    {
        var resultado = await _servicio.ObtenerProduccionAsync(fecha, lineaId);
        return resultado.IsFailure ? ErrorResult(resultado) : Ok(resultado.Value);
    }

    private IActionResult ErrorResult(Result resultado) => ProblemaDe(resultado, "Error de validación");
}

/// <summary>
/// Punto de entrada del servicio de conteo por cámara (T-610). Sin sesión: misma clave de servicio que la
/// ingesta de calidad (header X-Clave-Ingesta contra Calidad:ClaveIngesta; sin clave 503). Idempotente.
/// </summary>
[Route("api/conteo-camara/ingesta")]
[ApiController]
[AllowAnonymous]
[ClaveIngestaCalidad]
public class ConteoCamaraIngestaController : ApiControllerBase
{
    private readonly IConteoCamaraService _servicio;
    private readonly ILogger<ConteoCamaraIngestaController> _logger;

    public ConteoCamaraIngestaController(IConteoCamaraService servicio, ILogger<ConteoCamaraIngestaController> logger)
    {
        _servicio = servicio;
        _logger = logger;
    }

    /// <summary>Guarda un lote de minutos y paradas. 200 con cuántos fueron nuevos y cuántos ya estaban.</summary>
    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(IngestaConteoCamaraRespuesta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ingestar([FromBody] IngestaConteoCamaraRequest request)
    {
        var resultado = await _servicio.IngestarAsync(request);
        if (resultado.IsFailure)
        {
            _logger.LogWarning("Lote de conteo por cámara rechazado: {Motivo}", resultado.ErrorMessage);
            return ProblemaDe(resultado, "Lote de conteo inválido");
        }
        return Ok(resultado.Value);
    }
}
