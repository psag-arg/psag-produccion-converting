namespace PSAG.Mantenimiento.Web.Models;

// T-610: paradas detectadas por la cámara y producción por hora (espejo de los DTO de la API).

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

public class ProduccionCamaraDto
{
    public DateOnly Fecha { get; set; }
    public List<int> Carriles { get; set; } = [];
    public List<ProduccionCamaraHoraDto> Horas { get; set; } = [];
    public decimal TotalRollos { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
}

public class ProduccionCamaraHoraDto
{
    public DateTimeOffset Hora { get; set; }
    public Dictionary<int, decimal> RollosPorCarril { get; set; } = [];
    public decimal TotalRollos { get; set; }
    public int MinutosCarrilMarcha { get; set; }
    public int MinutosCarrilConDatos { get; set; }
}
