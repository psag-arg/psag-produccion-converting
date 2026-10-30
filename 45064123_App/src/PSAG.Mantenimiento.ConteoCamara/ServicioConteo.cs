using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSAG.Mantenimiento.ConteoCamara.Analisis;
using PSAG.Mantenimiento.ConteoCamara.Salida;
using PSAG.Mantenimiento.ConteoCamara.Video;

namespace PSAG.Mantenimiento.ConteoCamara;

/// <summary>
/// Hora de cada cuadro en vivo: reloj al primer cuadro + n/fps (parejo, sin el jitter de llegada). Si se desfasa
/// más de 3 s del reloj real (el NVR mandó a los saltos o se colgó un rato) se vuelve a anclar.
/// </summary>
public sealed class RelojCuadros
{
    private readonly double _fps;
    private readonly TimeZoneInfo _zona;
    private readonly Func<DateTimeOffset> _ahora;
    private DateTimeOffset? _ancla;
    private long _n;

    public RelojCuadros(double fps, TimeZoneInfo zona, Func<DateTimeOffset>? ahora = null)
    {
        _fps = fps;
        _zona = zona;
        _ahora = ahora ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Devuelve la hora del cuadro y si hubo que re-anclar (hueco: la ventana en curso no sirve).</summary>
    public (DateTimeOffset Hora, bool Reanclado) Siguiente()
    {
        var ahora = TimeZoneInfo.ConvertTime(_ahora(), _zona);
        var reanclado = false;
        if (_ancla is { } ancla)
        {
            var esperado = ancla.AddSeconds(_n / _fps);
            if (Math.Abs((ahora - esperado).TotalSeconds) > 3)
            {
                _ancla = ahora;
                _n = 0;
                reanclado = true;
            }
        }
        else
        {
            _ancla = ahora;
            _n = 0;
        }
        var hora = _ancla!.Value.AddSeconds(_n / _fps);
        _n++;
        return (hora, reanclado);
    }

    public void Reiniciar() => _ancla = null;
}

/// <summary>
/// Servicio de Windows (NSSM): lee el canal 09 en vivo, cuenta y detecta paradas, y manda los lotes a la API.
/// Si se corta el video reintenta con espera creciente (5 s → 60 s); el hueco queda como SIN_SENAL.
/// </summary>
public sealed class ServicioConteo : BackgroundService
{
    private readonly OpcionesConteo _opciones;
    private readonly ILogger<ServicioConteo> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly TimeZoneInfo _zona;

    public ServicioConteo(IOptions<OpcionesConteo> opciones, ILogger<ServicioConteo> logger, IHttpClientFactory httpFactory, TimeZoneInfo zona)
    {
        _opciones = opciones.Value;
        _logger = logger;
        _httpFactory = httpFactory;
        _zona = zona;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var credenciales = CredencialesNvr.Leer(_opciones.Fuente.ArchivoCredenciales);
        var fuente = new FuenteFfmpeg(_opciones.Fuente, _logger, credenciales.Ocultar);
        var publicador = new PublicadorApi(_opciones, _httpFactory.CreateClient("api"), _logger);
        var procesador = new ProcesadorConteo(_opciones, publicador);
        var url = credenciales.UrlRtsp(_opciones.Fuente.PuertoRtsp, _opciones.Fuente.RutaRtsp);

        _logger.LogInformation("Conteo por cámara iniciado: punto {Punto}, NVR {Nvr}{Ruta}, {Carriles} carriles, {Fps} fps a {Ancho}x{Alto}",
            _opciones.PuntoControl, credenciales.Host, _opciones.Fuente.RutaRtsp, _opciones.Zonas.Carriles.Count,
            _opciones.Fuente.Fps, _opciones.Fuente.AnchoDecodificado, _opciones.Fuente.AltoDecodificado);

        using var envios = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var tareaEnvios = BucleEnviosAsync(publicador, envios.Token);

        var espera = TimeSpan.FromSeconds(Math.Max(1, _opciones.Fuente.SegundosEsperaReconexion));
        var reloj = new RelojCuadros(_opciones.Fuente.Fps, _zona);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cuadros = 0L;
                reloj.Reiniciar();
                try
                {
                    await foreach (var cuadro in fuente.LeerAsync(url, esRtsp: true, ct: stoppingToken))
                    {
                        var (hora, reanclado) = reloj.Siguiente();
                        if (reanclado)
                        {
                            _logger.LogWarning("Salto en la hora de los cuadros; se descarta la ventana en curso");
                            procesador.CortarSenal(final: false);
                        }
                        procesador.Cuadro(cuadro, hora);
                        if (cuadros++ == 0)
                            _logger.LogInformation("Video recibido del NVR");
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error leyendo el video");
                }

                procesador.CortarSenal(final: false);
                if (stoppingToken.IsCancellationRequested) break;

                espera = cuadros > 0 ? TimeSpan.FromSeconds(Math.Max(1, _opciones.Fuente.SegundosEsperaReconexion))
                    : TimeSpan.FromSeconds(Math.Min(60, espera.TotalSeconds * 2));
                _logger.LogWarning("Se cortó el video del NVR; reconexión en {Segundos} s", espera.TotalSeconds);
                try { await Task.Delay(espera, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            procesador.CortarSenal(final: true);
            publicador.GuardarPendiente();
            envios.Cancel();
            try { await tareaEnvios; } catch (OperationCanceledException) { }
            try
            {
                using var ultimo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await publicador.EnviarPendientesAsync(ultimo.Token);
            }
            catch (Exception ex) { _logger.LogWarning("No se pudo mandar el último lote ({Mensaje}); queda pendiente", ex.Message); }
            _logger.LogInformation("Conteo por cámara detenido");
        }
    }

    private async Task BucleEnviosAsync(PublicadorApi publicador, CancellationToken ct)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _opciones.Api.SegundosEntreEnvios));
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(intervalo, ct); } catch (OperationCanceledException) { return; }
            try
            {
                publicador.GuardarPendiente();
                await publicador.EnviarPendientesAsync(ct);
                if (publicador.UltimaVentana is { } v)
                    _logger.LogDebug("Última ventana {Hora:HH:mm:ss}: {Estado}, {Rollos:0} rollos/min", v.Inicio, v.EstadoLinea, v.Rollos / v.Segundos * 60);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando o enviando lotes");
            }
        }
    }
}
