using System.Globalization;
using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-798 etapa 5: presupuesto vs. real (api/indicadores). Reemplaza la "eficiencia por cámara" de la etapa 4.</summary>
public interface IIndicadoresService
{
    /// <summary>Presupuesto vs. real entre dos fechas de producción, agrupado por turno, dia, producto o linea.</summary>
    Task<PresupuestoRealDto> GetProduccionAsync(DateOnly desde, DateOnly hasta, int? lineaId = null, int? productoMaestroId = null, string agrupar = "turno");

    /// <summary>Detalle de un turno de producción (tramos, objetivo, real, eficiencia, paradas que cuentan). Lanza si la API falla.</summary>
    Task<IndicadoresTurnoDto> GetTurnoAsync(long turnoProduccionId);
}

public class IndicadoresService : IIndicadoresService
{
    private readonly HttpClient _httpClient;

    public IndicadoresService(HttpClient httpClient) => _httpClient = httpClient;

    private static string Fecha(DateOnly f) => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task<PresupuestoRealDto> GetProduccionAsync(DateOnly desde, DateOnly hasta, int? lineaId = null, int? productoMaestroId = null, string agrupar = "turno")
    {
        var query = new List<string> { $"desde={Fecha(desde)}", $"hasta={Fecha(hasta)}", $"agrupar={Uri.EscapeDataString(agrupar)}" };
        if (lineaId.HasValue) query.Add($"lineaId={lineaId}");
        if (productoMaestroId.HasValue) query.Add($"productoMaestroId={productoMaestroId}");
        var response = await _httpClient.GetAsync("api/indicadores/produccion?" + string.Join("&", query));
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<PresupuestoRealDto>() ?? new PresupuestoRealDto { Desde = desde, Hasta = hasta };
    }

    public async Task<IndicadoresTurnoDto> GetTurnoAsync(long turnoProduccionId)
    {
        var response = await _httpClient.GetAsync($"api/indicadores/turno/{turnoProduccionId}");
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<IndicadoresTurnoDto>() ?? new IndicadoresTurnoDto();
    }
}
