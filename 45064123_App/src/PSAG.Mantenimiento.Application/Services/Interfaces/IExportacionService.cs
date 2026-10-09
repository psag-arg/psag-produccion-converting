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

    /// <summary>
    /// Genera un libro .xlsx con las detenciones recibidas, una fila por detención.
    /// </summary>
    byte[] GenerarDetencionesXlsx(IEnumerable<Domain.Entities.Detencion> detenciones);
}
