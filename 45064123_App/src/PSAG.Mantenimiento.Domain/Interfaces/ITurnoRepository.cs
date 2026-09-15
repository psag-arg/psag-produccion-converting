using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ITurnoRepository : IRepository<Turno>
{
    Task<IEnumerable<Turno>> GetActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el turno correspondiente a una hora dada.
    /// Contempla turnos que cruzan medianoche.
    /// </summary>
    Task<Turno?> GetTurnoPorHoraAsync(TimeOnly hora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el turno actual basado en la hora del sistema.
    /// </summary>
    Task<Turno?> GetTurnoActualAsync(CancellationToken cancellationToken = default);
}
