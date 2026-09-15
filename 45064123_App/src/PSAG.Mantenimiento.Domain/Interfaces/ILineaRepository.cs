using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ILineaRepository : IRepository<Linea>
{
    Task<IEnumerable<Linea>> GetActivasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Linea>> GetLineasPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<Linea?> GetByIdWithEquiposAsync(int lineaId, CancellationToken cancellationToken = default);
}
