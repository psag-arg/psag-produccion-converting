using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface ICatalogosService
{
    // Lineas
    Task<List<LineaDto>> GetLineasAsync(bool incluirInactivas = false);
    Task CreateLineaAsync(LineaDto linea);
    Task UpdateLineaAsync(LineaDto linea);

    // Equipos
    Task<List<EquipoDto>> GetEquiposAsync(int? lineaId = null, bool incluirInactivos = false);
    Task CreateEquipoAsync(EquipoDto equipo);
    Task UpdateEquipoAsync(EquipoDto equipo);

    // Tipos de Falla
    Task<List<TipoFallaDto>> GetTiposFallaAsync(bool incluirInactivos = false);
    Task CreateTipoFallaAsync(TipoFallaDto tipoFalla);
    Task UpdateTipoFallaAsync(TipoFallaDto tipoFalla);

    // Detalles de Falla
    Task<List<DetalleFallaDto>> GetDetallesFallaAsync(int? tipoFallaId = null, int? equipoId = null, bool incluirInactivos = false);
    Task CreateDetalleFallaAsync(DetalleFallaDto detalleFalla);
    Task UpdateDetalleFallaAsync(DetalleFallaDto detalleFalla);

    // Turnos
    Task<List<TurnoDto>> GetTurnosAsync(bool incluirInactivos = false);
    Task CreateTurnoAsync(TurnoDto turno);
    Task UpdateTurnoAsync(TurnoDto turno);

    // Tiempo programado por dia (calendario de excepciones)
    Task<List<DiaProgramadoDto>> GetTiemposProgramadosAsync(int lineaId, DateOnly desde, DateOnly hasta);
    Task GuardarTiempoProgramadoAsync(int lineaId, DateOnly fecha, decimal horas, string? motivo);
    Task DesactivarTiempoProgramadoAsync(int tiempoProgramadoDiaId);

    // Reglas de analisis
    Task<List<ReglaAnalisisDto>> GetReglasAnalisisAsync();
    Task CreateReglaAnalisisAsync(ReglaAnalisisDto regla);
    Task UpdateReglaAnalisisAsync(ReglaAnalisisDto regla);

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

    public async Task<List<LineaDto>> GetLineasAsync(bool incluirInactivas = false)
    {
        var url = incluirInactivas ? "api/catalogos/lineas?soloActivas=false" : "api/catalogos/lineas";
        var result = await _httpClient.GetFromJsonAsync<List<LineaDto>>(url);
        return result ?? new List<LineaDto>();
    }

    public async Task CreateLineaAsync(LineaDto linea)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/lineas", linea);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateLineaAsync(LineaDto linea)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/lineas/{linea.LineaId}", linea);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<EquipoDto>> GetEquiposAsync(int? lineaId = null, bool incluirInactivos = false)
    {
        var queryParams = new List<string>();
        if (lineaId.HasValue) queryParams.Add($"lineaId={lineaId}");
        if (incluirInactivos) queryParams.Add("soloActivos=false");
        var url = "api/catalogos/equipos" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");

        var result = await _httpClient.GetFromJsonAsync<List<EquipoDto>>(url);
        return result ?? new List<EquipoDto>();
    }

    public async Task CreateEquipoAsync(EquipoDto equipo)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/equipos", equipo);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateEquipoAsync(EquipoDto equipo)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/equipos/{equipo.EquipoId}", equipo);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<TipoFallaDto>> GetTiposFallaAsync(bool incluirInactivos = false)
    {
        var url = incluirInactivos ? "api/catalogos/tipos-falla?soloActivos=false" : "api/catalogos/tipos-falla";
        var result = await _httpClient.GetFromJsonAsync<List<TipoFallaDto>>(url);
        return result ?? new List<TipoFallaDto>();
    }

    public async Task CreateTipoFallaAsync(TipoFallaDto tipoFalla)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/tipos-falla", tipoFalla);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateTipoFallaAsync(TipoFallaDto tipoFalla)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/tipos-falla/{tipoFalla.TipoFallaId}", tipoFalla);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<DetalleFallaDto>> GetDetallesFallaAsync(int? tipoFallaId = null, int? equipoId = null, bool incluirInactivos = false)
    {
        var url = "api/catalogos/detalles-falla";
        var queryParams = new List<string>();
        if (tipoFallaId.HasValue) queryParams.Add($"tipoFallaId={tipoFallaId}");
        if (equipoId.HasValue) queryParams.Add($"equipoId={equipoId}");
        if (incluirInactivos) queryParams.Add("soloActivos=false");
        if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

        var result = await _httpClient.GetFromJsonAsync<List<DetalleFallaDto>>(url);
        return result ?? new List<DetalleFallaDto>();
    }

    public async Task CreateDetalleFallaAsync(DetalleFallaDto detalleFalla)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/detalles-falla", detalleFalla);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateDetalleFallaAsync(DetalleFallaDto detalleFalla)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/detalles-falla/{detalleFalla.DetalleFallaId}", detalleFalla);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<TurnoDto>> GetTurnosAsync(bool incluirInactivos = false)
    {
        var url = incluirInactivos ? "api/catalogos/turnos?soloActivos=false" : "api/catalogos/turnos";
        var result = await _httpClient.GetFromJsonAsync<List<TurnoDto>>(url);
        return result ?? new List<TurnoDto>();
    }

    public async Task CreateTurnoAsync(TurnoDto turno)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/turnos", turno);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateTurnoAsync(TurnoDto turno)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/turnos/{turno.TurnoId}", turno);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<DiaProgramadoDto>> GetTiemposProgramadosAsync(int lineaId, DateOnly desde, DateOnly hasta)
    {
        var url = $"api/catalogos/tiempos-programados?lineaId={lineaId}&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        var result = await _httpClient.GetFromJsonAsync<List<DiaProgramadoDto>>(url);
        return result ?? new List<DiaProgramadoDto>();
    }

    public async Task GuardarTiempoProgramadoAsync(int lineaId, DateOnly fecha, decimal horas, string? motivo)
    {
        var request = new { lineaId, fecha, horas, motivo };
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/tiempos-programados", request);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task DesactivarTiempoProgramadoAsync(int tiempoProgramadoDiaId)
    {
        var response = await _httpClient.PostAsync($"api/catalogos/tiempos-programados/{tiempoProgramadoDiaId}/desactivar", null);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<ReglaAnalisisDto>> GetReglasAnalisisAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<ReglaAnalisisDto>>("api/catalogos/reglas-analisis");
        return result ?? new List<ReglaAnalisisDto>();
    }

    public async Task CreateReglaAnalisisAsync(ReglaAnalisisDto regla)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalogos/reglas-analisis", regla);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateReglaAnalisisAsync(ReglaAnalisisDto regla)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/reglas-analisis/{regla.ReglaAnalisisId}", regla);
        await ApiErrores.AsegurarExitoAsync(response);
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
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateMaquinistaAsync(MaquinistaDto maquinista)
    {
        var request = new
        {
            maquinista.LineaId,
            maquinista.TurnoId
        };
        var response = await _httpClient.PutAsJsonAsync($"api/catalogos/maquinistas/{maquinista.UsuarioLineaId}", request);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task DeleteMaquinistaAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/catalogos/maquinistas/{id}");
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<ProductoDto>> GetProductosAsync(int? lineaId = null)
    {
        var url = "api/catalogos/productos";
        if (lineaId.HasValue) url += $"?lineaId={lineaId}";

        var result = await _httpClient.GetFromJsonAsync<List<ProductoDto>>(url);
        return result ?? new List<ProductoDto>();
    }
}
