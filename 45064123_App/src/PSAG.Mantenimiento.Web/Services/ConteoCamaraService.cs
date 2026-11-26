using System.Globalization;
using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-610: paradas detectadas por la cámara (con el cruce con detenciones) y producción por hora.</summary>
public interface IConteoCamaraService
{
    Task<ParadasCamaraResultado> GetParadasAsync(DateOnly desde, DateOnly hasta, int? lineaId, bool soloSinDetencion, int segundosMinimos, int? camaraId = null, bool? superaUmbral = null);
    Task<ProduccionCamaraDto> GetProduccionAsync(DateOnly fecha, int? lineaId, int? camaraId = null);

    /// <summary>Alertas de cámara (congelada, sin video, sin datos, sin configuración): abiertas y las cerradas en las últimas horas.</summary>
    Task<List<AlertaCamaraDto>> GetAlertasAsync(int? lineaId = null, int horas = 24);
}

public class ConteoCamaraService : IConteoCamaraService
{
    private const string Base = "api/conteo-camara";
    private readonly HttpClient _httpClient;

    public ConteoCamaraService(HttpClient httpClient) => _httpClient = httpClient;

    private static string Fecha(DateOnly f) => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task<ParadasCamaraResultado> GetParadasAsync(DateOnly desde, DateOnly hasta, int? lineaId, bool soloSinDetencion, int segundosMinimos, int? camaraId = null, bool? superaUmbral = null)
    {
        var query = new List<string> { $"fechaDesde={Fecha(desde)}", $"fechaHasta={Fecha(hasta)}", $"segundosMinimos={segundosMinimos}" };
        if (lineaId.HasValue) query.Add($"lineaId={lineaId}");
        if (camaraId.HasValue) query.Add($"camaraId={camaraId}");
        if (soloSinDetencion) query.Add("soloSinDetencion=true");
        if (superaUmbral.HasValue) query.Add($"superaUmbral={(superaUmbral.Value ? "true" : "false")}");
        var response = await _httpClient.GetAsync($"{Base}/paradas?" + string.Join("&", query));
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<ParadasCamaraResultado>() ?? new ParadasCamaraResultado();
    }

    public async Task<ProduccionCamaraDto> GetProduccionAsync(DateOnly fecha, int? lineaId, int? camaraId = null)
    {
        var url = $"{Base}/produccion?fecha={Fecha(fecha)}" + (lineaId.HasValue ? $"&lineaId={lineaId}" : "")
            + (camaraId.HasValue ? $"&camaraId={camaraId}" : "");
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<ProduccionCamaraDto>() ?? new ProduccionCamaraDto();
    }

    public async Task<List<AlertaCamaraDto>> GetAlertasAsync(int? lineaId = null, int horas = 24)
    {
        var url = $"{Base}/alertas?horas={horas}" + (lineaId.HasValue ? $"&lineaId={lineaId}" : "");
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<List<AlertaCamaraDto>>() ?? [];
    }
}
