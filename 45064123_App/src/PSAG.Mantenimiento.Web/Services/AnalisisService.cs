using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Services;

public interface IAnalisisService
{
    Task<AnalisisDetencionDto?> GetAnalisisAsync(long detencionId);
    Task<(bool Success, string? Error)> GuardarBorradorAsync(long detencionId, GuardarAnalisisRequest request);
    Task<(bool Success, string? Error)> CerrarAnalisisAsync(long detencionId, GuardarAnalisisRequest request);
    Task<MisPendientesDto> GetMisPendientesAsync(string? responsableRol = null, string? responsableUsuarioId = null);
    Task<int> GetConteoPendientesAsync();
    Task<SolucionesResultadoDto> BuscarSolucionesAsync(BuscarSolucionesFiltro filtro);

    /// <summary>
    /// Corrige un análisis cerrado (administración y supervisión). Devuelve el análisis actualizado.
    /// </summary>
    Task<(AnalisisDetencionDto? Analisis, string? Error)> EditarCerradoAsync(long detencionId, GuardarAnalisisRequest request);

    /// <summary>
    /// Sube una foto JPG o PNG al análisis.
    /// </summary>
    Task<(AdjuntoAnalisisDto? Adjunto, string? Error)> SubirAdjuntoAsync(long detencionId, IBrowserFile archivo);

    /// <summary>
    /// Baja una foto (con la sesión del usuario) y la devuelve como data URL para mostrarla en un img.
    /// Queda en memoria por AnalisisAdjuntoId mientras dure la sesión: volver a mostrar la misma foto
    /// no la baja de nuevo. Si falla, el próximo pedido la intenta otra vez.
    /// </summary>
    Task<string?> GetAdjuntoDataUrlAsync(long detencionId, long adjuntoId);

    /// <summary>
    /// Se dispara después de guardar o cerrar un análisis, cuando cambia la bandeja de pendientes.
    /// </summary>
    event Action? PendientesCambiaron;
}

public class AnalisisService : IAnalisisService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<long, Task<string?>> _fotos = new();

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

    public async Task<(AnalisisDetencionDto? Analisis, string? Error)> EditarCerradoAsync(long detencionId, GuardarAnalisisRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/analisis/{detencionId}/cerrado", request);
        var (success, error) = await ApiErrores.ResultadoAsync(response, "No se pudo guardar el análisis");
        return success
            ? (await response.Content.ReadFromJsonAsync<AnalisisDetencionDto>(), null)
            : (null, error);
    }

    public async Task<(AdjuntoAnalisisDto? Adjunto, string? Error)> SubirAdjuntoAsync(long detencionId, IBrowserFile archivo)
    {
        // Mismo límite que la API; se avisa antes de mandar el archivo.
        var motivo = AdjuntosAnalisisReglas.MotivoRechazoPrevio(archivo.Name, archivo.Size);
        if (motivo != null)
        {
            return (null, motivo);
        }

        using var contenido = new MultipartFormDataContent();
        var stream = new StreamContent(archivo.OpenReadStream(AdjuntosAnalisisReglas.TamanoMaximoBytes));
        stream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrEmpty(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType);
        contenido.Add(stream, "archivo", archivo.Name);

        var response = await _httpClient.PostAsync($"api/analisis/{detencionId}/adjuntos", contenido);
        if (response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
        {
            return (null, $"La foto {archivo.Name} es demasiado grande. El máximo es 10 MB por foto.");
        }

        var (success, error) = await ApiErrores.ResultadoAsync(response, $"No se pudo subir {archivo.Name}");
        return success
            ? (await response.Content.ReadFromJsonAsync<AdjuntoAnalisisDto>(), null)
            : (null, $"{archivo.Name}: {error}");
    }

    public async Task<string?> GetAdjuntoDataUrlAsync(long detencionId, long adjuntoId)
    {
        // Los adjuntos no se modifican: el id alcanza como clave. Se guarda la tarea para que dos
        // componentes que piden la misma foto a la vez compartan una sola descarga.
        if (!_fotos.TryGetValue(adjuntoId, out var tarea))
        {
            tarea = BajarAdjuntoDataUrlAsync(detencionId, adjuntoId);
            _fotos[adjuntoId] = tarea;
        }

        try
        {
            var dataUrl = await tarea;
            if (dataUrl == null)
            {
                _fotos.Remove(adjuntoId);
            }

            return dataUrl;
        }
        catch
        {
            _fotos.Remove(adjuntoId);
            throw;
        }
    }

    private async Task<string?> BajarAdjuntoDataUrlAsync(long detencionId, long adjuntoId)
    {
        var response = await _httpClient.GetAsync($"api/analisis/{detencionId}/adjuntos/{adjuntoId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tipo = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return $"data:{tipo};base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<SolucionesResultadoDto> BuscarSolucionesAsync(BuscarSolucionesFiltro filtro)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda)) query.Add($"busqueda={Uri.EscapeDataString(filtro.Busqueda)}");
        if (filtro.LineaId.HasValue) query.Add($"lineaId={filtro.LineaId}");
        if (filtro.EquipoId.HasValue) query.Add($"equipoId={filtro.EquipoId}");
        if (filtro.TipoFallaId.HasValue) query.Add($"tipoFallaId={filtro.TipoFallaId}");

        var url = "api/analisis/soluciones" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return await _httpClient.GetFromJsonAsync<SolucionesResultadoDto>(url) ?? new SolucionesResultadoDto();
    }
}
