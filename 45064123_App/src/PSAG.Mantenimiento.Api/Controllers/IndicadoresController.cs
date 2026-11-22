using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Indicadores de producción (T-798 2.10, etapa 5): presupuesto (parámetro línea + producto, snapshot en el tramo) vs.
/// real (producción oficial del turno), eficiencia, velocidad e indisponibilidad. Sólo lectura, para todos los usuarios
/// con sesión (como el dashboard). Reemplaza GET api/conteo-camara/eficiencia (un solo cálculo).
/// </summary>
[Route("api/indicadores")]
[ApiController]
[Authorize]
public class IndicadoresController : ApiControllerBase
{
    private readonly IIndicadoresService _servicio;

    public IndicadoresController(IIndicadoresService servicio) => _servicio = servicio;

    /// <summary>Detalle de un turno de producción: tramos, objetivo, real, eficiencia, velocidad y minutos de parada que cuentan.</summary>
    [HttpGet("turno/{turnoProduccionId:long}")]
    [ProducesResponseType(typeof(IndicadoresTurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTurno(long turnoProduccionId)
    {
        var resultado = await _servicio.ObtenerTurnoAsync(turnoProduccionId);
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudieron calcular los indicadores", "Turno no encontrado") : Ok(resultado.Value);
    }

    /// <summary>
    /// Presupuesto vs. real entre desde y hasta (fecha de producción), opcionalmente de una línea y de un producto,
    /// agrupado por turno (por defecto), dia, producto o linea. Sumas, nunca promedios de %. "Sin datos" nunca es 0.
    /// </summary>
    [HttpGet("produccion")]
    [ProducesResponseType(typeof(PresupuestoRealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProduccion(
        [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, [FromQuery] int? lineaId, [FromQuery] int? productoMaestroId,
        [FromQuery] string? agrupar)
    {
        var resultado = await _servicio.ObtenerProduccionAsync(new IndicadoresProduccionFiltro
        {
            Desde = desde, Hasta = hasta, LineaId = lineaId, ProductoMaestroId = productoMaestroId, Agrupar = agrupar
        });
        return resultado.IsFailure ? ProblemaDe(resultado, "No se pudo calcular el presupuesto vs. real") : Ok(resultado.Value);
    }
}
