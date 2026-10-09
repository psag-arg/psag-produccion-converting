using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IAnalisisService
{
    Task<AnalisisDetencionDto?> GetAnalisisAsync(long detencionId);
    Task<(bool Success, string? Error)> GuardarBorradorAsync(long detencionId, GuardarAnalisisRequest request);
    Task<(bool Success, string? Error)> CerrarAnalisisAsync(long detencionId, GuardarAnalisisRequest request);
    Task<MisPendientesDto> GetMisPendientesAsync(string? responsableRol = null, string? responsableUsuarioId = null);
    Task<int> GetConteoPendientesAsync();
    Task<List<SolucionDto>> BuscarSolucionesAsync(BuscarSolucionesFiltro filtro);

    /// <summary>
    /// Se dispara después de guardar o cerrar un análisis, cuando cambia la bandeja de pendientes.
    /// </summary>
    event Action? PendientesCambiaron;
}

public class AnalisisService : IAnalisisService
{
    private readonly HttpClient _httpClient;

    public event Action? PendientesCambiaron;

    public AnalisisService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AnalisisDetencionDto?> GetAnalisisAsync(long detencionId)
    {
        return await _httpClient.GetFromJsonAsync<AnalisisDetencionDto>($"api/analisis/{detencionId}");
    }

    public async Task<(bool Success, string? Error)> GuardarBorradorAsync(long detencionId, GuardarAnalisisRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/analisis/{detencionId}", request);
        return await ResultadoYAvisarAsync(response, "No se pudo guardar el análisis");
    }

    public async Task<(bool Success, string? Error)> CerrarAnalisisAsync(long detencionId, GuardarAnalisisRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/analisis/{detencionId}/cerrar", request);
        return await ResultadoYAvisarAsync(response, "No se pudo cerrar el análisis");
    }

    public async Task<MisPendientesDto> GetMisPendientesAsync(string? responsableRol = null, string? responsableUsuarioId = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(responsableRol)) query.Add($"responsableRol={Uri.EscapeDataString(responsableRol)}");
        if (!string.IsNullOrEmpty(responsableUsuarioId)) query.Add($"responsableUsuarioId={Uri.EscapeDataString(responsableUsuarioId)}");

        var url = "api/analisis/pendientes" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return await _httpClient.GetFromJsonAsync<MisPendientesDto>(url) ?? new MisPendientesDto();
    }

    public async Task<int> GetConteoPendientesAsync()
    {
        var conteo = await _httpClient.GetFromJsonAsync<ConteoPendientesDto>("api/analisis/pendientes/conteo");
        return conteo?.Total ?? 0;
    }

    private async Task<(bool Success, string? Error)> ResultadoYAvisarAsync(HttpResponseMessage response, string mensajeGenerico)
    {
        var resultado = await ApiErrores.ResultadoAsync(response, mensajeGenerico);
        if (resultado.Success)
        {
            PendientesCambiaron?.Invoke();
        }

        return resultado;
    }

    public async Task<List<SolucionDto>> BuscarSolucionesAsync(BuscarSolucionesFiltro filtro)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda)) query.Add($"busqueda={Uri.EscapeDataString(filtro.Busqueda)}");
        if (filtro.LineaId.HasValue) query.Add($"lineaId={filtro.LineaId}");
        if (filtro.EquipoId.HasValue) query.Add($"equipoId={filtro.EquipoId}");
        if (filtro.TipoFallaId.HasValue) query.Add($"tipoFallaId={filtro.TipoFallaId}");

        var url = "api/analisis/soluciones" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return await _httpClient.GetFromJsonAsync<List<SolucionDto>>(url) ?? new List<SolucionDto>();
    }
}
