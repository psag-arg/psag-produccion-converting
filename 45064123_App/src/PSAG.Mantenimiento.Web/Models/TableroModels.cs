namespace PSAG.Mantenimiento.Web.Models;

// T-770 · Tablero del operador (GET api/tablero/turno): espejo de los DTO de la API. Los números los calcula la API.

public class TableroTurnoDto
{
    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly FechaProduccion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Abierto { get; set; }
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }
    public string? ProductoNombre { get; set; }
    public DateTimeOffset? ProductoDesde { get; set; }
    public string? OperadorUsuarioId { get; set; }
    public string? OperadorNombre { get; set; }
    public bool SoloLectura { get; set; }
    public DateTimeOffset GeneradoEn { get; set; }
    public IndicadoresTurnoDto Indicadores { get; set; } = new();
    public List<MangasHoraDto> MangasPorHora { get; set; } = [];
    public ResumenParadasTableroDto DetencionesReales { get; set; } = new();
    public ResumenParadasTableroDto Microcortes { get; set; } = new();
    public List<DetencionTableroDto> Detenciones { get; set; } = [];
    public List<PendienteDeclaracionDto> PendientesDeclarar { get; set; } = [];
    public CamaraTableroDto Camara { get; set; } = new();
}

public class MangasHoraDto
{
    public DateTimeOffset Hora { get; set; }
    public decimal? Objetivo { get; set; }
    public decimal Real { get; set; }
    public int MinutosConDatos { get; set; }
    public bool EnCurso { get; set; }
}

public class ResumenParadasTableroDto
{
    public int Cantidad { get; set; }
    public int Segundos { get; set; }
    public int Minutos { get; set; }
    public List<ParadaCamaraDto> Items { get; set; } = [];
}

public class DetencionTableroDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranque { get; set; }
    public int MinutosEfectivos { get; set; }
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public string? TipoFallaNombre { get; set; }
    public string OrigenRegistro { get; set; } = string.Empty;
    public bool Cuenta { get; set; }
    public bool PendienteDeclaracion { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public class CamaraTableroDto
{
    public bool TieneCamara { get; set; }
    public DateTimeOffset? UltimoDato { get; set; }
    public int? MinutosDesdeUltimoDato { get; set; }
    public bool SinDatos { get; set; }
    public List<AlertaCamaraDto> Alertas { get; set; } = [];
}
