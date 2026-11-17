using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Textos y colores de la tarjeta "Eficiencia (cámara)" del Dashboard. Los números (objetivo, %) los calcula la
/// API; acá sólo se muestran.
/// </summary>
public static class EficienciaUi
{
    /// <summary>Desde este % el turno se pinta verde; desde <see cref="UmbralAmarillo"/>, amarillo; abajo, rojo.</summary>
    public const decimal UmbralVerde = 90m;
    public const decimal UmbralAmarillo = 70m;

    public const string TextoSinObjetivo = "Falta cargar el objetivo";
    public const string TextoNoProgramado = "Turno no programado";

    public static string ColorPorcentaje(decimal? porcentaje) => porcentaje switch
    {
        null => "text-muted",
        >= UmbralVerde => "text-success",
        >= UmbralAmarillo => "text-warning",
        _ => "text-danger"
    };

    public static string ColorBarra(decimal? porcentaje) => porcentaje switch
    {
        null => "bg-secondary",
        >= UmbralVerde => "bg-success",
        >= UmbralAmarillo => "bg-warning",
        _ => "bg-danger"
    };

    /// <summary>Ancho de la barra (0 a 100 %, aunque la eficiencia pase de 100).</summary>
    public static string AnchoBarra(decimal? porcentaje) =>
        Math.Clamp(porcentaje ?? 0m, 0m, 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>"87,5 %"; sin objetivo o turno no programado, el texto que corresponde (nunca "0 %").</summary>
    public static string TextoPorcentaje(EficienciaTurnoDto turno) => turno.Estado switch
    {
        "SIN_OBJETIVO" => TextoSinObjetivo,
        "NO_PROGRAMADO" => TextoNoProgramado,
        _ when turno.Porcentaje is { } p => p.ToString("0.#", CultureInfo.CurrentCulture) + " %",
        _ => "-"
    };

    public static string? TextoMomento(string momento) => momento switch
    {
        "EN_CURSO" => "En curso",
        "SIN_EMPEZAR" => "Sin empezar",
        _ => null
    };

    public static string Horario(EficienciaTurnoDto turno) => $"{turno.Inicio:HH:mm} a {turno.Fin:HH:mm}";

    /// <summary>Ej.: "Tango 2 · Cámara 10 (mangas)", "Tango 2 · Cámara 08 (bolsones)". La unidad viene del método del punto.</summary>
    public static string Titulo(EficienciaCamaraPuntoDto camara)
    {
        var nombre = camara.NumeroCamara is { } n ? $"Cámara {n:00}" : camara.Nombre;
        return $"{camara.LineaNombre} · {nombre} ({camara.Unidad})";
    }

    // ---------- "Datos hasta las HH:mm" (reunión 17/11) ----------

    public const string EstadoAlDia = "AL_DIA";
    public const string EstadoAtrasado = "ATRASADO";
    public const string EstadoSinDatos = "SIN_DATOS";

    /// <summary>
    /// Una tarjeta por cámara, aunque sólo tenga datos uno de los dos días, en el orden de la API (línea, número de
    /// cámara, código). Para el estado de los datos manda la respuesta de hoy (es la más nueva).
    /// </summary>
    public static List<EficienciaCamaraPuntoDto> Camaras(EficienciaCamaraDiaDto? hoy, EficienciaCamaraDiaDto? ayer) =>
        (hoy?.Camaras ?? []).Concat(ayer?.Camaras ?? [])
            .GroupBy(c => c.PuntoControlCalidadId)
            .Select(g => g.First())
            .ToList();

    /// <summary>"Datos hasta las 10:16"; si no es de hoy, "Datos hasta el 15/11 22:40"; sin datos, "Sin datos todavía".</summary>
    public static string TextoDatosHasta(EficienciaCamaraPuntoDto camara, DateTimeOffset generadoEn)
    {
        if (camara.DatosHasta is not { } hasta)
            return "Sin datos todavía";
        return hasta.Date == generadoEn.ToOffset(hasta.Offset).Date
            ? $"Datos hasta las {hasta:HH:mm}"
            : $"Datos hasta el {hasta:dd/MM HH:mm}";
    }

    /// <summary>Explicación (tooltip) de qué incluye la hora.</summary>
    public static string TituloDatosHasta(EficienciaCamaraPuntoDto camara)
    {
        if (camara.DatosHasta is not { } hasta)
            return "La cámara todavía no mandó ningún minuto de conteo.";
        var texto = $"Incluye todo lo contado hasta las {hasta:HH:mm} (último minuto cerrado: {hasta.AddMinutes(-1):HH:mm} a {hasta:HH:mm}). " +
                    "El panel va 1 o 2 minutos atrás del contador de la máquina: para comparar, mirar el contador a esa hora.";
        return camara.EstadoDatos == EstadoAtrasado ? AvisoAtraso(camara) + " " + texto : texto;
    }

    /// <summary>Clase del badge: verde suave al día, rojo atrasado, gris sin datos (clases de Bootstrap 5.3, siguen el tema).</summary>
    public static string ClaseDatosHasta(string estadoDatos) => estadoDatos switch
    {
        EstadoAtrasado => "text-bg-danger",
        EstadoAlDia => "bg-success-subtle text-success-emphasis border border-success-subtle",
        _ => "text-bg-secondary"
    };

    /// <summary>"Sin datos nuevos hace 12 min: revisar la cámara o el servicio de conteo."</summary>
    public static string AvisoAtraso(EficienciaCamaraPuntoDto camara) =>
        $"Sin datos nuevos hace {Minutos(camara.MinutosSinDatosNuevos ?? 0)}: revisar la cámara o el servicio de conteo.";

    public static bool Atrasada(EficienciaCamaraPuntoDto camara) => camara.EstadoDatos == EstadoAtrasado;

    /// <summary>
    /// Hora general del Dashboard: la más vieja de las cámaras al día (todo lo que se ve está completo hasta ahí).
    /// Las atrasadas se avisan aparte para que una cámara caída no arrastre la hora de las demás. Null si no hay
    /// ninguna al día.
    /// </summary>
    public static DateTimeOffset? DatosHastaGeneral(IEnumerable<EficienciaCamaraPuntoDto> camaras) =>
        camaras.Where(c => c.EstadoDatos == EstadoAlDia && c.DatosHasta != null)
            .Select(c => c.DatosHasta!.Value)
            .DefaultIfEmpty()
            .Min() is { } min && min != default ? min : null;

    public static string Numero(decimal valor) => valor.ToString("N0", CultureInfo.CurrentCulture);

    public static string Minutos(int minutos) =>
        minutos < 60 ? $"{minutos} min" : $"{minutos / 60} h {minutos % 60:00} min";
}
