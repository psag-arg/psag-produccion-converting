using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Jobs;

/// <summary>
/// T-798 etapa 2 · Cada minuto (TurnosProduccion:JobIntervaloSegundos). Turno automático (script 44): el turno es de la
/// MÁQUINA. Cierra a la hora de fin exacta los ABIERTOS (CERRADO_AUTOMATICO, FinReal = FinProgramado; corta las paradas
/// que siguen abiertas), abre a la hora de inicio el turno de cada línea PROGRAMADA (SISTEMA, sin operador, producto
/// heredado sin confirmar; si el job estuvo parado, los que faltan de las últimas 24 h se crean ya terminados) y cierra el
/// tramo del operario cuya tolerancia (ABM_Turnos.ToleranciaCierreOperadorMinutos) venció. Ya no genera SIN_APERTURA.
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
            var r = await servicio.ProcesarAsync(ahora, stoppingToken);
            var camaras = scope.ServiceProvider.GetRequiredService<IConteoCamaraService>();
            var sinDatos = await camaras.VigilarSinDatosAsync(ahora, _opciones.MinutosSinDatosCamara, stoppingToken);
            if (r.Cerrados > 0 || r.Abiertos > 0 || r.CreadosTerminados > 0 || r.SesionesVencidas > 0 || sinDatos > 0)
                _logger.LogInformation(
                    "Job de turnos de producción: {Cerrados} cerrados a la hora, {Abiertos} abiertos automáticamente, {Terminados} creados ya terminados, {Sesiones} sesiones de operario vencidas, {SinDatos} alertas de cámara sin datos",
                    r.Cerrados, r.Abiertos, r.CreadosTerminados, r.SesionesVencidas, sinDatos);
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
