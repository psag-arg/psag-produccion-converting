using System.Globalization;
using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-610: paradas detectadas por la cámara (con el cruce con detenciones) y producción por hora.</summary>
public interface IConteoCamaraService
{
    Task<ParadasCamaraResultado> GetParadasAsync(DateOnly desde, DateOnly hasta, int? lineaId, bool soloSinDetencion, int segundosMinimos, int? puntoControlCalidadId = null);
    Task<ProduccionCamaraDto> GetProduccionAsync(DateOnly fecha, int? lineaId, int? puntoControlCalidadId = null);

    /// <summary>Alertas de cámara congelada o sin video: abiertas y las cerradas en las últimas horas.</summary>
    Task<List<AlertaCamaraDto>> GetAlertasAsync(int? lineaId = null, int horas = 24);

    /// <summary>Eficiencia por turno de las cámaras que cuentan producción, en un día de planta (Dashboard).</summary>
    Task<EficienciaCamaraDiaDto> GetEficienciaAsync(DateOnly fecha, int? lineaId = null);
}

public class ConteoCamaraService : IConteoCamaraService
{
    private const string Base = "api/conteo-camara";
    private readonly HttpClient _httpClient;

    public ConteoCamaraService(HttpClient httpClient) => _httpClient = httpClient;

    private static string Fecha(DateOnly f) => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task<ParadasCamaraResultado> GetParadasAsync(DateOnly desde, DateOnly hasta, int? lineaId, bool soloSinDetencion, int segundosMinimos, int? puntoControlCalidadId = null)
    {
        var query = new List<string> { $"fechaDesde={Fecha(desde)}", $"fechaHasta={Fecha(hasta)}", $"segundosMinimos={segundosMinimos}" };
        if (lineaId.HasValue) query.Add($"lineaId={lineaId}");
        if (puntoControlCalidadId.HasValue) query.Add($"puntoControlCalidadId={puntoControlCalidadId}");
        if (soloSinDetencion) query.Add("soloSinDetencion=true");
        var response = await _httpClient.GetAsync($"{Base}/paradas?" + string.Join("&", query));
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<ParadasCamaraResultado>() ?? new ParadasCamaraResultado();
    }

    public async Task<ProduccionCamaraDto> GetProduccionAsync(DateOnly fecha, int? lineaId, int? puntoControlCalidadId = null)
    {
        var url = $"{Base}/produccion?fecha={Fecha(fecha)}" + (lineaId.HasValue ? $"&lineaId={lineaId}" : "")
            + (puntoControlCalidadId.HasValue ? $"&puntoControlCalidadId={puntoControlCalidadId}" : "");
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<ProduccionCamaraDto>() ?? new ProduccionCamaraDto();
    }

    public async Task<EficienciaCamaraDiaDto> GetEficienciaAsync(DateOnly fecha, int? lineaId = null)
    {
        var url = $"{Base}/eficiencia?fecha={Fecha(fecha)}" + (lineaId.HasValue ? $"&lineaId={lineaId}" : "");
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<EficienciaCamaraDiaDto>() ?? new EficienciaCamaraDiaDto { Fecha = fecha };
    }

    public async Task<List<AlertaCamaraDto>> GetAlertasAsync(int? lineaId = null, int horas = 24)
    {
        var url = $"{Base}/alertas?horas={horas}" + (lineaId.HasValue ? $"&lineaId={lineaId}" : "");
        var response = await _httpClient.GetAsync(url);
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<List<AlertaCamaraDto>>() ?? [];
    }
}
