using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ICalendarioLineaRepository : IRepository<CalendarioLinea>
{
    /// <summary>
    /// Versiones activas del calendario con sus turnos, de una línea o de todas si lineaId es null.
    /// Sólo lectura (sin seguimiento): para modificar una versión, usar GetByIdAsync o FindAsync.
    /// </summary>
    Task<IReadOnlyList<CalendarioLinea>> GetActivosAsync(int? lineaId = null, CancellationToken cancellationToken = default);
}
