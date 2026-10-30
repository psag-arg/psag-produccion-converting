using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Conteo de producción y paradas detectadas por cámara (T-610).
/// </summary>
public interface IConteoCamaraService
{
    /// <summary>Guarda un lote del servicio de conteo. Idempotente: lo que ya estaba se cuenta como duplicado.</summary>
    Task<Result<IngestaConteoCamaraRespuesta>> IngestarAsync(IngestaConteoCamaraRequest request, CancellationToken cancellationToken = default);

    /// <summary>Paradas de la cámara cruzadas con las detenciones cargadas.</summary>
    Task<Result<ParadasCamaraResultado>> ObtenerParadasAsync(ParadasCamaraFiltroRequest filtro, CancellationToken cancellationToken = default);

    /// <summary>Rollos por hora y carril de un día de planta.</summary>
    Task<Result<ProduccionCamaraDto>> ObtenerProduccionAsync(DateOnly fecha, int? lineaId, CancellationToken cancellationToken = default);
}
