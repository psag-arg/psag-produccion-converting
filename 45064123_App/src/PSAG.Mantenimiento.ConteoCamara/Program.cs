using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara.Analisis;
using PSAG.Mantenimiento.ConteoCamara.Herramientas;
using PSAG.Mantenimiento.ConteoCamara.Salida;
using PSAG.Mantenimiento.ConteoCamara.Video;

// T-610 · Conteo de producción y paradas con la cámara 09 (salida de la cortadora).
//   (sin argumentos)                    servicio (lo corre NSSM)
//   procesar --archivo X.mp4 --inicio 2026-10-28T10:15:00-03:00 [--json salida.json] [--eco] [--enviar]
//   vivo --segundos 300 [--json salida.json]          prueba en vivo contra el NVR, sin mandar a la API
//   foto --salida foto.jpg [--archivo X.mp4 --segundo 30]  foto a resolución completa (NVR o video)
//   calibrar --foto foto.jpg            arma calibracion.html al lado de la foto para mover las zonas
//   sugerir (--archivo X.mp4 | --segundos 300)       busca mejores puntos de muestreo por carril

var argumentos = Argumentos.Parsear(args);
var configuracion = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("zonas-camara.json", optional: true)
    .AddJsonFile("appsettings.local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var opciones = configuracion.GetSection(OpcionesConteo.Seccion).Get<OpcionesConteo>() ?? new OpcionesConteo();
var zona = ZonaPlanta(configuracion["ConteoCamara:ZonaHoraria"]);

if (argumentos.Comando is null or "servicio")
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory, Args = [] });
    builder.Configuration.AddConfiguration(configuracion);
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "yyyy-MM-dd HH:mm:ss "; });
    builder.Services.Configure<OpcionesConteo>(configuracion.GetSection(OpcionesConteo.Seccion));
    builder.Services.AddSingleton(zona);
    builder.Services.AddHttpClient("api", c =>
    {
        c.BaseAddress = new Uri(opciones.Api.UrlBase.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(30);
    });
    builder.Services.AddHostedService<ServicioConteo>();
    await builder.Build().RunAsync();
    return 0;
}

using var fabricaLogs = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
var logger = fabricaLogs.CreateLogger("ConteoCamara");
using var cancelar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancelar.Cancel(); };

try
{
    switch (argumentos.Comando)
    {
        case "procesar":
            return await ProcesarAsync();
        case "vivo":
            return await VivoAsync();
        case "foto":
            return await FotoAsync();
        case "calibrar":
            return Calibrar();
        case "sugerir":
            return await SugerirAsync();
        default:
            Console.Error.WriteLine($"Comando desconocido: {argumentos.Comando}. Ver el encabezado de Program.cs o docs/CONTEO_CAMARA.md.");
            return 2;
    }
}
catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    return 1;
}

async Task<int> ProcesarAsync()
{
    var archivo = argumentos.Requerido("archivo");
    var inicio = argumentos.Valor("inicio") is { } t
        ? DateTimeOffset.Parse(t, CultureInfo.InvariantCulture)
        : TimeZoneInfo.ConvertTime(File.GetLastWriteTimeUtc(archivo), zona);
    var fuente = new FuenteFfmpeg(opciones.Fuente, logger);

    PublicadorApi? publicador = null;
    if (argumentos.Tiene("enviar"))
        publicador = new PublicadorApi(opciones, new HttpClient { BaseAddress = new Uri(opciones.Api.UrlBase.TrimEnd('/') + "/") }, logger);
    var resumen = new ResumenConsola(publicador, eco: argumentos.Tiene("eco"));
    var procesador = new ProcesadorConteo(opciones, resumen);

    var reloj = Stopwatch.StartNew();
    long n = 0;
    await foreach (var cuadro in fuente.LeerAsync(Path.GetFullPath(archivo), esRtsp: false, ct: cancelar.Token))
        procesador.Cuadro(cuadro, inicio.AddSeconds(n++ / procesador.Fps));
    procesador.CortarSenal(final: true);
    reloj.Stop();

    return await TerminarAsync(resumen, Path.GetFileNameWithoutExtension(archivo), reloj.Elapsed.TotalSeconds, publicador);
}

async Task<int> VivoAsync()
{
    var segundos = int.Parse(argumentos.Valor("segundos") ?? "300", CultureInfo.InvariantCulture);
    var credenciales = CredencialesNvr.Leer(opciones.Fuente.ArchivoCredenciales);
    var fuente = new FuenteFfmpeg(opciones.Fuente, logger, credenciales.Ocultar);
    var resumen = new ResumenConsola(eco: true);
    var procesador = new ProcesadorConteo(opciones, resumen);
    var relojCuadros = new RelojCuadros(opciones.Fuente.Fps, zona);
    Console.WriteLine($"En vivo desde {credenciales} {opciones.Fuente.RutaRtsp} durante {segundos} s (sólo lectura, una conexión)...");

    using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelar.Token);
    limite.CancelAfter(TimeSpan.FromSeconds(segundos));
    var reloj = Stopwatch.StartNew();
    try
    {
        await foreach (var cuadro in fuente.LeerAsync(credenciales.UrlRtsp(opciones.Fuente.PuertoRtsp, opciones.Fuente.RutaRtsp), esRtsp: true, ct: limite.Token))
        {
            var (hora, reanclado) = relojCuadros.Siguiente();
            if (reanclado) procesador.CortarSenal(final: false);
            procesador.Cuadro(cuadro, hora);
        }
    }
    catch (OperationCanceledException) { }
    procesador.CortarSenal(final: true);
    return await TerminarAsync(resumen, "vivo-" + DateTime.Now.ToString("yyyyMMdd-HHmm"), reloj.Elapsed.TotalSeconds, null);
}

async Task<int> TerminarAsync(ResumenConsola resumen, string nombre, double segundos, PublicadorApi? publicador)
{
    var r = resumen.Resumen(nombre, segundos);
    var json = ResumenConsola.AJson(r);
    if (argumentos.Valor("json") is { } salida)
    {
        var completo = new { resumen = r, minutos = resumen.MinutosRecibidos.Select(MinutoLote.Desde) };
        await File.WriteAllTextAsync(salida, JsonSerializer.Serialize(completo, new JsonSerializerOptions { WriteIndented = true }));
    }
    Console.WriteLine(json);
    if (publicador != null)
    {
        publicador.GuardarPendiente();
        await publicador.EnviarPendientesAsync(cancelar.Token);
        Console.WriteLine($"Lotes pendientes sin enviar: {publicador.Pendientes}");
    }
    return 0;
}

async Task<int> FotoAsync()
{
    var salida = Path.GetFullPath(argumentos.Valor("salida") ?? "foto-camara.jpg");
    if (argumentos.Valor("archivo") is { } archivo)
    {
        var fuente = new FuenteFfmpeg(opciones.Fuente, logger);
        var segundo = double.Parse(argumentos.Valor("segundo") ?? "0", CultureInfo.InvariantCulture);
        await fuente.FotoAsync(Path.GetFullPath(archivo), esRtsp: false, salida, segundo, cancelar.Token);
    }
    else
    {
        var credenciales = CredencialesNvr.Leer(opciones.Fuente.ArchivoCredenciales);
        var fuente = new FuenteFfmpeg(opciones.Fuente, logger, credenciales.Ocultar);
        await fuente.FotoAsync(credenciales.UrlRtsp(opciones.Fuente.PuertoRtsp, opciones.Fuente.RutaRtsp), esRtsp: true, salida, null, cancelar.Token);
    }
    Console.WriteLine($"Foto guardada en {salida}. Siguiente paso: calibrar --foto \"{salida}\"");
    return 0;
}

int Calibrar()
{
    var foto = Path.GetFullPath(argumentos.Requerido("foto"));
    if (!File.Exists(foto)) throw new FileNotFoundException($"No existe la foto {foto}");
    var plantilla = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Herramientas", "calibracion.html"));
    var html = plantilla
        .Replace("\"__FOTO__\"", JsonSerializer.Serialize(Path.GetFileName(foto)))
        .Replace("__ZONAS__", JsonSerializer.Serialize(opciones.Zonas));
    var destino = Path.Combine(Path.GetDirectoryName(foto)!, "calibracion.html");
    File.WriteAllText(destino, html);
    Console.WriteLine($"Abrí {destino} en el navegador (funciona sin internet). Descargá zonas-camara.json y copialo a {AppContext.BaseDirectory}.");
    return 0;
}

async Task<int> SugerirAsync()
{
    var sugeridor = new SugeridorZonas(opciones,
        int.Parse(argumentos.Valor("radio") ?? "48", CultureInfo.InvariantCulture),
        int.Parse(argumentos.Valor("paso") ?? "8", CultureInfo.InvariantCulture));
    if (argumentos.Valor("archivo") is { } archivo)
    {
        var fuente = new FuenteFfmpeg(opciones.Fuente, logger);
        await foreach (var cuadro in fuente.LeerAsync(Path.GetFullPath(archivo), esRtsp: false, ct: cancelar.Token))
            sugeridor.Cuadro(cuadro);
    }
    else
    {
        var segundos = int.Parse(argumentos.Valor("segundos") ?? "300", CultureInfo.InvariantCulture);
        var credenciales = CredencialesNvr.Leer(opciones.Fuente.ArchivoCredenciales);
        var fuente = new FuenteFfmpeg(opciones.Fuente, logger, credenciales.Ocultar);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelar.Token);
        limite.CancelAfter(TimeSpan.FromSeconds(segundos));
        try
        {
            await foreach (var cuadro in fuente.LeerAsync(credenciales.UrlRtsp(opciones.Fuente.PuertoRtsp, opciones.Fuente.RutaRtsp), esRtsp: true, ct: limite.Token))
                sugeridor.Cuadro(cuadro);
        }
        catch (OperationCanceledException) { }
    }

    Console.WriteLine("carril  x     y     puntaje  peor   amplitud pertenencia");
    foreach (var s in sugeridor.Sugerir())
        Console.WriteLine($"{s.Carril,-7} {s.X,-5} {s.Y,-5} {s.Puntaje,-8:0.000} {s.PeorVentana,-6:0.00} {s.Amplitud,-8:0.0} {s.Pertenencia,-11:0.00} {(s.EsActual ? "(actual)" : "")}");
    Console.WriteLine("Puntaje = percentil 5 del pico en la frecuencia de corte (1 = señal limpia); 0 si el punto no pertenece al carril.");
    Console.WriteLine("Pertenencia = parecido con el punto actual del carril (>= 0,5). Cambiá zonas-camara.json sólo si mejora claramente.");
    return 0;
}

static TimeZoneInfo ZonaPlanta(string? id)
{
    foreach (var candidato in new[] { id, "Argentina Standard Time", "America/Argentina/Buenos_Aires" })
    {
        if (string.IsNullOrWhiteSpace(candidato)) continue;
        try { return TimeZoneInfo.FindSystemTimeZoneById(candidato); } catch (TimeZoneNotFoundException) { }
    }
    return TimeZoneInfo.CreateCustomTimeZone("Planta", TimeSpan.FromHours(-3), "Planta", "Planta");
}

internal sealed class Argumentos
{
    public string? Comando { get; private init; }
    private readonly Dictionary<string, string?> _valores = new(StringComparer.OrdinalIgnoreCase);

    public static Argumentos Parsear(string[] args)
    {
        var a = new Argumentos { Comando = args.Length > 0 && !args[0].StartsWith("--") ? args[0].ToLowerInvariant() : null };
        for (var i = a.Comando == null ? 0 : 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var clave = args[i][2..];
            var valor = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : null;
            a._valores[clave] = valor;
        }
        return a;
    }

    public bool Tiene(string clave) => _valores.ContainsKey(clave);
    public string? Valor(string clave) => _valores.TryGetValue(clave, out var v) ? v : null;
    public string Requerido(string clave) => Valor(clave) ?? throw new ArgumentException($"Falta --{clave}.");
}
