using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Indicadores de producción (T-798 2.10, etapa 5): presupuesto (snapshot del parámetro en cada tramo) vs. real
/// (producción oficial; parcial al vuelo si el tramo está abierto), eficiencia, velocidad e indisponibilidad desde los
/// turnos de producción. Un solo cálculo: reemplaza la "eficiencia por cámara" de la etapa 4.
/// </summary>
public interface IIndicadoresService
{
    /// <summary>Detalle de un turno: tramos, objetivo, real, eficiencia, velocidad y minutos de parada que cuentan.</summary>
    Task<Result<IndicadoresTurnoDto>> ObtenerTurnoAsync(long turnoProduccionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// T-805 · Igual, calculado con un "ahora" dado (el tablero pasa el suyo, para que todos sus números usen el mismo
    /// momento). Null = el reloj del servicio.
    /// </summary>
    Task<Result<IndicadoresTurnoDto>> ObtenerTurnoAsync(long turnoProduccionId, DateTimeOffset? ahora, CancellationToken cancellationToken = default);

    /// <summary>Presupuesto vs. real en un período (fecha de producción), agrupado por turno, día, producto o línea.</summary>
    Task<Result<PresupuestoRealDto>> ObtenerProduccionAsync(IndicadoresProduccionFiltro filtro, CancellationToken cancellationToken = default);
}
