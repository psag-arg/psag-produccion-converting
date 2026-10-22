using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class UsuarioLineaRepository : RepositoryBase<UsuarioLinea>, IUsuarioLineaRepository
{
    public UsuarioLineaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<UsuarioLinea?> GetByUsuarioIdAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .Include(ul => ul.Turno)
            .FirstOrDefaultAsync(ul => ul.UsuarioId == usuarioId, cancellationToken);
    }

    public async Task<UsuarioLinea?> GetByUsuarioYLineaAsync(string usuarioId, int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .Include(ul => ul.Turno)
            .FirstOrDefaultAsync(ul => ul.UsuarioId == usuarioId && ul.LineaId == lineaId, cancellationToken);
    }

    public async Task<IEnumerable<UsuarioLinea>> GetAllWithRelationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Linea)
            .Include(ul => ul.Turno)
            .OrderBy(ul => ul.Linea.Nombre)
            .ThenBy(ul => ul.UsuarioId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UsuarioLinea>> GetByLineaIdAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(ul => ul.Turno)
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
