namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// DTO para visualización de detención.
/// </summary>
public class DetencionDto : DatosAnalisisDetencion
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int? TipoFallaId { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int? DetalleFallaId { get; set; }
    public string? DetalleFallaNombre { get; set; }
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;

    /// <summary>Fecha de producción del turno (T-798: sale de TurnoProduccion; el T3 es del día de las 21:00).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Turno de producción del que cuelga (T-798 etapa 3).</summary>
    public long TurnoProduccionId { get; set; }

    /// <summary>Operador (usuario) del turno a la hora de la parada (snapshot).</summary>
    public string? OperadorUsuarioId { get; set; }

    // Modelo de tiempo
    public int? MinutosDeclarados { get; set; }
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranque { get; set; }
    public string OrigenTiempo { get; set; } = string.Empty;

    /// <summary>T-786: MANUAL (la cargó una persona) o CAMARA (la creó el servicio de cámara).</summary>
    public string OrigenRegistro { get; set; } = "MANUAL";
    public int MinutosEfectivos { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;

    public string? AccionesRealizadas { get; set; }
    public string? ResolucionDescripcion { get; set; }

    public DateTimeOffset? FechaHoraCierre { get; set; }
    public string? CerradoPorUsuarioId { get; set; }
    public string? CerradoPorUsuarioNombre { get; set; }

    public string CreadoPorUsuarioId { get; set; } = string.Empty;
    public string CreadoPorUsuarioNombre { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraCreacion { get; set; }
    public DateTimeOffset FechaHoraModificacion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// DTO para listado resumido de detenciones.
/// </summary>
public class DetencionListaDto : DatosAnalisisDetencion
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public string? TipoFallaNombre { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public int MinutosEfectivos { get; set; }
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;

    public string CreadoPorUsuarioNombre { get; set; } = string.Empty;
}
