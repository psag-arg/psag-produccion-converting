using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

/// <summary>Agrupador de detenciones (script 32): microcortes de cámara agrupados bajo una detención.</summary>
public interface IDetencionAgrupacionRepository : IRepository<DetencionAgrupacion>
{
    /// <summary>Agrupaciones activas de esas paradas (en cualquier detención), con seguimiento.</summary>
    Task<IReadOnlyList<DetencionAgrupacion>> GetActivasPorParadasAsync(IReadOnlyCollection<long> paradaCamaraIds, CancellationToken cancellationToken = default);

    /// <summary>Agrupaciones activas de la detención con su parada, con seguimiento (para desagrupar), por hora de inicio.</summary>
    Task<IReadOnlyList<DetencionAgrupacion>> GetActivasPorDetencionAsync(long detencionId, CancellationToken cancellationToken = default);
}
