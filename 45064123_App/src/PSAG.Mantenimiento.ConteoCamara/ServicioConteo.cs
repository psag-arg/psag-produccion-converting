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
        // La 8 (bolsones) nunca detecta paradas; las demás, salvo que Maestros → Cámaras diga que no.
        IAnalizadorCuadros analizador = opciones.EsMangas || opciones.EsBolsones
            ? new ProcesadorMangas(opciones, salida, detectarParadas: !opciones.EsBolsones && opciones.DetectaParadas != false)
            : new ProcesadorConteo(opciones, salida);
        return new VigilanteVideo(analizador, salida, opciones.Vigilancia,
            opciones.Fuente.AnchoDecodificado * opciones.Fuente.AltoDecodificado);
    }
}

/// <summary>
/// Servicio de Windows (NSSM): lee en vivo cada cámara (09 por carriles, 10 por mangas, 8 por bolsones), cuenta,
/// detecta paradas y manda los lotes a la API. Cada cámara corre por separado: si una se corta, las otras siguen.
/// Si se corta el video reintenta con espera creciente (5 s → 60 s); el hueco queda como SIN_SENAL y, pasados
/// SegundosSinVideoParaAlerta, se abre una alerta SIN_VIDEO (pérdida de conexión) que se cierra al volver.
/// <para>
/// T-798 etapa 4: al arrancar y cada <c>Api:MinutosRefrescoConfiguracion</c> (5) pide a la API las cámaras
/// habilitadas (Maestros → Cámaras) y corre sólo las que además tienen calibración local (ConteoCamara:Camaras).
/// Una habilitada sin calibración se loguea y se alerta (SIN_CONFIGURACION). Si la API no responde, sigue con la
/// última lista conocida (cache en la carpeta de pendientes).
/// </para>
/// </summary>
public sealed class ServicioConteo : BackgroundService
{
    private readonly IReadOnlyList<OpcionesConteo> _camaras;
    private readonly OpcionesApi _api;
    private readonly ILogger<ServicioConteo> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly TimeZoneInfo _zona;

    public ServicioConteo(IReadOnlyList<OpcionesConteo> camaras, OpcionesApi api, ILogger<ServicioConteo> logger,
        IHttpClientFactory httpFactory, TimeZoneInfo zona)
    {
        _camaras = camaras;
        _api = api;
        _logger = logger;
        _httpFactory = httpFactory;
        _zona = zona;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var c in _camaras.Where(c => c.ActivaIgnorada))
            _logger.LogWarning("Cámara {Camara}: \"Activa\" del appsettings ya no se usa y se ignora. Habilitala o deshabilitala en Maestros → Cámaras.", c.Camara);

        var cliente = new ClienteConfiguracionCamaras(_api, _httpFactory.CreateClient("api"), _logger);
        var corriendo = new Dictionary<string, (CancellationTokenSource Cts, Task Tarea)>(StringComparer.OrdinalIgnoreCase);
        var alertadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CamaraRemota>? ultima = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var (lista, deCache) = await cliente.ObtenerAsync(stoppingToken);
                // La lista en memoria es tan nueva como el cache o más: el cache sólo sirve al arrancar sin API.
                if (lista != null && (!deCache || ultima == null))
                    ultima = lista;

                var espera = TimeSpan.FromMinutes(Math.Max(1, _api.MinutosRefrescoConfiguracion));
                if (ultima == null)
                {
                    _logger.LogWarning("Sin respuesta de la API y sin lista de cámaras guardada: no se corre ninguna cámara; se reintenta en 30 s");
                    espera = TimeSpan.FromSeconds(30);
                }
                else
                {
                    var (correr, sinCalibracion) = PlanCamaras.Elegir(_camaras, ultima);
                    var codigos = correr.Select(c => c.Camara).ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var codigo in corriendo.Keys.Where(k => !codigos.Contains(k)).ToList())
                    {
                        _logger.LogInformation("Cámara {Camara} deshabilitada en Maestros → Cámaras: se detiene", codigo);
                        var (cts, tarea) = corriendo[codigo];
                        cts.Cancel();
                        await EsperarAsync(tarea);
                        cts.Dispose();
                        corriendo.Remove(codigo);
                    }
                    foreach (var o in correr.Where(c => !corriendo.ContainsKey(c.Camara)))
                    {
                        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        corriendo[o.Camara] = (cts, Task.Run(() => CorrerCamaraAsync(o, cts.Token), CancellationToken.None));
                    }
                    foreach (var r in sinCalibracion.Where(r => alertadas.Add(r.Codigo)))
                    {
                        _logger.LogError("Cámara {Camara} habilitada en Maestros → Cámaras pero sin calibración en ConteoCamara:Camaras: no se lee", r.Codigo);
                        await AlertarSinConfiguracionAsync(r, stoppingToken);
                    }
                    if (corriendo.Count == 0)
                        _logger.LogWarning("Ninguna cámara para correr: habilitalas en Maestros → Cámaras (y que tengan su calibración local)");
                }

                try { await Task.Delay(espera, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            foreach (var (cts, _) in corriendo.Values) cts.Cancel();
            foreach (var (cts, tarea) in corriendo.Values)
            {
                await EsperarAsync(tarea);
                cts.Dispose();
            }
        }
    }

    private async Task EsperarAsync(Task tarea)
    {
        try { await tarea; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Error al detener una cámara"); }
    }

    /// <summary>Manda la alerta SIN_CONFIGURACION de una cámara habilitada que el servicio no sabe analizar.</summary>
    private async Task AlertarSinConfiguracionAsync(CamaraRemota remota, CancellationToken ct)
    {
        try
        {
            var o = new OpcionesConteo
            {
                Camara = remota.Codigo,
                Api = new OpcionesApi
                {
                    UrlBase = _api.UrlBase,
                    ClaveIngesta = _api.ClaveIngesta,
                    CarpetaPendientes = Path.Combine(_api.CarpetaPendientes, remota.Codigo),
                    SegundosEntreEnvios = _api.SegundosEntreEnvios
                }
            };
            var publicador = new PublicadorApi(o, _httpFactory.CreateClient("api"), _logger);
            publicador.Alerta(new AlertaVideo(TipoAlertaVideo.SIN_CONFIGURACION, TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _zona), null,
                "Habilitada en Maestros → Cámaras pero el servicio no tiene su calibración (ConteoCamara:Camaras del appsettings)."));
            publicador.GuardarPendiente();
            await publicador.EnviarPendientesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Cámara {Camara}: no se pudo mandar la alerta SIN_CONFIGURACION ({Mensaje})", remota.Codigo, ex.Message);
        }
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
            _logger.LogError("Cámara {Camara} sin credenciales del NVR: {Mensaje}. No se analiza.", opciones.Camara, ex.Message);
            return;
        }
        var fuente = new FuenteFfmpeg(opciones.Fuente, _logger, credenciales.Ocultar);
        var publicador = new PublicadorApi(opciones, _httpFactory.CreateClient("api"), _logger);
        VigilanteVideo cadena;
        try { cadena = Analizadores.Crear(opciones, publicador); }
        catch (InvalidOperationException ex)
        {
            // Calibración inválida (p. ej. Mangas:DetectorParadas desconocido, T-804): no se lee esta cámara.
            _logger.LogError("Cámara {Camara}: {Error} No se lee.", opciones.Camara, ex.Message);
            return;
        }
        var url = credenciales.UrlRtsp(opciones.Fuente.PuertoRtsp, opciones.Fuente.RutaEfectiva);

        _logger.LogInformation("Cámara {Camara} iniciada: método {Metodo}{Detector}, NVR {Nvr}{Ruta}, {Fps} fps a {Ancho}x{Alto}",
            opciones.Camara, opciones.Metodo,
            opciones.EsMangas && opciones.DetectaParadas != false ? $" (paradas: {opciones.Mangas.DetectorParadas.Trim().ToUpperInvariant()})" : "",
            credenciales.Host, opciones.Fuente.RutaEfectiva,
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
                            _logger.LogWarning("Cámara {Camara}: salto en la hora de los cuadros; se descarta la ventana en curso", opciones.Camara);
                            cadena.CortarSenal(final: false);
                        }
                        if (sinVideo.Volvio(hora) is { } cerrada) publicador.Alerta(cerrada);
                        cadena.Cuadro(cuadro, hora);
                        if (cuadros++ == 0)
                            _logger.LogInformation("Cámara {Camara}: video recibido del NVR", opciones.Camara);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cámara {Camara}: error leyendo el video", opciones.Camara);
                }

                cadena.CortarSenal(final: false);
                if (stoppingToken.IsCancellationRequested) break;
                if (sinVideo.Revisar(cadena.UltimoCuadro) is { } abierta) publicador.Alerta(abierta);

                espera = cuadros > 0 ? TimeSpan.FromSeconds(Math.Max(1, opciones.Fuente.SegundosEsperaReconexion))
                    : TimeSpan.FromSeconds(Math.Min(60, espera.TotalSeconds * 2));
                _logger.LogWarning("Cámara {Camara}: se cortó el video del NVR; reconexión en {Segundos} s", opciones.Camara, espera.TotalSeconds);
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
            catch (Exception ex) { _logger.LogWarning("Cámara {Camara}: no se pudo mandar el último lote ({Mensaje}); queda pendiente", opciones.Camara, ex.Message); }
            _logger.LogInformation("Cámara {Camara} detenida", opciones.Camara);
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
                    _logger.LogDebug("Cámara {Camara}: última ventana {Hora:HH:mm:ss} {Estado}", opciones.Camara, v.Inicio, v.EstadoLinea);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cámara {Camara}: error guardando o enviando lotes", opciones.Camara);
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
