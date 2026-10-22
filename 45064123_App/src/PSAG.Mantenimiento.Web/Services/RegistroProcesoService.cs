using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IRegistroProcesoService
{
    Task<PagedResult<RegistroProcesoDto>> GetRegistrosAsync(
        int page = 1,
        int pageSize = 10,
        int? lineaId = null,
        int? productoId = null,
        string? estado = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null);

    Task<RegistroProcesoCompletoDto?> GetRegistroAsync(long id);

    Task<(IniciarRegistroProcesoResponse? Resultado, string? Error)> IniciarRegistroAsync(IniciarRegistroProcesoRequest request);

    Task<(bool Success, string? Error)> GuardarValoresAsync(GuardarValoresRequest request);

    Task<(bool Success, string? Error)> CerrarRegistroAsync(long id, CerrarRegistroProcesoRequest request);

    Task<List<GrupoParametroDto>> GetGruposConEstandaresAsync(int lineaId, int? productoId);
}

public class RegistroProcesoService : IRegistroProcesoService
{
    private readonly HttpClient _httpClient;

    public RegistroProcesoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<RegistroProcesoDto>> GetRegistrosAsync(
        int page = 1,
        int pageSize = 10,
        int? lineaId = null,
        int? productoId = null,
        string? estado = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null)
    {
        var url = $"api/registrosproceso?page={page}&pageSize={pageSize}";

        if (lineaId.HasValue) url += $"&lineaId={lineaId}";
        if (productoId.HasValue) url += $"&productoId={productoId}";
        if (!string.IsNullOrEmpty(estado)) url += $"&estado={estado}";
        if (fechaDesde.HasValue) url += $"&fechaDesde={fechaDesde.Value:yyyy-MM-dd}";
        if (fechaHasta.HasValue) url += $"&fechaHasta={fechaHasta.Value:yyyy-MM-dd}";

        var result = await _httpClient.GetFromJsonAsync<PagedResult<RegistroProcesoDto>>(url);
        return result ?? new PagedResult<RegistroProcesoDto>();
    }

    public async Task<RegistroProcesoCompletoDto?> GetRegistroAsync(long id)
    {
        return await _httpClient.GetFromJsonAsync<RegistroProcesoCompletoDto>($"api/registrosproceso/{id}");
    }

    public async Task<(IniciarRegistroProcesoResponse? Resultado, string? Error)> IniciarRegistroAsync(IniciarRegistroProcesoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/registrosproceso/iniciar", request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<IniciarRegistroProcesoResponse>(), null);
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return (null, problem?.Detail ?? "Error al iniciar el control de proceso.");
        }
        catch
        {
            return (null, $"Error {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
    }

    public async Task<(bool Success, string? Error)> GuardarValoresAsync(GuardarValoresRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/registrosproceso/guardar-valores", request);

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return (false, problem?.Detail ?? "Error al guardar los valores");
        }
        catch
        {
            return (false, $"Error {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
    }

    public async Task<(bool Success, string? Error)> CerrarRegistroAsync(long id, CerrarRegistroProcesoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/registrosproceso/{id}/cerrar", request);

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return (false, problem?.Detail ?? "Error al cerrar el registro");
        }
        catch
        {
            return (false, $"Error {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
    }

    public async Task<List<GrupoParametroDto>> GetGruposConEstandaresAsync(int lineaId, int? productoId)
    {
        var url = productoId.HasValue
            ? $"api/registrosproceso/grupos/{lineaId}/{productoId}"
            : $"api/registrosproceso/grupos/{lineaId}";
        var result = await _httpClient.GetFromJsonAsync<List<GrupoParametroDto>>(url);
        return result ?? new List<GrupoParametroDto>();
    }
}

public class ProblemDetailsDto
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
}
