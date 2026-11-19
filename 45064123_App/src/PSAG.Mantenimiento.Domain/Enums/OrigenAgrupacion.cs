namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Quién agrupó un microcorte de cámara bajo una detención (reunión 17/11/2026, pedido de Emiliano).
/// </summary>
public enum OrigenAgrupacion
{
    /// <summary>El operario respondió "Sí, mismo motivo" al declarar o editar la detención.</summary>
    OPERADOR = 1,

    /// <summary>Reservado para una agrupación automática futura (hoy no la hace nadie).</summary>
    AUTOMATICO = 2
}
