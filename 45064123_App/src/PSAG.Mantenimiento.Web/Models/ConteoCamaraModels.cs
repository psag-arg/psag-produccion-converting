namespace PSAG.Mantenimiento.Web.Models;

// T-610: paradas detectadas por la cámara y producción por hora (espejo de los DTO de la API).

public class ParadaCamaraDto
{
    public long ParadaCamaraId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int CamaraId { get; set; }
    public string CamaraCodigo { get; set; } = string.Empty;
    public long? TurnoProduccionId { get; set; }
    public string? TurnoNombre { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Carriles { get; set; } = string.Empty;
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }
    public bool SuperaUmbral { get; set; }
    public bool CreaDetencion { get; set; }

    /// <summary>Umbral efectivo de la cámara que arma la API ("5 min", "45 s").</summary>
    public string? UmbralTexto { get; set; }
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

/// <summary>Producción de un día de planta, una entrada por cámara (cada una en su magnitud: no se suman).</summary>
public class ProduccionCamaraDto
{
    public DateOnly Fecha { get; set; }
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public List<ProduccionCamaraPuntoDto> Camaras { get; set; } = [];
}

public class ProduccionCamaraPuntoDto
{
    public int CamaraId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string? Magnitud { get; set; }
    public bool CuentaProduccionOficial { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public List<ProduccionCamaraHoraDto> Horas { get; set; } = [];
    public decimal Total { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
    public List<ProduccionCamaraTurnoDto> Turnos { get; set; } = [];
}

public class ProduccionCamaraTurnoDto
{
    public long? TurnoProduccionId { get; set; }
    public int? TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset? Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public decimal Total { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
}

public class AlertaCamaraDto
{
    public long AlertaCamaraId { get; set; }
    public int CamaraId { get; set; }
    public string CamaraCodigo { get; set; } = string.Empty;
    public string CamaraNombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public bool Activa { get; set; }
    public string? Detalle { get; set; }
}

// T-610: declaración de las detenciones automáticas.

public class DeclaracionDetencionDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranqueDetectada { get; set; }
    public string OrigenRegistro { get; set; } = string.Empty;
    public string? CamaraCodigo { get; set; }
    public string? CamaraNombre { get; set; }
    public string? CuerpoCamaraNombre { get; set; }
    public int? CuerpoCamaraEquipoId { get; set; }
    public string? MaquinistaNombre { get; set; }
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public bool PendienteDeclaracion { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class DeclararDetencionRequest
{
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public TimeOnly? HoraArranque { get; set; }
    public int? MinutosDeclarados { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class DeclararDetencionResponse
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int MinutosEfectivos { get; set; }
    public bool RequiereAnalisis { get; set; }
}

public class AvisoFinTurnoDto
{
    public bool Mostrar { get; set; }
    public int Pendientes { get; set; }
    public int PendientesTurnoActual { get; set; }
    public int? MinutosParaFin { get; set; }
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
    public DateOnly? FechaInicioTurno { get; set; }
    public int MinutosAviso { get; set; }
}

public class ProduccionCamaraHoraDto
{
    public DateTimeOffset Hora { get; set; }
    public Dictionary<int, decimal> CantidadPorCarril { get; set; } = [];
    public decimal Total { get; set; }
    public int MinutosCarrilMarcha { get; set; }
    public int MinutosCarrilConDatos { get; set; }
}

