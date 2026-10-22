using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class AnalisisEdicionService : IAnalisisEdicionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAnalisisService _analisisService;
    private readonly IAlmacenAdjuntos _almacen;
    private readonly ILogger<AnalisisEdicionService> _logger;

    public AnalisisEdicionService(
        IUnitOfWork unitOfWork,
        IAnalisisService analisisService,
        IAlmacenAdjuntos almacen,
        ILogger<AnalisisEdicionService> logger)
    {
        _unitOfWork = unitOfWork;
        _analisisService = analisisService;
        _almacen = almacen;
        _logger = logger;
    }

    public async Task<Result<AnalisisDetencionDto>> EditarCerradoAsync(
        long detencionId,
        GuardarAnalisisRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<AnalisisDetencionDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!AnalisisEdicionReglas.PuedeEditarCerrado(roles))
            return Result<AnalisisDetencionDto>.Failure("FORBIDDEN", "Solo administración o supervisión pueden editar un análisis cerrado.");

        if (detencion.EstadoActual != EstadoDetencion.CERRADA)
            return Result<AnalisisDetencionDto>.Failure("VALIDATION", "Solo se editan desde acá los análisis cerrados. Si está abierto, completalo desde el detalle de la detención.");

        if (!detencion.RowVersion.SequenceEqual(request.RowVersion))
            return Result<AnalisisDetencionDto>.Failure("CONCURRENCY", "Alguien guardó este análisis mientras lo editabas. Copiá tu texto, recargá la página y volvé a guardar.");

        var motivo = MotivoNoGuardable(request.AccionesRealizadas, request.CausaFalla)
            ?? AnalisisService.ValidarLargos(request);
        if (motivo != null)
            return Result<AnalisisDetencionDto>.Failure("VALIDATION", motivo);

        var solucion = AnalisisService.Limpiar(request.AccionesRealizadas);
        var causa = AnalisisService.Limpiar(request.CausaFalla);
        var plan = AnalisisService.Limpiar(request.PlanAccion);

        var cambios = AnalisisEdicionReglas.Cambios(detencion, solucion, causa, plan, usuarioId, DateTimeOffset.UtcNow);
        if (cambios.Count > 0)
        {
            // Se guarda sobre el análisis original; la firma de cierre (quién y cuándo cerró) no cambia.
            detencion.AccionesRealizadas = solucion;
            detencion.CausaFalla = causa;
            detencion.PlanAccion = plan;
            detencion.ModificadoPorUsuarioId = usuarioId;
            foreach (var cambio in cambios)
                detencion.CambiosAnalisis.Add(cambio);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Análisis cerrado de la detención {DetencionId} editado por usuario {UsuarioId}: {Campos}",
                detencionId, usuarioId, string.Join(", ", cambios.Select(c => c.Campo)));
        }

        return await _analisisService.ObtenerAsync(detencionId, usuarioId, roles, cancellationToken);
    }

    public async Task<Result<AdjuntoAnalisisDto>> AgregarAdjuntoAsync(
        long detencionId,
        string? nombreOriginal,
        long tamanoBytes,
        Stream contenido,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<AdjuntoAnalisisDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!AnalisisEdicionReglas.PuedeAdjuntar(detencion, usuarioId, roles))
        {
            var mensaje = detencion.EstadoActual == EstadoDetencion.CERRADA
                ? "Solo administración o supervisión pueden agregar fotos a un análisis cerrado."
                : "Solo el responsable del análisis o un administrador puede agregar fotos.";
            return Result<AdjuntoAnalisisDto>.Failure("FORBIDDEN", mensaje);
        }

        var cantidad = detencion.AdjuntosAnalisis.Count;

        // Primero cantidad y tamaño declarado, para no leer a memoria un archivo enorme.
        if (cantidad >= AdjuntoImagenReglas.MaximoPorAnalisis
            || tamanoBytes <= 0
            || tamanoBytes > AdjuntoImagenReglas.TamanoMaximoBytes)
        {
            return Result<AdjuntoAnalisisDto>.Failure("VALIDATION",
                AdjuntoImagenReglas.MotivoRechazo(nombreOriginal, tamanoBytes, ReadOnlySpan<byte>.Empty, cantidad)!);
        }

        using var copia = new MemoryStream();
        await CopiarConLimiteAsync(contenido, copia, AdjuntoImagenReglas.TamanoMaximoBytes + 1, cancellationToken);
        var cabecera = copia.GetBuffer().AsSpan(0, (int)Math.Min(copia.Length, AdjuntoImagenReglas.BytesCabecera)).ToArray();

        var motivo = AdjuntoImagenReglas.MotivoRechazo(nombreOriginal, copia.Length, cabecera, cantidad);
        if (motivo != null)
            return Result<AdjuntoAnalisisDto>.Failure("VALIDATION", motivo);

        var contentType = AdjuntoImagenReglas.DetectarTipo(cabecera)!;
        var nombreArchivo = $"{Guid.NewGuid():N}{AdjuntoImagenReglas.Extension(contentType)}";
        var ahora = DateTimeOffset.UtcNow;

        var adjunto = new AnalisisAdjunto
        {
            DetencionId = detencion.DetencionId,
            NombreArchivo = nombreArchivo,
            NombreOriginal = NombreParaMostrar(nombreOriginal),
            ContentType = contentType,
            TamanoBytes = copia.Length,
            UsuarioId = usuarioId,
            FechaHora = ahora
        };
        detencion.AdjuntosAnalisis.Add(adjunto);

        if (detencion.EstadoActual == EstadoDetencion.CERRADA)
        {
            detencion.CambiosAnalisis.Add(new AnalisisCambio
            {
                DetencionId = detencion.DetencionId,
                Campo = AnalisisCambio.CampoFoto,
                ValorNuevo = adjunto.NombreOriginal,
                UsuarioId = usuarioId,
                FechaHora = ahora
            });
        }

        copia.Position = 0;
        await _almacen.GuardarAsync(nombreArchivo, copia, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _almacen.Eliminar(nombreArchivo);
            throw;
        }

        _logger.LogInformation(
            "Foto {NombreArchivo} ({Bytes} bytes) agregada al análisis de la detención {DetencionId} por usuario {UsuarioId}",
            nombreArchivo, adjunto.TamanoBytes, detencionId, usuarioId);

        return Result<AdjuntoAnalisisDto>.Success(AnalisisService.MapearAdjunto(adjunto));
    }

    public async Task<Result<AdjuntoArchivo>> AbrirAdjuntoAsync(
        long detencionId,
        long adjuntoId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        var adjunto = detencion?.AdjuntosAnalisis.FirstOrDefault(a => a.AnalisisAdjuntoId == adjuntoId);
        if (adjunto == null)
            return Result<AdjuntoArchivo>.Failure("NOT_FOUND", "La foto no existe.");

        var stream = _almacen.Abrir(adjunto.NombreArchivo);
        if (stream == null)
        {
            _logger.LogWarning("Falta en disco la foto {NombreArchivo} de la detención {DetencionId}", adjunto.NombreArchivo, detencionId);
            return Result<AdjuntoArchivo>.Failure("NOT_FOUND", "No se encontró el archivo de la foto.");
        }

        return Result<AdjuntoArchivo>.Success(new AdjuntoArchivo(stream, adjunto.ContentType, adjunto.NombreOriginal));
    }

    /// <summary>
    /// Al editar un análisis cerrado se exige lo mismo que al cerrarlo: solución y causa.
    /// </summary>
    private static string? MotivoNoGuardable(string? solucion, string? causa)
    {
        if (string.IsNullOrWhiteSpace(solucion))
            return "La solución no puede quedar vacía.";

        if (string.IsNullOrWhiteSpace(causa))
            return "La causa de la falla no puede quedar vacía.";

        return null;
    }

    /// <summary>
    /// Copia hasta <paramref name="limite"/> bytes. Si el archivo es más grande, la validación de tamaño lo rechaza.
    /// </summary>
    private static async Task CopiarConLimiteAsync(Stream origen, Stream destino, long limite, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (total < limite)
        {
            var leidos = await origen.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limite - total)), cancellationToken);
            if (leidos == 0)
                break;

            await destino.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
            total += leidos;
        }
    }

    /// <summary>
    /// Solo el nombre (sin carpetas) y recortado al largo de la columna. Es solo para mostrar.
    /// </summary>
    private static string NombreParaMostrar(string? nombreOriginal)
    {
        var nombre = Path.GetFileName((nombreOriginal ?? string.Empty).Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(nombre))
            nombre = "foto";
        return nombre.Length > 255 ? nombre[^255..] : nombre;
    }
}
