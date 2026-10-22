using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Calendario base de horas programadas por línea (qué turnos trabaja cada día de la semana).
/// </summary>
public interface ICalendarioLineaService
{
    Task<List<LineaCalendarioDto>> GetLineasAsync();
    Task<CalendarioLineaDto?> GetCalendarioAsync(int lineaId);
    Task GuardarAsync(int lineaId, DateOnly vigenteDesde, List<CalendarioLineaDiaDto> dias);
    Task DesactivarVersionAsync(int calendarioLineaId);
}

public class CalendarioLineaService : ICalendarioLineaService
{
    private readonly HttpClient _httpClient;

    public CalendarioLineaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<LineaCalendarioDto>> GetLineasAsync() =>
        await _httpClient.GetFromJsonAsync<List<LineaCalendarioDto>>("api/calendario-lineas/lineas") ?? new();

    public Task<CalendarioLineaDto?> GetCalendarioAsync(int lineaId) =>
        _httpClient.GetFromJsonAsync<CalendarioLineaDto>($"api/calendario-lineas/{lineaId}");

    public async Task GuardarAsync(int lineaId, DateOnly vigenteDesde, List<CalendarioLineaDiaDto> dias)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/calendario-lineas/{lineaId}", new { vigenteDesde, dias });
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task DesactivarVersionAsync(int calendarioLineaId)
    {
        var response = await _httpClient.PostAsync($"api/calendario-lineas/versiones/{calendarioLineaId}/desactivar", null);
        await ApiErrores.AsegurarExitoAsync(response);
    }
}
