namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// De dónde viene un registro de calidad (T-611).
/// </summary>
public enum OrigenRegistroCalidad
{
    /// <summary>
    /// Lo mandó el detector de la cámara por el endpoint de ingesta.
    /// </summary>
    AUTOMATICO = 1,

    /// <summary>
    /// Lo cargó una persona (supervisión o administración) desde la pantalla de Calidad.
    /// </summary>
    MANUAL = 2
}

/// <summary>
/// Revisión humana de un registro de calidad. Un registro automático nace PENDIENTE;
/// supervisión, mantenimiento o administración lo marcan CONFIRMADO o FALSO_POSITIVO.
/// Nunca vuelve a PENDIENTE: una corrección pasa de CONFIRMADO a FALSO_POSITIVO (o al revés)
/// y queda en el historial.
/// </summary>
public enum EstadoRevisionCalidad
{
    PENDIENTE = 1,
    CONFIRMADO = 2,
    FALSO_POSITIVO = 3
}

public static class CalidadEnumsExtensions
{
    public static bool PuedeTransicionarA(this EstadoRevisionCalidad estadoActual, EstadoRevisionCalidad estadoNuevo)
    {
        return (estadoActual, estadoNuevo) switch
        {
            (EstadoRevisionCalidad.PENDIENTE, EstadoRevisionCalidad.CONFIRMADO) => true,
            (EstadoRevisionCalidad.PENDIENTE, EstadoRevisionCalidad.FALSO_POSITIVO) => true,
            (EstadoRevisionCalidad.CONFIRMADO, EstadoRevisionCalidad.FALSO_POSITIVO) => true,
            (EstadoRevisionCalidad.FALSO_POSITIVO, EstadoRevisionCalidad.CONFIRMADO) => true,
            _ => false
        };
    }

    public static string ObtenerDescripcion(this EstadoRevisionCalidad estado) => estado switch
    {
        EstadoRevisionCalidad.PENDIENTE => "Pendiente",
        EstadoRevisionCalidad.CONFIRMADO => "Confirmado",
        EstadoRevisionCalidad.FALSO_POSITIVO => "Falso positivo",
        _ => estado.ToString()
    };

    public static string ObtenerDescripcion(this OrigenRegistroCalidad origen) => origen switch
    {
        OrigenRegistroCalidad.AUTOMATICO => "Automático",
        OrigenRegistroCalidad.MANUAL => "Manual",
        _ => origen.ToString()
    };
}
