using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class ParametroRepository : RepositoryBase<Parametro>, IParametroRepository
{
    public ParametroRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Parametro>> GetByGrupoIdAsync(int grupoParametroId, CancellationToken cancellationToken = default)
    {
        return await _context.Parametros
            .Where(p => p.Activo && p.GrupoParametroId == grupoParametroId)
            .OrderBy(p => p.Seccion)
            .ThenBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Parametro>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Parametros
            .Where(p => p.Activo)
            .Include(p => p.GrupoParametro)
            .OrderBy(p => p.GrupoParametro.Orden)
            .ThenBy(p => p.Seccion)
            .ThenBy(p => p.Orden)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Parametro>> GetPlantillaLineaAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _context.Parametros
            .Include(p => p.GrupoParametro)
            .Where(p => p.Activo && p.GrupoParametro.Activo && p.GrupoParametro.LineaId == lineaId)
            .OrderBy(p => p.GrupoParametro.Orden)
            .ThenBy(p => p.Seccion)
            .ThenBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByLineaAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _context.Parametros.CountAsync(p => p.GrupoParametro.LineaId == lineaId, cancellationToken);
    }

    public async Task<bool> TieneCargasAsync(int parametroId, CancellationToken cancellationToken = default)
    {
        return await _context.RegistrosProcesoDetalle.AnyAsync(d => d.ParametroId == parametroId, cancellationToken);
    }
}
