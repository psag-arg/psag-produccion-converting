using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-783: maquinista de cada turno (confirmación del operario después del login y corrección del supervisor).</summary>
public interface IMaquinistaTurnoService
{
    Task<PropuestaMaquinistaTurnoDto?> GetPropuestaAsync();
    Task<(ConfirmacionMaquinistaTurnoDto? Resultado, string? Error)> ConfirmarAsync(int lineaId);
    Task<MaquinistasTurnoDiaDto?> GetDiaAsync(DateOnly fecha);
    Task<(CorreccionMaquinistaTurnoDto? Resultado, string? Error)> CorregirAsync(CorregirMaquinistaTurnoRequest request);
}

public class MaquinistaTurnoService : IMaquinistaTurnoService
{
    private const string Base = "api/maquinistas-turno";
    private readonly HttpClient _httpClient;

    public MaquinistaTurnoService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PropuestaMaquinistaTurnoDto?> GetPropuestaAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PropuestaMaquinistaTurnoDto>($"{Base}/propuesta");
        }
        catch (HttpRequestException)
        {
            return null; // si falla, no se traba el login: se sigue sin preguntar
        }
    }

    public async Task<(ConfirmacionMaquinistaTurnoDto? Resultado, string? Error)> ConfirmarAsync(int lineaId)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/confirmar", new ConfirmarMaquinistaTurnoRequest { LineaId = lineaId });
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudo confirmar el maquinista del turno.");
        return ok ? (await response.Content.ReadFromJsonAsync<ConfirmacionMaquinistaTurnoDto>(), null) : (null, error);
    }

    public async Task<MaquinistasTurnoDiaDto?> GetDiaAsync(DateOnly fecha)
    {
        var response = await _httpClient.GetAsync($"{Base}?fecha={fecha:yyyy-MM-dd}");
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<MaquinistasTurnoDiaDto>();
    }

    public async Task<(CorreccionMaquinistaTurnoDto? Resultado, string? Error)> CorregirAsync(CorregirMaquinistaTurnoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/corregir", request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudo corregir el maquinista del turno.");
        return ok ? (await response.Content.ReadFromJsonAsync<CorreccionMaquinistaTurnoDto>(), null) : (null, error);
    }
}
