namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Dónde quedan guardados los archivos adjuntos (fotos del análisis). La implementación de producción
/// los guarda en disco, en la carpeta de Adjuntos:Carpeta, fuera de wwwroot.
/// Los nombres los genera la aplicación; nunca vienen del usuario.
/// </summary>
public interface IAlmacenAdjuntos
{
    Task GuardarAsync(string nombreArchivo, Stream contenido, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre el archivo para leerlo, o null si no existe.
    /// </summary>
    Stream? Abrir(string nombreArchivo);

    /// <summary>
    /// Solo para deshacer un guardado cuando falla la base de datos.
    /// </summary>
    void Eliminar(string nombreArchivo);
}
