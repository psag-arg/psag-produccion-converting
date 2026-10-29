using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class PuntoControlCalidadRepository : RepositoryBase<PuntoControlCalidad>, IPuntoControlCalidadRepository
{
    public PuntoControlCalidadRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<PuntoControlCalidad>> GetListadoAsync(
        int? lineaId = null,
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Include(p => p.Linea).AsQueryable();
        if (lineaId.HasValue)
            query = query.Where(p => p.LineaId == lineaId.Value);
        if (!incluirInactivos)
            query = query.Where(p => p.Activo);

        return await query
            .OrderBy(p => p.Linea.Orden)
            .ThenBy(p => p.Linea.Nombre)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<PuntoControlCalidad?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Include(p => p.Linea).FirstOrDefaultAsync(p => p.Codigo == codigo, cancellationToken);
    }
}

public class TipoDefectoCalidadRepository : RepositoryBase<TipoDefectoCalidad>, ITipoDefectoCalidadRepository
{
    public TipoDefectoCalidadRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<TipoDefectoCalidad>> GetListadoAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();
        if (!incluirInactivos)
            query = query.Where(t => t.Activo);

        return await query.OrderBy(t => t.Nombre).ToListAsync(cancellationToken);
    }

    public async Task<TipoDefectoCalidad?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(t => t.Codigo == codigo, cancellationToken);
    }
}

public class RegistroCalidadRepository : RepositoryBase<RegistroCalidad>, IRegistroCalidadRepository
{
    public RegistroCalidadRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<RegistroCalidad?> GetByIdConDetalleAsync(long registroCalidadId, CancellationToken cancellationToken = default)
    {
        return await ConRelaciones()
            .Include(r => r.Revisiones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.RegistroCalidadId == registroCalidadId, cancellationToken);
    }

    public async Task<RegistroCalidad?> GetByIdentificadorExternoAsync(string identificadorExterno, CancellationToken cancellationToken = default)
    {
        return await ConRelaciones()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdentificadorExterno == identificadorExterno, cancellationToken);
    }

    public async Task<(IReadOnlyList<RegistroCalidad> Items, int TotalCount)> GetPaginadoAsync(
        FiltroRegistrosCalidad filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();

        if (filtro.Desde.HasValue)
            query = query.Where(r => r.FechaHoraDeteccion >= filtro.Desde.Value);
        if (filtro.Hasta.HasValue)
            query = query.Where(r => r.FechaHoraDeteccion < filtro.Hasta.Value);
        if (filtro.TurnoId.HasValue)
            query = query.Where(r => r.TurnoId == filtro.TurnoId.Value);
        if (filtro.LineaId.HasValue)
            query = query.Where(r => r.LineaId == filtro.LineaId.Value);
        if (filtro.PuntoControlCalidadId.HasValue)
            query = query.Where(r => r.PuntoControlCalidadId == filtro.PuntoControlCalidadId.Value);
        if (filtro.TipoDefectoCalidadId.HasValue)
            query = query.Where(r => r.TipoDefectoCalidadId == filtro.TipoDefectoCalidadId.Value);
        if (filtro.EstadoRevision.HasValue)
            query = query.Where(r => r.EstadoRevision == filtro.EstadoRevision.Value);
        if (filtro.Origen.HasValue)
            query = query.Where(r => r.Origen == filtro.Origen.Value);

        var total = await query.CountAsync(cancellationToken);

        // Primero los ids de la página (orden y paginado sobre la tabla sola) y después las relaciones.
        var ids = await query
            .OrderByDescending(r => r.FechaHoraDeteccion)
            .ThenByDescending(r => r.RegistroCalidadId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.RegistroCalidadId)
            .ToListAsync(cancellationToken);

        var items = await ConRelaciones()
            .AsNoTracking()
            .Where(r => ids.Contains(r.RegistroCalidadId))
            .ToListAsync(cancellationToken);

        var orden = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return (items.OrderBy(r => orden[r.RegistroCalidadId]).ToList(), total);
    }

    private IQueryable<RegistroCalidad> ConRelaciones() =>
        _dbSet
            .Include(r => r.Linea)
            .Include(r => r.PuntoControl)
            .Include(r => r.Turno)
            .Include(r => r.TipoDefecto)
            .Include(r => r.Producto)
            .Include(r => r.Detencion);
}
