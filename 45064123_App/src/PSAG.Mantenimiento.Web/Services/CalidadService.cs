using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Services;

public interface ICalidadService
{
    Task<PagedResult<RegistroCalidadDto>> GetRegistrosAsync(RegistrosCalidadFiltro filtro);
    Task<RegistroCalidadDto?> GetRegistroAsync(long id);
    Task<(RegistroCalidadDto? Registro, string? Error)> RevisarAsync(long id, string estado, string? comentario, byte[] rowVersion);
    Task<(RegistroCalidadDto? Registro, string? Error)> VincularDetencionAsync(long id, string? codigoDetencion);
    Task<(RegistroCalidadDto? Registro, string? Error)> CrearManualAsync(NuevoRegistroCalidadManual datos, IBrowserFile? imagen);

    /// <summary>
    /// Baja la imagen con la sesión del usuario y la devuelve como data URL. Queda en memoria por registro
    /// (las imágenes no cambian). Si falla, el próximo pedido la intenta otra vez.
    /// </summary>
    Task<string?> GetImagenDataUrlAsync(long id);

    Task<List<PuntoControlCalidadDto>> GetPuntosControlAsync(int? lineaId = null, bool incluirInactivos = false);
    Task<(PuntoControlCalidadDto? Punto, string? Error)> GuardarPuntoControlAsync(PuntoControlCalidadDto punto);
    Task<List<TipoDefectoCalidadDto>> GetTiposDefectoAsync(bool incluirInactivos = false);
    Task<(TipoDefectoCalidadDto? Tipo, string? Error)> GuardarTipoDefectoAsync(TipoDefectoCalidadDto tipo);
}

public class CalidadService : ICalidadService
{
    private const string Base = "api/calidad";

    private readonly HttpClient _httpClient;
    private readonly Dictionary<long, Task<string?>> _imagenes = new();

    public CalidadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<RegistroCalidadDto>> GetRegistrosAsync(RegistrosCalidadFiltro filtro)
    {
        var query = new List<string> { $"page={filtro.Page}", $"pageSize={filtro.PageSize}" };
        if (filtro.FechaDesde.HasValue) query.Add($"fechaDesde={filtro.FechaDesde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filtro.FechaHasta.HasValue) query.Add($"fechaHasta={filtro.FechaHasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filtro.TurnoId.HasValue) query.Add($"turnoId={filtro.TurnoId}");
        if (filtro.LineaId.HasValue) query.Add($"lineaId={filtro.LineaId}");
        if (filtro.PuntoControlCalidadId.HasValue) query.Add($"puntoControlCalidadId={filtro.PuntoControlCalidadId}");
        if (filtro.TipoDefectoCalidadId.HasValue) query.Add($"tipoDefectoCalidadId={filtro.TipoDefectoCalidadId}");
        if (!string.IsNullOrEmpty(filtro.EstadoRevision)) query.Add($"estadoRevision={Uri.EscapeDataString(filtro.EstadoRevision)}");
        if (!string.IsNullOrEmpty(filtro.Origen)) query.Add($"origen={Uri.EscapeDataString(filtro.Origen)}");

        var response = await _httpClient.GetAsync($"{Base}/registros?" + string.Join("&", query));
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<PagedResult<RegistroCalidadDto>>() ?? new PagedResult<RegistroCalidadDto>();
    }

    public async Task<RegistroCalidadDto?> GetRegistroAsync(long id) =>
        await _httpClient.GetFromJsonAsync<RegistroCalidadDto>($"{Base}/registros/{id}");

    public async Task<(RegistroCalidadDto? Registro, string? Error)> RevisarAsync(long id, string estado, string? comentario, byte[] rowVersion)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/registros/{id}/revision",
            new { estado, comentario, rowVersion });
        return await LeerAsync<RegistroCalidadDto>(response, "No se pudo guardar la revisión");
    }

    public async Task<(RegistroCalidadDto? Registro, string? Error)> VincularDetencionAsync(long id, string? codigoDetencion)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/registros/{id}/detencion", new { codigoDetencion });
        return await LeerAsync<RegistroCalidadDto>(response, "No se pudo relacionar la detención");
    }

    public async Task<(RegistroCalidadDto? Registro, string? Error)> CrearManualAsync(NuevoRegistroCalidadManual datos, IBrowserFile? imagen)
    {
        if (imagen != null)
        {
            var motivo = AdjuntosAnalisisReglas.MotivoRechazoPrevio(imagen.Name, imagen.Size);
            if (motivo != null)
                return (null, motivo);
        }

        using var contenido = new MultipartFormDataContent();
        void Campo(string nombre, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
                contenido.Add(new StringContent(valor), nombre);
        }

        Campo("puntoControlCalidadId", datos.PuntoControlCalidadId.ToString(CultureInfo.InvariantCulture));
        Campo("tipoDefectoCalidadId", datos.TipoDefectoCalidadId.ToString(CultureInfo.InvariantCulture));
        // Sin offset: la API la toma como hora de la planta.
        Campo("fechaHora", datos.FechaHora.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        Campo("turnoId", datos.TurnoId?.ToString(CultureInfo.InvariantCulture));
        Campo("valorMedido", datos.ValorMedido?.ToString(CultureInfo.InvariantCulture));
        Campo("unidadMedida", datos.UnidadMedida);
        Campo("productoId", datos.ProductoId?.ToString(CultureInfo.InvariantCulture));
        Campo("codigoDetencion", datos.CodigoDetencion);
        Campo("observaciones", datos.Observaciones);

        if (imagen != null)
        {
            var stream = new StreamContent(imagen.OpenReadStream(AdjuntosAnalisisReglas.TamanoMaximoBytes));
            stream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrEmpty(imagen.ContentType) ? "application/octet-stream" : imagen.ContentType);
            contenido.Add(stream, "archivo", imagen.Name);
        }

        var response = await _httpClient.PostAsync($"{Base}/registros", contenido);
        if (response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
            return (null, "La imagen es demasiado grande. El máximo es 10 MB.");

        return await LeerAsync<RegistroCalidadDto>(response, "No se pudo guardar el registro");
    }

    public async Task<string?> GetImagenDataUrlAsync(long id)
    {
        if (!_imagenes.TryGetValue(id, out var tarea))
        {
            tarea = BajarImagenAsync(id);
            _imagenes[id] = tarea;
        }

        try
        {
            var dataUrl = await tarea;
            if (dataUrl == null)
                _imagenes.Remove(id);
            return dataUrl;
        }
        catch
        {
            _imagenes.Remove(id);
            throw;
        }
    }

    private async Task<string?> BajarImagenAsync(long id)
    {
        var response = await _httpClient.GetAsync($"{Base}/registros/{id}/imagen");
        if (!response.IsSuccessStatusCode)
            return null;

        var tipo = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return $"data:{tipo};base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<List<PuntoControlCalidadDto>> GetPuntosControlAsync(int? lineaId = null, bool incluirInactivos = false)
    {
        var query = new List<string> { $"incluirInactivos={(incluirInactivos ? "true" : "false")}" };
        if (lineaId.HasValue) query.Add($"lineaId={lineaId}");
        return await _httpClient.GetFromJsonAsync<List<PuntoControlCalidadDto>>($"{Base}/puntos-control?" + string.Join("&", query))
            ?? new List<PuntoControlCalidadDto>();
    }

    public async Task<(PuntoControlCalidadDto? Punto, string? Error)> GuardarPuntoControlAsync(PuntoControlCalidadDto punto)
    {
        var cuerpo = new { punto.LineaId, punto.Codigo, punto.Nombre, punto.Descripcion, punto.Activo };
        var response = punto.PuntoControlCalidadId == 0
            ? await _httpClient.PostAsJsonAsync($"{Base}/puntos-control", cuerpo)
            : await _httpClient.PutAsJsonAsync($"{Base}/puntos-control/{punto.PuntoControlCalidadId}", cuerpo);
        return await LeerAsync<PuntoControlCalidadDto>(response, "No se pudo guardar el punto de control");
    }

    public async Task<List<TipoDefectoCalidadDto>> GetTiposDefectoAsync(bool incluirInactivos = false) =>
        await _httpClient.GetFromJsonAsync<List<TipoDefectoCalidadDto>>(
            $"{Base}/tipos-defecto?incluirInactivos={(incluirInactivos ? "true" : "false")}") ?? new List<TipoDefectoCalidadDto>();

    public async Task<(TipoDefectoCalidadDto? Tipo, string? Error)> GuardarTipoDefectoAsync(TipoDefectoCalidadDto tipo)
    {
        var cuerpo = new { tipo.Codigo, tipo.Nombre, tipo.Descripcion, tipo.UnidadMedida, tipo.Activo };
        var response = tipo.TipoDefectoCalidadId == 0
            ? await _httpClient.PostAsJsonAsync($"{Base}/tipos-defecto", cuerpo)
            : await _httpClient.PutAsJsonAsync($"{Base}/tipos-defecto/{tipo.TipoDefectoCalidadId}", cuerpo);
        return await LeerAsync<TipoDefectoCalidadDto>(response, "No se pudo guardar el tipo de defecto");
    }

    private static async Task<(T? Valor, string? Error)> LeerAsync<T>(HttpResponseMessage response, string mensajeGenerico)
    {
        var (success, error) = await ApiErrores.ResultadoAsync(response, mensajeGenerico);
        return success ? (await response.Content.ReadFromJsonAsync<T>(), null) : (default, error);
    }
}
