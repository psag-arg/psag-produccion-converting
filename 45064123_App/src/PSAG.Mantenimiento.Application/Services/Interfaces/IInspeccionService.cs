using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IInspeccionService
{
    /// <summary>
    /// Obtiene planes de inspección para una línea y puesto específico.
    /// </summary>
    Task<Result<IReadOnlyList<PlanInspeccionDto>>> ObtenerPlanesAsync(
        int lineaId,
        string puesto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene planes pendientes de ejecución para hoy.
    /// R15: Según frecuencia y última ejecución.
    /// </summary>
    Task<Result<IReadOnlyList<PlanInspeccionConItemsDto>>> ObtenerPendientesHoyAsync(
        int lineaId,
        string puesto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un plan con todos sus ítems.
    /// </summary>
    Task<Result<PlanInspeccionConItemsDto>> ObtenerPlanConItemsAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia una nueva ejecución de inspección.
    /// </summary>
    Task<Result<IniciarEjecucionResponse>> IniciarEjecucionAsync(
        IniciarEjecucionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una ejecución completa con sus detalles.
    /// </summary>
    Task<Result<EjecucionInspeccionCompletoDto>> ObtenerEjecucionAsync(
        long ejecucionInspeccionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda resultados de inspección.
    /// R16: Si resultado es NO_OK, la observación es obligatoria.
    /// </summary>
    Task<Result> GuardarResultadosAsync(
        GuardarResultadosInspeccionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene ejecuciones paginadas con filtros.
    /// </summary>
    Task<Result<PagedResult<EjecucionInspeccionDto>>> ObtenerEjecucionesPaginadoAsync(
        EjecucionInspeccionFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene hallazgos NO OK pendientes.
    /// </summary>
    Task<Result<IReadOnlyList<HallazgoNoOkDto>>> ObtenerHallazgosPendientesAsync(
        int? lineaId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene hallazgos paginados con filtros.
    /// </summary>
    Task<Result<PagedResult<HallazgoNoOkDto>>> ObtenerHallazgosPaginadoAsync(
        HallazgoNoOkFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resuelve un hallazgo NO OK.
    /// R17: No se borra, se marca como resuelto.
    /// </summary>
    Task<Result> ResolverHallazgoAsync(
        ResolverHallazgoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);
}
