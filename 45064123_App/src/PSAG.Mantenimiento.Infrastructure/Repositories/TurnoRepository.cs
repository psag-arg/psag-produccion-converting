using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class TurnoRepository : RepositoryBase<Turno>, ITurnoRepository
{
    public TurnoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Turno>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.HoraInicio)
            .ToListAsync(cancellationToken);
    }

    public async Task<Turno?> GetTurnoPorHoraAsync(TimeOnly hora, CancellationToken cancellationToken = default)
    {
        var turnos = await _dbSet
            .Where(t => t.Activo)
            .ToListAsync(cancellationToken);

        // Buscar el turno que contiene la hora dada
        // El método ContieneHora en la entidad maneja turnos que cruzan medianoche
        return turnos.FirstOrDefault(t => t.ContieneHora(hora));
    }

    public async Task<Turno?> GetTurnoActualAsync(CancellationToken cancellationToken = default)
    {
        var horaActual = TimeOnly.FromDateTime(DateTime.Now);
        return await GetTurnoPorHoraAsync(horaActual, cancellationToken);
    }
}
