using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class ConteoCamaraMinutoRepository : RepositoryBase<ConteoCamaraMinuto>, IConteoCamaraMinutoRepository
{
    public ConteoCamaraMinutoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<HashSet<(DateTimeOffset Minuto, byte Carril)>> GetClavesExistentesAsync(
        int puntoControlCalidadId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken = default)
    {
        var claves = await _dbSet.AsNoTracking()
            .Where(m => m.PuntoControlCalidadId == puntoControlCalidadId && m.Minuto >= desde && m.Minuto <= hasta)
            .Select(m => new { m.Minuto, m.Carril })
            .ToListAsync(cancellationToken);
        return claves.Select(c => (c.Minuto.ToUniversalTime(), c.Carril)).ToHashSet();
    }

    public async Task<IReadOnlyList<ConteoCamaraMinuto>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(m => m.Minuto >= desde && m.Minuto < hasta);
        if (lineaId.HasValue)
            query = query.Where(m => m.LineaId == lineaId.Value);
        return await query.OrderBy(m => m.Minuto).ThenBy(m => m.Carril).ToListAsync(cancellationToken);
    }
}

public class ParadaCamaraRepository : RepositoryBase<ParadaCamara>, IParadaCamaraRepository
{
    public ParadaCamaraRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<HashSet<string>> GetIdentificadoresExistentesAsync(IReadOnlyCollection<string> identificadores, CancellationToken cancellationToken = default)
    {
        if (identificadores.Count == 0)
            return [];
        var existentes = await _dbSet.AsNoTracking()
            .Where(p => identificadores.Contains(p.IdentificadorExterno))
            .Select(p => p.IdentificadorExterno)
            .ToListAsync(cancellationToken);
        return existentes.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<ParadaCamara>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Include(p => p.Linea)
            .Include(p => p.PuntoControl)
            .Include(p => p.Turno)
            .Where(p => p.FechaHoraInicio >= desde && p.FechaHoraInicio < hasta);
        if (lineaId.HasValue)
            query = query.Where(p => p.LineaId == lineaId.Value);
        var lista = await query.ToListAsync(cancellationToken);
        // Orden en memoria: SQLite (tests) no ordena DateTimeOffset en la base.
        return lista.OrderByDescending(p => p.FechaHoraInicio).ThenByDescending(p => p.ParadaCamaraId).ToList();
    }
}
