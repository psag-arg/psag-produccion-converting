using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Cantidad contada por la cámara en un minuto y un carril (T-610). Lo manda el servicio de conteo por la
/// ingesta; nunca se edita ni se borra. Único por cámara + minuto + carril: un reenvío no duplica.
/// T-798 etapa 4: cuelga de la cámara (la línea sale de ella) y del turno de producción; la magnitud (mangas,
/// rollos, bolsones...) la da la cámara.
/// </summary>
public class ConteoCamaraMinuto
{
    public long ConteoCamaraMinutoId { get; set; }

    /// <summary>Inicio del minuto (hora de la planta, segundos en cero).</summary>
    public DateTimeOffset Minuto { get; set; }

    public int CamaraId { get; set; }

    /// <summary>Turno de producción de la línea a esa hora (AsegurarTurno). Null si ningún turno de planta cubre la hora.</summary>
    public long? TurnoProduccionId { get; set; }

    /// <summary>Carril de salida de la cortadora (1 a 4 en la Tango 2, de izquierda a derecha en la imagen).</summary>
    public byte Carril { get; set; }

    /// <summary>Unidades del minuto en la magnitud de la cámara (ex Rollos), con un decimal. 0 = no produjo (no es "sin datos").</summary>
    public decimal Cantidad { get; set; }

    public short SegundosMarcha { get; set; }
    public short SegundosParada { get; set; }
    public short SegundosAcumulacion { get; set; }
    public short SegundosSinSenal { get; set; }

    public EstadoCarrilCamara Estado { get; set; }

    public DateTimeOffset FechaHoraCreacion { get; set; }

    // Navegación
    public virtual Camara Camara { get; set; } = null!;
    public virtual TurnoProduccion? TurnoProduccion { get; set; }
}
