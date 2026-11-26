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

    /// <summary>
    /// Fecha de la hora desde (la Web la sigue mandando). T-798 etapa 3: NO se guarda; la fecha de producción y el
    /// turno salen del turno de producción en que cae la hora desde (o el momento de la carga, sin horas).
    /// </summary>
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
    /// Maquinista elegido a mano (admin/supervisor): el USUARIO (T-798 etapa 3). Tiene que estar habilitado en la línea.
    /// Si no viene, el operador es el del turno de producción a la hora de la parada o, si no hay, quien carga.
    /// </summary>
    public string? OperadorUsuarioId { get; set; }

    /// <summary>
    /// Compatibilidad: la Web anterior mandaba la habilitación (ABM_OperadoresLinea). Se convierte a su usuario.
    /// Si vienen los dos, manda <see cref="OperadorUsuarioId"/>.
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
