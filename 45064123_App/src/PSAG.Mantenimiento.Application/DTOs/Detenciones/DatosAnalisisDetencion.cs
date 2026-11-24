namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Campos de eliminación y análisis que muestran las vistas de detención.
/// Se completan desde la entidad con DatosAnalisisMapper.
/// </summary>
public abstract class DatosAnalisisDetencion
{
    public bool Eliminada { get; set; }
    public DateTimeOffset? FechaHoraEliminacion { get; set; }
    public string? EliminadaPorUsuarioId { get; set; }
    /// <summary>Lo completa el controller (requiere buscar el usuario).</summary>
    public string? EliminadaPorUsuarioNombre { get; set; }
    public string? MotivoEliminacion { get; set; }
    public bool RequiereAnalisis { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public string? AnalisisPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }
    public bool EquipoCritico { get; set; }
    public bool AnalisisPendiente { get; set; }
    public string? EstadoAnalisisDescripcion { get; set; }

    // ===== Tiempo detectado por la cámara (pedido 24/11): una pendiente tiene 0 minutos efectivos hasta que se declara,
    // pero se muestra lo que vio la cámara (DetencionCamaraReglas.SegundosDetectados, la misma regla del tablero).

    /// <summary>Pendiente de cámara: segundos detectados (parada → arranque detectado, o → ahora si sigue). Null si no aplica.</summary>
    public int? SegundosDetectados { get; set; }

    /// <summary>Pendiente de cámara cuya parada sigue abierta (sin arranque detectado): la duración es "en curso".</summary>
    public bool ParadaEnCurso { get; set; }

    /// <summary>Arranque que vio la cámara (en una parte cortada por el cambio de turno, la hora del corte).</summary>
    public DateTimeOffset? FechaHoraArranqueDetectada { get; set; }

    /// <summary>Turno automático (D.12): parte anterior de la parada (viene del turno anterior).</summary>
    public long? DetencionAnteriorId { get; set; }
    public bool VieneDelTurnoAnterior => DetencionAnteriorId != null;

    /// <summary>Sólo en el detalle: código de la parte anterior y la parte siguiente (si la parada siguió en el turno siguiente).</summary>
    public string? ParteAnteriorCodigo { get; set; }
    public long? ParteSiguienteId { get; set; }
    public string? ParteSiguienteCodigo { get; set; }
}
