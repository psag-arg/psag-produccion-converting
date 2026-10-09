using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Turno de trabajo. Los horarios se expresan como TimeOnly.
/// Turno nocturno: HoraFin < HoraInicio (cruza medianoche).
/// </summary>
public class Turno : CatalogoBase
{
    public int TurnoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Hora de inicio del turno.
    /// </summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>
    /// Hora de fin del turno.
    /// Si HoraFin < HoraInicio, el turno cruza medianoche.
    /// </summary>
    public TimeOnly HoraFin { get; set; }

    public int Orden { get; set; }

    // Navegación
    public virtual ICollection<Detencion> Detenciones { get; set; } = new List<Detencion>();

    /// <summary>
    /// Duración del turno en minutos. Si HoraFin no es posterior a HoraInicio, cruza medianoche
    /// (ej.: 21:00 a 05:00 = 480 minutos), igual que ContieneHora.
    /// </summary>
    public int DuracionMinutos()
    {
        var inicio = HoraInicio.ToTimeSpan();
        var fin = HoraFin.ToTimeSpan();

        var duracion = fin > inicio
            ? fin - inicio
            : TimeSpan.FromDays(1) - inicio + fin;

        return (int)duracion.TotalMinutes;
    }

    /// <summary>
    /// Verifica si una hora dada pertenece a este turno.
    /// Contempla turnos que cruzan medianoche.
    /// </summary>
    public bool ContieneHora(TimeOnly hora)
    {
        if (HoraFin > HoraInicio)
        {
            // Turno normal (no cruza medianoche)
            return hora >= HoraInicio && hora < HoraFin;
        }
        else
        {
            // Turno nocturno (cruza medianoche)
            return hora >= HoraInicio || hora < HoraFin;
        }
    }
}
