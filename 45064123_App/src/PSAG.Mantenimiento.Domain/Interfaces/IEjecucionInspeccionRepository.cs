using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IEjecucionInspeccionRepository
{
    Task<EjecucionInspeccion?> GetByIdAsync(long ejecucionInspeccionId, CancellationToken cancellationToken = default);

    Task<EjecucionInspeccion?> GetByIdWithDetallesAsync(long ejecucionInspeccionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la última ejecución de un plan.
    /// R15: Para calcular si el plan vence hoy.
    /// </summary>
    Task<EjecucionInspeccion?> GetUltimaEjecucionAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene ejecuciones históricas de un plan.
    /// </summary>
    Task<IReadOnlyList<EjecucionInspeccion>> GetByPlanAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene ejecuciones por línea en un rango de fechas.
    /// </summary>
    Task<IReadOnlyList<EjecucionInspeccion>> GetByLineaYFechasAsync(
        int lineaId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene ejecuciones paginadas con filtros.
    /// </summary>
    Task<(IReadOnlyList<EjecucionInspeccion> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? planInspeccionId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        bool? soloConNoOk = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(EjecucionInspeccion ejecucion, CancellationToken cancellationToken = default);
}
