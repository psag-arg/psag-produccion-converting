namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Expresa un instante con el offset de la planta. La base puede devolverlo en UTC (SQLite) o con otro
/// offset; así convertido, su hora de pared (<c>HH:mm</c>) es la de la planta, no la del servidor ni la del navegador.
/// </summary>
public static class HoraPlanta
{
    public static DateTimeOffset? En(DateTimeOffset? instante, TimeZoneInfo zona) =>
        instante is { } valor ? TimeZoneInfo.ConvertTime(valor, zona) : null;

    /// <summary>
    /// Instante de una hora de pared de la planta (fecha y hora sin zona): le aplica el offset que
    /// <paramref name="zona"/> tiene en esa fecha, así no depende de la zona horaria del servidor.
    /// </summary>
    public static DateTimeOffset DesdeHoraDePared(DateTime horaDePared, TimeZoneInfo zona) =>
        new(horaDePared, zona.GetUtcOffset(horaDePared));
}
