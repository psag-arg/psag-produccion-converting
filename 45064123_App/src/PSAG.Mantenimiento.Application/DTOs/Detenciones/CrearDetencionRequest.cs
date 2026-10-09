namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Request para crear una nueva detención.
/// </summary>
public class CrearDetencionRequest
{
    public int LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }
    public DateOnly Fecha { get; set; }

    /// <summary>
    /// Minutos declarados manualmente por el operador.
    /// Requerido si OrigenTiempo = DECLARADO.
    /// </summary>
    public int? MinutosDeclarados { get; set; }

    /// <summary>
    /// Fecha/hora de parada. Requerido si OrigenTiempo = CALCULADO.
    /// </summary>
    public DateTimeOffset? FechaHoraParada { get; set; }

    /// <summary>
    /// Fecha/hora de arranque. Requerido si OrigenTiempo = CALCULADO.
    /// </summary>
    public DateTimeOffset? FechaHoraArranque { get; set; }

    /// <summary>
    /// Origen del tiempo: DECLARADO o CALCULADO (PLC es solo para Fase 2).
    /// </summary>
    public string OrigenTiempo { get; set; } = "DECLARADO";

    /// <summary>
    /// Hora de inicio de la carga manual. Si se envían HoraInicio y HoraFin, el backend calcula
    /// FechaHoraParada, FechaHoraArranque y MinutosEfectivos (y el turno), y el origen pasa a CALCULADO.
    /// </summary>
    public TimeOnly? HoraInicio { get; set; }

    /// <summary>
    /// Hora de fin de la carga manual. Si es anterior a HoraInicio, la detención termina al día siguiente.
    /// </summary>
    public TimeOnly? HoraFin { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// UsuarioLineaId del maquinista asignado.
    /// Opcional: Si se proporciona, se usa este maquinista/turno.
    /// Si no, se busca la asignación del usuario actual.
    /// </summary>
    public int? UsuarioLineaId { get; set; }

    /// <summary>
    /// Token de idempotencia para evitar duplicados.
    /// </summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Response de creación de detención.
/// </summary>
public class CrearDetencionResponse
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int MinutosEfectivos { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }
}

/// <summary>
/// Indica si la detención que se está cargando va a requerir análisis (sin guardarla).
/// </summary>
public class PrevisualizacionAnalisisResponse
{
    public bool RequiereAnalisis { get; set; }
}
