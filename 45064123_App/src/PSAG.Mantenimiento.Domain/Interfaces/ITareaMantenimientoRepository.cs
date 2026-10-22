using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ITareaMantenimientoRepository : IRepository<TareaMantenimiento>
{
    /// <summary>
    /// Con línea y equipo, con seguimiento (para programar o cerrar).
    /// </summary>
    Task<TareaMantenimiento?> GetByIdAsync(long tareaMantenimientoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TareaMantenimiento>> GetByIdsAsync(
        IReadOnlyCollection<long> tareaMantenimientoIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tareas no cerradas (pendientes y programadas), de más a menos crítica y, a igual criticidad, las más viejas primero. Sólo lectura (sin seguimiento).
    /// </summary>
    Task<IReadOnlyList<TareaMantenimiento>> GetActivasAsync(
        int? lineaId = null,
        CriticidadTarea? criticidad = null,
        EstadoTareaMantenimiento? estado = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Historial de tareas cerradas, de la más reciente a la más vieja. Las fechas filtran por fecha de cierre. Sólo lectura (sin seguimiento).
    /// </summary>
    Task<(IReadOnlyList<TareaMantenimiento> Items, int TotalCount)> GetCerradasPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        CriticidadTarea? criticidad = null,
        TipoTareaMantenimiento? tipoTarea = null,
        int? equipoId = null,
        DateTimeOffset? cierreDesde = null,
        DateTimeOffset? cierreHasta = null,
        CancellationToken cancellationToken = default);
}
