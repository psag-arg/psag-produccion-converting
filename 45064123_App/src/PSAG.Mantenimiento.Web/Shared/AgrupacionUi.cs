using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>Textos del agrupador de microcortes (reunión 17/11/2026). Funciones puras, para testear sin la pantalla.</summary>
public static class AgrupacionUi
{
    /// <summary>"8 min 20 s", "45 s", "5 min".</summary>
    public static string Duracion(int segundos)
    {
        if (segundos < 60)
            return $"{segundos} s";
        var min = segundos / 60;
        var s = segundos % 60;
        return s == 0 ? $"{min} min" : $"{min} min {s} s";
    }

    /// <summary>"11:08 (3 min 20 s), 11:12 (2 min)".</summary>
    public static string Lista(IEnumerable<MicrocorteDto> microcortes) =>
        string.Join(", ", microcortes.OrderBy(m => m.FechaHoraInicio)
            .Select(m => $"{m.FechaHoraInicio:HH:mm} ({Duracion(m.DuracionSegundos)})"));

    /// <summary>
    /// Resumen del grupo para el operario: "Inicio real 11:08 · tiempo total 13 min 20 s (detención 8 min + 2
    /// microcortes 5 min 20 s)". Con la detención pendiente (0 minutos todavía) no se suma "detención".
    /// </summary>
    public static string Resumen(AgrupacionDetencionDto a)
    {
        var micro = a.CantidadMicrocortes == 1 ? "1 microcorte" : $"{a.CantidadMicrocortes} microcortes";
        var detalle = a.SegundosDetencion > 0
            ? $"detención {Duracion(a.SegundosDetencion)} + {micro} {Duracion(a.SegundosMicrocortes)}"
            : $"{micro} {Duracion(a.SegundosMicrocortes)}; falta declarar la detención";
        var inicio = a.InicioReal is { } i ? $"Inicio real {i:HH:mm} · " : string.Empty;
        return $"{inicio}tiempo total {Duracion(a.SegundosTotales)} ({detalle})";
    }
}
