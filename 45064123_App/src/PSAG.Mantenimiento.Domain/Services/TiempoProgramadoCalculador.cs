using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Minutos programados por día. Función pura: no toca la base.
/// </summary>
public static class TiempoProgramadoCalculador
{
    /// <summary>
    /// Para cada día del rango (ambos extremos incluidos) devuelve los minutos programados:
    /// si hay una excepción activa para ese día, sus horas * 60; si no, los minutos del día que cubren
    /// los turnos activos (una hora en la que se superponen dos turnos cuenta una vez, máximo 24 h).
    /// Las filas o turnos inactivos se ignoran.
    /// </summary>
    public static IReadOnlyList<MinutosProgramadosDia> MinutosProgramadosPorDia(
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<Turno> turnos,
        IEnumerable<TiempoProgramadoDia> excepciones)
    {
        var estandar = TurnoReglas.MinutosCubiertos(turnos.Where(t => t.Activo));

        // Una fila por fecha (índice único en base de datos).
        var excepcionPorFecha = excepciones
            .Where(e => e.Activo)
            .ToDictionary(e => e.Fecha);

        var resultado = new List<MinutosProgramadosDia>();
        for (var dia = desde; dia <= hasta; dia = dia.AddDays(1))
        {
            var minutos = excepcionPorFecha.TryGetValue(dia, out var excepcion)
                ? (int)Math.Round(excepcion.Horas * 60m, MidpointRounding.AwayFromZero)
                : estandar;

            resultado.Add(new MinutosProgramadosDia(dia, minutos));
        }

        return resultado;
    }
}

public readonly record struct MinutosProgramadosDia(DateOnly Fecha, int Minutos);
