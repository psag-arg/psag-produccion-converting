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

    /// <summary>Turno automático: "Terminar mi turno" (cierra la sesión del operador; el turno de máquina sigue hasta su hora).</summary>
    Task<(TurnoProduccionDto? Resultado, string? Error)> TerminarMiTurnoAsync(long id);
    Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirOperadorAsync(long id, CorregirOperadorTurnoRequest request);
    Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirProductoAsync(long id, CorregirProductoTurnoRequest request);

    /// <summary>Turno abierto de la línea (o el de la hora actual); null si no hay (204). Lanza si la API falla.</summary>
    Task<TurnoProduccionDto?> GetActualAsync(int lineaId);

    /// <summary>Producción oficial de un tramo: VALIDAR, CORREGIR o MANUAL (supervisor).</summary>
    Task<(ProduccionTurnoDto? Resultado, string? Error)> RegistrarProduccionAsync(long id, RegistrarProduccionTurnoRequest request);
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

    public async Task<(TurnoProduccionDto? Resultado, string? Error)> TerminarMiTurnoAsync(long id)
    {
        var response = await _httpClient.PostAsync($"{Base}/{id}/terminar", null);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudo terminar tu turno.");
        return ok ? (await response.Content.ReadFromJsonAsync<TurnoProduccionDto>(), null) : (null, error);
    }

    public Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirOperadorAsync(long id, CorregirOperadorTurnoRequest request) =>
        PostTurnoAsync($"{Base}/{id}/corregir-operador", request, "No se pudo corregir el operador.");

    public Task<(TurnoProduccionDto? Resultado, string? Error)> CorregirProductoAsync(long id, CorregirProductoTurnoRequest request) =>
        PostTurnoAsync($"{Base}/{id}/corregir-producto", request, "No se pudo corregir el producto.");

    public async Task<TurnoProduccionDto?> GetActualAsync(int lineaId)
    {
        var response = await _httpClient.GetAsync($"{Base}/actual?lineaId={lineaId}");
        await ApiErrores.AsegurarExitoAsync(response);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        return await response.Content.ReadFromJsonAsync<TurnoProduccionDto>();
    }

    public async Task<(ProduccionTurnoDto? Resultado, string? Error)> RegistrarProduccionAsync(long id, RegistrarProduccionTurnoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/{id}/produccion", request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudo registrar la producción.");
        return ok ? (await response.Content.ReadFromJsonAsync<ProduccionTurnoDto>(), null) : (null, error);
    }

    private async Task<(TurnoProduccionDto? Resultado, string? Error)> PostTurnoAsync<T>(string url, T request, string mensaje)
    {
        var response = await _httpClient.PostAsJsonAsync(url, request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, mensaje);
        return ok ? (await response.Content.ReadFromJsonAsync<TurnoProduccionDto>(), null) : (null, error);
    }
}
