namespace PSAG.Mantenimiento.Web.Models;

// T-798 etapa 5 · Indicadores (api/indicadores): espejo de los DTO de la API. Los números los calcula la API.

public class ComparacionProduccionDto
{
    public string Magnitud { get; set; } = string.Empty;
    public decimal? Objetivo { get; set; }
    public decimal? Real { get; set; }
    public decimal? Porcentaje { get; set; }

    /// <summary>OK, SIN_OBJETIVO o SIN_DATOS.</summary>
    public string Estado { get; set; } = string.Empty;
    public bool Provisorio { get; set; }
    public int TramosSinDatos { get; set; }
    public int TramosSinObjetivo { get; set; }
}

public class FilaPresupuestoRealDto
{
    public string Clave { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public long? TurnoProduccionId { get; set; }
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public DateOnly? FechaProduccion { get; set; }
    public string? TurnoCodigo { get; set; }
    public string? TurnoNombre { get; set; }
    public int? ProductoMaestroId { get; set; }
    public string? ProductoNombre { get; set; }
    public int Turnos { get; set; }
    public bool IncluyeAbiertos { get; set; }
    public int? MinutosProgramados { get; set; }
    public int? MinutosParadaQueCuentan { get; set; }
    public decimal? Indisponibilidad { get; set; }
    public ComparacionProduccionDto Mangas { get; set; } = new();
    public ComparacionProduccionDto Bolsones { get; set; } = new();
    public ComparacionProduccionDto Eficiencia { get; set; } = new();
    public decimal? VelocidadMetrosMinuto { get; set; }
}

public class PresupuestoRealDto
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int? LineaId { get; set; }
    public int? ProductoMaestroId { get; set; }
    public string Agrupar { get; set; } = "turno";
    public List<FilaPresupuestoRealDto> Filas { get; set; } = [];
    public FilaPresupuestoRealDto Total { get; set; } = new();
}

// ===================== Detalle del turno (GET api/indicadores/turno/{id}, T-798 etapa 6) =====================

public class ProduccionTurnoDto
{
    public long ProduccionTurnoId { get; set; }
    public long TurnoProduccionProductoId { get; set; }
    public string Magnitud { get; set; } = string.Empty;
    public decimal? CantidadCamara { get; set; }
    public int? MinutosConDatos { get; set; }
    public decimal? CantidadOficial { get; set; }

    /// <summary>CAMARA, CAMARA_CORREGIDA o MANUAL.</summary>
    public string Origen { get; set; } = string.Empty;

    /// <summary>PENDIENTE, VALIDADA o SIN_DATOS.</summary>
    public string EstadoValidacion { get; set; } = string.Empty;
    public string? ValidadoPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraValidacion { get; set; }
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public string? Motivo { get; set; }
    public long? AnuladoPorId { get; set; }
}

public class MagnitudTramoDto
{
    public string Magnitud { get; set; } = string.Empty;
    public decimal? Objetivo { get; set; }
    public decimal? Real { get; set; }

    /// <summary>VALIDADA, PENDIENTE, PARCIAL o SIN_DATOS.</summary>
    public string EstadoReal { get; set; } = string.Empty;
    public bool Provisorio { get; set; }
    public bool TieneCamaraOficial { get; set; }
    public ProduccionTurnoDto? Vigente { get; set; }
}

public class TramoIndicadorDto
{
    public long TurnoProduccionProductoId { get; set; }
    public int ProductoMaestroId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public bool Abierto { get; set; }
    public decimal MinutosTramo { get; set; }
    public decimal MinutosObjetivo { get; set; }
    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; }
    public decimal? MetrosPorManga { get; set; }
    public MagnitudTramoDto Mangas { get; set; } = new();
    public MagnitudTramoDto Bolsones { get; set; } = new();
    public ComparacionProduccionDto Eficiencia { get; set; } = new();
    public decimal? VelocidadMetrosMinuto { get; set; }
    public int MinutosMarcha { get; set; }
    public List<ProduccionTurnoDto> Producciones { get; set; } = [];
}

public class IndicadoresTurnoDto
{
    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int TurnoId { get; set; }
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly FechaProduccion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Abierto { get; set; }
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }
    public int MinutosProgramados { get; set; }
    public int MinutosParadaQueCuentan { get; set; }
    public decimal? Indisponibilidad { get; set; }
    public bool ACompletar { get; set; }
    public ComparacionProduccionDto Mangas { get; set; } = new();
    public ComparacionProduccionDto Bolsones { get; set; } = new();
    public ComparacionProduccionDto Eficiencia { get; set; } = new();
    public decimal? VelocidadMetrosMinuto { get; set; }
    public List<TramoIndicadorDto> Tramos { get; set; } = [];
}

/// <summary>POST api/turnos-produccion/{id}/produccion: VALIDAR, CORREGIR o MANUAL.</summary>
public class RegistrarProduccionTurnoRequest
{
    public long TurnoProduccionProductoId { get; set; }
    public string Magnitud { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public decimal? Cantidad { get; set; }
    public string? Motivo { get; set; }
}
