using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Representa una detención de máquina (parada de línea).
/// El operador la registra después de resolver el problema.
/// </summary>
public class Detencion : EntityBase
{
    public long DetencionId { get; set; }

    /// <summary>
    /// Código visible único (ej: DET-20260808-000001).
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public int LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }

    /// <summary>
    /// Turno materializado. Se guarda al registrar, no se recalcula.
    /// </summary>
    public int TurnoId { get; set; }

    /// <summary>
    /// Maquinista que registró la detención (UsuarioLinea).
    /// </summary>
    public int? UsuarioLineaId { get; set; }

    /// <summary>
    /// Fecha del registro (sin hora, para agrupación).
    /// </summary>
    public DateOnly Fecha { get; set; }

    // ========== MODELO DE TIEMPO ==========

    /// <summary>
    /// Minutos declarados manualmente por el operador (Fase 1).
    /// </summary>
    public int? MinutosDeclarados { get; set; }

    /// <summary>
    /// Hora de parada (opcional en Fase 1, viene del PLC en Fase 2).
    /// </summary>
    public DateTimeOffset? FechaHoraParada { get; set; }

    /// <summary>
    /// Hora de arranque (opcional en Fase 1, viene del PLC en Fase 2).
    /// </summary>
    public DateTimeOffset? FechaHoraArranque { get; set; }

    /// <summary>
    /// Origen del tiempo: DECLARADO | CALCULADO | PLC.
    /// </summary>
    public OrigenTiempo OrigenTiempo { get; set; }

    /// <summary>
    /// ÚNICO CAMPO QUE CONSUME TODO EL SISTEMA.
    /// Se calcula en backend según OrigenTiempo.
    /// </summary>
    public int MinutosEfectivos { get; set; }

    // ========== DATOS DE LA DETENCIÓN ==========

    /// <summary>
    /// Descripción del problema/resolución.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Estado actual de la detención.
    /// </summary>
    public EstadoDetencion EstadoActual { get; set; } = EstadoDetencion.REGISTRADA;

    /// <summary>
    /// Acciones realizadas para resolver el problema.
    /// </summary>
    public string? AccionesRealizadas { get; set; }

    /// <summary>
    /// Resolución documentada (al cerrar).
    /// </summary>
    public string? ResolucionDescripcion { get; set; }

    /// <summary>
    /// Fecha y hora de cierre (si aplica).
    /// </summary>
    public DateTimeOffset? FechaHoraCierre { get; set; }

    /// <summary>
    /// Usuario que cerró la detención.
    /// </summary>
    public string? CerradoPorUsuarioId { get; set; }

    /// <summary>
    /// Control de concurrencia optimista.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Equipo? Equipo { get; set; }
    public virtual TipoFalla? TipoFalla { get; set; }
    public virtual DetalleFalla? DetalleFalla { get; set; }
    public virtual Turno Turno { get; set; } = null!;
    public virtual UsuarioLinea? UsuarioLinea { get; set; }
    public virtual ICollection<DetencionEstadoHistorial> HistorialEstados { get; set; } = new List<DetencionEstadoHistorial>();
    public virtual ICollection<DetencionNota> Notas { get; set; } = new List<DetencionNota>();

    /// <summary>
    /// Verifica si la detención puede transicionar al estado especificado.
    /// </summary>
    public bool PuedeTransicionarA(EstadoDetencion nuevoEstado)
    {
        return EstadoActual.PuedeTransicionarA(nuevoEstado);
    }

    /// <summary>
    /// Verifica si la detención está cerrada.
    /// </summary>
    public bool EstaCerrada => EstadoActual == EstadoDetencion.CERRADA;

    /// <summary>
    /// Verifica si la detención puede ser editada.
    /// </summary>
    public bool PuedeSerEditada => !EstaCerrada;
}
