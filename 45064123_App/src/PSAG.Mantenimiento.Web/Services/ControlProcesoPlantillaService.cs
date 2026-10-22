using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Maestro de la plantilla de Control de Proceso por línea (T-545).
/// </summary>
public interface IControlProcesoPlantillaService
{
    Task<List<GrupoParametroDto>> GetPlantillaAsync(int lineaId);
    Task<(bool Success, string? Error)> CrearGrupoAsync(GuardarGrupoControlRequest request);
    Task<(bool Success, string? Error)> ActualizarGrupoAsync(int grupoParametroId, GuardarGrupoControlRequest request);
    Task<(bool Success, string? Error)> CrearPuntoAsync(GuardarPuntoControlRequest request);
    Task<(bool Success, string? Error)> ActualizarPuntoAsync(int parametroId, GuardarPuntoControlRequest request);
}

public class ControlProcesoPlantillaService : IControlProcesoPlantillaService
{
    private readonly HttpClient _httpClient;

    public ControlProcesoPlantillaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<GrupoParametroDto>> GetPlantillaAsync(int lineaId)
    {
        var result = await _httpClient.GetFromJsonAsync<List<GrupoParametroDto>>($"api/control-proceso/plantilla/{lineaId}");
        return result ?? new List<GrupoParametroDto>();
    }

    public async Task<(bool Success, string? Error)> CrearGrupoAsync(GuardarGrupoControlRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PostAsJsonAsync("api/control-proceso/grupos", request),
            "No se pudo crear el grupo.");

    public async Task<(bool Success, string? Error)> ActualizarGrupoAsync(int grupoParametroId, GuardarGrupoControlRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PutAsJsonAsync($"api/control-proceso/grupos/{grupoParametroId}", request),
            "No se pudo guardar el grupo.");

    public async Task<(bool Success, string? Error)> CrearPuntoAsync(GuardarPuntoControlRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PostAsJsonAsync("api/control-proceso/puntos", request),
            "No se pudo crear el punto de control.");

    public async Task<(bool Success, string? Error)> ActualizarPuntoAsync(int parametroId, GuardarPuntoControlRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PutAsJsonAsync($"api/control-proceso/puntos/{parametroId}", request),
            "No se pudo guardar el punto de control.");
}
