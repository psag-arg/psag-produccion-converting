using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IParametroRepository : IRepository<Parametro>
{
    Task<IEnumerable<Parametro>> GetByGrupoIdAsync(int grupoParametroId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parametro>> GetActivosAsync(CancellationToken cancellationToken = default);
}
