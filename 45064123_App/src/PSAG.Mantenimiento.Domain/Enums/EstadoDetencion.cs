namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Estados posibles de una detención.
/// REGISTRADA es un dato completo y válido - NO es un pendiente.
/// El flujo a EN_SEGUIMIENTO y CERRADA es opcional.
/// </summary>
public enum EstadoDetencion
{
    /// <summary>
    /// Detención registrada con minutos, clasificación y descripción.
    /// Cuenta en dashboard. NO es un pendiente.
    /// </summary>
    REGISTRADA = 1,

    /// <summary>
    /// Mantenimiento decidió hacer seguimiento (opcional).
    /// </summary>
    EN_SEGUIMIENTO = 2,

    /// <summary>
    /// Detención cerrada con resolución documentada (opcional).
    /// </summary>
    CERRADA = 3,

    /// <summary>
    /// Detención creada automáticamente por PLC, pendiente de justificación (Fase 2).
    /// </summary>
    PENDIENTE_JUSTIFICACION = 4
}

public static class EstadoDetencionExtensions
{
    public static bool PuedeTransicionarA(this EstadoDetencion estadoActual, EstadoDetencion estadoNuevo)
    {
        return (estadoActual, estadoNuevo) switch
        {
            // Desde REGISTRADA puede ir a seguimiento o cerrar directamente
            (EstadoDetencion.REGISTRADA, EstadoDetencion.EN_SEGUIMIENTO) => true,
            (EstadoDetencion.REGISTRADA, EstadoDetencion.CERRADA) => true,

            // Desde EN_SEGUIMIENTO solo puede cerrar
            (EstadoDetencion.EN_SEGUIMIENTO, EstadoDetencion.CERRADA) => true,

            // Desde PENDIENTE_JUSTIFICACION puede pasar a REGISTRADA (al completar datos)
            (EstadoDetencion.PENDIENTE_JUSTIFICACION, EstadoDetencion.REGISTRADA) => true,

            _ => false
        };
    }

    public static string ObtenerDescripcion(this EstadoDetencion estado)
    {
        return estado switch
        {
            EstadoDetencion.REGISTRADA => "Registrada",
            EstadoDetencion.EN_SEGUIMIENTO => "En análisis",
            EstadoDetencion.CERRADA => "Cerrada",
            EstadoDetencion.PENDIENTE_JUSTIFICACION => "Pendiente de justificación",
            _ => estado.ToString()
        };
    }

    public static string GetDescripcion(this EstadoDetencion estado) => ObtenerDescripcion(estado);
}
