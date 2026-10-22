using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Calendario base de una línea: qué turnos trabaja cada día de la semana, desde una fecha.
/// Rige desde VigenteDesde hasta el día anterior a la versión siguiente. Las versiones no se
/// editan: guardar crea una nueva (y desactiva la de la misma fecha, si había).
/// Sin calendario vigente, la línea trabaja todos los turnos activos todos los días.
/// Una excepción del día (TiempoProgramadoDia) gana sobre el calendario.
/// </summary>
public class CalendarioLinea : CatalogoBase
{
    public int CalendarioLineaId { get; set; }
    public int LineaId { get; set; }
    public DateOnly VigenteDesde { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual ICollection<CalendarioLineaTurno> Turnos { get; set; } = new List<CalendarioLineaTurno>();

    /// <summary>
    /// Turnos que la línea trabaja ese día de la semana. Un turno que cruza medianoche
    /// cuenta en el día en que empieza.
    /// </summary>
    public IEnumerable<int> TurnosDelDia(DayOfWeek dia) =>
        Turnos.Where(t => t.DiaSemana == dia).Select(t => t.TurnoId);
}

/// <summary>
/// Un turno que la línea trabaja un día de la semana dentro de una versión del calendario.
/// </summary>
public class CalendarioLineaTurno
{
    public int CalendarioLineaTurnoId { get; set; }
    public int CalendarioLineaId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public int TurnoId { get; set; }

    // Navegación
    public virtual CalendarioLinea CalendarioLinea { get; set; } = null!;
    public virtual Turno Turno { get; set; } = null!;
}
