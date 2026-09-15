using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IUsuarioLineaRepository : IRepository<UsuarioLinea>
{
    Task<UsuarioLinea?> GetByUsuarioIdAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<UsuarioLinea?> GetByUsuarioYLineaAsync(string usuarioId, int lineaId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UsuarioLinea>> GetAllWithRelationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<UsuarioLinea>> GetByLineaIdAsync(int lineaId, CancellationToken cancellationToken = default);
}
