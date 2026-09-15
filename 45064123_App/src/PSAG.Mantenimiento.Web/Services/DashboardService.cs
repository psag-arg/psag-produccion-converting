using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IDashboardService
{
    Task<DashboardCompletoDto?> GetDashboardAsync(int? lineaId = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    Task<IndisponibilidadReporteDto?> GetIndisponibilidadAsync(int? lineaId = null, int? anio = null);
    Task<FallosMayoresReporteDto?> GetFallosMayoresAsync(int? lineaId = null, int? anio = null, int umbralMinutos = 60);
    Task<ParetoDetencionesReporteDto?> GetParetoAsync(int? lineaId = null, int? turnoId = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
}

public class DashboardService : IDashboardService
{
    private readonly HttpClient _httpClient;

    public DashboardService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DashboardCompletoDto?> GetDashboardAsync(
        int? lineaId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null)
    {
        var url = "api/dashboard";
        var queryParams = new List<string>();

        if (lineaId.HasValue)
            queryParams.Add($"lineaId={lineaId}");

        if (fechaDesde.HasValue)
            queryParams.Add($"fechaDesde={fechaDesde.Value:yyyy-MM-dd}");

        if (fechaHasta.HasValue)
            queryParams.Add($"fechaHasta={fechaHasta.Value:yyyy-MM-dd}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<DashboardCompletoDto>(url);
    }

    public async Task<IndisponibilidadReporteDto?> GetIndisponibilidadAsync(
        int? lineaId = null,
        int? anio = null)
    {
        var url = "api/dashboard/indisponibilidad";
        var queryParams = new List<string>();

        if (lineaId.HasValue)
            queryParams.Add($"lineaId={lineaId}");

        queryParams.Add($"anio={anio ?? DateTime.Today.Year}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<IndisponibilidadReporteDto>(url);
    }

    public async Task<FallosMayoresReporteDto?> GetFallosMayoresAsync(
        int? lineaId = null,
        int? anio = null,
        int umbralMinutos = 60)
    {
        var url = "api/dashboard/fallos-mayores";
        var queryParams = new List<string>();

        if (lineaId.HasValue)
            queryParams.Add($"lineaId={lineaId}");

        queryParams.Add($"anio={anio ?? DateTime.Today.Year}");
        queryParams.Add($"umbralMinutos={umbralMinutos}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<FallosMayoresReporteDto>(url);
    }

    public async Task<ParetoDetencionesReporteDto?> GetParetoAsync(
        int? lineaId = null,
        int? turnoId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null)
    {
        var url = "api/dashboard/pareto";
        var queryParams = new List<string>();

        if (lineaId.HasValue)
            queryParams.Add($"lineaId={lineaId}");

        if (turnoId.HasValue)
            queryParams.Add($"turnoId={turnoId}");

        if (fechaDesde.HasValue)
            queryParams.Add($"fechaDesde={fechaDesde.Value:yyyy-MM-dd}");

        if (fechaHasta.HasValue)
            queryParams.Add($"fechaHasta={fechaHasta.Value:yyyy-MM-dd}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<ParetoDetencionesReporteDto>(url);
    }
}
