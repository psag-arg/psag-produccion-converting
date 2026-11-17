using Microsoft.Extensions.Configuration;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Refresco automático de una pantalla (Dashboard, reunión 17/11/2026): llama a la carga cada
/// <see cref="Intervalo"/> sin solaparse nunca consigo misma.
/// <list type="bullet">
/// <item>Si la carga anterior todavía no terminó (red lenta), el tic del timer se saltea: no se apilan llamadas.</item>
/// <item><see cref="SolicitarAsync"/> (botón Actualizar, cambio de filtro): si hay una carga en curso, se repite una
/// sola vez al terminar, para que el filtro nuevo siempre se cargue.</item>
/// <item>Un error de la carga no corta el refresco: se informa en <c>alFallar</c> y el próximo tic vuelve a probar.</item>
/// <item><see cref="DisposeAsync"/> cancela la carga en curso y libera el timer (al salir de la pantalla).</item>
/// </list>
/// </summary>
public sealed class RefrescoPeriodico : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _cargar;
    private readonly Action<Exception>? _alFallar;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private PeriodicTimer? _timer;
    private Task _enCurso = Task.CompletedTask;
    private bool _pendiente;
    private bool _liberado;

    public RefrescoPeriodico(TimeSpan intervalo, Func<CancellationToken, Task> cargar, Action<Exception>? alFallar = null)
    {
        if (intervalo <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(intervalo), "El intervalo tiene que ser mayor que cero.");
        Intervalo = intervalo;
        _cargar = cargar;
        _alFallar = alFallar;
    }

    public TimeSpan Intervalo { get; }

    /// <summary>Hay una carga en curso.</summary>
    public bool EnCurso
    {
        get { lock (_lock) return !_enCurso.IsCompleted; }
    }

    /// <summary>Arranca el timer (la primera carga la hace la pantalla). Llamarlo dos veces no hace nada.</summary>
    public void Iniciar()
    {
        lock (_lock)
        {
            if (_liberado || _timer != null)
                return;
            _timer = new PeriodicTimer(Intervalo);
            _ = BucleAsync(_timer);
        }
    }

    /// <summary>Carga ya. Si hay una en curso, la espera y vuelve a cargar una vez más al terminar.</summary>
    public Task SolicitarAsync() => Ejecutar(encolarSiOcupado: true);

    private async Task BucleAsync(PeriodicTimer timer)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
                await Ejecutar(encolarSiOcupado: false);
        }
        catch (OperationCanceledException)
        {
            // Se salió de la pantalla.
        }
    }

    private Task Ejecutar(bool encolarSiOcupado)
    {
        lock (_lock)
        {
            if (_liberado)
                return Task.CompletedTask;
            if (!_enCurso.IsCompleted)
            {
                if (!encolarSiOcupado)
                    return Task.CompletedTask; // tic del timer con una carga lenta: se saltea
                _pendiente = true;
                return _enCurso;
            }
            _enCurso = CorrerAsync();
            return _enCurso;
        }
    }

    private async Task CorrerAsync()
    {
        // Sin esto, en un hilo con contexto síncrono la carga correría dentro del lock de Ejecutar.
        await Task.Yield();
        while (true)
        {
            lock (_lock)
                _pendiente = false;
            if (_cts.IsCancellationRequested)
                return;
            try
            {
                await _cargar(_cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _alFallar?.Invoke(ex);
            }
            lock (_lock)
            {
                if (!_pendiente || _liberado)
                    return;
            }
        }
    }

    /// <summary>
    /// Corta el timer y cancela la carga en curso. No espera a que esa carga termine (una llamada sin token de
    /// cancelación podría demorar la salida de la pantalla): lo que llegue después no se aplica ni dispara otra carga.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_liberado)
                return ValueTask.CompletedTask;
            _liberado = true;
        }
        _cts.Cancel();
        _timer?.Dispose();
        // El CancellationTokenSource no se libera: el bucle y la carga cancelados todavía pueden leer el token.
        return ValueTask.CompletedTask;
    }
}

/// <summary>Opciones del Dashboard en <c>wwwroot/appsettings.json</c> → sección <c>Dashboard</c>.</summary>
public sealed record OpcionesDashboard(int SegundosRefresco, int MinutosDatosAtrasados)
{
    public const int SegundosRefrescoPorDefecto = 60;
    public const int SegundosRefrescoMinimo = 15;
    public const int SegundosRefrescoMaximo = 3600;
    public const int MinutosDatosAtrasadosPorDefecto = 5;

    /// <summary>0 = sin refresco automático (sólo el botón Actualizar).</summary>
    public bool RefrescoActivo => SegundosRefresco > 0;

    /// <summary>
    /// <c>Dashboard:SegundosRefresco</c> (60 por defecto; 0 lo apaga; si no, 15 a 3600) y
    /// <c>Dashboard:MinutosDatosAtrasados</c> (5 por defecto; la API lo acota a 2..120).
    /// </summary>
    public static OpcionesDashboard Leer(IConfiguration configuracion)
    {
        var segundos = LeerEntero(configuracion["Dashboard:SegundosRefresco"]) ?? SegundosRefrescoPorDefecto;
        if (segundos != 0)
            segundos = Math.Clamp(segundos, SegundosRefrescoMinimo, SegundosRefrescoMaximo);
        var minutos = LeerEntero(configuracion["Dashboard:MinutosDatosAtrasados"]) ?? MinutosDatosAtrasadosPorDefecto;
        return new OpcionesDashboard(Math.Max(0, segundos), minutos > 0 ? minutos : MinutosDatosAtrasadosPorDefecto);
    }

    private static int? LeerEntero(string? valor) => int.TryParse(valor, out var n) ? n : null;
}
