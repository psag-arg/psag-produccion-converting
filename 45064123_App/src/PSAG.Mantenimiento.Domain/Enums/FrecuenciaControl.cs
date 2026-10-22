namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Cada cuánto hay que tomar un punto de control de proceso (T-545).
/// Hoy es informativa: el control se carga una vez por línea + turno + fecha
/// (+ producto si aplica). Si el cliente pide varias lecturas por turno
/// (ej. cada 2 horas) hay que sumar un número de lectura al detalle.
/// </summary>
public enum FrecuenciaControl
{
    /// <summary>Una vez por turno.</summary>
    POR_TURNO = 1,

    /// <summary>Al arrancar el turno.</summary>
    INICIO_TURNO = 2,

    /// <summary>Cada vez que cambia el producto que corre la línea.</summary>
    CAMBIO_PRODUCTO = 3,

    /// <summary>Cada hora.</summary>
    CADA_HORA = 4,

    /// <summary>Cada 2 horas.</summary>
    CADA_2_HORAS = 5,

    /// <summary>Una vez por día.</summary>
    DIARIA = 6
}

public static class FrecuenciaControlExtensions
{
    public static string ObtenerDescripcion(this FrecuenciaControl frecuencia)
    {
        return frecuencia switch
        {
            FrecuenciaControl.POR_TURNO => "Por turno",
            FrecuenciaControl.INICIO_TURNO => "Inicio de turno",
            FrecuenciaControl.CAMBIO_PRODUCTO => "Cambio de producto",
            FrecuenciaControl.CADA_HORA => "Cada hora",
            FrecuenciaControl.CADA_2_HORAS => "Cada 2 horas",
            FrecuenciaControl.DIARIA => "Diaria",
            _ => frecuencia.ToString()
        };
    }
}
