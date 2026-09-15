namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Tipos de notas de seguimiento de eventos.
/// </summary>
public enum TipoNota
{
    /// <summary>
    /// Nota operativa ingresada por operarios o supervisores.
    /// </summary>
    Operativa = 1,

    /// <summary>
    /// Nota técnica ingresada por personal de mantenimiento.
    /// </summary>
    Tecnica = 2,

    /// <summary>
    /// Nota de cierre del evento.
    /// </summary>
    Cierre = 3
}
