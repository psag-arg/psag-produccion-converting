namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>Qué cambió de un turno de producción (para refrescar el tablero en línea, T-770).</summary>
public sealed record CambioTablero(long TurnoProduccionId, string Motivo);

/// <summary>Motivos de <see cref="CambioTablero"/>: el tablero se recarga entero igual; sirven para log y diagnóstico.</summary>
public static class MotivoCambioTablero
{
    /// <summary>Llegaron minutos de conteo de la cámara (ingesta).</summary>
    public const string Conteo = "CONTEO";
    /// <summary>Paradas de cámara (detenciones reales o microcortes).</summary>
    public const string Paradas = "PARADAS";
    /// <summary>Alta, declaración, edición o baja de una detención del turno.</summary>
    public const string Detenciones = "DETENCIONES";
    /// <summary>El turno o su producto cambió (cambio de producto, relevo, cierre, producción oficial).</summary>
    public const string Turno = "TURNO";
}

/// <summary>
/// T-770 · Avisa que cambiaron datos de turnos de producción, para que los tableros abiertos se actualicen sin recargar.
/// Lo llama el UnitOfWork después de guardar (ingesta de cámara, detenciones, turno). La API lo implementa con SignalR;
/// sin implementación registrada (jobs de tests, herramientas) no se avisa nada. Nunca tiene que hacer fallar el guardado.
/// </summary>
public interface INotificadorTablero
{
    Task NotificarAsync(IReadOnlyCollection<CambioTablero> cambios, CancellationToken cancellationToken = default);
}
