using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Infrastructure.Services;

/// <summary>
/// Guarda los adjuntos en una carpeta de disco fuera de wwwroot. La API no sirve archivos estáticos:
/// las fotos solo salen por el endpoint autenticado de AnalisisController.
/// </summary>
public class AlmacenAdjuntosDisco : IAlmacenAdjuntos
{
    /// <summary>
    /// Carpeta por defecto, relativa a la carpeta de la API. En producción conviene una ruta absoluta
    /// fuera de la carpeta publicada (ver database/README.md y docs de despliegue).
    /// </summary>
    public const string CarpetaPorDefecto = "App_Data/AdjuntosAnalisis";

    private readonly string _carpeta;

    public AlmacenAdjuntosDisco(string carpeta)
    {
        _carpeta = Path.GetFullPath(carpeta);
    }

    public string Carpeta => _carpeta;

    public async Task GuardarAsync(string nombreArchivo, Stream contenido, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_carpeta);
        await using var archivo = new FileStream(Ruta(nombreArchivo), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await contenido.CopyToAsync(archivo, cancellationToken);
    }

    public Stream? Abrir(string nombreArchivo)
    {
        var ruta = Ruta(nombreArchivo);
        return File.Exists(ruta)
            ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
    }

    public void Eliminar(string nombreArchivo)
    {
        var ruta = Ruta(nombreArchivo);
        if (File.Exists(ruta))
            File.Delete(ruta);
    }

    /// <summary>
    /// El nombre lo genera la aplicación (GUID + extensión). Igual se rechaza cualquier cosa con carpetas
    /// para que nunca se pueda leer o escribir fuera de la carpeta de adjuntos.
    /// </summary>
    private string Ruta(string nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo)
            || nombreArchivo != Path.GetFileName(nombreArchivo)
            || nombreArchivo.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Nombre de adjunto inválido.", nameof(nombreArchivo));
        }

        return Path.Combine(_carpeta, nombreArchivo);
    }
}

public static class AlmacenAdjuntosExtensions
{
    /// <summary>
    /// Registra el almacén de adjuntos en la carpeta de Adjuntos:Carpeta. Una ruta relativa se toma desde
    /// <paramref name="carpetaBase"/> (la carpeta de la API).
    /// </summary>
    public static IServiceCollection AddAlmacenAdjuntos(this IServiceCollection services, IConfiguration configuration, string carpetaBase)
    {
        var configurada = configuration["Adjuntos:Carpeta"];
        var carpeta = string.IsNullOrWhiteSpace(configurada) ? AlmacenAdjuntosDisco.CarpetaPorDefecto : configurada;
        if (!Path.IsPathRooted(carpeta))
            carpeta = Path.Combine(carpetaBase, carpeta);

        services.AddSingleton<IAlmacenAdjuntos>(sp =>
        {
            var almacen = new AlmacenAdjuntosDisco(carpeta);
            sp.GetService<ILoggerFactory>()?.CreateLogger<AlmacenAdjuntosDisco>()
                .LogInformation("Fotos de análisis en {Carpeta}", almacen.Carpeta);
            return almacen;
        });
        return services;
    }
}
