namespace PSAG.Mantenimiento.Application.DTOs.ConteoCamara;

// ===================== Ingesta (servicio de conteo, T-610) =====================

/// <summary>
/// Lote que manda el servicio de conteo cada ~30 s (JSON). Contrato en docs/CONTEO_CAMARA.md.
/// </summary>
public class IngestaConteoCamaraRequest
{
    /// <summary>Código del punto de control (Maestros → Calidad), ej.: T2-CAM09. Define la línea.</summary>
    public string PuntoControl { get; set; } = string.Empty;
    public List<MinutoCamaraIngesta> Minutos { get; set; } = [];
    public List<ParadaCamaraIngesta> Paradas { get; set; } = [];
}

public class MinutoCamaraIngesta
{
    public DateTimeOffset Minuto { get; set; }
    public int Carril { get; set; }
    public decimal Rollos { get; set; }
    public int SegundosMarcha { get; set; }
    public int SegundosParada { get; set; }
    public int SegundosAcumulacion { get; set; }
    public int SegundosSinSenal { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ParadaCamaraIngesta
{
    public string IdentificadorExterno { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }
}

public class IngestaConteoCamaraRespuesta
{
    public int MinutosNuevos { get; set; }
    public int MinutosDuplicados { get; set; }
    public int ParadasNuevas { get; set; }
    public int ParadasDuplicadas { get; set; }
}

// ===================== Consultas =====================

/// <summary>Filtro del listado de paradas por cámara (días de planta).</summary>
public class ParadasCamaraFiltroRequest
{
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int? LineaId { get; set; }

    /// <summary>Sólo las que no tienen detención cargada (SIN_DETENCION).</summary>
    public bool SoloSinDetencion { get; set; }

    /// <summary>Duración mínima en segundos (para ocultar microparadas). 0 = todas.</summary>
    public int SegundosMinimos { get; set; }
}

public class ParadaCamaraDto
{
    public long ParadaCamaraId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string PuntoControlCodigo { get; set; } = string.Empty;
    public string? TurnoNombre { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Carriles { get; set; } = string.Empty;
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }

    /// <summary>CON_DETENCION, POSIBLE o SIN_DETENCION (calculado al consultar).</summary>
    public string Cruce { get; set; } = string.Empty;
    public List<string> CodigosDetencion { get; set; } = [];
}

public class ParadasCamaraResultado
{
    public List<ParadaCamaraDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int SinDetencion { get; set; }
    public int MinutosSinDetencion { get; set; }
}

/// <summary>Producción de un día de planta según la cámara, por hora y carril.</summary>
public class ProduccionCamaraDto
{
    public DateOnly Fecha { get; set; }
    public int? LineaId { get; set; }
    public List<int> Carriles { get; set; } = [];
    public List<ProduccionCamaraHoraDto> Horas { get; set; } = [];
    public decimal TotalRollos { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
}

public class ProduccionCamaraHoraDto
{
    /// <summary>Inicio de la hora, en hora de la planta.</summary>
    public DateTimeOffset Hora { get; set; }
    public Dictionary<int, decimal> RollosPorCarril { get; set; } = [];
    public decimal TotalRollos { get; set; }

    /// <summary>Minutos-carril en marcha / con datos (4 carriles x 60 min = 240 en una hora completa).</summary>
    public int MinutosCarrilMarcha { get; set; }
    public int MinutosCarrilConDatos { get; set; }
}
