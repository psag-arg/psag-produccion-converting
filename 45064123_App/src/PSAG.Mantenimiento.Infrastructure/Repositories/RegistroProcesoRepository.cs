using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class RegistroProcesoRepository : RepositoryBase<RegistroProceso>, IRegistroProcesoRepository
{
    public RegistroProcesoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<RegistroProceso?> GetByIdWithDetailsAsync(long registroProcesoId, CancellationToken cancellationToken = default)
    {
        return await _context.RegistrosProceso
            .Include(r => r.Linea)
            .Include(r => r.Producto)
            .Include(r => r.Turno)
            .Include(r => r.Detalles)
                .ThenInclude(d => d.Parametro)
                    .ThenInclude(p => p.GrupoParametro)
            .FirstOrDefaultAsync(r => r.RegistroProcesoId == registroProcesoId, cancellationToken);
    }

    public async Task<RegistroProceso?> GetByLineaProductoTurnoFechaAsync(
        int lineaId,
        int? productoId,
        int turnoId,
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        return await _context.RegistrosProceso
            .Include(r => r.Detalles)
            .FirstOrDefaultAsync(r =>
                r.LineaId == lineaId &&
                r.ProductoId == productoId &&
                r.TurnoId == turnoId &&
                r.Fecha == fecha,
                cancellationToken);
    }

    public async Task<(IEnumerable<RegistroProceso> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? productoId = null,
        int? turnoId = null,
        EstadoRegistroProceso? estado = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        string? ordenarPor = null,
        bool ordenDescendente = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RegistrosProceso
            .Include(r => r.Linea)
            .Include(r => r.Producto)
            .Include(r => r.Turno)
            .AsQueryable();

        if (lineaId.HasValue)
            query = query.Where(r => r.LineaId == lineaId.Value);

        if (productoId.HasValue)
            query = query.Where(r => r.ProductoId == productoId.Value);

        if (turnoId.HasValue)
            query = query.Where(r => r.TurnoId == turnoId.Value);

        if (estado.HasValue)
            query = query.Where(r => r.Estado == estado.Value);

        if (fechaDesde.HasValue)
            query = query.Where(r => r.Fecha >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(r => r.Fecha <= fechaHasta.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = ordenDescendente
            ? query.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.FechaHoraCreacion)
            : query.OrderBy(r => r.Fecha).ThenBy(r => r.FechaHoraCreacion);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IEnumerable<RegistroProceso>> GetHistoricoAsync(
        int lineaId,
        int productoId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken cancellationToken = default)
    {
        return await _context.RegistrosProceso
            .Include(r => r.Linea)
            .Include(r => r.Producto)
            .Include(r => r.Turno)
            .Include(r => r.Detalles)
                .ThenInclude(d => d.Parametro)
                    .ThenInclude(p => p.GrupoParametro)
            .Where(r =>
                r.LineaId == lineaId &&
                r.ProductoId == productoId &&
                r.Fecha >= fechaDesde &&
                r.Fecha <= fechaHasta)
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.TurnoId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<(int LineaId, int? ProductoId, int ParametroId, int CantidadDesvios)>> GetConteoDesviosAsync(
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RegistrosProcesoDetalle
            .Include(d => d.RegistroProceso)
            .Where(d => d.FueraDeEstandar);

        if (fechaDesde.HasValue)
            query = query.Where(d => d.RegistroProceso.Fecha >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(d => d.RegistroProceso.Fecha <= fechaHasta.Value);

        var result = await query
            .GroupBy(d => new { d.RegistroProceso.LineaId, d.RegistroProceso.ProductoId, d.ParametroId })
            .Select(g => new
            {
                g.Key.LineaId,
                g.Key.ProductoId,
                g.Key.ParametroId,
                CantidadDesvios = g.Count()
            })
            .ToListAsync(cancellationToken);

        return result.Select(r => (r.LineaId, r.ProductoId, r.ParametroId, r.CantidadDesvios));
    }
}
