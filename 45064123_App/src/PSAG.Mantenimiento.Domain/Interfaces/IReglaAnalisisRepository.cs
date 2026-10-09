using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IReglaAnalisisRepository : IRepository<ReglaAnalisis>
{
    /// <summary>
    /// Reglas activas, para evaluar con ReglaAnalisisEvaluador.
    /// </summary>
    Task<IEnumerable<ReglaAnalisis>> GetActivasAsync(CancellationToken cancellationToken = default);
}
