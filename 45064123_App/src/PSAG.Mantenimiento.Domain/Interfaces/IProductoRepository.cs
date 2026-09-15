using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IProductoRepository : IRepository<Producto>
{
    Task<IEnumerable<Producto>> GetByLineaIdAsync(int lineaId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Producto>> GetActivosAsync(CancellationToken cancellationToken = default);
}
