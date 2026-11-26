using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>Maestro de cámaras (api/camaras, T-798 etapa 4).</summary>
public interface ICamarasService
{
    Task<List<CamaraDto>> GetCamarasAsync(int? lineaId = null, bool incluirInactivas = false);
    Task<(CamaraDto? Camara, string? Error)> GuardarAsync(CamaraDto camara);
    Task<(CamaraDto? Camara, string? Error)> DesactivarAsync(int camaraId);
}

public class CamarasService : ICamarasService
{
    private const string Base = "api/camaras";
    private readonly HttpClient _httpClient;

    public CamarasService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<CamaraDto>> GetCamarasAsync(int? lineaId = null, bool incluirInactivas = false)
    {
        var url = $"{Base}?incluirInactivas={(incluirInactivas ? "true" : "false")}" + (lineaId.HasValue ? $"&lineaId={lineaId}" : "");
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<List<CamaraDto>>() ?? [];
    }

    public async Task<(CamaraDto? Camara, string? Error)> GuardarAsync(CamaraDto c)
    {
        var cuerpo = new
        {
            c.Codigo, c.Nombre, c.LineaId, c.EquipoId, c.NumeroCanalGrabador, c.Funcion, c.Magnitud, c.MetodoDeteccion,
            c.CantidadCarriles, c.DetectaParadas, c.CreaDetenciones, c.UmbralDetencionSegundos, c.CuentaProduccionOficial,
            c.Habilitada, c.Activo
        };
        var response = c.CamaraId == 0
            ? await _httpClient.PostAsJsonAsync(Base, cuerpo)
            : await _httpClient.PutAsJsonAsync($"{Base}/{c.CamaraId}", cuerpo);
        return await LeerAsync(response, "No se pudo guardar la cámara");
    }

    public async Task<(CamaraDto? Camara, string? Error)> DesactivarAsync(int camaraId) =>
        await LeerAsync(await _httpClient.PostAsync($"{Base}/{camaraId}/desactivar", null), "No se pudo desactivar la cámara");

    private static async Task<(CamaraDto? Valor, string? Error)> LeerAsync(HttpResponseMessage response, string mensajeGenerico)
    {
        var (success, error) = await ApiErrores.ResultadoAsync(response, mensajeGenerico);
        return success ? (await response.Content.ReadFromJsonAsync<CamaraDto>(), null) : (default, error);
    }
}
