namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Límites de las fotos del análisis, iguales a los de la API (AdjuntoImagenReglas).
/// Acá solo sirven para avisar antes de subir; la API valida el contenido real del archivo.
/// </summary>
public static class AdjuntosAnalisisReglas
{
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;
    public const int MaximoPorAnalisis = 5;
    public const string Accept = ".jpg,.jpeg,.png,image/jpeg,image/png";

    private static readonly string[] Extensiones = [".jpg", ".jpeg", ".png"];

    public static string? MotivoRechazoPrevio(string nombre, long tamanoBytes)
    {
        if (!Extensiones.Contains(Path.GetExtension(nombre).ToLowerInvariant()))
        {
            return $"{nombre}: solo se pueden adjuntar fotos JPG o PNG.";
        }

        if (tamanoBytes > TamanoMaximoBytes)
        {
            return $"{nombre}: pesa {tamanoBytes / (1024.0 * 1024.0):0.0} MB. El máximo es 10 MB por foto.";
        }

        if (tamanoBytes <= 0)
        {
            return $"{nombre}: el archivo está vacío.";
        }

        return null;
    }
}
