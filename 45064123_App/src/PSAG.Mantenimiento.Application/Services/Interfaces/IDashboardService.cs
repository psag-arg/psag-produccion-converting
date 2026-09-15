using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Dashboard;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IDashboardService
{
    Task<Result<DashboardCompletoDto>> ObtenerDashboardCompletoAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    Task<Result<DashboardResumenDto>> ObtenerResumenAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DetencionesPorLineaDto>>> ObtenerDetencionesPorLineaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DetencionesPorTipoFallaDto>>> ObtenerDetencionesPorTipoFallaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DetencionesPorDetalleFallaDto>>> ObtenerTopDetallesFallaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        int top = 10,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TendenciaDetencionesDto>>> ObtenerTendenciaDiariaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    // Reportes de Indisponibilidad, Fallos Mayores y Pareto
    Task<Result<IndisponibilidadReporteDto>> ObtenerIndisponibilidadAsync(
        ReporteFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    Task<Result<FallosMayoresReporteDto>> ObtenerFallosMayoresAsync(
        ReporteFiltroRequest filtro,
        int umbralMinutos = 60,
        CancellationToken cancellationToken = default);

    Task<Result<ParetoDetencionesReporteDto>> ObtenerParetoDetencionesAsync(
        DashboardFiltroRequest filtro,
        CancellationToken cancellationToken = default);
}
