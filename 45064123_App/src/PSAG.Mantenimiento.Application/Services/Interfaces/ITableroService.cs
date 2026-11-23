using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// T-770 · Tablero del operador (tablet): presupuesto vs. real del turno en proceso, mangas por hora, detenciones reales
/// (paradas que superan el umbral) separadas de los microcortes, detenciones del turno, pendientes de declarar y el
/// último dato de cámara, en una sola llamada. Qué turno puede ver cada usuario lo decide la API.
/// </summary>
public interface ITableroService
{
    Task<Result<TableroTurnoDto>> ObtenerAsync(long turnoProduccionId, bool soloLectura, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modo gerencial: líneas activas al alcance del usuario (administrador y mantenimiento, todas; el resto, sus líneas o
    /// todas si no tiene asignadas) y sus turnos de producción de la fecha (en curso o cerrados).
    /// </summary>
    Task<TurnosTableroDto> ListarTurnosAsync(DateOnly fecha, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default);
}
