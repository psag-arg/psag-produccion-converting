using System.Globalization;
using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

public interface ITareasMantenimientoService
{
    Task<List<TareaMantenimientoDto>> GetActivasAsync(int? lineaId = null, string? criticidad = null, string? estado = null);
    Task<PagedResult<TareaMantenimientoDto>> GetHistorialAsync(TareasHistorialFiltro filtro);
    Task<(TareaMantenimientoDto? Tarea, string? Error)> CrearAsync(CrearTareaMantenimientoRequest request);
    Task<(bool Success, string? Error)> ProgramarAsync(IEnumerable<long> tareaIds);
    Task<(bool Success, string? Error)> CerrarAsync(long tareaId);
}

public class TareasMantenimientoService : ITareasMantenimientoService
{
    private const string Base = "api/tareas-mantenimiento";

    private readonly HttpClient _httpClient;

    public TareasMantenimientoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TareaMantenimientoDto>> GetActivasAsync(int? lineaId = null, string? criticidad = null, string? estado = null)
    {
        var query = new List<string>();
        if (lineaId.HasValue) query.Add($"lineaId={lineaId}");
        if (!string.IsNullOrEmpty(criticidad)) query.Add($"criticidad={Uri.EscapeDataString(criticidad)}");
        if (!string.IsNullOrEmpty(estado)) query.Add($"estado={Uri.EscapeDataString(estado)}");

        var url = Base + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return await _httpClient.GetFromJsonAsync<List<TareaMantenimientoDto>>(url) ?? new List<TareaMantenimientoDto>();
    }

    public async Task<PagedResult<TareaMantenimientoDto>> GetHistorialAsync(TareasHistorialFiltro filtro)
    {
        var query = new List<string> { $"page={filtro.Page}", $"pageSize={filtro.PageSize}" };
        if (filtro.LineaId.HasValue) query.Add($"lineaId={filtro.LineaId}");
        if (filtro.EquipoId.HasValue) query.Add($"equipoId={filtro.EquipoId}");
        if (!string.IsNullOrEmpty(filtro.Criticidad)) query.Add($"criticidad={Uri.EscapeDataString(filtro.Criticidad)}");
        if (!string.IsNullOrEmpty(filtro.TipoTarea)) query.Add($"tipoTarea={Uri.EscapeDataString(filtro.TipoTarea)}");
        if (filtro.FechaDesde.HasValue) query.Add($"fechaDesde={filtro.FechaDesde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filtro.FechaHasta.HasValue) query.Add($"fechaHasta={filtro.FechaHasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

        var url = $"{Base}/historial?" + string.Join("&", query);
        return await _httpClient.GetFromJsonAsync<PagedResult<TareaMantenimientoDto>>(url) ?? new PagedResult<TareaMantenimientoDto>();
    }

    public async Task<(TareaMantenimientoDto? Tarea, string? Error)> CrearAsync(CrearTareaMantenimientoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(Base, request);
        var (success, error) = await ApiErrores.ResultadoAsync(response, "No se pudo guardar la tarea");
        if (!success)
            return (null, error);

        return (await response.Content.ReadFromJsonAsync<TareaMantenimientoDto>(), null);
    }

    public async Task<(bool Success, string? Error)> ProgramarAsync(IEnumerable<long> tareaIds)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/programar",
            new ProgramarTareasRequest { TareaMantenimientoIds = tareaIds.ToList() });
        return await ApiErrores.ResultadoAsync(response, "No se pudieron programar las tareas");
    }

    public async Task<(bool Success, string? Error)> CerrarAsync(long tareaId)
    {
        var response = await _httpClient.PostAsync($"{Base}/{tareaId}/cerrar", null);
        return await ApiErrores.ResultadoAsync(response, "No se pudo cerrar la tarea");
    }
}
