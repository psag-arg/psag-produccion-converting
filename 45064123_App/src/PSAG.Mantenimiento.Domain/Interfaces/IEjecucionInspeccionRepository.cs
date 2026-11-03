using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

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
        CancellationToken cancellationToken = default,
        int? equipoId = null,
        TipoPuesto? puesto = null,
        int? frecuenciaDias = null,
        bool? completada = null);

    /// <summary>
    /// Inspección de un plan para una fecha y un "quién realiza" (null = todos). Si existe, se retoma.
    /// </summary>
    Task<EjecucionInspeccion?> GetByPlanFechaPuestoAsync(int planInspeccionId, DateOnly fecha, TipoPuesto? puesto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Última fecha en que se respondió cada ítem del plan, en inspecciones anteriores a <paramref name="antesDe"/>.
    /// Es lo que define si un punto con frecuencia (cada 10 días) vence hoy.
    /// </summary>
    Task<Dictionary<int, DateOnly>> GetUltimasRespuestasPorItemAsync(int planInspeccionId, DateOnly antesDe, CancellationToken cancellationToken = default);

    Task AddAsync(EjecucionInspeccion ejecucion, CancellationToken cancellationToken = default);
}
