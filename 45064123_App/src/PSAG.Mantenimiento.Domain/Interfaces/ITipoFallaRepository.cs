using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ITipoFallaRepository : IRepository<TipoFalla>
{
    Task<IEnumerable<TipoFalla>> GetActivosAsync(CancellationToken cancellationToken = default);
}
