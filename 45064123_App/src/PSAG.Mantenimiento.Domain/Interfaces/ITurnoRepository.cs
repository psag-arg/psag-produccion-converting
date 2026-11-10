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
    /// Turno actual según la hora de la planta (<paramref name="zonaPlanta"/>), no la del servidor.
    /// </summary>
    Task<Turno?> GetTurnoActualAsync(TimeZoneInfo zonaPlanta, CancellationToken cancellationToken = default);
}
