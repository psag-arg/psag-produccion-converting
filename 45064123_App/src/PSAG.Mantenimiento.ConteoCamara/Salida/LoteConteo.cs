using System.Text.Json;
using System.Text.Json.Serialization;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Salida;

/// <summary>
/// Cuerpo de POST api/conteo-camara/ingesta (contrato en docs/CONTEO_CAMARA.md). Lo mismo se guarda en disco
/// mientras la API no responde. Fechas ISO 8601 con offset de la planta.
/// </summary>
public sealed class LoteConteo
{
    public string PuntoControl { get; set; } = string.Empty;
    public List<MinutoLote> Minutos { get; set; } = [];
    public List<ParadaLote> Paradas { get; set; } = [];

    [JsonIgnore]
    public bool Vacio => Minutos.Count == 0 && Paradas.Count == 0;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class MinutoLote
{
    public DateTimeOffset Minuto { get; set; }
    public int Carril { get; set; }
    public decimal Rollos { get; set; }
    public int SegundosMarcha { get; set; }
    public int SegundosParada { get; set; }
    public int SegundosAcumulacion { get; set; }
    public int SegundosSinSenal { get; set; }
    public string Estado { get; set; } = string.Empty;

    public static MinutoLote Desde(ConteoMinuto m) => new()
    {
        Minuto = m.Minuto,
        Carril = m.Carril,
        Rollos = (decimal)m.Rollos,
        SegundosMarcha = m.SegundosMarcha,
        SegundosParada = m.SegundosParada,
        SegundosAcumulacion = m.SegundosAcumulacion,
        SegundosSinSenal = m.SegundosSinSenal,
        Estado = m.Estado.ToString()
    };
}

public sealed class ParadaLote
{
    /// <summary>Punto + hora de inicio: un reintento no duplica la parada.</summary>
    public string IdentificadorExterno { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }

    public static ParadaLote Desde(string puntoControl, ParadaDetectada p) => new()
    {
        IdentificadorExterno = $"{puntoControl}-{p.Inicio.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}",
        Inicio = p.Inicio,
        Fin = p.Fin,
        Tipo = p.Tipo.ToString(),
        Carriles = p.Carriles.ToList(),
        InicioIncierto = p.InicioIncierto,
        FinIncierto = p.FinIncierto
    };
}
