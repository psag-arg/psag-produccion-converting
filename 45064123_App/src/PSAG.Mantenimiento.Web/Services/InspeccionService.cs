using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IInspeccionService
{
    Task<List<PlanInspeccionDto>> GetPlanesAsync(int lineaId, string puesto);
    Task<List<PlanInspeccionDto>> GetPendientesAsync(int lineaId, string puesto);
    Task<PlanInspeccionDto?> GetPlanAsync(int planId);

    /// <summary>Planes activos de la línea con sus puntos y si vencen hoy (Nueva inspección).</summary>
    Task<List<PlanInspeccionDto>> GetPlanesLineaAsync(int lineaId);

    Task<(IniciarEjecucionResponse? Resultado, string? Error)> IniciarEjecucionAsync(IniciarEjecucionRequest request);
    Task<EjecucionInspeccionDto?> GetEjecucionAsync(long ejecucionId);
    Task<(bool Success, string? Error)> GuardarResultadosAsync(GuardarResultadosInspeccionRequest request);
    Task<(bool Success, string? Error)> CerrarEjecucionAsync(long ejecucionId, CerrarEjecucionRequest request);
    Task<PagedResult<EjecucionInspeccionDto>> GetEjecucionesAsync(FiltroInspecciones filtro);
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

    public async Task<List<PlanInspeccionDto>> GetPlanesLineaAsync(int lineaId)
    {
        var result = await _httpClient.GetFromJsonAsync<List<PlanInspeccionDto>>($"api/inspecciones/planes-linea/{lineaId}");
        return result ?? new List<PlanInspeccionDto>();
    }

    public async Task<(IniciarEjecucionResponse? Resultado, string? Error)> IniciarEjecucionAsync(IniciarEjecucionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/inspecciones/ejecuciones", request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<IniciarEjecucionResponse>(), null);
        }

        var (_, error) = await ApiErrores.ResultadoAsync(response, "No se pudo empezar la inspección.");
        return (null, error);
    }

    public async Task<EjecucionInspeccionDto?> GetEjecucionAsync(long ejecucionId)
    {
        return await _httpClient.GetFromJsonAsync<EjecucionInspeccionDto>($"api/inspecciones/ejecuciones/{ejecucionId}");
    }

    public async Task<(bool Success, string? Error)> GuardarResultadosAsync(GuardarResultadosInspeccionRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PostAsJsonAsync("api/inspecciones/ejecuciones/guardar", request),
            "No se pudo guardar la inspección.");

    public async Task<(bool Success, string? Error)> CerrarEjecucionAsync(long ejecucionId, CerrarEjecucionRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PostAsJsonAsync($"api/inspecciones/ejecuciones/{ejecucionId}/cerrar", request),
            "No se pudo cerrar la inspección.");

    public async Task<PagedResult<EjecucionInspeccionDto>> GetEjecucionesAsync(FiltroInspecciones filtro)
    {
        var url = $"api/inspecciones/ejecuciones?page={filtro.Page}&pageSize={filtro.PageSize}";

        if (filtro.LineaId.HasValue) url += $"&lineaId={filtro.LineaId}";
        if (filtro.PlanInspeccionId.HasValue) url += $"&planInspeccionId={filtro.PlanInspeccionId}";
        if (!string.IsNullOrEmpty(filtro.Puesto)) url += $"&puesto={Uri.EscapeDataString(filtro.Puesto)}";
        if (filtro.FrecuenciaDias.HasValue) url += $"&frecuenciaDias={filtro.FrecuenciaDias}";
        if (!string.IsNullOrEmpty(filtro.Estado)) url += $"&estado={filtro.Estado}";
        if (filtro.FechaDesde.HasValue) url += $"&fechaDesde={filtro.FechaDesde.Value:yyyy-MM-dd}";
        if (filtro.FechaHasta.HasValue) url += $"&fechaHasta={filtro.FechaHasta.Value:yyyy-MM-dd}";
        if (filtro.SoloConNoOk) url += "&soloConNoOk=true";

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

    public async Task<(bool Success, string? Error)> ResolverHallazgoAsync(long hallazgoId, ResolverHallazgoRequest request) =>
        await ApiErrores.ResultadoAsync(
            await _httpClient.PostAsJsonAsync($"api/inspecciones/no-ok/{hallazgoId}/resolver", request),
            "Error al resolver hallazgo");
}

/// <summary>
/// Maestro de planes de inspección (Maestros &gt; Planes de inspección).
/// </summary>
public interface IInspeccionPlantillaService
{
    Task<List<PlanInspeccionDto>> GetPlantillaAsync(int lineaId);
    Task<(bool Success, string? Error)> CrearPlanAsync(GuardarPlanInspeccionRequest request);
    Task<(bool Success, string? Error)> ActualizarPlanAsync(int planInspeccionId, GuardarPlanInspeccionRequest request);
    Task<(bool Success, string? Error)> CrearItemAsync(GuardarItemInspeccionRequest request);
    Task<(bool Success, string? Error)> ActualizarItemAsync(int itemPlanInspeccionId, GuardarItemInspeccionRequest request);
}

public class InspeccionPlantillaService : IInspeccionPlantillaService
{
    private readonly HttpClient _httpClient;

    public InspeccionPlantillaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PlanInspeccionDto>> GetPlantillaAsync(int lineaId)
    {
        var result = await _httpClient.GetFromJsonAsync<List<PlanInspeccionDto>>($"api/inspecciones/maestro/plantilla/{lineaId}");
        return result ?? new List<PlanInspeccionDto>();
    }

    public async Task<(bool Success, string? Error)> CrearPlanAsync(GuardarPlanInspeccionRequest request) =>
        await ApiErrores.ResultadoAsync(await _httpClient.PostAsJsonAsync("api/inspecciones/maestro/planes", request), "No se pudo crear el plan.");

    public async Task<(bool Success, string? Error)> ActualizarPlanAsync(int planInspeccionId, GuardarPlanInspeccionRequest request) =>
        await ApiErrores.ResultadoAsync(await _httpClient.PutAsJsonAsync($"api/inspecciones/maestro/planes/{planInspeccionId}", request), "No se pudo guardar el plan.");

    public async Task<(bool Success, string? Error)> CrearItemAsync(GuardarItemInspeccionRequest request) =>
        await ApiErrores.ResultadoAsync(await _httpClient.PostAsJsonAsync("api/inspecciones/maestro/puntos", request), "No se pudo crear el punto.");

    public async Task<(bool Success, string? Error)> ActualizarItemAsync(int itemPlanInspeccionId, GuardarItemInspeccionRequest request) =>
        await ApiErrores.ResultadoAsync(await _httpClient.PutAsJsonAsync($"api/inspecciones/maestro/puntos/{itemPlanInspeccionId}", request), "No se pudo guardar el punto.");
}
