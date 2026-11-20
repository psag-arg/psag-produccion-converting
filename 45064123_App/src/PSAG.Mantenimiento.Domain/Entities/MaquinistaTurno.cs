using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Maquinista de UN turno concreto de una línea (T-783): la línea, el turno y el día en que empezó el turno
/// (un nocturno de 22:00 a 06:00 es del día en que empieza). INSERT-ONLY: cada confirmación del operador en el
/// login o corrección del supervisor es un registro nuevo y el vigente es el más reciente. El historial queda.
/// El maquinista siempre es una asignación de la línea (Maestros → Maquinistas): sin relación no hay registro.
/// </summary>
public class MaquinistaTurno
{
    public long MaquinistaTurnoId { get; set; }

    public int LineaId { get; set; }
    public int TurnoId { get; set; }

    /// <summary>Día en que empezó el turno, en hora de planta.</summary>
    public DateOnly FechaTurno { get; set; }

    public int UsuarioLineaId { get; set; }

    /// <summary>Copia del usuario de la asignación al momento del registro (la asignación se puede editar).</summary>
    public string UsuarioId { get; set; } = string.Empty;

    public OrigenMaquinistaTurno Origen { get; set; }

    public DateTimeOffset FechaHora { get; set; }

    /// <summary>Quien lo registró: el mismo operador (LOGIN) o el supervisor que corrigió.</summary>
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;

    /// <summary>Motivo de la corrección (obligatorio para SUPERVISOR) o a quién reemplazó.</summary>
    public string? Observacion { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Turno Turno { get; set; } = null!;
    public virtual UsuarioLinea UsuarioLinea { get; set; } = null!;
}
