using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Rollos contados por la cámara en un minuto y un carril (T-610). Lo manda el servicio de conteo por la
/// ingesta; nunca se edita ni se borra. Único por punto de control + minuto + carril: un reenvío no duplica.
/// </summary>
public class ConteoCamaraMinuto
{
    public long ConteoCamaraMinutoId { get; set; }

    /// <summary>Inicio del minuto (hora de la planta, segundos en cero).</summary>
    public DateTimeOffset Minuto { get; set; }

    /// <summary>Línea del punto de control, guardada para filtrar sin JOIN.</summary>
    public int LineaId { get; set; }

    public int PuntoControlCalidadId { get; set; }

    /// <summary>Turno materializado al guardar.</summary>
    public int? TurnoId { get; set; }

    /// <summary>Carril de salida de la cortadora (1 a 4 en la Tango 2, de izquierda a derecha en la imagen).</summary>
    public byte Carril { get; set; }

    /// <summary>Rollos estimados en el minuto (frecuencia de paso x segundos en marcha), con un decimal.</summary>
    public decimal Rollos { get; set; }

    public short SegundosMarcha { get; set; }
    public short SegundosParada { get; set; }
    public short SegundosAcumulacion { get; set; }
    public short SegundosSinSenal { get; set; }

    public EstadoCarrilCamara Estado { get; set; }

    public DateTimeOffset FechaHoraCreacion { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual PuntoControlCalidad PuntoControl { get; set; } = null!;
    public virtual Turno? Turno { get; set; }
}
