using System.Net;
using System.Text.Json;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Lee los errores que devuelve la API (ProblemDetails) para mostrarlos en pantalla.
/// </summary>
public static class ApiErrores
{
    public const string SinPermiso = "Tu usuario no tiene permiso para hacer esto. Si lo necesitás, pedíselo al administrador.";
    public const string SesionVencida = "Tu sesión venció. Volvé a entrar para seguir.";
    public const string SinConexion = "No hay conexión con el servidor. Probá de nuevo en un rato.";
    public const string DatosInvalidos = "Hay datos con un formato que no se puede leer. Revisalos e intentá de nuevo.";

    /// <summary>
    /// Lanza HttpRequestException con el detalle del error si la respuesta no fue exitosa.
    /// </summary>
    public static async Task AsegurarExitoAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var mensaje = await MensajeAsync(response, "No se pudo completar la operación. Intentá de nuevo.");
        throw new HttpRequestException(mensaje, null, response.StatusCode);
    }

    /// <summary>
    /// Éxito, o el detalle del error de la API. Si la API no envía detalle, usa el texto genérico.
    /// </summary>
    public static async Task<(bool Success, string? Error)> ResultadoAsync(HttpResponseMessage response, string mensajeGenerico)
    {
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        return (false, await MensajeAsync(response, mensajeGenerico));
    }

    /// <summary>
    /// Texto para el usuario: el detail de la API (siempre en castellano) o, si no viene, uno según el código.
    /// El title por defecto de ASP.NET está en inglés, así que no se muestra.
    /// </summary>
    private static async Task<string> MensajeAsync(HttpResponseMessage response, string mensajeGenerico)
    {
        var contenido = await response.Content.ReadAsStringAsync();
        var esValidacionDeFormato = false;
        try
        {
            using var doc = JsonDocument.Parse(contenido);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("detail", out var detail)
                    && detail.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(detail.GetString()))
                {
                    return detail.GetString()!;
                }
                esValidacionDeFormato = doc.RootElement.TryGetProperty("errors", out _);
            }
        }
        catch (JsonException)
        {
            // Sin cuerpo JSON: se decide por el código.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => SinPermiso,
            HttpStatusCode.Unauthorized => SesionVencida,
            HttpStatusCode.BadRequest when esValidacionDeFormato => DatosInvalidos,
            _ => $"{mensajeGenerico} (error {(int)response.StatusCode})"
        };
    }
}
