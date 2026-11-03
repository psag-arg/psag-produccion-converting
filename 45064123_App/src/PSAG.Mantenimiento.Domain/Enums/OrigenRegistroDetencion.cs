namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Quién dio de alta la detención. No cambia después del alta.
/// </summary>
public enum OrigenRegistroDetencion
{
    /// <summary>La cargó una persona desde "Nueva detención".</summary>
    MANUAL = 1,

    /// <summary>
    /// La creó el servicio de cámara (T-610) al superar el umbral de parada. Nace en PENDIENTE_JUSTIFICACION
    /// ("Pendiente de declaración") y el operador la completa.
    /// </summary>
    CAMARA = 2
}
