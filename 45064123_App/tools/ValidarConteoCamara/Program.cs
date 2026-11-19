using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara.Analisis;
using PSAG.Mantenimiento.ConteoCamara.Video;

// Validación de la cámara 10 (método MANGAS) con video grabado, usando la cadena REAL del servicio.
// No toca el NVR ni la API: sólo lee archivos locales.
//
//   decodificar --video X.ps --ffmpeg ffmpeg.exe --salida X.raw [--segundos N] [--config appsettings.json]
//       Decodifica como el servicio (FuenteFfmpeg con la Fuente de T2-CAM10: gris 320x180 a 5 fps) y guarda los
//       cuadros crudos, así se puede simular muchas veces sin volver a decodificar (lo lento es el H.265).
//
//   simular --raw X.raw@2026-11-14T09:53:38-03:00 [--raw Y.raw@...] [--saltar N]
//           --variante antes:Mangas:UmbralPasoAlto=12;Mangas:SegundosMinimosEntrePasos=1.2 --variante despues
//           [--ref eventos.txt] [--tramo "A=2026-11-14T10:10/2026-11-14T10:26"] [--ventanas salida.csv]
//       Cada variante es la configuración de T2-CAM10 del appsettings.json (--config, por defecto el del servicio)
//       con los cambios indicados (claves relativas a la cámara). Todas corren sobre los mismos cuadros, en una
//       pasada. Los raw se encadenan sin cortar la señal (son tramos seguidos del mismo video).
//       Mangas por tramo = suma de las ventanas que EMPIEZAN en el tramo (ya resueltas: las que el servicio manda
//       a los minutos). La referencia (un evento por línea, hora local ISO) se cuenta en esas mismas ventanas.

var a = Args.Parsear(args);
var config = a.Valor("config") ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

switch (a.Comando)
{
    case "decodificar": return await Decodificar();
    case "simular": return Simular();
    default:
        Console.Error.WriteLine("Uso: decodificar | simular (ver encabezado de Program.cs)");
        return 2;
}

async Task<int> Decodificar()
{
    var opciones = Camara10(config, new Dictionary<string, string>());
    opciones.Fuente.RutaFfmpeg = Path.GetFullPath(a.Requerido("ffmpeg"));
    var fuente = new FuenteFfmpeg(opciones.Fuente, NullLogger.Instance);
    var limite = a.Valor("segundos") is { } s ? (long)(double.Parse(s, CultureInfo.InvariantCulture) * opciones.Fuente.Fps) : long.MaxValue;
    await using var salida = File.Create(a.Requerido("salida"));
    long n = 0;
    await foreach (var cuadro in fuente.LeerAsync(Path.GetFullPath(a.Requerido("video")), esRtsp: false))
    {
        await salida.WriteAsync(cuadro);
        if (++n % 5000 == 0) Console.WriteLine($"{n} cuadros");
        if (n >= limite) break;
    }
    Console.WriteLine($"{n} cuadros de {opciones.Fuente.AnchoDecodificado}x{opciones.Fuente.AltoDecodificado} ({n / (double)opciones.Fuente.Fps:0} s)");
    return 0;
}

int Simular()
{
    var variantes = a.Valores("variante").Select(v =>
    {
        var i = v.IndexOf(':');
        var nombre = i < 0 ? v : v[..i];
        var cambios = i < 0 ? new Dictionary<string, string>() : v[(i + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Split('=', 2)).ToDictionary(c => c[0].Trim(), c => c[1].Trim());
        var opciones = Camara10(config, cambios);
        var salida = new Captura();
        return (Nombre: nombre, Cambios: cambios, Opciones: opciones, Salida: salida, Cadena: Analizadores.Crear(opciones, salida));
    }).ToList();
    if (variantes.Count == 0) throw new ArgumentException("Falta --variante.");

    var fuente0 = variantes[0].Opciones.Fuente;
    var largo = fuente0.AnchoDecodificado * fuente0.AltoDecodificado;
    var fps = (double)fuente0.Fps;
    var saltar = int.Parse(a.Valor("saltar") ?? "0", CultureInfo.InvariantCulture);
    var buffer = new byte[largo];
    foreach (var raw in a.Valores("raw"))
    {
        var partes = raw.Split('@', 2);
        var inicio = DateTimeOffset.Parse(partes[1], CultureInfo.InvariantCulture);
        using var f = File.OpenRead(partes[0]);
        long n = 0;
        while (f.ReadAtLeast(buffer, largo, throwOnEndOfStream: false) == largo)
        {
            if (n < saltar) { n++; continue; }
            var hora = inicio.AddSeconds(n++ / fps);
            foreach (var v in variantes) v.Cadena.Cuadro(buffer, hora);
        }
        saltar = 0;
    }
    foreach (var v in variantes) v.Cadena.CortarSenal(final: true);

    var referencia = a.Valor("ref") is { } r
        ? File.ReadAllLines(r).Where(l => l.Length > 0)
            .Select(l => new DateTimeOffset(DateTime.Parse(l, CultureInfo.InvariantCulture), TimeSpan.FromHours(-3))).ToList()
        : null;
    var tramos = a.Valores("tramo").Select(t =>
    {
        var p = t.Split('=', 2);
        var ab = p[1].Split('/');
        return (Nombre: p[0], Desde: Hora(ab[0]), Hasta: Hora(ab[1]));
    }).ToList();

    foreach (var v in variantes)
    {
        var s = v.Salida;
        var o = v.Opciones;
        Console.WriteLine();
        Console.WriteLine($"=== {v.Nombre}  " + (v.Cambios.Count == 0 ? "(appsettings)" : string.Join("; ", v.Cambios.Select(c => $"{c.Key}={c.Value}"))));
        Console.WriteLine($"    PasoAlto {o.Mangas.UmbralPasoAlto} PasoBajo {o.Mangas.UmbralPasoBajo} EntrePasos {o.Mangas.SegundosMinimosEntrePasos} " +
                          $"RodilloGira {o.Mangas.UmbralRodilloGira} FraccionQuieto {o.Mangas.FraccionRodilloQuieto} FraccionGiraMarcha {o.Mangas.FraccionRodilloGiraParaMarcha} " +
                          $"VentanasConfirmar {o.Analisis.VentanasParaConfirmar} VentanasArranque {o.Analisis.VentanasParaConfirmarArranque} DescontarEnParada {o.Mangas.DescontarMangasEnParada}");
        Console.WriteLine($"    mangas totales {s.Ventanas.Sum(w => w.Rollos):0}  ventanas {s.Ventanas.Count}");
        foreach (var t in tramos)
        {
            var ventanas = s.Ventanas.Where(w => w.Inicio >= t.Desde && w.Inicio < t.Hasta).ToList();
            var mangas = ventanas.Sum(w => w.Rollos);
            var minutos = s.Minutos_.Where(m => m.Minuto >= t.Desde && m.Minuto < t.Hasta).ToList();
            var texto = $"    tramo {t.Nombre,-22} mangas {mangas,5:0}";
            if (referencia != null)
            {
                var real = referencia.Count(e => ventanas.Any(w => e >= w.Inicio && e < w.Fin));
                texto += $"  real {real,5}  dif {mangas - real,4:+0;-0;0} ({(real > 0 ? (mangas - real) * 100.0 / real : 0),5:+0.0;-0.0;0.0} %)";
            }
            texto += $"  | minutos: marcha {minutos.Sum(m => m.SegundosMarcha)} s, parada {minutos.Sum(m => m.SegundosParada)} s, acum {minutos.Sum(m => m.SegundosAcumulacion)} s";
            Console.WriteLine(texto);
        }
        Console.WriteLine("    paradas cerradas:");
        foreach (var p in s.Paradas)
            Console.WriteLine($"      {p.Inicio:HH:mm:ss} - {p.Fin:HH:mm:ss}  {p.DuracionSegundos / 60}:{p.DuracionSegundos % 60:00}  {p.Tipo}{(p.FinIncierto ? " (fin incierto)" : "")}{(p.DuracionSegundos >= 300 ? "  -> detención" : "")}");
        // Detenciones que crearía la API (umbral 5 min): por parada en curso que llega al umbral o por la cerrada.
        var detenciones = s.EnCurso.Where(p => p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5)).Select(p => p.Inicio)
            .Concat(s.Paradas.Where(p => p.DuracionSegundos >= 300).Select(p => p.Inicio)).Distinct().OrderBy(x => x).ToList();
        Console.WriteLine($"    detenciones (>= 5 min): {detenciones.Count}  " + string.Join(", ", detenciones.Select(d =>
        {
            var primera = s.EnCurso.Where(p => p.Inicio == d && p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5)).Select(p => (DateTimeOffset?)p.Hasta).FirstOrDefault();
            return $"inicio {d:HH:mm:ss}" + (primera is { } h ? $" (en curso, creada al ver {h:HH:mm:ss})" : " (al cerrar)");
        })));
    }

    if (a.Valor("ventanas") is { } csv)
    {
        using var w = new StreamWriter(csv);
        w.WriteLine("variante;inicio;estado;mangas;mesa;rodillo");
        foreach (var v in variantes)
            foreach (var x in v.Salida.Ventanas)
                w.WriteLine($"{v.Nombre};{x.Inicio:HH:mm:ss};{x.EstadoLinea};{x.Rollos:0};{x.Carriles[0].Amplitud:0.0};{x.Carriles[0].ProporcionPico:0.0}");
    }
    return 0;
}

DateTimeOffset Hora(string s) => new(DateTime.Parse(s, CultureInfo.InvariantCulture), TimeSpan.FromHours(-3));

static OpcionesConteo Camara10(string ruta, IDictionary<string, string> cambios)
{
    var baseConf = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(ruta)).Build();
    var indice = baseConf.GetSection("ConteoCamara:Camaras").GetChildren()
        .First(c => string.Equals(c["PuntoControl"], "T2-CAM10", StringComparison.OrdinalIgnoreCase)).Key;
    var conf = new ConfigurationBuilder()
        .AddConfiguration(baseConf)
        .AddInMemoryCollection(cambios.Select(c => KeyValuePair.Create($"ConteoCamara:Camaras:{indice}:{c.Key}", (string?)c.Value)))
        .Build();
    return OpcionesConteo.CargarCamaras(conf).First(c => c.PuntoControl == "T2-CAM10");
}

internal sealed class Captura : ISalidaConteo
{
    public List<ResultadoVentana> Ventanas { get; } = [];
    public List<ConteoMinuto> Minutos_ { get; } = [];
    public List<ParadaDetectada> Paradas { get; } = [];
    public List<ParadaEnCurso> EnCurso { get; } = [];

    public void Ventana(ResultadoVentana ventana) => Ventanas.Add(ventana);
    public void Minutos(IReadOnlyList<ConteoMinuto> minutos) => Minutos_.AddRange(minutos);
    public void Parada(ParadaDetectada parada) => Paradas.Add(parada);
    public void ParadaEnCurso(ParadaEnCurso parada) => EnCurso.Add(parada);
}

internal sealed class Args
{
    public string? Comando { get; private init; }
    private readonly List<(string Clave, string Valor)> _valores = [];

    public static Args Parsear(string[] args)
    {
        var a = new Args { Comando = args.Length > 0 ? args[0] : null };
        for (var i = 1; i < args.Length; i++)
            if (args[i].StartsWith("--"))
                a._valores.Add((args[i][2..], i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : ""));
        return a;
    }

    public string? Valor(string clave) => _valores.LastOrDefault(v => v.Clave == clave).Valor;
    public IEnumerable<string> Valores(string clave) => _valores.Where(v => v.Clave == clave).Select(v => v.Valor);
    public string Requerido(string clave) => Valor(clave) ?? throw new ArgumentException($"Falta --{clave}.");
}
