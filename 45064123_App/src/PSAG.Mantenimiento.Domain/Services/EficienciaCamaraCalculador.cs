using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Qué cuenta una cámara (el mismo método que tiene en el servicio de conteo).</summary>
public static class MetodoConteoCamara
{
    /// <summary>Brillo de los carriles de rollitos a la salida de la cortadora (cámara 09): cuenta rollos.</summary>
    public const string Carriles = "CARRILES";

    /// <summary>Paso de mangas por la mesa de la rebobinadora (cámara 10): cuenta mangas.</summary>
    public const string Mangas = "MANGAS";

    public static readonly IReadOnlyList<string> Todos = [Carriles, Mangas];
}

/// <summary>Estado del indicador de eficiencia de un turno.</summary>
public static class EstadoEficiencia
{
    /// <summary>Hay objetivo y horas programadas: se calcula el %.</summary>
    public const string Ok = "OK";

    /// <summary>La cámara no tiene objetivo por hora cargado: "Falta cargar el objetivo" (no 0 %).</summary>
    public const string SinObjetivo = "SIN_OBJETIVO";

    /// <summary>La línea no trabaja ese turno ese día (calendario o excepción en 0 h): no hay objetivo.</summary>
    public const string NoProgramado = "NO_PROGRAMADO";
}

/// <summary>En qué momento está el turno respecto de ahora.</summary>
public static class MomentoTurno
{
    public const string SinEmpezar = "SIN_EMPEZAR";
    public const string EnCurso = "EN_CURSO";
    public const string Terminado = "TERMINADO";
}

/// <summary>
/// Eficiencia por turno de una cámara (reunión 03/11 [8:31]): contadas en el turno / objetivo del turno, con
/// objetivo = ObjetivoPorHora x horas programadas del turno. Funciones puras: no tocan la base.
/// </summary>
public static class EficienciaCamaraCalculador
{
    public const decimal ObjetivoPorHoraMaximo = 100_000m;

    /// <summary>
    /// Inicio y fin del turno en hora de pared de la planta. Un turno que cruza medianoche (ej.: 21:00 a 05:00)
    /// pertenece al día en que empieza, como en el calendario de la línea: el del 02/11 va del 02/11 21:00 al
    /// 03/11 05:00.
    /// </summary>
    public static (DateTime Inicio, DateTime Fin) VentanaTurno(DateOnly dia, Turno turno)
    {
        var inicio = dia.ToDateTime(turno.HoraInicio);
        return (inicio, inicio.AddMinutes(turno.DuracionMinutos()));
    }

    /// <summary>
    /// Minutos programados de un turno en un día, con la misma regla que las horas programadas del día
    /// (<see cref="TiempoProgramadoCalculador"/>, T-541/T-542):
    /// <list type="number">
    /// <item>si el turno está inactivo, o el calendario vigente de la línea no lo incluye ese día de la semana, 0;</item>
    /// <item>si no hay excepción para el día, la duración del turno (un nocturno cuenta entero en el día en que empieza);</item>
    /// <item>si hay excepción (EXCEPCION o HISTORICO), las horas del día se reparten entre los turnos del día en
    /// proporción a su duración, sin pasar la duración del turno (0 h = no se trabaja).</item>
    /// </list>
    /// Sin excepciones, la suma de los turnos del día es igual a las horas programadas del día.
    /// </summary>
    public static int MinutosProgramadosTurno(
        DateOnly dia,
        Turno turno,
        IEnumerable<Turno> turnos,
        IEnumerable<CalendarioLinea> calendariosDeLaLinea,
        IEnumerable<TiempoProgramadoDia> excepcionesDeLaLinea)
    {
        if (!turno.Activo)
            return 0;

        var activos = turnos.Where(t => t.Activo).ToList();
        var version = calendariosDeLaLinea
            .Where(c => c.Activo && c.VigenteDesde <= dia)
            .OrderBy(c => c.VigenteDesde)
            .LastOrDefault();
        var delDia = version == null
            ? activos
            : activos.Where(t => version.TurnosDelDia(dia.DayOfWeek).Contains(t.TurnoId)).ToList();
        if (delDia.All(t => t.TurnoId != turno.TurnoId))
            return 0;

        var duracion = turno.DuracionMinutos();
        var excepcion = excepcionesDeLaLinea.FirstOrDefault(e => e.Activo && e.Fecha == dia);
        if (excepcion == null)
            return duracion;

        var minutosDia = (int)Math.Round(excepcion.Horas * 60m, MidpointRounding.AwayFromZero);
        var baseDia = TurnoReglas.MinutosProgramados(delDia);
        if (minutosDia <= 0 || baseDia <= 0)
            return 0;
        if (minutosDia >= baseDia)
            return duracion;
        return (int)Math.Round((decimal)duracion * minutosDia / baseDia, MidpointRounding.AwayFromZero);
    }

    /// <summary>Objetivo del turno = objetivo por hora x horas programadas, redondeado a unidades. Null sin objetivo.</summary>
    public static decimal? ObjetivoTurno(decimal? objetivoPorHora, int minutosProgramados)
    {
        if (objetivoPorHora is not { } porHora)
            return null;
        return Math.Round(porHora * Math.Max(minutosProgramados, 0) / 60m, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>% de eficiencia con un decimal. Null si no hay objetivo o es 0 (nunca divide por cero).</summary>
    public static decimal? Porcentaje(decimal contadas, decimal? objetivo)
    {
        if (objetivo is not { } valor || valor <= 0)
            return null;
        return Math.Round(contadas * 100m / valor, 1, MidpointRounding.AwayFromZero);
    }

    public static string Estado(decimal? objetivoPorHora, int minutosProgramados) =>
        objetivoPorHora is null ? EstadoEficiencia.SinObjetivo
        : minutosProgramados <= 0 ? EstadoEficiencia.NoProgramado
        : EstadoEficiencia.Ok;

    public static string Momento(DateTimeOffset ahora, DateTimeOffset inicio, DateTimeOffset fin) =>
        ahora < inicio ? MomentoTurno.SinEmpezar
        : ahora < fin ? MomentoTurno.EnCurso
        : MomentoTurno.Terminado;

    /// <summary>
    /// Para un turno en curso, la parte del objetivo que corresponde al tiempo transcurrido (referencia para ver si
    /// se viene bien). Null si el turno no está en curso o no hay objetivo.
    /// </summary>
    public static decimal? ObjetivoHastaAhora(decimal? objetivo, DateTimeOffset ahora, DateTimeOffset inicio, DateTimeOffset fin)
    {
        if (objetivo is not { } valor || ahora < inicio || ahora >= fin || fin <= inicio)
            return null;
        var fraccion = (decimal)((ahora - inicio).TotalSeconds / (fin - inicio).TotalSeconds);
        return Math.Round(valor * fraccion, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Minutos de parada dentro de la ventana del turno: cada parada se recorta a la ventana (una parada que empieza
    /// antes de las 05:00 cuenta en el turno siguiente sólo lo que pasó después de las 05:00).
    /// </summary>
    public static int MinutosParadaEnVentana(
        IEnumerable<(DateTimeOffset Inicio, DateTimeOffset Fin)> paradas, DateTimeOffset desde, DateTimeOffset hasta)
    {
        var segundos = 0d;
        foreach (var (inicio, fin) in paradas)
        {
            var a = inicio > desde ? inicio : desde;
            var b = fin < hasta ? fin : hasta;
            if (b > a)
                segundos += (b - a).TotalSeconds;
        }
        return (int)Math.Round(segundos / 60d, MidpointRounding.AwayFromZero);
    }

    /// <summary>Unidad que se muestra: rollos (CARRILES), mangas (MANGAS); sin método, se deduce de los carriles con datos.</summary>
    public static string Unidad(string? metodo, int cantidadCarriles)
    {
        if (string.Equals(metodo, MetodoConteoCamara.Mangas, StringComparison.OrdinalIgnoreCase))
            return "mangas";
        if (string.Equals(metodo, MetodoConteoCamara.Carriles, StringComparison.OrdinalIgnoreCase))
            return "rollos";
        return cantidadCarriles switch
        {
            1 => "mangas",
            > 1 => "rollos",
            _ => "unidades"
        };
    }

    public static string? MotivoObjetivoInvalido(decimal? objetivoPorHora) => objetivoPorHora switch
    {
        null => null,
        <= 0 => "El objetivo por hora tiene que ser mayor que 0. Si todavía no se sabe, dejalo vacío.",
        > ObjetivoPorHoraMaximo => $"El objetivo por hora no puede pasar de {ObjetivoPorHoraMaximo:N0}.",
        _ => null
    };

    /// <summary>Normaliza el método (mayúsculas, vacío = null). Devuelve false si no es CARRILES ni MANGAS.</summary>
    public static bool TryNormalizarMetodo(string? metodo, out string? normalizado)
    {
        normalizado = string.IsNullOrWhiteSpace(metodo) ? null : metodo.Trim().ToUpperInvariant();
        return normalizado == null || MetodoConteoCamara.Todos.Contains(normalizado);
    }
}
