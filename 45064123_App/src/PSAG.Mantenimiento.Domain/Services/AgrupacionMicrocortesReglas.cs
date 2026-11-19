using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Tiempo agrupado de una detención con sus microcortes (lo mismo que calcula mnt.vw_DetencionesAgrupadas).</summary>
public sealed record ResumenAgrupacion(
    DateTimeOffset? InicioReal,
    DateTimeOffset? FinReal,
    int CantidadMicrocortes,
    int SegundosMicrocortes,
    int SegundosDetencion,
    int SegundosTotales);

/// <summary>
/// Agrupador de detenciones (reunión 17/11/2026, pedido de Emiliano): se guarda todo, se marca lo que supera el
/// umbral y los microcortes previos a una detención se agrupan en una tabla anexa sin modificar los registros
/// originales. Funciones puras: las usan el servicio de agrupación y los tests.
/// <para>
/// Caso real (13/11, T2-CAM10): paradas 11:08:23–11:11:43, 11:12:03–11:14:03 y 11:14:43–11:23:03. Sólo la última
/// superó 5 min y generó la detención (parada 11:14:43); las dos primeras son microcortes en los 10 min previos y
/// se agrupan si el operario dice "mismo motivo".
/// </para>
/// </summary>
public static class AgrupacionMicrocortesReglas
{
    /// <summary>Minutos antes del inicio de la detención en que se buscan microcortes.</summary>
    public const int VentanaMinutos = 10;

    /// <summary>Tope de microcortes por pedido de agrupación (en 10 min no entran más de ~30 paradas de 20 s).</summary>
    public const int MaximoPorPedido = 50;

    /// <summary>
    /// La parada fue un microcorte (no llegó al umbral). Usa la marca guardada al insertar (script 31); las paradas
    /// anteriores al script (marca NULL) se evalúan con el umbral actual de la cámara, sin guardar nada.
    /// </summary>
    public static bool EsMicrocorte(ParadaCamara parada, int umbralSegundosActual) =>
        !(parada.SuperaUmbral ?? DetencionCamaraReglas.SuperaUmbral(parada.FechaHoraInicio, parada.FechaHoraFin, umbralSegundosActual));

    /// <summary>
    /// La parada empezó dentro de los <see cref="VentanaMinutos"/> minutos previos al inicio de la detención
    /// (inclusive el borde de 10 min) y antes de ese inicio (la parada que generó la detención no es un hijo).
    /// </summary>
    public static bool EnVentana(ParadaCamara parada, DateTimeOffset inicioDetencion) =>
        parada.FechaHoraInicio >= InicioVentana(inicioDetencion) && parada.FechaHoraInicio < inicioDetencion;

    /// <summary>Desde dónde buscar en la base (el repositorio filtra [desde, inicio)).</summary>
    public static DateTimeOffset InicioVentana(DateTimeOffset inicioDetencion) => inicioDetencion.AddMinutes(-VentanaMinutos);

    /// <summary>
    /// La parada es de la misma cámara que la detención (si la detención vino de una cámara) o, si la detención es
    /// manual, de cualquier cámara de la misma línea.
    /// </summary>
    public static bool MismaCamaraOLinea(ParadaCamara parada, int lineaIdDetencion, int? puntoControlDetencion) =>
        parada.LineaId == lineaIdDetencion
        && (puntoControlDetencion == null || parada.PuntoControlCalidadId == puntoControlDetencion);

    /// <summary>
    /// Motivo por el que la parada no se puede agrupar bajo la detención, o null.
    /// <paramref name="yaAgrupada"/>: la parada ya está activa en algún grupo (de esta u otra detención).
    /// </summary>
    public static string? MotivoNoAgrupable(
        ParadaCamara? parada, int lineaIdDetencion, int? puntoControlDetencion, DateTimeOffset? inicioDetencion,
        int umbralSegundosActual, bool yaAgrupada)
    {
        if (parada == null)
            return "La parada de cámara no existe.";
        if (!MismaCamaraOLinea(parada, lineaIdDetencion, puntoControlDetencion))
            return $"La parada {parada.ParadaCamaraId} no es de la misma cámara o línea que la detención.";
        if (inicioDetencion is not { } inicio)
            return "La detención no tiene hora de parada: no se pueden buscar microcortes previos.";
        if (!EnVentana(parada, inicio))
            return $"La parada {parada.ParadaCamaraId} no está en los {VentanaMinutos} minutos previos a la detención.";
        if (!EsMicrocorte(parada, umbralSegundosActual))
            return $"La parada {parada.ParadaCamaraId} superó el umbral: no es un microcorte.";
        if (yaAgrupada)
            return $"La parada {parada.ParadaCamaraId} ya está agrupada.";
        return null;
    }

    /// <summary>
    /// Inicio real = el menor entre la parada de la detención y el inicio de los microcortes activos; fin real = el
    /// mayor entre el arranque de la detención (declarado o, si no, el detectado) y el fin de los microcortes.
    /// Segundos totales = MinutosEfectivos x 60 + duración de los microcortes (no suma los ratos en marcha entre
    /// uno y otro). NO cambia MinutosEfectivos de la detención: es sólo para reportes.
    /// </summary>
    public static ResumenAgrupacion Resumir(Detencion detencion, IEnumerable<ParadaCamara> microcortesActivos)
    {
        var hijos = microcortesActivos.ToList();
        var inicios = hijos.Select(h => h.FechaHoraInicio).ToList();
        if (detencion.FechaHoraParada is { } p) inicios.Add(p);
        var fines = hijos.Select(h => h.FechaHoraFin).ToList();
        if ((detencion.FechaHoraArranque ?? detencion.FechaHoraArranqueDetectada) is { } a) fines.Add(a);

        var segundosHijos = hijos.Sum(h => h.DuracionSegundos);
        var segundosDetencion = detencion.MinutosEfectivos * 60;
        return new ResumenAgrupacion(
            inicios.Count == 0 ? null : inicios.Min(),
            fines.Count == 0 ? null : fines.Max(),
            hijos.Count,
            segundosHijos,
            segundosDetencion,
            segundosDetencion + segundosHijos);
    }

    /// <summary>Pregunta al operario: "Hubo 2 microcortes antes de esta parada (11:08, 11:12). ¿Mismo motivo?". Horas de planta.</summary>
    public static string Pregunta(IEnumerable<DateTimeOffset> iniciosPlanta)
    {
        var horas = iniciosPlanta.OrderBy(h => h).Select(h => h.ToString("HH:mm")).ToList();
        var cuantos = horas.Count == 1 ? "1 microcorte" : $"{horas.Count} microcortes";
        return $"Hubo {cuantos} antes de esta parada ({string.Join(", ", horas)}). ¿Mismo motivo?";
    }

    /// <summary>Texto del historial de la detención al agrupar o desagrupar. Horas de planta.</summary>
    public static string ObservacionHistorial(bool agrupar, IEnumerable<DateTimeOffset> iniciosPlanta)
    {
        var horas = string.Join(", ", iniciosPlanta.OrderBy(h => h).Select(h => h.ToString("HH:mm:ss")));
        return agrupar
            ? $"Microcortes agrupados (mismo motivo): {horas}. El tiempo de la detención (minutos efectivos) no cambia."
            : $"Microcortes desagrupados: {horas}.";
    }
}
