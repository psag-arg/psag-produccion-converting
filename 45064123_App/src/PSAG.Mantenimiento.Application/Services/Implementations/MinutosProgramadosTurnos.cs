using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Minutos programados de turnos de producción para los indicadores (T-798 etapa 5): el snapshot del turno; los que
/// tienen 0 (convertidos sin snapshot, o fuera de programación) se recalculan al consultar desde la programación de la
/// línea y la excepción del día (la misma regla que al abrir el turno). Un turno fuera de programación da 0 igual.
/// Lo usan los indicadores y la indisponibilidad del dashboard.
/// </summary>
public static class MinutosProgramadosTurnos
{
    public static async Task<IReadOnlyDictionary<long, int>> CalcularAsync(
        IUnitOfWork unitOfWork, IReadOnlyCollection<TurnoProduccion> turnos, CancellationToken cancellationToken)
    {
        var resultado = turnos.ToDictionary(t => t.TurnoProduccionId, t => t.MinutosProgramados);
        var sinSnapshot = turnos.Where(t => t.MinutosProgramados <= 0).ToList();
        if (sinSnapshot.Count == 0)
            return resultado;

        var turnosPlanta = (await unitOfWork.Turnos.GetAllAsync(cancellationToken)).ToList();
        var programacion = (await unitOfWork.CalendariosLinea.GetActivosAsync(null, cancellationToken)).ToList();
        var excepciones = (await unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(
            sinSnapshot.Min(t => t.FechaProduccion), sinSnapshot.Max(t => t.FechaProduccion), null, cancellationToken)).ToList();

        foreach (var tp in sinSnapshot)
        {
            var turno = turnosPlanta.FirstOrDefault(t => t.TurnoId == tp.TurnoId);
            if (turno == null)
                continue;
            resultado[tp.TurnoProduccionId] = IndicadoresProduccionCalculador.MinutosProgramados(tp.MinutosProgramados, () =>
                TurnoProduccionReglas.MinutosProgramados(tp.FechaProduccion, turno, turnosPlanta,
                    programacion.Where(p => p.LineaId == tp.LineaId), excepciones.Where(e => e.LineaId == tp.LineaId)));
        }
        return resultado;
    }
}
