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

    /// <summary>Rollos (o mangas) por hora, carril y turno de un día de planta, de una línea o de una cámara.</summary>
    Task<Result<ProduccionCamaraDto>> ObtenerProduccionAsync(DateOnly fecha, int? lineaId, int? puntoControlCalidadId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Eficiencia por turno de cada cámara que cuenta producción (de una línea o de todas) en un día de planta:
    /// contadas, objetivo (ObjetivoPorHora x horas programadas del turno), %, minutos de parada y microcortes.
    /// </summary>
    Task<Result<EficienciaCamaraDiaDto>> ObtenerEficienciaAsync(DateOnly fecha, int? lineaId, DateTimeOffset? ahora = null, CancellationToken cancellationToken = default);

    /// <summary>Alertas de cámara abiertas y las cerradas en las últimas horas.</summary>
    Task<IReadOnlyList<AlertaCamaraDto>> ObtenerAlertasAsync(int? lineaId, int horas = 24, CancellationToken cancellationToken = default);
}
