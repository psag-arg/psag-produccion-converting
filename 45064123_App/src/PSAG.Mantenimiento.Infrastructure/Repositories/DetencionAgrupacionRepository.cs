using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class DetencionAgrupacionRepository : RepositoryBase<DetencionAgrupacion>, IDetencionAgrupacionRepository
{
    public DetencionAgrupacionRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DetencionAgrupacion>> GetActivasPorParadasAsync(
        IReadOnlyCollection<long> paradaCamaraIds, CancellationToken cancellationToken = default)
    {
        if (paradaCamaraIds.Count == 0)
            return [];
        return await _dbSet
            .Where(a => a.Activo && paradaCamaraIds.Contains(a.ParadaCamaraId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DetencionAgrupacion>> GetActivasPorDetencionAsync(long detencionId, CancellationToken cancellationToken = default)
    {
        var lista = await _dbSet
            .Include(a => a.ParadaCamara)
            .Where(a => a.Activo && a.DetencionId == detencionId)
            .ToListAsync(cancellationToken);
        // Orden en memoria: SQLite (tests) no ordena DateTimeOffset en la base.
        return lista.OrderBy(a => a.ParadaCamara.FechaHoraInicio).ThenBy(a => a.ParadaCamaraId).ToList();
    }
}
