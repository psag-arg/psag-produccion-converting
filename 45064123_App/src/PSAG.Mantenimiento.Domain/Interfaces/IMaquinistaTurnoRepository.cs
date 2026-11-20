using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IMaquinistaTurnoRepository : IRepository<MaquinistaTurno>
{
    /// <summary>Registro vigente (el más reciente) del turno de esa línea ese día, con su asignación, o null. Sólo lectura.</summary>
    Task<MaquinistaTurno?> GetVigenteAsync(int lineaId, int turnoId, DateOnly fechaTurno, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos los registros de los turnos que empezaron ese día (de las líneas indicadas, o de todas), del más viejo
    /// al más nuevo, con su asignación. Sólo lectura.
    /// </summary>
    Task<IReadOnlyList<MaquinistaTurno>> GetDelDiaAsync(DateOnly fechaTurno, IReadOnlyCollection<int>? lineasIds = null, CancellationToken cancellationToken = default);
}
