using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface ICatalogosService
{
    // Lineas
    Task<List<LineaDto>> GetLineasAsync();
    Task CreateLineaAsync(LineaDto linea);
    Task UpdateLineaAsync(LineaDto linea);

    // Equipos
    Task<List<EquipoDto>> GetEquiposAsync(int? lineaId = null);
    Task CreateEquipoAsync(EquipoDto equipo);
    Task UpdateEquipoAsync(EquipoDto equipo);

    // Tipos de Falla
    Task<List<TipoFallaDto>> GetTiposFallaAsync();
    Task CreateTipoFallaAsync(TipoFallaDto tipoFalla);
    Task UpdateTipoFallaAsync(TipoFallaDto tipoFalla);

    // Detalles de Falla
    Task<List<DetalleFallaDto>> GetDetallesFallaAsync(int? tipoFallaId = null, int? equipoId = null);
    Task CreateDetalleFallaAsync(DetalleFallaDto detalleFalla);
    Task UpdateDetalleFallaAsync(DetalleFallaDto detalleFalla);

    // Turnos
    Task<List<TurnoDto>> GetTurnosAsync();
    Task CreateTurnoAsync(TurnoDto turno);
    Task UpdateTurnoAsync(TurnoDto turno);

    // Maquinistas
    Task<List<MaquinistaDto>> GetMaquinistasAsync(int? lineaId = null);
    Task<List<MaquinistaDto>> GetMisAsignacionesAsync();
    Task CreateMaquinistaAsync(MaquinistaDto maquinista);
    Task UpdateMaquinistaAsync(MaquinistaDto maquinista);
    Task DeleteMaquinistaAsync(int id);

    // Productos
    Task<List<ProductoDto>> GetProductosAsync(int? lineaId = null);
}

public class CatalogosService : ICatalogosService
{
    private readonly HttpClient _httpClient;

    public CatalogosService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private static async Task EnsureSuccessWithDetailAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            string errorMessage;

            try
            {
                using var doc = JsonDocument.Parse(content);
                errorMessage = doc.RootElement.TryGetProperty("detail", out var detail)
                    ? detail.GetString() ?? response.ReasonPhrase ?? "Error desconocido"
                    : doc.RootElement.TryGetProperty("title", out var title)
                        ? title.GetString() ?? response.ReasonPhrase ?? "Error desconocido"
                        : response.ReasonPhrase ?? "Error desconocido";
            }
            catch
            {
                errorMessage = string.IsNullOrEmpty(content)
                    ? response.ReasonPhrase ?? "Error desconocido"
                    : content;
            }

            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }
    }

    public async Task<List<LineaDto>> GetLineasAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<LineaDto>>("api/catalogos/lineas");
        return result ?? new List<LineaDto>();
    }

    public async Task CreateLineaAsync(LineaDto linea)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/lineas", linea);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task UpdateLineaAsync(LineaDto linea)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/lineas/{linea.LineaId}", linea);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task<List<EquipoDto>> GetEquiposAsync(int? lineaId = null)
    {
        var url = "api/catalogos/equipos";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<List<EquipoDto>>(url);
        return result ?? new List<EquipoDto>();
    }

    public async Task CreateEquipoAsync(EquipoDto equipo)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/equipos", equipo);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task UpdateEquipoAsync(EquipoDto equipo)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/equipos/{equipo.EquipoId}", equipo);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task<List<TipoFallaDto>> GetTiposFallaAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<TipoFallaDto>>("api/catalogos/tipos-falla");
        return result ?? new List<TipoFallaDto>();
    }

    public async Task CreateTipoFallaAsync(TipoFallaDto tipoFalla)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/tipos-falla", tipoFalla);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task UpdateTipoFallaAsync(TipoFallaDto tipoFalla)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/tipos-falla/{tipoFalla.TipoFallaId}", tipoFalla);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task<List<DetalleFallaDto>> GetDetallesFallaAsync(int? tipoFallaId = null, int? equipoId = null)
    {
        var url = "api/catalogos/detalles-falla";
        var queryParams = new List<string>();
        if (tipoFallaId.HasValue) queryParams.Add($"tipoFallaId={tipoFallaId}");
        if (equipoId.HasValue) queryParams.Add($"equipoId={equipoId}");
        if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

        var result = await _httpClient.GetFromJsonAsync<List<DetalleFallaDto>>(url);
        return result ?? new List<DetalleFallaDto>();
    }

    public async Task CreateDetalleFallaAsync(DetalleFallaDto detalleFalla)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/detalles-falla", detalleFalla);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task UpdateDetalleFallaAsync(DetalleFallaDto detalleFalla)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/detalles-falla/{detalleFalla.DetalleFallaId}", detalleFalla);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task<List<TurnoDto>> GetTurnosAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<TurnoDto>>("api/catalogos/turnos");
        return result ?? new List<TurnoDto>();
    }

    public async Task CreateTurnoAsync(TurnoDto turno)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/turnos", turno);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task UpdateTurnoAsync(TurnoDto turno)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/turnos/{turno.TurnoId}", turno);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task<List<MaquinistaDto>> GetMaquinistasAsync(int? lineaId = null)
    {
        var url = "api/catalogos/maquinistas";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<List<MaquinistaDto>>(url);
        return result ?? new List<MaquinistaDto>();
    }

    public async Task<List<MaquinistaDto>> GetMisAsignacionesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<MaquinistaDto>>("api/catalogos/maquinistas/mis-asignaciones");
        return result ?? new List<MaquinistaDto>();
    }

    public async Task CreateMaquinistaAsync(MaquinistaDto maquinista)
    {
        var request = new
        {
            maquinista.UsuarioId,
            maquinista.LineaId,
            maquinista.TurnoId
        };
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/maquinistas", request);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task UpdateMaquinistaAsync(MaquinistaDto maquinista)
    {
        var request = new
        {
            maquinista.LineaId,
            maquinista.TurnoId
        };
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/maquinistas/{maquinista.UsuarioLineaId}", request);
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task DeleteMaquinistaAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/catalogos/maquinistas/{id}");
        await EnsureSuccessWithDetailAsync(response);
    }

    public async Task<List<ProductoDto>> GetProductosAsync(int? lineaId = null)
    {
        var url = "api/catalogos/productos";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<List<ProductoDto>>(url);
        return result ?? new List<ProductoDto>();
    }
}
