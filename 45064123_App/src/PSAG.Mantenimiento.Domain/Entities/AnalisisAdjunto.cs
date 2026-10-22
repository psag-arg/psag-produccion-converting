namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Foto adjunta al análisis de falla de una detención. El archivo vive en disco, en la carpeta
/// configurada en Adjuntos:Carpeta, con un nombre generado (NombreArchivo); nunca se sirve estáticamente.
/// Esta tabla es de solo inserción - no se permiten actualizaciones ni eliminaciones.
/// </summary>
public class AnalisisAdjunto
{
    public long AnalisisAdjuntoId { get; set; }
    public long DetencionId { get; set; }

    /// <summary>
    /// Nombre generado del archivo en disco (GUID + extensión). Nunca viene del usuario.
    /// </summary>
    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>
    /// Nombre con el que el usuario subió el archivo. Solo para mostrar.
    /// </summary>
    public string NombreOriginal { get; set; } = string.Empty;

    /// <summary>
    /// image/jpeg | image/png, detectado por el contenido del archivo.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }

    // Navegación
    public virtual Detencion Detencion { get; set; } = null!;
}
