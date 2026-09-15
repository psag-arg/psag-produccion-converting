using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class LineaRepository : RepositoryBase<Linea>, ILineaRepository
{
    public LineaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Linea>> GetActivasAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(l => l.Activo)
            .OrderBy(l => l.Orden)
            .ThenBy(l => l.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Linea>> GetLineasPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.UsuariosLineas
            .Where(ul => ul.UsuarioId == usuarioId)
            .Select(ul => ul.Linea)
            .Where(l => l.Activo)
            .OrderBy(l => l.Orden)
            .ThenBy(l => l.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Linea?> GetByIdWithEquiposAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(l => l.Equipos.Where(e => e.Activo).OrderBy(e => e.Orden))
            .FirstOrDefaultAsync(l => l.LineaId == lineaId, cancellationToken);
    }
}
