using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-610: detenciones automáticas de la cámara (pendientes de declaración) y aviso de fin de turno.</summary>
public interface IDeclaracionDetencionService
{
    Task<DeclaracionDetencionDto?> GetAsync(long detencionId);
    Task<(DeclararDetencionResponse? Resultado, string? Error)> DeclararAsync(long detencionId, DeclararDetencionRequest request);
    Task<AvisoFinTurnoDto?> GetAvisoFinTurnoAsync();
}

public class DeclaracionDetencionService : IDeclaracionDetencionService
{
    private const string Base = "api/detenciones";
    private readonly HttpClient _httpClient;

    public DeclaracionDetencionService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<DeclaracionDetencionDto?> GetAsync(long detencionId)
    {
        var response = await _httpClient.GetAsync($"{Base}/{detencionId}/declaracion");
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<DeclaracionDetencionDto>();
    }

    public async Task<(DeclararDetencionResponse? Resultado, string? Error)> DeclararAsync(long detencionId, DeclararDetencionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/{detencionId}/declarar", request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudo declarar la detención.");
        return ok ? (await response.Content.ReadFromJsonAsync<DeclararDetencionResponse>(), null) : (null, error);
    }

    public async Task<AvisoFinTurnoDto?> GetAvisoFinTurnoAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AvisoFinTurnoDto>($"{Base}/aviso-fin-turno");
        }
        catch (HttpRequestException)
        {
            return null; // el aviso nunca molesta: si falla, no se muestra
        }
    }
}
