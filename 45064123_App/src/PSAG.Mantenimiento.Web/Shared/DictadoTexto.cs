namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Reglas puras del dictado por voz: cómo se suma lo dictado al campo y qué mensaje ve el usuario ante cada error.
/// </summary>
public static class DictadoTexto
{
    /// <summary>
    /// Agrega lo dictado al final del texto que ya había, separado por un espacio.
    /// Si el campo está vacío o terminaba en punto, la frase nueva empieza con mayúscula.
    /// Respeta el largo máximo del campo (corta lo que sobra).
    /// </summary>
    public static string Agregar(string? actual, string? dictado, int? largoMaximo = null)
    {
        var nuevo = (dictado ?? string.Empty).Trim();
        var previo = actual ?? string.Empty;
        if (nuevo.Length == 0)
        {
            return previo;
        }

        var previoSinEspacios = previo.TrimEnd();
        var empiezaFrase = previoSinEspacios.Length == 0
            || previo.TrimEnd(' ', '\t').EndsWith('\n')
            || ".!?".Contains(previoSinEspacios[^1]);
        if (empiezaFrase)
        {
            nuevo = char.ToUpper(nuevo[0], System.Globalization.CultureInfo.GetCultureInfo("es-AR")) + nuevo[1..];
        }

        var resultado = previoSinEspacios.Length == 0
            ? nuevo
            : previo.EndsWith('\n') ? previo + nuevo : previoSinEspacios + " " + nuevo;

        return largoMaximo is int max && max > 0 && resultado.Length > max ? resultado[..max] : resultado;
    }

    /// <summary>Mensaje claro para el usuario según el código de error de la Web Speech API (o los propios del módulo).</summary>
    public static string MensajeError(string? codigo) => codigo switch
    {
        "not-allowed" or "service-not-allowed" =>
            "El navegador bloqueó el micrófono. Tocá el candado de la barra de direcciones y permití el micrófono para este sitio.",
        "no-speech" => "No se escuchó nada. Tocá Dictar y hablá cerca del micrófono.",
        "audio-capture" => "No se encontró un micrófono en este equipo.",
        "network" => "No hay conexión con el servicio de dictado del navegador. Probá de nuevo o escribilo a mano.",
        "aborted" => "Se cortó el dictado.",
        "language-not-supported" => "El navegador no tiene dictado en castellano.",
        "sin-api" => "Este navegador no tiene dictado por voz. Usá Chrome o Edge.",
        "sin-https" => "El dictado necesita que la app se abra por HTTPS (https://…). Por http:// el navegador no deja usar el micrófono.",
        "sin-local" => "El dictado en el equipo (sin mandar el audio afuera) no está disponible en este navegador.",
        _ => "No se pudo usar el dictado. Escribilo a mano o probá de nuevo."
    };
}
