using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Minutos programados por día. Función pura: no toca la base.
/// </summary>
public static class TiempoProgramadoCalculador
{
    private const int MinutosPorDia = 24 * 60;

    /// <summary>
    /// Para cada día del rango (ambos extremos incluidos) devuelve los minutos programados:
    /// si hay una excepción activa para ese día, sus horas * 60; si no, la suma de la duración
    /// de los turnos activos. Las filas o turnos inactivos se ignoran.
    /// </summary>
    public static IReadOnlyList<MinutosProgramadosDia> MinutosProgramadosPorDia(
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<Turno> turnos,
        IEnumerable<TiempoProgramadoDia> excepciones)
    {
        var estandar = turnos.Where(t => t.Activo).Sum(DuracionMinutos);

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

    /// <summary>
    /// Duración de un turno en minutos. Si HoraFin no es posterior a HoraInicio,
    /// el turno cruza medianoche (ej.: 21:00 a 05:00 = 480 minutos), igual que Turno.ContieneHora.
    /// </summary>
    public static int DuracionMinutos(Turno turno)
    {
        var inicio = turno.HoraInicio.ToTimeSpan();
        var fin = turno.HoraFin.ToTimeSpan();

        var duracion = fin > inicio
            ? fin - inicio
            : TimeSpan.FromMinutes(MinutosPorDia) - inicio + fin;

        return (int)duracion.TotalMinutes;
    }
}

public readonly record struct MinutosProgramadosDia(DateOnly Fecha, int Minutos);
