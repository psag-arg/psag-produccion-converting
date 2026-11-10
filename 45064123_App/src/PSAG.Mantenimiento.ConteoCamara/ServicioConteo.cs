using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

/// <summary>Arma la cadena de análisis de una cámara según su método: vigilante de video → analizador.</summary>
public static class Analizadores
{
    public static VigilanteVideo Crear(OpcionesConteo opciones, ISalidaConteo salida)
    {
        IAnalizadorCuadros analizador = opciones.EsMangas
            ? new ProcesadorMangas(opciones, salida)
            : new ProcesadorConteo(opciones, salida);
        return new VigilanteVideo(analizador, salida, opciones.Vigilancia,
            opciones.Fuente.AnchoDecodificado * opciones.Fuente.AltoDecodificado);
    }
}

/// <summary>
/// Servicio de Windows (NSSM): lee en vivo cada cámara configurada (09 por carriles, 10 por mangas), cuenta,
/// detecta paradas y manda los lotes a la API. Cada cámara corre por separado: si una se corta, las otras siguen.
/// Si se corta el video reintenta con espera creciente (5 s → 60 s); el hueco queda como SIN_SENAL y, pasados
/// SegundosSinVideoParaAlerta, se abre una alerta SIN_VIDEO (pérdida de conexión) que se cierra al volver.
/// </summary>
public sealed class ServicioConteo : BackgroundService
{
    private readonly IReadOnlyList<OpcionesConteo> _camaras;
    private readonly ILogger<ServicioConteo> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly TimeZoneInfo _zona;

    public ServicioConteo(IReadOnlyList<OpcionesConteo> camaras, ILogger<ServicioConteo> logger, IHttpClientFactory httpFactory, TimeZoneInfo zona)
    {
        _camaras = camaras;
        _logger = logger;
        _httpFactory = httpFactory;
        _zona = zona;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var activas = _camaras.Where(c => c.Activa).ToList();
        foreach (var c in _camaras.Where(c => !c.Activa))
            _logger.LogInformation("Cámara {Punto} apagada en la configuración (Activa=false): no se lee", c.PuntoControl);
        if (activas.Count == 0)
            _logger.LogWarning("No hay cámaras activas en ConteoCamara");
        return Task.WhenAll(activas.Select(c => Task.Run(() => CorrerCamaraAsync(c, stoppingToken), stoppingToken)));
    }

    private async Task CorrerCamaraAsync(OpcionesConteo opciones, CancellationToken stoppingToken)
    {
        CredencialesNvr credenciales;
        try
        {
            credenciales = CredencialesNvr.Leer(opciones.Fuente.ArchivoCredenciales);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            // Una cámara mal configurada no tira abajo a las otras.
            _logger.LogError("Cámara {Punto} sin credenciales del NVR: {Mensaje}. No se analiza.", opciones.PuntoControl, ex.Message);
            return;
        }
        var fuente = new FuenteFfmpeg(opciones.Fuente, _logger, credenciales.Ocultar);
        var publicador = new PublicadorApi(opciones, _httpFactory.CreateClient("api"), _logger);
        var cadena = Analizadores.Crear(opciones, publicador);
        var url = credenciales.UrlRtsp(opciones.Fuente.PuertoRtsp, opciones.Fuente.RutaRtsp);

        _logger.LogInformation("Cámara {Punto} iniciada: método {Metodo}, NVR {Nvr}{Ruta}, {Fps} fps a {Ancho}x{Alto}",
            opciones.PuntoControl, opciones.Metodo, credenciales.Host, opciones.Fuente.RutaRtsp,
            opciones.Fuente.Fps, opciones.Fuente.AnchoDecodificado, opciones.Fuente.AltoDecodificado);

        using var envios = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var tareaEnvios = BucleEnviosAsync(opciones, publicador, envios.Token);

        var espera = TimeSpan.FromSeconds(Math.Max(1, opciones.Fuente.SegundosEsperaReconexion));
        var reloj = new RelojCuadros(opciones.Fuente.Fps, _zona);
        var sinVideo = new AlertaSinVideo(opciones.Vigilancia.SegundosSinVideoParaAlerta, _zona);
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
                            _logger.LogWarning("Cámara {Punto}: salto en la hora de los cuadros; se descarta la ventana en curso", opciones.PuntoControl);
                            cadena.CortarSenal(final: false);
                        }
                        if (sinVideo.Volvio(hora) is { } cerrada) publicador.Alerta(cerrada);
                        cadena.Cuadro(cuadro, hora);
                        if (cuadros++ == 0)
                            _logger.LogInformation("Cámara {Punto}: video recibido del NVR", opciones.PuntoControl);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cámara {Punto}: error leyendo el video", opciones.PuntoControl);
                }

                cadena.CortarSenal(final: false);
                if (stoppingToken.IsCancellationRequested) break;
                if (sinVideo.Revisar(cadena.UltimoCuadro) is { } abierta) publicador.Alerta(abierta);

                espera = cuadros > 0 ? TimeSpan.FromSeconds(Math.Max(1, opciones.Fuente.SegundosEsperaReconexion))
                    : TimeSpan.FromSeconds(Math.Min(60, espera.TotalSeconds * 2));
                _logger.LogWarning("Cámara {Punto}: se cortó el video del NVR; reconexión en {Segundos} s", opciones.PuntoControl, espera.TotalSeconds);
                try { await Task.Delay(espera, stoppingToken); } catch (OperationCanceledException) { break; }
                if (sinVideo.Revisar(cadena.UltimoCuadro) is { } abierta2) publicador.Alerta(abierta2);
            }
        }
        finally
        {
            cadena.CortarSenal(final: true);
            // El minuto de reloj en curso no se manda partido: se completa al volver a arrancar (ver PublicadorApi).
            publicador.GuardarPendiente(apartarMinutoEnCurso: true);
            envios.Cancel();
            try { await tareaEnvios; } catch (OperationCanceledException) { }
            try
            {
                using var ultimo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await publicador.EnviarPendientesAsync(ultimo.Token);
            }
            catch (Exception ex) { _logger.LogWarning("Cámara {Punto}: no se pudo mandar el último lote ({Mensaje}); queda pendiente", opciones.PuntoControl, ex.Message); }
            _logger.LogInformation("Cámara {Punto} detenida", opciones.PuntoControl);
        }
    }

    private async Task BucleEnviosAsync(OpcionesConteo opciones, PublicadorApi publicador, CancellationToken ct)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(5, opciones.Api.SegundosEntreEnvios));
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(intervalo, ct); } catch (OperationCanceledException) { return; }
            try
            {
                publicador.GuardarPendiente();
                await publicador.EnviarPendientesAsync(ct);
                if (publicador.UltimaVentana is { } v)
                    _logger.LogDebug("Cámara {Punto}: última ventana {Hora:HH:mm:ss} {Estado}", opciones.PuntoControl, v.Inicio, v.EstadoLinea);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cámara {Punto}: error guardando o enviando lotes", opciones.PuntoControl);
            }
        }
    }
}

/// <summary>
/// Alerta SIN_VIDEO (pérdida de conexión): se abre cuando pasan <c>segundos</c> sin cuadros desde el último
/// (o desde el arranque del servicio, si nunca llegó ninguno) y se cierra con el primer cuadro que vuelve.
/// </summary>
public sealed class AlertaSinVideo
{
    private readonly TimeSpan _limite;
    private readonly TimeZoneInfo _zona;
    private readonly Func<DateTimeOffset> _ahora;
    private readonly DateTimeOffset _arranque;
    private DateTimeOffset? _abiertaDesde;

    public AlertaSinVideo(int segundos, TimeZoneInfo zona, Func<DateTimeOffset>? ahora = null)
    {
        _limite = TimeSpan.FromSeconds(Math.Max(10, segundos));
        _zona = zona;
        _ahora = ahora ?? (() => DateTimeOffset.UtcNow);
        _arranque = Ahora();
    }

    private DateTimeOffset Ahora() => TimeZoneInfo.ConvertTime(_ahora(), _zona);

    public bool Abierta => _abiertaDesde != null;

    /// <summary>Sin video: devuelve la alerta a abrir si ya pasó el límite y no estaba abierta.</summary>
    public AlertaVideo? Revisar(DateTimeOffset? ultimoCuadro)
    {
        if (_abiertaDesde != null) return null;
        var desde = ultimoCuadro ?? _arranque;
        if (Ahora() - desde < _limite) return null;
        _abiertaDesde = desde;
        return new AlertaVideo(TipoAlertaVideo.SIN_VIDEO, desde, null, "No llega video del grabador (pérdida de conexión de cámara).");
    }

    /// <summary>Llegó un cuadro: devuelve la alerta cerrada si estaba abierta.</summary>
    public AlertaVideo? Volvio(DateTimeOffset hora)
    {
        if (_abiertaDesde is not { } desde) return null;
        _abiertaDesde = null;
        return new AlertaVideo(TipoAlertaVideo.SIN_VIDEO, desde, hora < desde ? desde : hora, "Volvió el video.");
    }
}
