namespace PSAG.Mantenimiento.Domain.Services;

public readonly record struct TiempoManual(DateTimeOffset Parada, DateTimeOffset Arranque, int Minutos);

/// <summary>
/// Convierte la fecha y la hora de inicio/fin de una carga manual en instantes de parada y arranque.
/// Función pura: no toca la base.
/// </summary>
public static class TiempoManualCalculador
{
    /// <summary>
    /// Si el fin es anterior al inicio, la detención cruza medianoche y termina al día siguiente.
    /// Los instantes conservan la hora de pared ingresada, con el offset local de la fecha.
    /// </summary>
    public static TiempoManual Calcular(DateOnly fecha, TimeOnly horaInicio, TimeOnly horaFin)
    {
        var parada = ConOffsetLocal(fecha.ToDateTime(horaInicio));
        var arranque = ConOffsetLocal(fecha.ToDateTime(horaFin));

        if (horaFin < horaInicio)
            arranque = arranque.AddDays(1);

        return new TiempoManual(parada, arranque, (int)(arranque - parada).TotalMinutes);
    }

    private static DateTimeOffset ConOffsetLocal(DateTime localSinOffset) =>
        new(localSinOffset, TimeZoneInfo.Local.GetUtcOffset(localSinOffset));
}
