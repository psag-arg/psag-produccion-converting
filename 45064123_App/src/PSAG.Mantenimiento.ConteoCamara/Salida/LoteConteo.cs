using System.Text.Json;
using System.Text.Json.Serialization;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Salida;

/// <summary>
/// Cuerpo de POST api/conteo-camara/ingesta (contrato en docs/CONTEO_CAMARA.md). Lo mismo se guarda en disco
/// mientras la API no responde. Fechas ISO 8601 con offset de la planta. T-798 etapa 4: "camara" y "cantidad" (los
/// lotes viejos que quedaron en pendientes\ con "puntoControl" y "rollos" la API los sigue aceptando).
/// </summary>
public sealed class LoteConteo
{
    /// <summary>Código de la cámara (Maestros → Cámaras).</summary>
    public string Camara { get; set; } = string.Empty;
    public List<MinutoLote> Minutos { get; set; } = [];
    public List<ParadaLote> Paradas { get; set; } = [];

    /// <summary>Paradas que siguen abiertas (la última vista de cada una): la API crea la detención al llegar al umbral.</summary>
    public List<ParadaEnCursoLote> ParadasEnCurso { get; set; } = [];

    /// <summary>Alertas de cámara congelada, sin video o sin configuración (abiertas y cierres).</summary>
    public List<AlertaLote> Alertas { get; set; } = [];

    [JsonIgnore]
    public bool Vacio => Minutos.Count == 0 && Paradas.Count == 0 && ParadasEnCurso.Count == 0 && Alertas.Count == 0;

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

    /// <summary>Unidades del minuto (rollos, mangas o bolsones: la magnitud la da la cámara en Maestros).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>
    /// Sólo lectura de archivos viejos (minuto-en-curso.json con "rollos"): pasa a <see cref="Cantidad"/>. No se escribe.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Rollos
    {
        get => null;
        set { if (value is { } v) Cantidad = v; }
    }

    public int SegundosMarcha { get; set; }
    public int SegundosParada { get; set; }
    public int SegundosAcumulacion { get; set; }
    public int SegundosSinSenal { get; set; }
    public string Estado { get; set; } = string.Empty;

    public static MinutoLote Desde(ConteoMinuto m) => new()
    {
        Minuto = m.Minuto,
        Carril = m.Carril,
        Cantidad = (decimal)m.Rollos,
        SegundosMarcha = m.SegundosMarcha,
        SegundosParada = m.SegundosParada,
        SegundosAcumulacion = m.SegundosAcumulacion,
        SegundosSinSenal = m.SegundosSinSenal,
        Estado = m.Estado.ToString()
    };
}

public sealed class ParadaLote
{
    /// <summary>Cámara + hora de inicio: un reintento no duplica la parada.</summary>
    public string IdentificadorExterno { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }

    /// <summary>Cámara + inicio en UTC. Lo comparten la parada en curso y la cerrada: es la clave de la detención.</summary>
    public static string Identificador(string camara, DateTimeOffset inicio) =>
        $"{camara}-{inicio.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}";

    public static ParadaLote Desde(string puntoControl, ParadaDetectada p) => new()
    {
        IdentificadorExterno = Identificador(puntoControl, p.Inicio),
        Inicio = p.Inicio,
        Fin = p.Fin,
        Tipo = p.Tipo.ToString(),
        Carriles = p.Carriles.ToList(),
        InicioIncierto = p.InicioIncierto,
        FinIncierto = p.FinIncierto
    };
}

public sealed class ParadaEnCursoLote
{
    public string IdentificadorExterno { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Hasta { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public bool InicioIncierto { get; set; }

    public static ParadaEnCursoLote Desde(string puntoControl, ParadaEnCurso p) => new()
    {
        IdentificadorExterno = ParadaLote.Identificador(puntoControl, p.Inicio),
        Inicio = p.Inicio,
        Hasta = p.Hasta,
        Tipo = p.Tipo.ToString(),
        InicioIncierto = p.InicioIncierto
    };
}

public sealed class AlertaLote
{
    public string Tipo { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public string? Detalle { get; set; }

    public static AlertaLote Desde(AlertaVideo a) => new() { Tipo = a.Tipo.ToString(), Inicio = a.Inicio, Fin = a.Fin, Detalle = a.Detalle };
}
