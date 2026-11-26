using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class OperadorLineaRepository : RepositoryBase<OperadorLinea>, IOperadorLineaRepository
{
    public OperadorLineaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<OperadorLinea?> GetByUsuarioIdAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .FirstOrDefaultAsync(ul => ul.UsuarioId == usuarioId, cancellationToken);
    }

    public async Task<OperadorLinea?> GetByUsuarioYLineaAsync(string usuarioId, int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .FirstOrDefaultAsync(ul => ul.UsuarioId == usuarioId && ul.LineaId == lineaId, cancellationToken);
    }

    public async Task<IEnumerable<OperadorLinea>> GetAllWithRelationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .OrderBy(ul => ul.Linea.Nombre)
            .ThenBy(ul => ul.UsuarioId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<OperadorLinea>> GetByLineaIdAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .Where(ul => ul.LineaId == lineaId)
            .OrderBy(ul => ul.UsuarioId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int[]> GetLineaIdsAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(ul => ul.UsuarioId == usuarioId)
            .Select(ul => ul.LineaId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
