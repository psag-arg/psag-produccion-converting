using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PSAG.Mantenimiento.ConteoCamara.Video;

/// <summary>
/// Decodifica con ffmpeg.exe portable como proceso aparte: ffmpeg lee el RTSP (o un archivo), baja a pocos fps y
/// a resolución reducida, y entrega cuadros grises crudos por stdout. Una sola conexión al NVR, sólo lectura.
/// </summary>
public sealed class FuenteFfmpeg
{
    private readonly string _rutaFfmpeg;
    private readonly OpcionesFuente _opciones;
    private readonly Func<string, string> _ocultar;
    private readonly ILogger _logger;

    public FuenteFfmpeg(OpcionesFuente opciones, ILogger logger, Func<string, string>? ocultar = null)
    {
        _opciones = opciones;
        _logger = logger;
        _ocultar = ocultar ?? (s => s);
        _rutaFfmpeg = ResolverFfmpeg(opciones.RutaFfmpeg);
    }

    public int BytesPorCuadro => _opciones.AnchoDecodificado * _opciones.AltoDecodificado;

    public static string ResolverFfmpeg(string ruta)
    {
        var completa = Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);
        if (File.Exists(completa)) return completa;
        if (File.Exists(ruta)) return Path.GetFullPath(ruta);
        throw new FileNotFoundException(
            $"No se encontró ffmpeg en {completa}. Copiá ffmpeg.exe portable ahí o configurá ConteoCamara:Fuente:RutaFfmpeg.");
    }

    /// <summary>Argumentos de ffmpeg. Para RTSP: TCP y timeout de socket, así un NVR colgado no deja el proceso esperando.</summary>
    internal static List<string> Argumentos(OpcionesFuente o, string entrada, bool esRtsp, double? desdeSegundo = null)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-threads", o.HilosDecodificacion.ToString() };
        if (esRtsp)
            args.AddRange(["-rtsp_transport", "tcp", "-timeout", "10000000"]);
        if (desdeSegundo is { } s)
            args.AddRange(["-ss", s.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        args.AddRange(["-i", entrada, "-an", "-sn", "-dn",
            "-vf", $"fps={o.Fps},scale={o.AnchoDecodificado}:{o.AltoDecodificado}:flags=area",
            "-pix_fmt", "gray", "-f", "rawvideo", "pipe:1"]);
        return args;
    }

    /// <summary>
    /// Lanza ffmpeg y entrega cuadros hasta que termina el video, se corta el stream o pasan
    /// SegundosSinCuadrosParaReiniciar sin datos. El buffer se reutiliza: copiarlo si hace falta guardarlo.
    /// </summary>
    public async IAsyncEnumerable<byte[]> LeerAsync(
        string entrada, bool esRtsp, double? desdeSegundo = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var info = new ProcessStartInfo(_rutaFfmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in Argumentos(_opciones, entrada, esRtsp, desdeSegundo)) info.ArgumentList.Add(a);

        using var proceso = new Process { StartInfo = info };
        var errores = new System.Text.StringBuilder();
        proceso.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (errores) if (errores.Length < 4000) errores.AppendLine(e.Data);
        };
        proceso.Start();
        proceso.BeginErrorReadLine();
        try { proceso.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* sin permiso: sigue normal */ }

        var stdout = proceso.StandardOutput.BaseStream;
        var buffer = new byte[BytesPorCuadro];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
                limite.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, _opciones.SegundosSinCuadrosParaReiniciar)));
                int leidos;
                try
                {
                    leidos = await LeerCompletoAsync(stdout, buffer, limite.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("ffmpeg no entregó cuadros en {Segundos} s; se reinicia", _opciones.SegundosSinCuadrosParaReiniciar);
                    yield break;
                }
                if (leidos < buffer.Length) break; // fin del video o se cortó
                yield return buffer;
            }
        }
        finally
        {
            if (!proceso.HasExited)
            {
                try { proceso.Kill(entireProcessTree: true); } catch { /* ya terminó */ }
            }
            try { proceso.WaitForExit(5000); } catch { /* nada */ }
            string texto;
            lock (errores) texto = errores.ToString().Trim();
            if (texto.Length > 0)
                _logger.LogWarning("ffmpeg terminó (código {Codigo}): {Errores}",
                    proceso.HasExited ? proceso.ExitCode : -1, _ocultar(texto));
        }
    }

    private static async Task<int> LeerCompletoAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    /// <summary>Saca una foto a resolución completa (JPG) para recalibrar las zonas.</summary>
    public async Task FotoAsync(string entrada, bool esRtsp, string salida, double? segundo, CancellationToken ct)
    {
        var info = new ProcessStartInfo(_rutaFfmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y" }) info.ArgumentList.Add(a);
        if (esRtsp) foreach (var a in new[] { "-rtsp_transport", "tcp", "-timeout", "10000000" }) info.ArgumentList.Add(a);
        if (segundo is { } s) { info.ArgumentList.Add("-ss"); info.ArgumentList.Add(s.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        foreach (var a in new[] { "-i", entrada, "-frames:v", "1", "-q:v", "3", salida }) info.ArgumentList.Add(a);

        using var proceso = Process.Start(info)!;
        var error = await proceso.StandardError.ReadToEndAsync(ct);
        await proceso.WaitForExitAsync(ct);
        if (proceso.ExitCode != 0 || !File.Exists(salida))
            throw new InvalidOperationException("ffmpeg no pudo sacar la foto: " + _ocultar(error));
    }
}
