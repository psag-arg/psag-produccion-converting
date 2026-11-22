using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Textos y colores de las pantallas de producción (T-798 etapa 6): barra de turno, turnos de producción (supervisor),
/// presupuesto vs. real y microcortes. Los números los calcula la API; acá sólo se muestran.
/// "Sin datos" nunca es 0 y "sin objetivo" nunca es 0 %.
/// </summary>
public static class ProduccionUi
{
    /// <summary>Roles que corrigen, cierran o cambian producto en cualquier línea.</summary>
    public static readonly IReadOnlyList<string> RolesSupervision = ["SUPERVISOR", "MANTENIMIENTO", "ADMINISTRADOR"];

    // ===================== Estado de la producción oficial =====================

    /// <summary>Texto del estado del real de una magnitud (VALIDADA, PENDIENTE, PARCIAL, SIN_DATOS).</summary>
    public static string TextoEstadoReal(string? estado) => estado switch
    {
        "VALIDADA" => "Validada",
        "PENDIENTE" => "Pendiente de validar",
        "PARCIAL" => "Parcial (en curso)",
        "SIN_DATOS" => "Sin datos",
        _ => "Sin datos"
    };

    /// <summary>Color (badge) del estado: verde validada, amarillo pendiente, celeste en curso, gris sin datos.</summary>
    public static string ClaseEstadoReal(string? estado) => estado switch
    {
        "VALIDADA" => "text-bg-success",
        "PENDIENTE" => "text-bg-warning",
        "PARCIAL" => "text-bg-info",
        _ => "text-bg-secondary"
    };

    /// <summary>De dónde sale la cantidad oficial.</summary>
    public static string TextoOrigen(string? origen) => origen switch
    {
        "CAMARA" => "cámara",
        "CAMARA_CORREGIDA" => "cámara corregida",
        "MANUAL" => "carga manual",
        _ => "-"
    };

    /// <summary>"4.100" o "Sin datos" (nunca 0 por falta de dato).</summary>
    public static string TextoCantidad(decimal? cantidad) =>
        cantidad is { } c ? EficienciaUi.Numero(c) : EficienciaUi.TextoSinDatos;

    /// <summary>
    /// Qué acciones tiene el supervisor sobre una magnitud de un tramo. Sólo con el tramo cerrado: validar si hay
    /// conteo de cámara pendiente; corregir si hubo cámara; cargar a mano siempre (p. ej. bolsones sin cámara).
    /// </summary>
    public static (bool Validar, bool Corregir, bool Manual) AccionesProduccion(bool tramoAbierto, MagnitudTramoDto m)
    {
        if (tramoAbierto)
            return (false, false, false);
        var hayCamara = m.Vigente?.CantidadCamara != null;
        return (m.EstadoReal == "PENDIENTE" && hayCamara, hayCamara, true);
    }

    // ===================== Barra de turno =====================

    /// <summary>"T2 (13–21)": código del turno y horario programado (con minutos sólo si no son en punto).</summary>
    public static string TextoTurno(string turnoCodigo, DateTimeOffset inicio, DateTimeOffset fin) =>
        $"{turnoCodigo} ({Hora(inicio)}–{Hora(fin)})";

    private static string Hora(DateTimeOffset h) => h.Minute == 0 ? h.ToString("HH", CultureInfo.InvariantCulture) : h.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Objetivo prorrateado a una hora: objetivo por turno × minutos transcurridos ÷ minutos de referencia (misma regla
    /// que la API). Null sin objetivo o con referencia inválida; nunca negativo.
    /// </summary>
    public static decimal? ObjetivoALaHora(int? objetivoTurno, int minutosTurnoReferencia, decimal minutosTranscurridos)
    {
        if (objetivoTurno is not { } objetivo || minutosTurnoReferencia <= 0)
            return null;
        return Math.Round(objetivo * Math.Max(0m, minutosTranscurridos) / minutosTurnoReferencia, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>"Eficiencia parcial 81 %" (turno abierto) o "Eficiencia 83 %"; "Eficiencia: sin datos" / "sin objetivo".</summary>
    public static string TextoEficienciaBarra(ComparacionProduccionDto c, bool abierto)
    {
        var titulo = abierto ? "Eficiencia parcial" : "Eficiencia";
        return c.Estado switch
        {
            "OK" when c.Porcentaje is { } p => $"{titulo} {p.ToString("N0", CultureInfo.CurrentCulture)} %",
            "SIN_OBJETIVO" => $"{titulo}: sin objetivo",
            _ => $"{titulo}: sin datos"
        };
    }

    /// <summary>"(mangas 4.100 / obj. a la hora 5.060)" o con "sin datos" / "sin objetivo" en lugar del número.</summary>
    public static string TextoMangasBarra(ComparacionProduccionDto mangas, bool abierto)
    {
        var real = mangas.Real is { } r ? EficienciaUi.Numero(r) : "sin datos";
        var objetivo = mangas.Objetivo is { } o ? EficienciaUi.Numero(o) : "sin objetivo";
        return $"(mangas {real} / {(abierto ? "obj. a la hora" : "obj.")} {objetivo})";
    }

    /// <summary>
    /// Partes de la barra de turno: "Tango 2", "T2 (13–21)", "SH 30 desde 13:05", "Op. Juan", "Eficiencia parcial 81 %
    /// (mangas 4.100 / obj. a la hora 5.060)". Sin indicadores (la API falló) se omite la última.
    /// </summary>
    public static List<string> PartesBarra(TurnoProduccionDto t, IndicadoresTurnoDto? ind)
    {
        var abierto = t.Estado == "ABIERTO";
        var partes = new List<string>
        {
            t.LineaNombre,
            TextoTurno(t.TurnoCodigo, t.InicioProgramado, t.FinProgramado),
            t.ProductoActual is { } p ? $"{p.ProductoNombre} desde {p.Desde:HH:mm}" : "Producto a completar",
            t.OperadorActual is { } o ? $"Op. {o.OperadorNombre ?? o.OperadorUsuarioId}" : "Sin operador"
        };
        if (!abierto)
            partes.Add(t.Estado == "SIN_APERTURA" ? "Sin apertura" : "Cerrado");
        if (ind != null)
            partes.Add($"{TextoEficienciaBarra(ind.Eficiencia, abierto)} {TextoMangasBarra(ind.Mangas, abierto)}");
        return partes;
    }

    /// <summary>Puede cambiar producto / cerrar desde la barra: supervisión, o el operario habilitado en esa línea.</summary>
    public static bool PuedeOperarTurno(IEnumerable<string> roles, IEnumerable<int> lineasHabilitadas, int lineaId)
    {
        var r = roles.ToList();
        return r.Any(RolesSupervision.Contains) || (r.Contains("OPERARIO") && lineasHabilitadas.Contains(lineaId));
    }

    // ===================== Microcortes por turno y cámara =====================

    /// <summary>Paradas de una cámara en un turno: las que llegaron al umbral y los microcortes (no llegaron).</summary>
    public sealed record MicrocortesTurnoCamara(
        long? TurnoProduccionId, string TurnoNombre, int CamaraId, string CamaraCodigo, string LineaNombre,
        int Paradas, int SegundosParadas, int Microcortes, int SegundosMicrocortes)
    {
        public int MinutosParadas => (int)Math.Round(SegundosParadas / 60m, MidpointRounding.AwayFromZero);
        public int MinutosMicrocortes => (int)Math.Round(SegundosMicrocortes / 60m, MidpointRounding.AwayFromZero);
    }

    /// <summary>Agrupa las paradas de cámara por turno de producción y cámara (las sin turno van juntas, "Sin turno").</summary>
    public static List<MicrocortesTurnoCamara> MicrocortesPorTurno(IEnumerable<ParadaCamaraDto> paradas) =>
        paradas
            .GroupBy(p => (p.TurnoProduccionId, p.CamaraId))
            .Select(g =>
            {
                var primera = g.OrderBy(p => p.FechaHoraInicio).First();
                var llegaron = g.Where(p => p.SuperaUmbral).ToList();
                var micro = g.Where(p => !p.SuperaUmbral).ToList();
                return new MicrocortesTurnoCamara(
                    g.Key.TurnoProduccionId, primera.TurnoNombre ?? "Sin turno", g.Key.CamaraId, primera.CamaraCodigo, primera.LineaNombre,
                    llegaron.Count, llegaron.Sum(p => p.DuracionSegundos), micro.Count, micro.Sum(p => p.DuracionSegundos));
            })
            .OrderBy(x => x.LineaNombre).ThenBy(x => x.TurnoProduccionId ?? long.MaxValue).ThenBy(x => x.CamaraCodigo)
            .ToList();
}
