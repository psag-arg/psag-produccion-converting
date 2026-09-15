using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IHallazgoNoOkRepository
{
    Task<HallazgoNoOk?> GetByIdAsync(long hallazgoNoOkId, CancellationToken cancellationToken = default);

    Task<HallazgoNoOk?> GetByIdWithDetallesAsync(long hallazgoNoOkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene hallazgos pendientes (no resueltos) para una línea.
    /// Panel de supervisor/mantenimiento.
    /// </summary>
    Task<IReadOnlyList<HallazgoNoOk>> GetPendientesByLineaAsync(
        int lineaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todos los hallazgos pendientes (sin filtro de línea).
    /// Para supervisores con acceso a múltiples líneas.
    /// </summary>
    Task<IReadOnlyList<HallazgoNoOk>> GetAllPendientesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene hallazgos paginados con filtros.
    /// </summary>
    Task<(IReadOnlyList<HallazgoNoOk> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? equipoId = null,
        bool? soloPendientes = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(HallazgoNoOk hallazgo, CancellationToken cancellationToken = default);
}
