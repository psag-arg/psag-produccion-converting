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
/// Tiempo tras una edición. RederivarTurno indica que las horas cambiaron y el turno sale de la nueva hora de inicio.
/// </summary>
public readonly record struct TiempoEditado(TiempoDetencion Tiempo, bool RederivarTurno);

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

    /// <summary>
    /// Edición. Sólo cambia el tiempo si el usuario lo cargó:
    /// horas nuevas (recalcula; el turno se re-deriva sólo si cambiaron), fechas de parada y arranque (se toman tal cual),
    /// o minutos de una DECLARADO. Una CALCULADO o PLC sin horas nuevas conserva tiempo, origen y turno.
    /// </summary>
    public static TiempoEditado Editar(TiempoDetencion guardado, TiempoCargado cargado, TimeZoneInfo zona)
    {
        if (cargado.HoraInicio is { } inicio && cargado.HoraFin is { } fin)
        {
            var manual = TiempoManualCalculador.Calcular(cargado.Fecha, inicio, fin, zona);
            var horarioCambio = manual.Parada != guardado.FechaHoraParada || manual.Arranque != guardado.FechaHoraArranque;
            var recalculado = new TiempoDetencion(OrigenTiempo.CALCULADO, manual.Minutos, null, manual.Parada, manual.Arranque);
            return new TiempoEditado(recalculado, horarioCambio);
        }

        if (cargado.FechaHoraParada.HasValue || cargado.FechaHoraArranque.HasValue)
        {
            var origen = cargado.Origen ?? guardado.Origen;
            var minutos = origen switch
            {
                OrigenTiempo.DECLARADO => cargado.MinutosDeclarados ?? 0,
                OrigenTiempo.CALCULADO when cargado.FechaHoraParada is { } parada && cargado.FechaHoraArranque is { } arranque
                    => (int)(arranque - parada).TotalMinutes,
                _ => guardado.MinutosEfectivos
            };
            var conFechas = new TiempoDetencion(origen, minutos, cargado.MinutosDeclarados, cargado.FechaHoraParada, cargado.FechaHoraArranque);
            return new TiempoEditado(conFechas, false);
        }

        if (guardado.Origen is OrigenTiempo.CALCULADO or OrigenTiempo.PLC)
            return new TiempoEditado(guardado, false);

        var declarado = new TiempoDetencion(OrigenTiempo.DECLARADO, cargado.MinutosDeclarados ?? 0, cargado.MinutosDeclarados, null, null);
        return new TiempoEditado(declarado, false);
    }
}
