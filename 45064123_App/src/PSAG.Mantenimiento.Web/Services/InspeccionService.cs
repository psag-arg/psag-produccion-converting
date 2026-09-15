using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IInspeccionService
{
    Task<List<PlanInspeccionDto>> GetPlanesAsync(int lineaId, string puesto);
    Task<List<PlanInspeccionDto>> GetPendientesAsync(int lineaId, string puesto);
    Task<PlanInspeccionDto?> GetPlanAsync(int planId);
    Task<IniciarEjecucionResponse?> IniciarEjecucionAsync(IniciarEjecucionRequest request);
    Task<EjecucionInspeccionDto?> GetEjecucionAsync(long ejecucionId);
    Task<(bool Success, string? Error)> GuardarResultadosAsync(GuardarResultadosInspeccionRequest request);
    Task<PagedResult<EjecucionInspeccionDto>> GetEjecucionesAsync(
        int page = 1, int pageSize = 10, int? lineaId = null, int? planId = null,
        DateTime? fechaDesde = null, DateTime? fechaHasta = null, bool? soloConNoOk = null);
    Task<List<HallazgoNoOkDto>> GetHallazgosPendientesAsync(int? lineaId = null);
    Task<PagedResult<HallazgoNoOkDto>> GetHallazgosAsync(
        int page = 1, int pageSize = 10, int? lineaId = null, bool? soloPendientes = null);
    Task<(bool Success, string? Error)> ResolverHallazgoAsync(long hallazgoId, ResolverHallazgoRequest request);
}

public class InspeccionService : IInspeccionService
{
    private readonly HttpClient _httpClient;

    public InspeccionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PlanInspeccionDto>> GetPlanesAsync(int lineaId, string puesto)
    {
        var result = await _httpClient.GetFromJsonAsync<List<PlanInspeccionDto>>(
            $"api/inspecciones/planes?lineaId={lineaId}&puesto={puesto}");
        return result ?? new List<PlanInspeccionDto>();
    }

    public async Task<List<PlanInspeccionDto>> GetPendientesAsync(int lineaId, string puesto)
    {
        var result = await _httpClient.GetFromJsonAsync<List<PlanInspeccionDto>>(
            $"api/inspecciones/pendientes?lineaId={lineaId}&puesto={puesto}");
        return result ?? new List<PlanInspeccionDto>();
    }

    public async Task<PlanInspeccionDto?> GetPlanAsync(int planId)
    {
        return await _httpClient.GetFromJsonAsync<PlanInspeccionDto>($"api/inspecciones/planes/{planId}");
    }

    public async Task<IniciarEjecucionResponse?> IniciarEjecucionAsync(IniciarEjecucionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/inspecciones/ejecuciones", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<IniciarEjecucionResponse>();
        }
        return null;
    }

    public async Task<EjecucionInspeccionDto?> GetEjecucionAsync(long ejecucionId)
    {
        return await _httpClient.GetFromJsonAsync<EjecucionInspeccionDto>($"api/inspecciones/ejecuciones/{ejecucionId}");
    }

    public async Task<(bool Success, string? Error)> GuardarResultadosAsync(GuardarResultadosInspeccionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/inspecciones/ejecuciones/guardar", request);

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return (false, problem?.Detail ?? "Error al guardar resultados");
        }
        catch
        {
            return (false, $"Error {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
    }

    public async Task<PagedResult<EjecucionInspeccionDto>> GetEjecucionesAsync(
        int page = 1, int pageSize = 10, int? lineaId = null, int? planId = null,
        DateTime? fechaDesde = null, DateTime? fechaHasta = null, bool? soloConNoOk = null)
    {
        var url = $"api/inspecciones/ejecuciones?page={page}&pageSize={pageSize}";

        if (lineaId.HasValue) url += $"&lineaId={lineaId}";
        if (planId.HasValue) url += $"&planInspeccionId={planId}";
        if (fechaDesde.HasValue) url += $"&fechaDesde={fechaDesde.Value:yyyy-MM-dd}";
        if (fechaHasta.HasValue) url += $"&fechaHasta={fechaHasta.Value:yyyy-MM-dd}";
        if (soloConNoOk.HasValue) url += $"&soloConNoOk={soloConNoOk}";

        var result = await _httpClient.GetFromJsonAsync<PagedResult<EjecucionInspeccionDto>>(url);
        return result ?? new PagedResult<EjecucionInspeccionDto>();
    }

    public async Task<List<HallazgoNoOkDto>> GetHallazgosPendientesAsync(int? lineaId = null)
    {
        var url = "api/inspecciones/no-ok";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<List<HallazgoNoOkDto>>(url);
        return result ?? new List<HallazgoNoOkDto>();
    }

    public async Task<PagedResult<HallazgoNoOkDto>> GetHallazgosAsync(
        int page = 1, int pageSize = 10, int? lineaId = null, bool? soloPendientes = null)
    {
        var url = $"api/inspecciones/no-ok/paginado?page={page}&pageSize={pageSize}";

        if (lineaId.HasValue) url += $"&lineaId={lineaId}";
        if (soloPendientes.HasValue) url += $"&soloPendientes={soloPendientes}";

        var result = await _httpClient.GetFromJsonAsync<PagedResult<HallazgoNoOkDto>>(url);
        return result ?? new PagedResult<HallazgoNoOkDto>();
    }

    public async Task<(bool Success, string? Error)> ResolverHallazgoAsync(long hallazgoId, ResolverHallazgoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inspecciones/no-ok/{hallazgoId}/resolver", request);

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return (false, problem?.Detail ?? "Error al resolver hallazgo");
        }
        catch
        {
            return (false, $"Error {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
    }
}
