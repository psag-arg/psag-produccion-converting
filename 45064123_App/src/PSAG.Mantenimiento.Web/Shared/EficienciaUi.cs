using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Textos de la tarjeta "Producción por cámara" del Dashboard (ex "Eficiencia (cámara)"). Los números los calcula la
/// API; acá sólo se muestran. T-798 etapa 4: sin objetivo ni % hasta la etapa 5 (objetivo desde el parámetro
/// línea + producto); un turno sin minutos dice "Sin datos", nunca 0.
/// </summary>
public static class EficienciaUi
{
    public const string TextoSinDatos = "Sin datos";

    /// <summary>"9.300" o "Sin datos" si la cámara no mandó ningún minuto en el turno.</summary>
    public static string TextoContadas(EficienciaTurnoDto turno) => turno.SinDatos ? TextoSinDatos : Numero(turno.Contadas);

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
        var nombre = camara.NumeroCanalGrabador is { } n ? $"Cámara {n:00}" : camara.Nombre;
        return $"{camara.LineaNombre} · {nombre} ({camara.Unidad})";
    }

    public static string Numero(decimal valor) => valor.ToString("N0", CultureInfo.CurrentCulture);

    public static string Minutos(int minutos) =>
        minutos < 60 ? $"{minutos} min" : $"{minutos / 60} h {minutos % 60:00} min";
}

/// <summary>Maestros → Cámaras: color y texto del "último dato recibido" (el estado lo calcula la API).</summary>
public static class CamarasUi
{
    public static string ClaseUltimoDato(string estado) => estado switch
    {
        "VERDE" => "bg-success",
        "AMARILLO" => "bg-warning text-dark",
        "ROJO" => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>"hace 3 min", "hace 2 h 05 min" o "nunca".</summary>
    public static string TextoUltimoDato(int? minutos) => minutos switch
    {
        null => "nunca",
        < 1 => "hace menos de 1 min",
        < 60 => $"hace {minutos} min",
        < 60 * 48 => $"hace {minutos / 60} h {minutos % 60:00} min",
        _ => $"hace {minutos / (60 * 24)} días"
    };

    public static string Unidad(string? magnitud) => magnitud switch
    {
        "MANGAS" => "mangas",
        "ROLLOS" => "rollos",
        "PAQUETES" => "paquetes",
        "BOLSONES" => "bolsones",
        "PALLETS" => "pallets",
        _ => "-"
    };
}
