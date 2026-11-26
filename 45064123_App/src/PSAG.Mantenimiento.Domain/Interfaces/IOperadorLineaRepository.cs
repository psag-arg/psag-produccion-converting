using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IOperadorLineaRepository : IRepository<OperadorLinea>
{
    Task<OperadorLinea?> GetByUsuarioIdAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<OperadorLinea?> GetByUsuarioYLineaAsync(string usuarioId, int lineaId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OperadorLinea>> GetAllWithRelationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OperadorLinea>> GetByLineaIdAsync(int lineaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids de las líneas asignadas al usuario, sin repetir.
    /// </summary>
    Task<int[]> GetLineaIdsAsync(string usuarioId, CancellationToken cancellationToken = default);
}
