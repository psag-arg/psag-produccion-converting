namespace PSAG.Mantenimiento.Domain.Services;

public readonly record struct TiempoManual(DateTimeOffset Parada, DateTimeOffset Arranque, int Minutos);

/// <summary>
/// Convierte la fecha y la hora de inicio/fin de una carga manual en instantes de parada y arranque.
/// Función pura: no toca la base ni el reloj del servidor.
/// </summary>
public static class TiempoManualCalculador
{
    /// <summary>
    /// Si el fin es anterior al inicio, la detención cruza medianoche y termina al día siguiente.
    /// Las horas son las de pared de la planta: se les aplica el offset que la zona <paramref name="zona"/>
    /// tiene en esa fecha, así el resultado no depende de la zona horaria del servidor.
    /// </summary>
    public static TiempoManual Calcular(DateOnly fecha, TimeOnly horaInicio, TimeOnly horaFin, TimeZoneInfo zona)
    {
        var fechaFin = horaFin < horaInicio ? fecha.AddDays(1) : fecha;

        var parada = ConOffset(fecha.ToDateTime(horaInicio), zona);
        var arranque = ConOffset(fechaFin.ToDateTime(horaFin), zona);

        return new TiempoManual(parada, arranque, (int)(arranque - parada).TotalMinutes);
    }

    private static DateTimeOffset ConOffset(DateTime horaDePared, TimeZoneInfo zona) =>
        new(horaDePared, zona.GetUtcOffset(horaDePared));
}
