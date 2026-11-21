using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-798 etapa 2: turno de producción (inicio de turno del operario y pantalla del supervisor).</summary>
public interface ITurnosProduccionService
{
    /// <summary>Qué preguntar después del login. Null si la API falla (no se traba el login).</summary>
    Task<PropuestaTurnoProduccionDto?> GetPropuestaAsync();
    Task<(AperturaTurnoProduccionDto? Resultado, string? Error)> AbrirAsync(AbrirTurnoProduccionRequest request);
    Task<TurnosProduccionDiaDto?> GetDiaAsync(DateOnly fecha, int? lineaId = null);
    Task<(TurnoProduccionDto? Resultado, string? Error)> CambiarProductoAsync(long id, CambiarProductoTurnoRequest request);
    Task<(TurnoProduccionDto? Resultado, string? Error)> CerrarAsync(long id, CerrarTurnoProduccionRequest request);
    Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirOperadorAsync(long id, CorregirOperadorTurnoRequest request);
    Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirProductoAsync(long id, CorregirProductoTurnoRequest request);
}

public class TurnosProduccionService : ITurnosProduccionService
{
    private const string Base = "api/turnos-produccion";
    private readonly HttpClient _httpClient;

    public TurnosProduccionService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PropuestaTurnoProduccionDto?> GetPropuestaAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PropuestaTurnoProduccionDto>($"{Base}/propuesta");
        }
        catch (Exception)
        {
            return null; // si falla la API, se sigue sin preguntar: no se bloquea el login
        }
    }

    public async Task<(AperturaTurnoProduccionDto? Resultado, string? Error)> AbrirAsync(AbrirTurnoProduccionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/abrir", request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudo tomar el turno.");
        return ok ? (await response.Content.ReadFromJsonAsync<AperturaTurnoProduccionDto>(), null) : (null, error);
    }

    public async Task<TurnosProduccionDiaDto?> GetDiaAsync(DateOnly fecha, int? lineaId = null)
    {
        var url = $"{Base}?fecha={fecha:yyyy-MM-dd}" + (lineaId is { } l ? $"&lineaId={l}" : string.Empty);
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<TurnosProduccionDiaDto>();
    }

    public Task<(TurnoProduccionDto? Resultado, string? Error)> CambiarProductoAsync(long id, CambiarProductoTurnoRequest request) =>
        PostTurnoAsync($"{Base}/{id}/cambiar-producto", request, "No se pudo cambiar el producto.");

    public Task<(TurnoProduccionDto? Resultado, string? Error)> CerrarAsync(long id, CerrarTurnoProduccionRequest request) =>
        PostTurnoAsync($"{Base}/{id}/cerrar", request, "No se pudo cerrar el turno.");

    public Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirOperadorAsync(long id, CorregirOperadorTurnoRequest request) =>
        PostTurnoAsync($"{Base}/{id}/corregir-operador", request, "No se pudo corregir el operador.");

    public Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirProductoAsync(long id, CorregirProductoTurnoRequest request) =>
        PostTurnoAsync($"{Base}/{id}/corregir-producto", request, "No se pudo corregir el producto.");

    private async Task<(TurnoProduccionDto? Resultado, string? Error)> PostTurnoAsync<T>(string url, T request, string mensaje)
    {
        var response = await _httpClient.PostAsJsonAsync(url, request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, mensaje);
        return ok ? (await response.Content.ReadFromJsonAsync<TurnoProduccionDto>(), null) : (null, error);
    }
}
