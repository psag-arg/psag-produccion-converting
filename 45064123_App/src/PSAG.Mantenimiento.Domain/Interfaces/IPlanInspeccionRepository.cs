using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IPlanInspeccionRepository
{
    Task<PlanInspeccion?> GetByIdAsync(int planInspeccionId, CancellationToken cancellationToken = default);

    Task<PlanInspeccion?> GetByIdWithItemsAsync(int planInspeccionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene planes activos para una línea y puesto específico.
    /// R14: Filtrado por línea asignada y puesto del usuario.
    /// </summary>
    Task<IReadOnlyList<PlanInspeccion>> GetByLineaYPuestoAsync(
        int lineaId,
        TipoPuesto puesto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene planes activos para una línea (todos los puestos).
    /// </summary>
    Task<IReadOnlyList<PlanInspeccion>> GetByLineaAsync(
        int lineaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene planes que están pendientes de ejecución hoy según su frecuencia.
    /// R15: Sistema muestra ítems vencidos según frecuencia y última ejecución.
    /// </summary>
    Task<IReadOnlyList<PlanInspeccion>> GetPendientesHoyAsync(
        int lineaId,
        TipoPuesto puesto,
        CancellationToken cancellationToken = default);

    Task AddAsync(PlanInspeccion planInspeccion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanInspeccion>> GetAllActivosAsync(CancellationToken cancellationToken = default);
}
