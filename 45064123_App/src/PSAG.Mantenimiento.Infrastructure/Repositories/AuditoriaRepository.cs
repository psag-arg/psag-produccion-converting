using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly MantenimientoDbContext _context;

    public AuditoriaRepository(MantenimientoDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarAsync(Auditoria auditoria, CancellationToken cancellationToken = default)
    {
        await _context.Auditoria.AddAsync(auditoria, cancellationToken);
    }

    public async Task<(IEnumerable<Auditoria> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        string? usuarioId = null,
        TipoAccionAuditoria? accion = null,
        string? entidad = null,
        DateTimeOffset? fechaDesde = null,
        DateTimeOffset? fechaHasta = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Auditoria.AsQueryable();

        if (!string.IsNullOrEmpty(usuarioId))
            query = query.Where(a => a.UsuarioId == usuarioId);

        if (accion.HasValue)
            query = query.Where(a => a.Accion == accion.Value);

        if (!string.IsNullOrEmpty(entidad))
            query = query.Where(a => a.Entidad == entidad);

        if (fechaDesde.HasValue)
            query = query.Where(a => a.FechaHora >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(a => a.FechaHora <= fechaHasta.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.FechaHora)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
