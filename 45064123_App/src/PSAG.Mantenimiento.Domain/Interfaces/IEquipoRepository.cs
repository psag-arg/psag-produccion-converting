using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IEquipoRepository : IRepository<Equipo>
{
    Task<IEnumerable<Equipo>> GetActivosAsync(CancellationToken cancellationToken = default);
    /// <summary>Activos e inactivos, con su línea. Para el ABM.</summary>
    Task<IEnumerable<Equipo>> GetTodosAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Equipo>> GetByLineaAsync(int lineaId, CancellationToken cancellationToken = default);
    Task<bool> PerteneceALineaAsync(int equipoId, int lineaId, CancellationToken cancellationToken = default);
}
