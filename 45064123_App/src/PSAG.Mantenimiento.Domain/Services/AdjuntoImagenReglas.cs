namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Validación de las fotos del análisis de falla: solo JPG o PNG reales (se mira el contenido,
/// no solo la extensión), hasta 10 MB cada una y hasta 5 por análisis. Funciones puras.
/// </summary>
public static class AdjuntoImagenReglas
{
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;
    public const int MaximoPorAnalisis = 5;

    /// <summary>
    /// Bytes del principio del archivo que hacen falta para reconocer el formato.
    /// </summary>
    public const int BytesCabecera = 8;

    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    /// <summary>
    /// Tipo de imagen según los primeros bytes, o null si no es JPG ni PNG.
    /// </summary>
    public static string? DetectarTipo(ReadOnlySpan<byte> cabecera)
    {
        if (cabecera.StartsWith(FirmaPng))
            return Png;

        if (cabecera.StartsWith(FirmaJpeg))
            return Jpeg;

        return null;
    }

    public static string Extension(string contentType) => contentType == Png ? ".png" : ".jpg";

    /// <summary>
    /// Motivo por el que la foto se rechaza, o null si se puede guardar.
    /// </summary>
    public static string? MotivoRechazo(string? nombreArchivo, long tamanoBytes, ReadOnlySpan<byte> cabecera, int fotosYaCargadas)
    {
        if (fotosYaCargadas >= MaximoPorAnalisis)
            return $"El análisis ya tiene {MaximoPorAnalisis} fotos, que es el máximo.";

        return MotivoRechazoImagen(nombreArchivo, tamanoBytes, cabecera);
    }

    /// <summary>
    /// Mismas reglas de archivo sin el tope de fotos por análisis: JPG o PNG real, no vacío y hasta 10 MB.
    /// La usan también las imágenes de los registros de calidad (T-611).
    /// </summary>
    public static string? MotivoRechazoImagen(string? nombreArchivo, long tamanoBytes, ReadOnlySpan<byte> cabecera)
    {
        if (tamanoBytes <= 0)
            return "El archivo está vacío.";

        if (tamanoBytes > TamanoMaximoBytes)
            return $"La foto pesa {tamanoBytes / (1024.0 * 1024.0):0.0} MB. El máximo es {TamanoMaximoBytes / (1024 * 1024)} MB por foto.";

        var extension = Path.GetExtension(nombreArchivo ?? string.Empty);
        if (!ExtensionesPermitidas.Contains(extension))
            return "Solo se pueden adjuntar fotos JPG o PNG.";

        // Un PNG llamado .jpg (o al revés) se acepta: se guarda y se sirve con el tipo real.
        if (DetectarTipo(cabecera) == null)
            return "El archivo no es una imagen JPG o PNG válida.";

        return null;
    }
}
