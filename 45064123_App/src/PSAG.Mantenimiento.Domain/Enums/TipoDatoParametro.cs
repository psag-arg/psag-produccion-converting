namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Tipo de dato de un parámetro de proceso.
/// Determina cómo se compara el valor real contra el estándar.
/// </summary>
public enum TipoDatoParametro
{
    /// <summary>
    /// Valor numérico puntual (ej: 152, 98.5).
    /// Se compara exactamente o con tolerancia.
    /// </summary>
    NUMERICO = 1,

    /// <summary>
    /// Rango numérico (ej: 3.8 - 4.1 bar).
    /// El valor real debe estar dentro del rango.
    /// </summary>
    RANGO = 2,

    /// <summary>
    /// Valor de texto libre.
    /// No se compara automáticamente.
    /// </summary>
    TEXTO = 3,

    /// <summary>
    /// Punto de control que se responde OK / NO OK (T-545).
    /// NO OK es desvío y pide observación.
    /// </summary>
    OK_NOOK = 4
}

public static class TipoDatoParametroExtensions
{
    public static string ObtenerDescripcion(this TipoDatoParametro tipo)
    {
        return tipo switch
        {
            TipoDatoParametro.NUMERICO => "Numérico",
            TipoDatoParametro.RANGO => "Rango",
            TipoDatoParametro.TEXTO => "Texto",
            TipoDatoParametro.OK_NOOK => "OK / No OK",
            _ => tipo.ToString()
        };
    }

    /// <summary>
    /// NUMERICO y RANGO se cargan como número y se comparan contra mín/máx.
    /// </summary>
    public static bool EsNumerico(this TipoDatoParametro tipo) =>
        tipo is TipoDatoParametro.NUMERICO or TipoDatoParametro.RANGO;
}
