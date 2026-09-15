using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class DetalleFallaRepository : RepositoryBase<DetalleFalla>, IDetalleFallaRepository
{
    public DetalleFallaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DetalleFalla>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.TipoFalla)
            .Include(d => d.Equipo)
            .Where(d => d.Activo)
            .OrderBy(d => d.Orden)
            .ThenBy(d => d.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DetalleFalla>> GetByFiltroAsync(
        int? equipoId,
        int? tipoFallaId,
        CancellationToken cancellationToken = default)
    {
        // Obtiene detalles de falla aplicables:
        // - Genéricos (EquipoId = NULL y TipoFallaId = NULL)
        // - Específicos para el equipo (EquipoId = equipoId y TipoFallaId = NULL)
        // - Específicos para el tipo de falla (EquipoId = NULL y TipoFallaId = tipoFallaId)
        // - Específicos para ambos (EquipoId = equipoId y TipoFallaId = tipoFallaId)

        return await _dbSet
            .Include(d => d.TipoFalla)
            .Include(d => d.Equipo)
            .Where(d => d.Activo)
            .Where(d =>
                // Genéricos
                (d.EquipoId == null && d.TipoFallaId == null) ||
                // Específicos para equipo (si se especificó)
                (equipoId.HasValue && d.EquipoId == equipoId.Value && d.TipoFallaId == null) ||
                // Específicos para tipo de falla (si se especificó)
                (tipoFallaId.HasValue && d.TipoFallaId == tipoFallaId.Value && d.EquipoId == null) ||
                // Específicos para ambos (si se especificaron ambos)
                (equipoId.HasValue && tipoFallaId.HasValue &&
                 d.EquipoId == equipoId.Value && d.TipoFallaId == tipoFallaId.Value)
            )
            .OrderBy(d => d.Orden)
            .ThenBy(d => d.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> EsCoherenteAsync(
        int detalleFallaId,
        int? equipoId,
        int? tipoFallaId,
        CancellationToken cancellationToken = default)
    {
        var detalle = await _dbSet.FindAsync(new object[] { detalleFallaId }, cancellationToken);
        if (detalle == null)
            return false;

        // Validar coherencia:
        // - Si DetalleFalla.EquipoId no es null, debe coincidir con equipoId
        // - Si DetalleFalla.TipoFallaId no es null, debe coincidir con tipoFallaId

        if (detalle.EquipoId.HasValue && detalle.EquipoId != equipoId)
            return false;

        if (detalle.TipoFallaId.HasValue && detalle.TipoFallaId != tipoFallaId)
            return false;

        return true;
    }
}
