using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Textos de la tarjeta "Presupuesto vs. real" del Dashboard (T-798 etapa 5; reemplaza "Producción por cámara").
/// Los números los calcula la API (api/indicadores); acá sólo se muestran. "Sin datos" nunca es 0 y "sin objetivo"
/// nunca es 0 %.
/// </summary>
public static class EficienciaUi
{
    public const string TextoSinDatos = "Sin datos";
    public const string TextoSinObjetivo = "Sin objetivo";

    /// <summary>"9.300", o "Sin datos" si no hay real.</summary>
    public static string TextoReal(ComparacionProduccionDto c) => c.Real is { } r ? Numero(r) : TextoSinDatos;

    /// <summary>"11.200", o "Sin objetivo".</summary>
    public static string TextoObjetivo(ComparacionProduccionDto c) => c.Objetivo is { } o ? Numero(o) : TextoSinObjetivo;

    /// <summary>
    /// T-805 · Con turnos en curso el objetivo de la columna es el de "a la hora" (el del %): se agrega "del turno 11.200".
    /// Null si coincide con el del turno (turnos cerrados) o no hay.
    /// </summary>
    public static string? TextoObjetivoDelTurno(ComparacionProduccionDto c) =>
        c.ObjetivoTurno is { } t && c.Objetivo is { } o && Math.Round(t) != Math.Round(o) ? $"del turno {Numero(t)}" : null;

    /// <summary>"83,0 %" (con "*" si es provisorio), "Sin objetivo" o "Sin datos".</summary>
    public static string TextoPorcentaje(ComparacionProduccionDto c) => c.Estado switch
    {
        "OK" when c.Porcentaje is { } p => p.ToString("N1", CultureInfo.CurrentCulture) + " %" + (c.Provisorio ? " *" : ""),
        "SIN_OBJETIVO" => TextoSinObjetivo,
        _ => TextoSinDatos
    };

    /// <summary>
    /// Color del %: EL semáforo de eficiencia (T-805, <see cref="GraficosUi.NivelEficiencia"/>): rojo &lt; 65, amarillo
    /// 65–85, verde &gt; 85; gris sin dato.
    /// </summary>
    public static string ClasePorcentaje(ComparacionProduccionDto c) => ClaseNivel(TableroUi.NivelEficiencia(c));

    /// <summary>Clase de texto de un nivel del semáforo (gris sin dato).</summary>
    public static string ClaseNivel(TableroUi.Nivel nivel) => nivel switch
    {
        TableroUi.Nivel.Bien => "text-success",
        TableroUi.Nivel.Atencion => "text-warning-emphasis",
        TableroUi.Nivel.Mal => "text-danger",
        _ => "text-muted"
    };

    public static string TextoVelocidad(decimal? metrosMinuto) =>
        metrosMinuto is { } v ? v.ToString("N0", CultureInfo.CurrentCulture) + " m/min" : "-";

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
