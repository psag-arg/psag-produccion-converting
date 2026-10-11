using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IDetencionesService
{
    Task<PagedResult<DetencionDto>> GetDetencionesAsync(int page = 1, int pageSize = 10, int? lineaId = null, string? estado = null);
    Task<PagedResult<DetencionDto>> GetDetencionesConFiltrosAsync(DetencionFiltroRequest filtro);
    Task<byte[]?> ExportarExcelAsync(DetencionFiltroRequest filtro);
    Task<DetencionDetalleDto?> GetDetencionAsync(long id);
    Task<(CrearDetencionResponse? Detencion, string? Error)> CrearDetencionAsync(CrearDetencionRequest request);
    Task<(bool Success, string? Error)> ActualizarDetencionAsync(long id, ActualizarDetencionRequest request);
    Task<bool> PrevisualizarAnalisisAsync(CrearDetencionRequest request);
    Task<bool> EliminarDetencionAsync(long id, string motivo);
    Task<bool> RestaurarDetencionAsync(long id);
    Task<(bool Success, string? Error)> CerrarDetencionAsync(long id, CerrarDetencionRequest request);
    Task<List<DetencionDto>> GetDetencionesAbiertasAsync(int? lineaId = null);
    Task<ResumenTurnoResponse> GetResumenTurnoAsync(int? lineaId = null);
}

public class DetencionFiltroRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }
    public int? TurnoId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int? MinutosMinimo { get; set; }
    public string? Busqueda { get; set; }
    public bool? AnalisisPendiente { get; set; }
    public bool VerEliminadas { get; set; }
}

public class DetencionesService : IDetencionesService
{
    private readonly HttpClient _httpClient;

    public DetencionesService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<DetencionDto>> GetDetencionesAsync(int page = 1, int pageSize = 10, int? lineaId = null, string? estado = null)
    {
        var url = $"api/detenciones?page={page}&pageSize={pageSize}";
        if (lineaId.HasValue) url += $"&lineaId={lineaId}";
        if (!string.IsNullOrEmpty(estado)) url += $"&estado={estado}";

        var result = await _httpClient.GetFromJsonAsync<PagedResult<DetencionDto>>(url);
        return result ?? new PagedResult<DetencionDto>();
    }

    public async Task<PagedResult<DetencionDto>> GetDetencionesConFiltrosAsync(DetencionFiltroRequest filtro)
    {
        var url = $"api/detenciones?{ConstruirQuery(filtro)}";
        var result = await _httpClient.GetFromJsonAsync<PagedResult<DetencionDto>>(url);
        return result ?? new PagedResult<DetencionDto>();
    }

    public async Task<byte[]?> ExportarExcelAsync(DetencionFiltroRequest filtro)
    {
        var response = await _httpClient.GetAsync($"api/detenciones/export/xlsx?{ConstruirQuery(filtro)}");
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
    }

    public async Task<DetencionDetalleDto?> GetDetencionAsync(long id)
    {
        return await _httpClient.GetFromJsonAsync<DetencionDetalleDto>($"api/detenciones/{id}");
    }

    private const string ErrorGuardar = "No se pudo guardar la detención. Revisá los datos e intentá de nuevo.";

    public async Task<(CrearDetencionResponse? Detencion, string? Error)> CrearDetencionAsync(CrearDetencionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/detenciones", request);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<CrearDetencionResponse>(), null);
        }
        var (_, error) = await ApiErrores.ResultadoAsync(response, ErrorGuardar);
        return (null, error);
    }

    public async Task<(bool Success, string? Error)> ActualizarDetencionAsync(long id, ActualizarDetencionRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/detenciones/{id}", request);
        return await ApiErrores.ResultadoAsync(response, ErrorGuardar);
    }

    public async Task<bool> PrevisualizarAnalisisAsync(CrearDetencionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/detenciones/previsualizar-analisis", request);
        if (!response.IsSuccessStatusCode) return false;

        var resultado = await response.Content.ReadFromJsonAsync<PrevisualizacionAnalisisResponse>();
        return resultado?.RequiereAnalisis ?? false;
    }

    public async Task<bool> EliminarDetencionAsync(long id, string motivo)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/detenciones/{id}/eliminar", new EliminarDetencionRequest { Motivo = motivo });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RestaurarDetencionAsync(long id)
    {
        var response = await _httpClient.PostAsync($"api/detenciones/{id}/restaurar", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? Error)> CerrarDetencionAsync(long id, CerrarDetencionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/detenciones/{id}/cerrar", request);
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return (false, "No se pudo cerrar la detención: sólo Mantenimiento o un administrador pueden cerrarla.");
        }
        return await ApiErrores.ResultadoAsync(response, "No se pudo cerrar la detención. Probá de nuevo.");
    }

    public async Task<List<DetencionDto>> GetDetencionesAbiertasAsync(int? lineaId = null)
    {
        var url = "api/detenciones/abiertas";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<List<DetencionDto>>(url);
        return result ?? new List<DetencionDto>();
    }

    public async Task<ResumenTurnoResponse> GetResumenTurnoAsync(int? lineaId = null)
    {
        var url = "api/detenciones/resumen-turno";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<ResumenTurnoResponse>(url);
        return result ?? new ResumenTurnoResponse();
    }

    private static string ConstruirQuery(DetencionFiltroRequest filtro)
    {
        var queryParams = new List<string>
        {
            $"page={filtro.Page}",
            $"pageSize={filtro.PageSize}"
        };

        if (filtro.LineaId.HasValue) queryParams.Add($"lineaId={filtro.LineaId}");
        if (filtro.EquipoId.HasValue) queryParams.Add($"equipoId={filtro.EquipoId}");
        if (filtro.TipoFallaId.HasValue) queryParams.Add($"tipoFallaId={filtro.TipoFallaId}");
        if (filtro.DetalleFallaId.HasValue) queryParams.Add($"detalleFallaId={filtro.DetalleFallaId}");
        if (filtro.TurnoId.HasValue) queryParams.Add($"turnoId={filtro.TurnoId}");
        if (filtro.FechaDesde.HasValue) queryParams.Add($"fechaDesde={filtro.FechaDesde.Value:yyyy-MM-dd}");
        if (filtro.FechaHasta.HasValue) queryParams.Add($"fechaHasta={filtro.FechaHasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrEmpty(filtro.Estado)) queryParams.Add($"estado={filtro.Estado}");
        if (!string.IsNullOrEmpty(filtro.Busqueda)) queryParams.Add($"busqueda={Uri.EscapeDataString(filtro.Busqueda)}");
        if (filtro.AnalisisPendiente.HasValue) queryParams.Add($"analisisPendiente={filtro.AnalisisPendiente.Value.ToString().ToLowerInvariant()}");
        if (filtro.VerEliminadas) queryParams.Add("verEliminadas=true");

        return string.Join("&", queryParams);
    }
}
