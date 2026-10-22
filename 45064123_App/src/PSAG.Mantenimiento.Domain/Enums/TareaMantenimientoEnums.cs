namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Tipo de tarea de mantenimiento relevada en la máquina.
/// </summary>
public enum TipoTareaMantenimiento
{
    MECANICA = 1,
    ELECTRICA = 2,
    ELECTRONICA = 3
}

/// <summary>
/// Criticidad de una tarea de mantenimiento. El valor numérico ordena: más alto, más crítica.
/// </summary>
public enum CriticidadTarea
{
    BAJA = 1,
    MEDIA = 2,
    ALTA = 3
}

/// <summary>
/// Ciclo de vida de una tarea de mantenimiento: PENDIENTE → PROGRAMADA → CERRADA.
/// Las cerradas no se borran: quedan en el historial.
/// </summary>
public enum EstadoTareaMantenimiento
{
    /// <summary>
    /// Informada por el operador, todavía sin programar.
    /// </summary>
    PENDIENTE = 1,

    /// <summary>
    /// Mantenimiento o supervisión la programó para que la haga un técnico.
    /// </summary>
    PROGRAMADA = 2,

    /// <summary>
    /// Realizada. Sale de la grilla activa y queda en el historial.
    /// </summary>
    CERRADA = 3
}

public static class TareaMantenimientoEnumsExtensions
{
    public static bool PuedeTransicionarA(this EstadoTareaMantenimiento estadoActual, EstadoTareaMantenimiento estadoNuevo)
    {
        return (estadoActual, estadoNuevo) switch
        {
            (EstadoTareaMantenimiento.PENDIENTE, EstadoTareaMantenimiento.PROGRAMADA) => true,
            (EstadoTareaMantenimiento.PROGRAMADA, EstadoTareaMantenimiento.CERRADA) => true,
            _ => false
        };
    }

    public static string ObtenerDescripcion(this EstadoTareaMantenimiento estado) => estado switch
    {
        EstadoTareaMantenimiento.PENDIENTE => "Pendiente",
        EstadoTareaMantenimiento.PROGRAMADA => "Programada",
        EstadoTareaMantenimiento.CERRADA => "Cerrada",
        _ => estado.ToString()
    };

    public static string ObtenerDescripcion(this TipoTareaMantenimiento tipo) => tipo switch
    {
        TipoTareaMantenimiento.MECANICA => "Mecánica",
        TipoTareaMantenimiento.ELECTRICA => "Eléctrica",
        TipoTareaMantenimiento.ELECTRONICA => "Electrónica",
        _ => tipo.ToString()
    };

    public static string ObtenerDescripcion(this CriticidadTarea criticidad) => criticidad switch
    {
        CriticidadTarea.ALTA => "Alta",
        CriticidadTarea.MEDIA => "Media",
        CriticidadTarea.BAJA => "Baja",
        _ => criticidad.ToString()
    };
}
