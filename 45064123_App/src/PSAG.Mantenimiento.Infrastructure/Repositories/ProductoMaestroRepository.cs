using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class ProductoMaestroRepository : RepositoryBase<ProductoMaestro>, IProductoMaestroRepository
{
    public ProductoMaestroRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ProductoMaestro>> GetByLineaIdAsync(int lineaId, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        // Pocas filas por línea: la regla de vigencia se aplica en memoria con la función de dominio.
        var parametros = await _context.ParametrosLineaProducto
            .AsNoTracking()
            .Where(p => p.LineaId == lineaId && p.VigenteDesde <= fecha)
            .ToListAsync(cancellationToken);
        var habilitados = ParametroLineaProductoVigencia.ProductosHabilitados(parametros, lineaId, fecha);

        return await _context.ProductosMaestro
            .Where(p => p.Activo && habilitados.Contains(p.ProductoMaestroId))
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProductoMaestro>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProductosMaestro
            .Where(p => p.Activo)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }
}
