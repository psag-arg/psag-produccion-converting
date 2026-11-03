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

    /// <summary>Ej.: "Tango 2 · Cámara 10 (mangas)".</summary>
    public static string Titulo(EficienciaCamaraPuntoDto camara)
    {
        var nombre = camara.NumeroCamara is { } n ? $"Cámara {n:00}" : camara.Nombre;
        return $"{camara.LineaNombre} · {nombre} ({camara.Unidad})";
    }

    public static string Numero(decimal valor) => valor.ToString("N0", CultureInfo.CurrentCulture);

    public static string Minutos(int minutos) =>
        minutos < 60 ? $"{minutos} min" : $"{minutos / 60} h {minutos % 60:00} min";
}
