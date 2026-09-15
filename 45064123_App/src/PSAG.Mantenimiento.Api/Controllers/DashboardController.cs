using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Dashboard;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardService dashboardService,
        ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Obtener dashboard completo con indicadores de los 3 ejes.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardCompletoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] DashboardFiltroRequest filtro)
    {
        // TODO: En produccion, obtener lineas permitidas del usuario actual
        IEnumerable<int>? lineasPermitidas = null;

        var result = await _dashboardService.ObtenerDashboardCompletoAsync(
            filtro,
            lineasPermitidas);

        return HandleResult(result);
    }

    /// <summary>
    /// Obtener resumen de detenciones.
    /// </summary>
    [HttpGet("resumen")]
    [ProducesResponseType(typeof(DashboardResumenDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResumen([FromQuery] DashboardFiltroRequest filtro)
    {
        IEnumerable<int>? lineasPermitidas = null;

        var result = await _dashboardService.ObtenerResumenAsync(
            filtro,
            lineasPermitidas);

        return HandleResult(result);
    }

    /// <summary>
    /// Obtener detenciones agrupadas por linea.
    /// </summary>
    [HttpGet("detenciones-por-linea")]
    [ProducesResponseType(typeof(IReadOnlyList<DetencionesPorLineaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetencionesPorLinea([FromQuery] DashboardFiltroRequest filtro)
    {
        IEnumerable<int>? lineasPermitidas = null;

        var result = await _dashboardService.ObtenerDetencionesPorLineaAsync(
            filtro,
            lineasPermitidas);

        return HandleResult(result);
    }

    /// <summary>
    /// Obtener detenciones agrupadas por tipo de falla.
    /// </summary>
    [HttpGet("detenciones-por-tipo-falla")]
    [ProducesResponseType(typeof(IReadOnlyList<DetencionesPorTipoFallaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetencionesPorTipoFalla([FromQuery] DashboardFiltroRequest filtro)
    {
        IEnumerable<int>? lineasPermitidas = null;

        var result = await _dashboardService.ObtenerDetencionesPorTipoFallaAsync(
            filtro,
            lineasPermitidas);

        return HandleResult(result);
    }

    /// <summary>
    /// Obtener top detalles de falla con mas detenciones.
    /// </summary>
    [HttpGet("top-detalles-falla")]
    [ProducesResponseType(typeof(IReadOnlyList<DetencionesPorDetalleFallaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopDetallesFalla(
        [FromQuery] DashboardFiltroRequest filtro,
        [FromQuery] int top = 10)
    {
        IEnumerable<int>? lineasPermitidas = null;

        var result = await _dashboardService.ObtenerTopDetallesFallaAsync(
            filtro,
            lineasPermitidas,
            top);

        return HandleResult(result);
    }

    /// <summary>
    /// Obtener tendencia diaria de detenciones.
    /// </summary>
    [HttpGet("tendencia")]
    [ProducesResponseType(typeof(IReadOnlyList<TendenciaDetencionesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTendencia([FromQuery] DashboardFiltroRequest filtro)
    {
        IEnumerable<int>? lineasPermitidas = null;

        var result = await _dashboardService.ObtenerTendenciaDiariaAsync(
            filtro,
            lineasPermitidas);

        return HandleResult(result);
    }

    // ============ REPORTES DE INDISPONIBILIDAD, FALLOS MAYORES Y PARETO ============

    /// <summary>
    /// Obtener reporte de indisponibilidad mensual por tipo de falla.
    /// </summary>
    [HttpGet("indisponibilidad")]
    [ProducesResponseType(typeof(IndisponibilidadReporteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIndisponibilidad([FromQuery] ReporteFiltroRequest filtro)
    {
        var result = await _dashboardService.ObtenerIndisponibilidadAsync(filtro);
        return HandleResult(result);
    }

    /// <summary>
    /// Obtener reporte de fallos mayores (detenciones >= umbral minutos).
    /// </summary>
    [HttpGet("fallos-mayores")]
    [ProducesResponseType(typeof(FallosMayoresReporteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFallosMayores(
        [FromQuery] ReporteFiltroRequest filtro,
        [FromQuery] int umbralMinutos = 60)
    {
        var result = await _dashboardService.ObtenerFallosMayoresAsync(filtro, umbralMinutos);
        return HandleResult(result);
    }

    /// <summary>
    /// Obtener reporte Pareto de detenciones por tipo de falla y equipo.
    /// </summary>
    [HttpGet("pareto")]
    [ProducesResponseType(typeof(ParetoDetencionesReporteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPareto([FromQuery] DashboardFiltroRequest filtro)
    {
        var result = await _dashboardService.ObtenerParetoDetencionesAsync(filtro);
        return HandleResult(result);
    }
}
