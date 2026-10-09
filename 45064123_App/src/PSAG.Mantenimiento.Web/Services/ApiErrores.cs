using System.Net.Http.Json;
using System.Text.Json;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Lee los errores que devuelve la API (ProblemDetails) para mostrarlos en pantalla.
/// </summary>
public static class ApiErrores
{
    /// <summary>
    /// Lanza HttpRequestException con el detalle del error si la respuesta no fue exitosa.
    /// </summary>
    public static async Task AsegurarExitoAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        string errorMessage;

        try
        {
            using var doc = JsonDocument.Parse(content);
            errorMessage = doc.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString() ?? response.ReasonPhrase ?? "Error desconocido"
                : doc.RootElement.TryGetProperty("title", out var title)
                    ? title.GetString() ?? response.ReasonPhrase ?? "Error desconocido"
                    : response.ReasonPhrase ?? "Error desconocido";
        }
        catch
        {
            errorMessage = string.IsNullOrEmpty(content)
                ? response.ReasonPhrase ?? "Error desconocido"
                : content;
        }

        throw new HttpRequestException(errorMessage, null, response.StatusCode);
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

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return (false, problem?.Detail ?? mensajeGenerico);
        }
        catch
        {
            return (false, $"{mensajeGenerico} (error {(int)response.StatusCode})");
        }
    }
}
