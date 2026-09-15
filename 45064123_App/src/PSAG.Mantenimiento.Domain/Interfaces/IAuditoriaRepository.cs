using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IAuditoriaRepository
{
    Task RegistrarAsync(Auditoria auditoria, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Auditoria> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        string? usuarioId = null,
        TipoAccionAuditoria? accion = null,
        string? entidad = null,
        DateTimeOffset? fechaDesde = null,
        DateTimeOffset? fechaHasta = null,
        CancellationToken cancellationToken = default);
}
