using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Minutos programados por día. Función pura: no toca la base.
/// </summary>
public static class TiempoProgramadoCalculador
{
    /// <summary>
    /// Igual que la sobrecarga con calendarios, para una línea sin calendario base.
    /// </summary>
    public static IReadOnlyList<MinutosProgramadosDia> MinutosProgramadosPorDia(
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<Turno> turnos,
        IEnumerable<TiempoProgramadoDia> excepciones) =>
        MinutosProgramadosPorDia(desde, hasta, turnos, [], excepciones);

    /// <summary>
    /// Para cada día del rango (ambos extremos incluidos) devuelve los minutos programados de una línea:
    /// <list type="number">
    /// <item>si hay una excepción activa (EXCEPCION o HISTORICO) para ese día, sus horas * 60;</item>
    /// <item>si no, si hay una versión activa del calendario de la línea vigente ese día, los minutos que
    /// cubren los turnos activos que la línea trabaja ese día de la semana (0 si no trabaja);</item>
    /// <item>si no, los minutos que cubren todos los turnos activos (estándar de planta).</item>
    /// </list>
    /// Una hora en la que se superponen dos turnos cuenta una vez (máximo 24 h).
    /// Las filas, versiones o turnos inactivos se ignoran.
    /// </summary>
    public static IReadOnlyList<MinutosProgramadosDia> MinutosProgramadosPorDia(
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<Turno> turnos,
        IEnumerable<CalendarioLinea> calendarios,
        IEnumerable<TiempoProgramadoDia> excepciones)
    {
        var activos = turnos.Where(t => t.Activo).OrderBy(t => t.Orden).ThenBy(t => t.HoraInicio).ToList();
        var estandar = new Cuenta(TurnoReglas.MinutosCubiertos(activos), Codigos(activos));

        // Una fila por fecha (índice único en base de datos).
        var excepcionPorFecha = excepciones
            .Where(e => e.Activo)
            .ToDictionary(e => e.Fecha);

        var versiones = calendarios
            .Where(c => c.Activo)
            .OrderBy(c => c.VigenteDesde)
            .ToList();

        // Minutos por versión y día de la semana: se calculan una vez.
        var cuentaBase = new Dictionary<(int Version, DayOfWeek Dia), Cuenta>();

        var resultado = new List<MinutosProgramadosDia>();
        for (var dia = desde; dia <= hasta; dia = dia.AddDays(1))
        {
            if (excepcionPorFecha.TryGetValue(dia, out var excepcion))
            {
                var minutos = (int)Math.Round(excepcion.Horas * 60m, MidpointRounding.AwayFromZero);
                resultado.Add(new MinutosProgramadosDia(dia, minutos, excepcion.Origen));
                continue;
            }

            var version = VersionVigente(versiones, dia);
            if (version < 0)
            {
                resultado.Add(new MinutosProgramadosDia(dia, estandar.Minutos, OrigenTiempoProgramado.Estandar, estandar.Turnos));
                continue;
            }

            var clave = (version, dia.DayOfWeek);
            if (!cuentaBase.TryGetValue(clave, out var cuenta))
            {
                var ids = versiones[version].TurnosDelDia(dia.DayOfWeek).ToHashSet();
                var delDia = activos.Where(t => ids.Contains(t.TurnoId)).ToList();
                cuenta = new Cuenta(TurnoReglas.MinutosCubiertos(delDia), Codigos(delDia));
                cuentaBase[clave] = cuenta;
            }

            resultado.Add(new MinutosProgramadosDia(dia, cuenta.Minutos, OrigenTiempoProgramado.BaseLinea, cuenta.Turnos));
        }

        return resultado;
    }

    /// <summary>
    /// Índice de la última versión con VigenteDesde menor o igual al día, o -1 si ninguna rige todavía.
    /// </summary>
    private static int VersionVigente(List<CalendarioLinea> versionesOrdenadas, DateOnly dia)
    {
        var indice = -1;
        for (var i = 0; i < versionesOrdenadas.Count && versionesOrdenadas[i].VigenteDesde <= dia; i++)
            indice = i;
        return indice;
    }

    private static string Codigos(IEnumerable<Turno> turnos) =>
        string.Join(" + ", turnos.Select(t => string.IsNullOrWhiteSpace(t.Codigo) ? t.Nombre : t.Codigo));

    private readonly record struct Cuenta(int Minutos, string Turnos);
}

/// <summary>
/// Minutos programados de un día, de dónde salen (ver <see cref="OrigenTiempoProgramado"/>) y,
/// cuando salen de turnos, cuáles (ej.: "T1 + T2"; vacío si ese día no se trabaja).
/// </summary>
public readonly record struct MinutosProgramadosDia(
    DateOnly Fecha,
    int Minutos,
    string Origen = OrigenTiempoProgramado.Estandar,
    string? Turnos = null);
