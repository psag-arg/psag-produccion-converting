using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Agrupador de detenciones (reunión 17/11/2026): microcortes de cámara previos a una detención que el operario
/// declara con el mismo motivo. Tabla anexa mnt.DetencionAgrupacion; no modifica la detención ni las paradas.
/// La restricción por línea asignada la valida la API antes de llamar.
/// </summary>
public interface IAgrupacionDetencionService
{
    /// <summary>Microcortes sin agrupar en los 10 min previos a la hora de parada de la detención.</summary>
    Task<Result<MicrocortesPreviosDto>> BuscarMicrocortesAsync(long detencionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo mismo para una detención que todavía no existe (el operario la está cargando): línea, cámara opcional e
    /// inicio de la parada.
    /// </summary>
    Task<Result<MicrocortesPreviosDto>> BuscarMicrocortesAsync(
        int lineaId, int? puntoControlCalidadId, DateTimeOffset inicio, CancellationToken cancellationToken = default);

    /// <summary>Microcortes agrupados activos y el tiempo agrupado.</summary>
    Task<Result<AgrupacionDetencionDto>> ObtenerAsync(long detencionId, CancellationToken cancellationToken = default);

    /// <summary>Agrupa (origen OPERADOR). Todo o nada: si una parada no se puede agrupar, no se guarda ninguna.</summary>
    Task<Result<AgrupacionDetencionDto>> AgruparAsync(
        long detencionId, IReadOnlyCollection<long> paradaCamaraIds, string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Desagrupa (Activo = 0 con quién y cuándo; no borra). Todo o nada.</summary>
    Task<Result<AgrupacionDetencionDto>> DesagruparAsync(
        long detencionId, IReadOnlyCollection<long> paradaCamaraIds, string usuarioId, CancellationToken cancellationToken = default);
}
