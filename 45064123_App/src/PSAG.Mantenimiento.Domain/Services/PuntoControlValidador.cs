using System.Globalization;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Rango contra el que se compara un punto de control numérico. Cualquiera de los dos extremos puede faltar.
/// </summary>
public readonly record struct RangoControl(decimal? Min, decimal? Max)
{
    public bool TieneLimites => Min.HasValue || Max.HasValue;

    public bool Contiene(decimal valor) =>
        (!Min.HasValue || valor >= Min.Value) && (!Max.HasValue || valor <= Max.Value);

    /// <summary>
    /// Rango de un estándar por producto. Siempre inclusivo (los extremos son OK):
    /// <list type="bullet">
    /// <item>mín y máx → entre mín y máx ("152 - 153");</item>
    /// <item>mín y tolerancia → mín ± tolerancia; tolerancia 0 = valor exacto;</item>
    /// <item>solo mín → piso abierto, ≥ mín ("&gt; 10mm" de la planilla de Tango 2);</item>
    /// <item>sin mín → sin límites: el ValorEstandarTexto es una referencia ("202") que se muestra y no marca desvío.</item>
    /// </list>
    /// </summary>
    public static RangoControl DesdeEstandar(EstandarParametro estandar)
    {
        if (!estandar.ValorMin.HasValue)
            return new RangoControl(null, null);
        if (estandar.ValorMax.HasValue)
            return new RangoControl(estandar.ValorMin, estandar.ValorMax);
        if (estandar.Tolerancia.HasValue)
            return new RangoControl(estandar.ValorMin - estandar.Tolerancia, estandar.ValorMin + estandar.Tolerancia);
        return new RangoControl(estandar.ValorMin, null);
    }
}

/// <summary>
/// Resultado de validar lo que cargó el maquinista en un punto de control.
/// </summary>
public sealed record ResultadoPuntoControl(
    string? Error,
    bool SinValor,
    bool FueraDeEstandar,
    decimal? ValorReal,
    string? ValorRealTexto)
{
    public bool EsValido => Error == null;
}

/// <summary>
/// Reglas de la carga de Control de Proceso (T-545). Funciones puras: la API las aplica al guardar,
/// la UI solo las anticipa. El desvío (FueraDeEstandar) lo decide siempre el backend.
/// </summary>
public static class PuntoControlValidador
{
    public const string Ok = "OK";
    public const string NoOk = "NO OK";
    public const int LargoTexto = 200;        // RegistrosProcesoDetalle.ValorRealTexto
    public const int LargoObservacion = 1000; // RegistrosProcesoDetalle.Observacion

    /// <summary>
    /// Valida un valor cargado según el tipo de dato del punto. Sin valor no es error:
    /// el control puede quedar parcial y completarse en el mismo turno.
    /// </summary>
    public static ResultadoPuntoControl ValidarCarga(
        TipoDatoParametro tipo,
        RangoControl rango,
        decimal? valorReal,
        string? valorRealTexto,
        string? observacion)
    {
        var texto = string.IsNullOrWhiteSpace(valorRealTexto) ? null : valorRealTexto.Trim();

        if (observacion != null && observacion.Length > LargoObservacion)
            return Error($"La observación no puede superar {LargoObservacion} caracteres.");

        bool fuera;
        decimal? numero = null;
        string? textoFinal = null;

        if (tipo.EsNumerico())
        {
            if (texto != null)
                return Error("Este punto se carga con un número.");
            if (!valorReal.HasValue)
                return SinValor();

            numero = valorReal.Value;
            fuera = !rango.Contiene(numero.Value);
        }
        else if (tipo == TipoDatoParametro.OK_NOOK)
        {
            if (valorReal.HasValue)
                return Error("Este punto se carga con OK o NO OK, no con un número.");
            if (texto == null)
                return SinValor();

            textoFinal = NormalizarOkNoOk(texto);
            if (textoFinal == null)
                return Error("Este punto solo admite OK o NO OK.");
            fuera = textoFinal == NoOk;
        }
        else // TEXTO
        {
            if (valorReal.HasValue)
                return Error("Este punto se carga con texto.");
            if (texto == null)
                return SinValor();
            if (texto.Length > LargoTexto)
                return Error($"El texto no puede superar {LargoTexto} caracteres.");

            textoFinal = texto;
            fuera = false;
        }

        if (fuera && string.IsNullOrWhiteSpace(observacion))
            return Error(tipo == TipoDatoParametro.OK_NOOK
                ? "Está NO OK: la observación es obligatoria."
                : $"Está fuera de rango ({DescribirRango(rango, null)}): la observación es obligatoria.");

        return new ResultadoPuntoControl(null, false, fuera, numero, textoFinal);
    }

    /// <summary>
    /// "OK", "ok", "NO OK", "no_ok", "NOOK" → "OK" / "NO OK". Cualquier otra cosa → null.
    /// </summary>
    public static string? NormalizarOkNoOk(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var compacto = new string(valor.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        return compacto switch
        {
            "OK" => Ok,
            "NOOK" => NoOk,
            _ => null
        };
    }

    /// <summary>
    /// Motivo por el que la definición de un punto no se puede guardar, o null si es válida (maestro).
    /// </summary>
    public static string? MotivoDefinicionInvalida(string? nombre, TipoDatoParametro tipo, decimal? min, decimal? max)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return "El nombre del punto de control es obligatorio.";
        if (!Enum.IsDefined(tipo))
            return "El tipo de dato no es válido.";
        if (!tipo.EsNumerico() && (min.HasValue || max.HasValue))
            return "El mínimo y el máximo solo aplican a puntos numéricos.";
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            return "El mínimo no puede ser mayor que el máximo.";
        return null;
    }

    /// <summary>
    /// Texto del estándar que se guarda como snapshot en el detalle (ej. "3,8 - 4,1 bar", "≥ 10 m/min", "OK").
    /// </summary>
    public static string? DescribirEstandar(TipoDatoParametro tipo, RangoControl rango, string? unidad)
    {
        if (tipo == TipoDatoParametro.OK_NOOK)
            return Ok;
        if (!tipo.EsNumerico() || !rango.TieneLimites)
            return null;
        return DescribirRango(rango, unidad);
    }

    private static string DescribirRango(RangoControl rango, string? unidad)
    {
        var ar = CultureInfo.GetCultureInfo("es-AR");
        string F(decimal v) => v.ToString("0.####", ar);

        var texto = (rango.Min, rango.Max) switch
        {
            ({ } a, { } b) when a == b => F(a),
            ({ } a, { } b) => $"{F(a)} - {F(b)}",
            ({ } a, null) => $"≥ {F(a)}",
            (null, { } b) => $"≤ {F(b)}",
            _ => "sin rango"
        };
        return string.IsNullOrWhiteSpace(unidad) ? texto : $"{texto} {unidad.Trim()}";
    }

    private static ResultadoPuntoControl Error(string mensaje) => new(mensaje, false, false, null, null);

    private static ResultadoPuntoControl SinValor() => new(null, true, false, null, null);
}
