using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas de los horarios de turno y del tiempo programado por día. Funciones puras.
/// La suma de los turnos activos es el denominador de la indisponibilidad: no puede pasar de 24 h.
/// </summary>
public static class TurnoReglas
{
    private const int MinutosDia = 24 * 60;
    private const int LargoMotivo = 200; // largo de la columna Motivo

    /// <summary>
    /// Motivo por el que el horario no se puede guardar como turno activo, o null si es válido.
    /// </summary>
    public static string? MotivoHorarioInvalido(Turno candidato, IEnumerable<Turno> otrosActivos)
    {
        if (candidato.HoraInicio == candidato.HoraFin)
            return "La hora de inicio y la de fin no pueden ser iguales.";

        var otros = otrosActivos.Where(o => o.TurnoId != candidato.TurnoId).OrderBy(o => o.HoraInicio).ToList();
        var superpuesto = otros.FirstOrDefault(o => SeSuperponen(candidato, o));
        if (superpuesto != null)
            return $"Este turno se superpone con {superpuesto.Nombre} ({superpuesto.HoraInicio:HH:mm}-{superpuesto.HoraFin:HH:mm}). Ajustá los horarios.";

        var total = candidato.DuracionMinutos() + otros.Sum(o => o.DuracionMinutos());
        if (total > MinutosDia)
            return $"Los turnos activos suman {total / 60.0:0.#} h por día; no pueden superar 24 h.";

        return null;
    }

    private static bool SeSuperponen(Turno a, Turno b) =>
        Tramos(a).Any(x => Tramos(b).Any(y => x.Desde < y.Hasta && y.Desde < x.Hasta));

    /// <summary>
    /// Un turno que cruza medianoche son dos tramos del día: hasta las 24 y desde las 0.
    /// </summary>
    private static IEnumerable<(int Desde, int Hasta)> Tramos(Turno turno)
    {
        var inicio = (int)turno.HoraInicio.ToTimeSpan().TotalMinutes;
        var fin = (int)turno.HoraFin.ToTimeSpan().TotalMinutes;
        if (fin > inicio)
        {
            yield return (inicio, fin);
            yield break;
        }

        yield return (inicio, MinutosDia);
        if (fin > 0)
            yield return (0, fin);
    }

    /// <summary>
    /// Guardar un día histórico (HS PROG) sin cambiar horas ni motivo no lo convierte en excepción.
    /// </summary>
    public static bool HistoricoSinCambios(TiempoProgramadoDia fila, decimal horas, string? motivo) =>
        fila.Activo
        && fila.Origen == "HISTORICO"
        && fila.Horas == Math.Round(horas, 2)
        && string.IsNullOrWhiteSpace(motivo);

    /// <summary>
    /// Al reemplazar un día histórico, el valor anterior queda escrito en el motivo para no perderlo.
    /// </summary>
    public static string MotivoReemplazandoHistorico(decimal horasHistoricas, string? motivo)
    {
        var nota = $"antes {horasHistoricas:0.##} h históricas (HS PROG)";
        var texto = string.IsNullOrWhiteSpace(motivo) ? nota : $"{motivo.Trim()} · {nota}";
        return texto.Length <= LargoMotivo ? texto : texto[..LargoMotivo];
    }
}
