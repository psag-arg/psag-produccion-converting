namespace PSAG.Mantenimiento.ConteoCamara.Video;

/// <summary>
/// Credenciales del NVR leídas de un archivo .env fuera del repo (NVR=..., NVR_USER=..., NVR_PASS=...).
/// Nunca se imprimen: <see cref="ToString"/> y <see cref="Ocultar"/> tapan la clave.
/// </summary>
public sealed class CredencialesNvr
{
    public string Host { get; }
    public string Usuario { get; }
    private readonly string _clave;

    private CredencialesNvr(string host, string usuario, string clave)
    {
        Host = host;
        Usuario = usuario;
        _clave = clave;
    }

    public static CredencialesNvr Leer(string archivo)
    {
        if (!File.Exists(archivo))
            throw new FileNotFoundException($"No existe el archivo de credenciales del NVR: {archivo}");

        var valores = Parsear(File.ReadAllLines(archivo));
        string Requerido(string clave) =>
            valores.TryGetValue(clave, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : throw new InvalidOperationException($"Falta {clave} en {archivo}.");

        var host = Requerido("NVR").Replace("http://", "").Replace("https://", "").Trim('/');
        return new CredencialesNvr(host, Requerido("NVR_USER"), Requerido("NVR_PASS"));
    }

    internal static Dictionary<string, string> Parsear(IEnumerable<string> lineas)
    {
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cruda in lineas)
        {
            var linea = cruda.Trim();
            if (linea.Length == 0 || linea.StartsWith('#')) continue;
            var igual = linea.IndexOf('=');
            if (igual <= 0) continue;
            valores[linea[..igual].Trim()] = linea[(igual + 1)..].Trim().Trim('"', '\'');
        }
        return valores;
    }

    /// <summary>URL RTSP con usuario y clave (sólo para pasarle a ffmpeg; no loguearla).</summary>
    public string UrlRtsp(int puerto, string ruta) =>
        $"rtsp://{Uri.EscapeDataString(Usuario)}:{Uri.EscapeDataString(_clave)}@{Host}:{puerto}{ruta}";

    /// <summary>Reemplaza la clave (tal cual y escapada) por *** en un texto, p. ej. el stderr de ffmpeg.</summary>
    public string Ocultar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.Replace(Uri.EscapeDataString(_clave), "***").Replace(_clave, "***");
    }

    public override string ToString() => $"***@{Host}";
}
