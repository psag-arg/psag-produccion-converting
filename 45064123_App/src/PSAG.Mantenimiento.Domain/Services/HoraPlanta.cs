namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Expresa un instante con el offset de la planta. La base puede devolverlo en UTC (SQLite) o con otro
/// offset; así convertido, su hora de pared (<c>HH:mm</c>) es la de la planta, no la del servidor ni la del navegador.
/// </summary>
public static class HoraPlanta
{
    public static DateTimeOffset? En(DateTimeOffset? instante, TimeZoneInfo zona) =>
        instante is { } valor ? TimeZoneInfo.ConvertTime(valor, zona) : null;
}
