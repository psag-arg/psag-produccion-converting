namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// De dónde salen las horas programadas de un día.
/// </summary>
public static class OrigenTiempoProgramado
{
    /// <summary>Sin calendario ni excepción: todos los turnos activos de la planta.</summary>
    public const string Estandar = "ESTANDAR";

    /// <summary>Calendario base de la línea (turnos de ese día de la semana).</summary>
    public const string BaseLinea = "BASE_LINEA";

    /// <summary>Excepción cargada para ese día.</summary>
    public const string Excepcion = "EXCEPCION";

    /// <summary>Horas históricas (HS PROG del Excel del cliente).</summary>
    public const string Historico = "HISTORICO";
}
