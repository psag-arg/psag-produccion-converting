namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Origen del tiempo de la detención.
/// </summary>
public enum OrigenTiempo
{
    /// <summary>
    /// Minutos tipeados manualmente por el operador.
    /// </summary>
    DECLARADO = 1,

    /// <summary>
    /// Calculado a partir de FechaHoraParada y FechaHoraArranque ingresados manualmente.
    /// </summary>
    CALCULADO = 2,

    /// <summary>
    /// Horas informadas automáticamente desde el PLC (Fase 2).
    /// </summary>
    PLC = 3
}
