using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-798 etapa 6: Maestros → Productos (con SKU) y Parámetros línea + producto.</summary>
public interface IMaestrosProduccionService
{
    Task<List<ProductoMaestroDto>> GetProductosAsync(bool incluirInactivos = false);
    Task<(ProductoMaestroDto? Valor, string? Error)> GuardarProductoAsync(ProductoMaestroDto producto);
    Task<(ProductoSkuDto? Valor, string? Error)> GuardarSkuAsync(ProductoSkuDto sku);
    Task<List<ParametroLineaProductoDto>> GetParametrosAsync(int? lineaId = null);
    Task<(ParametroLineaProductoDto? Valor, string? Error)> GuardarParametroAsync(ParametroLineaProductoDto parametro);
}

public class MaestrosProduccionService : IMaestrosProduccionService
{
    private const string Productos = "api/productos-maestro";
    private const string Parametros = "api/parametros-linea-producto";
    private readonly HttpClient _httpClient;

    public MaestrosProduccionService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<ProductoMaestroDto>> GetProductosAsync(bool incluirInactivos = false)
    {
        var response = await _httpClient.GetAsync($"{Productos}?incluirInactivos={(incluirInactivos ? "true" : "false")}");
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ProductoMaestroDto>>() ?? [];
    }

    public async Task<(ProductoMaestroDto? Valor, string? Error)> GuardarProductoAsync(ProductoMaestroDto p)
    {
        var cuerpo = new { p.Codigo, p.Nombre, p.Descripcion, p.Orden, p.TipoProducto, p.MetrosPorManga, p.RollosPorManga, p.RollosPorPaquete, p.Activo };
        var response = p.ProductoMaestroId == 0
            ? await _httpClient.PostAsJsonAsync(Productos, cuerpo)
            : await _httpClient.PutAsJsonAsync($"{Productos}/{p.ProductoMaestroId}", cuerpo);
        return await LeerAsync<ProductoMaestroDto>(response, "No se pudo guardar el producto");
    }

    public async Task<(ProductoSkuDto? Valor, string? Error)> GuardarSkuAsync(ProductoSkuDto s)
    {
        var cuerpo = new { s.ProductoMaestroId, s.CodigoSku, s.Descripcion, s.TipoSku, s.PaquetesPorBolson, s.SkuContenidoId, s.BolsonesPorPallet, s.Activo };
        var response = s.ProductoSkuId == 0
            ? await _httpClient.PostAsJsonAsync($"{Productos}/skus", cuerpo)
            : await _httpClient.PutAsJsonAsync($"{Productos}/skus/{s.ProductoSkuId}", cuerpo);
        return await LeerAsync<ProductoSkuDto>(response, "No se pudo guardar el SKU");
    }

    public async Task<List<ParametroLineaProductoDto>> GetParametrosAsync(int? lineaId = null)
    {
        var response = await _httpClient.GetAsync(Parametros + (lineaId is { } l ? $"?lineaId={l}" : ""));
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ParametroLineaProductoDto>>() ?? [];
    }

    public async Task<(ParametroLineaProductoDto? Valor, string? Error)> GuardarParametroAsync(ParametroLineaProductoDto p)
    {
        var cuerpo = new
        {
            p.LineaId, p.ProductoMaestroId, VigenteDesde = p.VigenteDesde.ToString("yyyy-MM-dd"), p.Habilitado,
            p.MangasObjetivoTurno, p.BolsonesObjetivoTurno, p.MinutosTurnoReferencia
        };
        var response = p.ParametroLineaProductoId == 0
            ? await _httpClient.PostAsJsonAsync(Parametros, cuerpo)
            : await _httpClient.PutAsJsonAsync($"{Parametros}/{p.ParametroLineaProductoId}", cuerpo);
        return await LeerAsync<ParametroLineaProductoDto>(response, "No se pudo guardar el parámetro");
    }

    private static async Task<(T? Valor, string? Error)> LeerAsync<T>(HttpResponseMessage response, string mensajeGenerico)
    {
        var (ok, error) = await ApiErrores.ResultadoAsync(response, mensajeGenerico);
        return ok ? (await response.Content.ReadFromJsonAsync<T>(), null) : (default, error);
    }
}
