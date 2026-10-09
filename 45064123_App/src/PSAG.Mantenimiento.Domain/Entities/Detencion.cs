using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Exceptions;

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

    // ========== ELIMINACIÓN LÓGICA ==========

    /// <summary>
    /// Borrado lógico. Las eliminadas quedan fuera de todas las consultas (filtro global de EF).
    /// </summary>
    public bool Eliminada { get; set; }

    public DateTimeOffset? FechaHoraEliminacion { get; set; }
    public string? EliminadaPorUsuarioId { get; set; }
    public string? MotivoEliminacion { get; set; }

    // ========== ANÁLISIS DE FALLA ==========

    /// <summary>
    /// Verdadero cuando una regla de análisis aplica. Es una marca, no un estado: la detención sigue REGISTRADA.
    /// </summary>
    public bool RequiereAnalisis { get; set; }

    /// <summary>
    /// Regla que disparó el análisis (la que ganó la evaluación al crear o editar).
    /// </summary>
    public int? ReglaAnalisisId { get; set; }

    /// <summary>
    /// Rol responsable del análisis, copiado de la regla.
    /// </summary>
    public string? ResponsableRol { get; set; }

    /// <summary>
    /// Usuario responsable del análisis, copiado de la regla.
    /// </summary>
    public string? ResponsableUsuarioId { get; set; }

    /// <summary>
    /// Causa de la falla (el análisis). La solución es AccionesRealizadas.
    /// </summary>
    public string? CausaFalla { get; set; }

    /// <summary>
    /// Plan de acción (opcional).
    /// </summary>
    public string? PlanAccion { get; set; }

    public string? AnalisisPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }

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
    public virtual ReglaAnalisis? ReglaAnalisis { get; set; }
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
    /// Cambia el estado si la transición está permitida y deja la entrada en el historial.
    /// </summary>
    public void CambiarEstado(EstadoDetencion nuevoEstado, string usuarioId, string? observacion)
    {
        if (!PuedeTransicionarA(nuevoEstado))
            throw new TransicionEstadoInvalidaException(EstadoActual.ToString(), nuevoEstado.ToString());

        HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoAnterior = EstadoActual,
            EstadoNuevo = nuevoEstado,
            FechaHora = DateTimeOffset.UtcNow,
            UsuarioId = usuarioId,
            Observacion = observacion,
            OrigenAccion = "API"
        });

        EstadoActual = nuevoEstado;
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
