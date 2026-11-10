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

    /// <summary>
    /// Ahora en la planta (o <paramref name="instante"/>, para los tests): el turno actual y el "hoy" salen de
    /// esta hora, nunca de DateTime.Now / DateTime.Today del servidor.
    /// </summary>
    public static DateTimeOffset Ahora(TimeZoneInfo zona, DateTimeOffset? instante = null) =>
        TimeZoneInfo.ConvertTime(instante ?? DateTimeOffset.UtcNow, zona);

    /// <summary>Fecha de hoy en la planta.</summary>
    public static DateOnly Hoy(TimeZoneInfo zona, DateTimeOffset? instante = null) =>
        DateOnly.FromDateTime(Ahora(zona, instante).DateTime);

    /// <summary>Hora de pared de la planta, sin segundos (HH:mm), para buscar el turno.</summary>
    public static TimeOnly HoraMinuto(TimeZoneInfo zona, DateTimeOffset? instante = null)
    {
        var ahora = Ahora(zona, instante);
        return new TimeOnly(ahora.Hour, ahora.Minute);
    }
}
