using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class GrupoParametroRepository : RepositoryBase<GrupoParametro>, IGrupoParametroRepository
{
    public GrupoParametroRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<GrupoParametro>> GetByLineaIdAsync(int? lineaId, CancellationToken cancellationToken = default)
    {
        // Devuelve los globales (LineaId = null) y los específicos de la línea
        return await _context.GruposParametro
            .Where(g => g.Activo && (g.LineaId == null || g.LineaId == lineaId))
            .OrderBy(g => g.Orden)
            .ThenBy(g => g.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<GrupoParametro?> GetByIdWithParametrosAsync(int grupoParametroId, CancellationToken cancellationToken = default)
    {
        return await _context.GruposParametro
            .Include(g => g.Parametros.Where(p => p.Activo).OrderBy(p => p.Seccion).ThenBy(p => p.Orden))
            .FirstOrDefaultAsync(g => g.GrupoParametroId == grupoParametroId, cancellationToken);
    }

    public async Task<IEnumerable<GrupoParametro>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.GruposParametro
            .Where(g => g.Activo)
            .OrderBy(g => g.Orden)
            .ThenBy(g => g.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<GrupoParametro>> GetPlantillaLineaConPuntosAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _context.GruposParametro
            .Include(g => g.Parametros)
            .Where(g => g.LineaId == lineaId)
            .OrderBy(g => g.Orden)
            .ThenBy(g => g.Nombre)
            .ToListAsync(cancellationToken);
    }
}
