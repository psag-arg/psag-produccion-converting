using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class EquipoRepository : RepositoryBase<Equipo>, IEquipoRepository
{
    public EquipoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Equipo>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(e => e.Linea)
            .Where(e => e.Activo)
            .OrderBy(e => e.Linea.Orden)
            .ThenBy(e => e.Orden)
            .ThenBy(e => e.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Equipo>> GetByLineaAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.LineaId == lineaId && e.Activo)
            .OrderBy(e => e.Orden)
            .ThenBy(e => e.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> PerteneceALineaAsync(int equipoId, int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(e => e.EquipoId == equipoId && e.LineaId == lineaId, cancellationToken);
    }
}
