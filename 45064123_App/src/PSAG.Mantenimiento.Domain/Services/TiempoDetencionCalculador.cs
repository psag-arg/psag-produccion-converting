using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Lo que cargó el usuario sobre el tiempo de una detención. Origen null = el texto no era un origen válido.
/// </summary>
public sealed record TiempoCargado(
    DateOnly Fecha,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    OrigenTiempo? Origen,
    int? MinutosDeclarados,
    DateTimeOffset? FechaHoraParada,
    DateTimeOffset? FechaHoraArranque);

/// <summary>
/// Tiempo de una detención tal como queda guardado. MinutosEfectivos es el único que consume el sistema.
/// </summary>
public readonly record struct TiempoDetencion(
    OrigenTiempo Origen,
    int MinutosEfectivos,
    int? MinutosDeclarados,
    DateTimeOffset? FechaHoraParada,
    DateTimeOffset? FechaHoraArranque);

/// <summary>
/// Cálculo del tiempo de una detención nueva. Lo usan el alta y la previsualización de análisis, así no pueden divergir.
/// Función pura: la zona horaria llega por parámetro.
/// </summary>
public static class TiempoDetencionCalculador
{
    /// <summary>
    /// Con hora de inicio y fin, el tiempo sale de las horas (CALCULADO). Si no, sale del origen pedido:
    /// DECLARADO usa los minutos; CALCULADO usa las fechas de parada y arranque si vienen; PLC todavía no calcula.
    /// </summary>
    public static TiempoDetencion Calcular(TiempoCargado cargado, TimeZoneInfo zona)
    {
        if (cargado.HoraInicio is { } inicio && cargado.HoraFin is { } fin)
        {
            var manual = TiempoManualCalculador.Calcular(cargado.Fecha, inicio, fin, zona);
            return new TiempoDetencion(OrigenTiempo.CALCULADO, manual.Minutos, null, manual.Parada, manual.Arranque);
        }

        var origen = cargado.Origen ?? OrigenTiempo.DECLARADO;
        var minutos = origen switch
        {
            OrigenTiempo.DECLARADO => cargado.MinutosDeclarados ?? 0,
            OrigenTiempo.CALCULADO when cargado.FechaHoraParada is { } parada && cargado.FechaHoraArranque is { } arranque
                => (int)(arranque - parada).TotalMinutes,
            _ => 0
        };

        return new TiempoDetencion(origen, minutos, cargado.MinutosDeclarados, cargado.FechaHoraParada, cargado.FechaHoraArranque);
    }
}
