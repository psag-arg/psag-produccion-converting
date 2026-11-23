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
}
