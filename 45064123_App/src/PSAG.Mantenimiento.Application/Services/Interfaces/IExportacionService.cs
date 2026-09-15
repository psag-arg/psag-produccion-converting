using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IExportacionService
{
    Task<Result<byte[]>> ExportarDetencionesCsvAsync(
        DetencionFiltroRequest filtro,
        string usuarioId,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);
}
