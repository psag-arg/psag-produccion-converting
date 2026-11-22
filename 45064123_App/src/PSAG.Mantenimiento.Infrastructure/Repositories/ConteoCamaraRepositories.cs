using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class CamaraRepository : RepositoryBase<Camara>, ICamaraRepository
{
    public CamaraRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Camara>> GetListadoAsync(int? lineaId = null, bool incluirInactivas = false, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Include(c => c.Linea).Include(c => c.Equipo).AsQueryable();
        if (lineaId.HasValue)
            query = query.Where(c => c.LineaId == lineaId.Value);
        if (!incluirInactivas)
            query = query.Where(c => c.Activo);
        var lista = await query.ToListAsync(cancellationToken);
        return Ordenar(lista);
    }

    public async Task<Camara?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        await _dbSet.Include(c => c.Linea).FirstOrDefaultAsync(c => c.Codigo == codigo, cancellationToken);

    public async Task<IReadOnlyList<Camara>> GetHabilitadasAsync(CancellationToken cancellationToken = default) =>
        Ordenar(await _dbSet.AsNoTracking().Include(c => c.Linea).Where(c => c.Activo && c.Habilitada).ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<Camara>> GetOficialesAsync(CancellationToken cancellationToken = default) =>
        Ordenar(await _dbSet.AsNoTracking().Include(c => c.Linea).Where(c => c.Activo && c.CuentaProduccionOficial).ToListAsync(cancellationToken));

    public async Task<Camara?> GetOficialAsync(int lineaId, string magnitud, int? excluirCamaraId, CancellationToken cancellationToken = default) =>
        await _dbSet.AsNoTracking().FirstOrDefaultAsync(
            c => c.LineaId == lineaId && c.Magnitud == magnitud && c.CuentaProduccionOficial
                 && (excluirCamaraId == null || c.CamaraId != excluirCamaraId), cancellationToken);

    private static List<Camara> Ordenar(IEnumerable<Camara> camaras) => camaras
        .OrderBy(c => c.Linea?.Orden ?? 0).ThenBy(c => c.Linea?.Nombre)
        .ThenBy(c => c.NumeroCanalGrabador ?? int.MaxValue).ThenBy(c => c.Codigo)
        .ToList();
}

public class ConteoCamaraMinutoRepository : RepositoryBase<ConteoCamaraMinuto>, IConteoCamaraMinutoRepository
{
    public ConteoCamaraMinutoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<HashSet<(DateTimeOffset Minuto, byte Carril)>> GetClavesExistentesAsync(
        int camaraId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken = default)
    {
        var claves = await _dbSet.AsNoTracking()
            .Where(m => m.CamaraId == camaraId && m.Minuto >= desde && m.Minuto <= hasta)
            .Select(m => new { m.Minuto, m.Carril })
            .ToListAsync(cancellationToken);
        return claves.Select(c => (c.Minuto.ToUniversalTime(), c.Carril)).ToHashSet();
    }

    public async Task<IReadOnlyList<ConteoCamaraMinuto>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Include(m => m.Camara)
            .Include(m => m.TurnoProduccion)
            .Where(m => m.Minuto >= desde && m.Minuto < hasta);
        if (lineaId.HasValue)
            query = query.Where(m => m.Camara.LineaId == lineaId.Value);
        return await query.OrderBy(m => m.Minuto).ThenBy(m => m.Carril).ToListAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetUltimoMinutoAsync(int camaraId, CancellationToken cancellationToken = default)
    {
        // Por id (insert-only, en orden de llegada) y no por Minuto: SQLite (tests) no ordena DateTimeOffset en la base.
        // Entre los últimos que llegaron se toma el mayor minuto.
        var ultimos = await _dbSet.AsNoTracking()
            .Where(m => m.CamaraId == camaraId)
            .OrderByDescending(m => m.ConteoCamaraMinutoId)
            .Select(m => m.Minuto)
            .Take(200)
            .ToListAsync(cancellationToken);
        return ultimos.Count == 0 ? null : ultimos.Max();
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
            .Include(p => p.Camara).ThenInclude(c => c.Linea)
            .Include(p => p.TurnoProduccion).ThenInclude(t => t!.Turno)
            .Where(p => p.FechaHoraInicio >= desde && p.FechaHoraInicio < hasta);
        if (lineaId.HasValue)
            query = query.Where(p => p.Camara.LineaId == lineaId.Value);
        var lista = await query.ToListAsync(cancellationToken);
        // Orden en memoria: SQLite (tests) no ordena DateTimeOffset en la base.
        return lista.OrderByDescending(p => p.FechaHoraInicio).ThenByDescending(p => p.ParadaCamaraId).ToList();
    }
}

public class AlertaCamaraRepository : RepositoryBase<AlertaCamara>, IAlertaCamaraRepository
{
    public AlertaCamaraRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AlertaCamara>> GetPorIdentificadoresAsync(IReadOnlyCollection<string> identificadores, CancellationToken cancellationToken = default)
    {
        if (identificadores.Count == 0)
            return [];
        return await _dbSet.Where(a => identificadores.Contains(a.IdentificadorExterno)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertaCamara>> GetRecientesAsync(DateTimeOffset cerradasDesde, int? lineaId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Include(a => a.Camara).ThenInclude(c => c.Linea)
            .Where(a => a.FechaHoraFin == null || a.FechaHoraFin >= cerradasDesde);
        if (lineaId.HasValue)
            query = query.Where(a => a.Camara.LineaId == lineaId.Value);
        var lista = await query.ToListAsync(cancellationToken);
        return lista.OrderByDescending(a => a.FechaHoraInicio).ToList();
    }

    public async Task<IReadOnlyList<AlertaCamara>> GetAbiertasAsync(int camaraId, IReadOnlyCollection<Domain.Enums.TipoAlertaCamara> tipos, CancellationToken cancellationToken = default)
    {
        if (tipos.Count == 0)
            return [];
        return await _dbSet.Where(a => a.CamaraId == camaraId && a.FechaHoraFin == null && tipos.Contains(a.Tipo)).ToListAsync(cancellationToken);
    }
}
