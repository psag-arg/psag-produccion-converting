using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Jobs;

/// <summary>
/// T-798 etapa 2 · Cada minuto (TurnosProduccion:JobIntervaloSegundos): cierra los turnos de producción vencidos
/// (FinProgramado + tolerancia → CERRADO_AUTOMATICO) y marca SIN_APERTURA los programados que nadie abrió.
/// T-798 etapa 4: también abre la alerta SIN_DATOS de la cámara oficial de cada línea con turno ABIERTO que no manda
/// minutos hace más de TurnosProduccion:MinutosSinDatosCamara (5).
/// Un error no detiene el job: se registra y se reintenta en la vuelta siguiente.
/// </summary>
public sealed class TurnosProduccionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TurnosProduccionOpciones _opciones;
    private readonly ILogger<TurnosProduccionJob> _logger;

    public TurnosProduccionJob(IServiceScopeFactory scopeFactory, TurnosProduccionOpciones opciones, ILogger<TurnosProduccionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.JobHabilitado)
        {
            _logger.LogInformation("Job de turnos de producción deshabilitado (TurnosProduccion:JobHabilitado = false).");
            return;
        }

        // Se deja arrancar la API (y que el inicializador cree la base) antes de la primera vuelta.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, _opciones.JobIntervaloSegundos)));
        do
        {
            await EjecutarUnaVezAsync(stoppingToken);
        }
        while (await EsperarAsync(timer, stoppingToken));
    }

    private static async Task<bool> EsperarAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task EjecutarUnaVezAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<ITurnoProduccionService>();
            var ahora = DateTimeOffset.UtcNow;
            var cerrados = await servicio.CerrarVencidosAsync(ahora, stoppingToken);
            var sinApertura = await servicio.MarcarSinAperturaAsync(ahora, stoppingToken);
            var camaras = scope.ServiceProvider.GetRequiredService<IConteoCamaraService>();
            var sinDatos = await camaras.VigilarSinDatosAsync(ahora, _opciones.MinutosSinDatosCamara, stoppingToken);
            if (cerrados > 0 || sinApertura > 0 || sinDatos > 0)
                _logger.LogInformation("Job de turnos de producción: {Cerrados} cerrados por vencimiento, {SinApertura} sin apertura, {SinDatos} alertas de cámara sin datos",
                    cerrados, sinApertura, sinDatos);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // La API se está deteniendo.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el job de turnos de producción; se reintenta en la próxima vuelta.");
        }
    }
}
