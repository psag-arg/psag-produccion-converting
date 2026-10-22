using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Exceptions;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Tarea de mantenimiento relevada en la máquina por el operador.
/// Ciclo: PENDIENTE → PROGRAMADA → CERRADA. No se borra: las cerradas quedan en el historial.
/// Quién la cargó y cuándo son CreadoPorUsuarioId y FechaHoraCreacion.
/// </summary>
public class TareaMantenimiento : EntityBase
{
    public long TareaMantenimientoId { get; set; }

    /// <summary>
    /// Línea ("la máquina" en planta).
    /// </summary>
    public int LineaId { get; set; }

    /// <summary>
    /// Cuerpo de la máquina: el mismo maestro de equipos que usa la detención.
    /// </summary>
    public int EquipoId { get; set; }

    /// <summary>
    /// Parte específica del cuerpo (texto libre).
    /// </summary>
    public string? ParteEquipo { get; set; }

    /// <summary>
    /// Detalle de la tarea a realizar.
    /// </summary>
    public string Detalle { get; set; } = string.Empty;

    public TipoTareaMantenimiento TipoTarea { get; set; }
    public CriticidadTarea Criticidad { get; set; }
    public EstadoTareaMantenimiento Estado { get; set; } = EstadoTareaMantenimiento.PENDIENTE;

    public DateTimeOffset? FechaHoraProgramacion { get; set; }
    public string? ProgramadaPorUsuarioId { get; set; }

    public DateTimeOffset? FechaHoraCierre { get; set; }
    public string? CerradaPorUsuarioId { get; set; }

    /// <summary>
    /// Control de concurrencia optimista.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Equipo Equipo { get; set; } = null!;

    public bool EstaCerrada => Estado == EstadoTareaMantenimiento.CERRADA;

    /// <summary>
    /// PENDIENTE → PROGRAMADA. Guarda quién y cuándo.
    /// </summary>
    public void Programar(string usuarioId, DateTimeOffset ahora)
    {
        CambiarEstado(EstadoTareaMantenimiento.PROGRAMADA, usuarioId);
        FechaHoraProgramacion = ahora;
        ProgramadaPorUsuarioId = usuarioId;
    }

    /// <summary>
    /// PROGRAMADA → CERRADA. Guarda quién y cuándo; desde acá la tarea sólo se consulta.
    /// </summary>
    public void Cerrar(string usuarioId, DateTimeOffset ahora)
    {
        CambiarEstado(EstadoTareaMantenimiento.CERRADA, usuarioId);
        FechaHoraCierre = ahora;
        CerradaPorUsuarioId = usuarioId;
    }

    private void CambiarEstado(EstadoTareaMantenimiento nuevoEstado, string usuarioId)
    {
        if (!Estado.PuedeTransicionarA(nuevoEstado))
            throw new TransicionEstadoInvalidaException(Estado.ObtenerDescripcion(), nuevoEstado.ObtenerDescripcion());

        Estado = nuevoEstado;
        ModificadoPorUsuarioId = usuarioId;
    }
}
