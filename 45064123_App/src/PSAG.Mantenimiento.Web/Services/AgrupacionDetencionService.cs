using System.Globalization;
using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>Agrupador de detenciones: microcortes previos, agrupar y desagrupar (la validación es del backend).</summary>
public interface IAgrupacionDetencionService
{
    Task<MicrocortesPreviosDto?> GetMicrocortesAsync(long detencionId);
    Task<MicrocortesPreviosDto?> GetMicrocortesAsync(int lineaId, DateOnly fecha, TimeOnly hora);
    Task<AgrupacionDetencionDto?> GetAgrupacionAsync(long detencionId);
    Task<(AgrupacionDetencionDto? Resultado, string? Error)> AgruparAsync(long detencionId, IEnumerable<long> paradaCamaraIds);
    Task<(AgrupacionDetencionDto? Resultado, string? Error)> DesagruparAsync(long detencionId, IEnumerable<long> paradaCamaraIds);
}

public class AgrupacionDetencionService : IAgrupacionDetencionService
{
    private const string Base = "api/detenciones";
    private readonly HttpClient _httpClient;

    public AgrupacionDetencionService(HttpClient httpClient) => _httpClient = httpClient;

    public Task<MicrocortesPreviosDto?> GetMicrocortesAsync(long detencionId) =>
        LeerAsync<MicrocortesPreviosDto>($"{Base}/{detencionId}/microcortes");

    public Task<MicrocortesPreviosDto?> GetMicrocortesAsync(int lineaId, DateOnly fecha, TimeOnly hora) =>
        LeerAsync<MicrocortesPreviosDto>(
            $"{Base}/microcortes?lineaId={lineaId}&fecha={fecha:yyyy-MM-dd}&hora={hora.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}");

    public Task<AgrupacionDetencionDto?> GetAgrupacionAsync(long detencionId) =>
        LeerAsync<AgrupacionDetencionDto>($"{Base}/{detencionId}/agrupacion");

    public Task<(AgrupacionDetencionDto? Resultado, string? Error)> AgruparAsync(long detencionId, IEnumerable<long> paradaCamaraIds) =>
        EnviarAsync($"{Base}/{detencionId}/agrupacion", paradaCamaraIds, "No se pudieron agrupar los microcortes.");

    public Task<(AgrupacionDetencionDto? Resultado, string? Error)> DesagruparAsync(long detencionId, IEnumerable<long> paradaCamaraIds) =>
        EnviarAsync($"{Base}/{detencionId}/agrupacion/desagrupar", paradaCamaraIds, "No se pudieron desagrupar los microcortes.");

    /// <summary>La pregunta de microcortes nunca traba la carga: si falla la consulta, no se muestra.</summary>
    private async Task<T?> LeerAsync<T>(string url) where T : class
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<T>(url);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<(AgrupacionDetencionDto?, string?)> EnviarAsync(string url, IEnumerable<long> ids, string mensajeGenerico)
    {
        var response = await _httpClient.PostAsJsonAsync(url, new AgruparMicrocortesRequest { ParadaCamaraIds = ids.ToList() });
        var (ok, error) = await ApiErrores.ResultadoAsync(response, mensajeGenerico);
        return ok ? (await response.Content.ReadFromJsonAsync<AgrupacionDetencionDto>(), null) : (null, error);
    }
}
