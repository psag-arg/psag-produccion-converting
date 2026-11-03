using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Reglas de pantalla de Inspecciones que se pueden probar sin navegador (igual que ControlProcesoUi):
/// estado OK / NO OK de cada punto, observación obligatoria y filtros por frecuencia y quién realiza.
/// La API es la que valida al guardar; esto solo lo anticipa.
/// </summary>
public static class InspeccionUi
{
    public const string Ok = "OK";
    public const string NoOk = "NO_OK";
    public const string EnCurso = "EN_CURSO";
    public const string Cerrada = "CERRADA";

    /// <summary>Quién realiza, como en la columna de la planilla.</summary>
    public static readonly IReadOnlyList<(string Codigo, string Nombre)> Puestos = new[]
    {
        ("PACKAGING", "Op. Packaging"),
        ("MECANICO", "Mecánico"),
        ("MAQUINISTA", "Maquinista"),
        ("AYUDANTE", "Ayudante")
    };

    public static string NombrePuesto(string? codigo) =>
        Puestos.FirstOrDefault(p => p.Codigo == codigo).Nombre ?? "Todos";

    public static string DescribirFrecuencia(int dias) => dias switch
    {
        <= 1 => "Diaria",
        7 => "Semanal",
        _ => $"Cada {dias} días"
    };

    /// <summary>Frecuencias distintas de los puntos, de la más frecuente a la menos (para el filtro).</summary>
    public static List<(int Dias, string Nombre)> Frecuencias(IEnumerable<int> dias) =>
        dias.Distinct().OrderBy(d => d).Select(d => (d, DescribirFrecuencia(d))).ToList();

    public static bool TieneRespuesta(EjecucionInspeccionDetalleDto d) => !string.IsNullOrEmpty(d.Resultado);

    /// <summary>NO OK sin observación: la API lo rechaza, así que el autoguardado lo espera.</summary>
    public static bool FaltaObservacion(EjecucionInspeccionDetalleDto d) =>
        d.Resultado == NoOk && string.IsNullOrWhiteSpace(d.Observacion);

    /// <summary>El punto se ve con los filtros elegidos (null = todos).</summary>
    public static bool Coincide(EjecucionInspeccionDetalleDto d, int? frecuenciaDias, string? quienRealiza) =>
        (frecuenciaDias == null || d.FrecuenciaDias == frecuenciaDias)
        && (string.IsNullOrEmpty(quienRealiza) || d.QuienRealiza == quienRealiza);

    public static bool Coincide(ItemPlanInspeccionDto i, int? frecuenciaDias, string? quienRealiza) =>
        (frecuenciaDias == null || i.FrecuenciaDias == frecuenciaDias)
        && (string.IsNullOrEmpty(quienRealiza) || i.QuienRealiza == quienRealiza);

    /// <summary>"Vence hoy", "Próxima: 13/11" o "Ya respondido hoy".</summary>
    public static string TextoVencimiento(bool venceHoy, DateOnly? proximo) =>
        venceHoy ? "Vence hoy" : proximo.HasValue ? $"No vence hoy · próxima {proximo.Value:dd/MM}" : "No vence hoy";

    /// <summary>Faltan para cerrar: puntos que vencen hoy sin responder.</summary>
    public static int FaltanParaCerrar(IEnumerable<EjecucionInspeccionDetalleDto> detalles) =>
        detalles.Count(d => !d.FueraDePlantilla && d.VenceHoy && !TieneRespuesta(d));
}
