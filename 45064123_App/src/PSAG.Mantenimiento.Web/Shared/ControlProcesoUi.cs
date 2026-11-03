using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Reglas de pantalla del Control de Proceso que se pueden probar sin navegador:
/// números con coma (es-AR) desde el teclado numérico del celular y el estado de cada punto.
/// La API es la que decide el desvío al guardar; esto solo lo anticipa mientras se carga.
/// </summary>
public static class ControlProcesoUi
{
    public const string Ok = "OK";
    public const string FueraDeRango = "FUERA_DE_RANGO";
    public const string SinEstandar = "SIN_ESTANDAR";
    public const string Informativo = "INFORMATIVO";
    public const string SinValor = "SIN_VALOR";

    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>
    /// Lee lo que tipeó el maquinista. Acepta coma o punto como separador decimal ("0,7", "0.7", "-0,7", "152"),
    /// porque el teclado numérico del celular trae uno u otro según el equipo. No acepta miles ("1.234,5")
    /// ni dos separadores: es ambiguo y mejor que lo corrija.
    /// </summary>
    public static bool TryLeerNumero(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpio = texto.Trim().Replace(" ", string.Empty).Replace('−', '-'); // signo menos tipográfico
        if (limpio.Count(c => c is ',' or '.') > 1)
        {
            return false;
        }

        limpio = limpio.Replace(',', '.');
        if (limpio.StartsWith('.') || limpio.StartsWith("-."))
        {
            limpio = limpio.Replace(".", "0.");
        }

        return decimal.TryParse(limpio, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>Número como se muestra en la planilla: coma decimal y sin ceros de más (0,7 / -200 / 98,5).</summary>
    public static string MostrarNumero(decimal? valor) =>
        valor?.ToString("0.####", Ar) ?? string.Empty;

    /// <summary>
    /// El punto admite negativos (estándar "-0,7", "-200" o rango con mínimo negativo): se muestra el botón ± porque
    /// el teclado numérico de algunos celulares no trae el signo menos.
    /// </summary>
    public static bool AdmiteNegativo(RegistroProcesoDetalleDto d) =>
        d.ValorMin < 0 || d.ValorMax < 0 || (d.ValorEstandarSnapshot?.TrimStart().StartsWith('-') ?? false);

    /// <summary>
    /// Estado del punto mientras se carga (antes de que responda la API). Misma regla que el backend:
    /// numérico sin rango (sin estándar o solo una referencia sin tolerancia) = falta estándar, nunca OK.
    /// </summary>
    public static string EvaluarLocal(RegistroProcesoDetalleDto d)
    {
        if (ControlProcesoCatalogo.EsNumerico(d.TipoDato))
        {
            if (!d.ValorReal.HasValue) return SinValor;
            var tieneRango = d.ValorMin.HasValue || d.ValorMax.HasValue;
            if (!tieneRango) return SinEstandar;
            var fuera = (d.ValorMin.HasValue && d.ValorReal < d.ValorMin) || (d.ValorMax.HasValue && d.ValorReal > d.ValorMax);
            return fuera ? FueraDeRango : Ok;
        }

        if (string.IsNullOrWhiteSpace(d.ValorRealTexto)) return SinValor;
        if (d.TipoDato == ControlProcesoCatalogo.OkNoOk)
        {
            return d.ValorRealTexto == ControlProcesoCatalogo.NoOk ? FueraDeRango : Ok;
        }

        return Informativo;
    }

    /// <summary>Fuera de rango y sin observación: la API lo rechaza, así que el autoguardado lo espera.</summary>
    public static bool FaltaObservacion(RegistroProcesoDetalleDto d) =>
        EvaluarLocal(d) == FueraDeRango && string.IsNullOrWhiteSpace(d.Observacion);

    public static bool TieneValor(RegistroProcesoDetalleDto d) =>
        d.ValorReal.HasValue || !string.IsNullOrWhiteSpace(d.ValorRealTexto);
}
