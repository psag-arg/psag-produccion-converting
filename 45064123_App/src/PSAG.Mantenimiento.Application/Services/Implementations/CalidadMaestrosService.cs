using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Maestros de calidad (T-611): puntos de control (cámaras) por línea y tipos de defecto.
/// Sólo el administrador los modifica; nunca se borran (se desactivan).
/// </summary>
public class CalidadMaestrosService : ICalidadMaestrosService
{
    private const int LargoCodigo = 50;
    private const int LargoNombre = 100;
    private const int LargoDescripcion = 500;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalidadMaestrosService> _logger;

    public CalidadMaestrosService(IUnitOfWork unitOfWork, ILogger<CalidadMaestrosService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ===================== Puntos de control =====================

    public async Task<IReadOnlyList<PuntoControlCalidadDto>> ObtenerPuntosControlAsync(
        int? lineaId, bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var puntos = await _unitOfWork.PuntosControlCalidad.GetListadoAsync(lineaId, incluirInactivos, cancellationToken);
        return puntos.Select(ADto).ToList();
    }

    public async Task<Result<PuntoControlCalidadDto>> CrearPuntoControlAsync(
        GuardarPuntoControlCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<PuntoControlCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var validacion = await ValidarPuntoAsync(request, null, cancellationToken);
        if (validacion.IsFailure)
            return Result<PuntoControlCalidadDto>.Failure(validacion.ErrorCode!, validacion.ErrorMessage!);

        var punto = new PuntoControlCalidad
        {
            LineaId = request.LineaId,
            Codigo = CalidadReglas.NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            Descripcion = Limpiar(request.Descripcion),
            Activo = request.Activo,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };
        await _unitOfWork.PuntosControlCalidad.AddAsync(punto, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Punto de control de calidad {Codigo} creado por usuario {UsuarioId}", punto.Codigo, usuarioId);

        punto.Linea = validacion.Value!;
        return Result<PuntoControlCalidadDto>.Success(ADto(punto));
    }

    public async Task<Result<PuntoControlCalidadDto>> ActualizarPuntoControlAsync(
        int puntoControlCalidadId, GuardarPuntoControlCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<PuntoControlCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var punto = await _unitOfWork.PuntosControlCalidad.GetByIdAsync(puntoControlCalidadId, cancellationToken);
        if (punto == null)
            return Result<PuntoControlCalidadDto>.Failure("NOT_FOUND", "Punto de control no encontrado.");

        var validacion = await ValidarPuntoAsync(request, puntoControlCalidadId, cancellationToken);
        if (validacion.IsFailure)
            return Result<PuntoControlCalidadDto>.Failure(validacion.ErrorCode!, validacion.ErrorMessage!);

        // Los registros ya guardados tienen su propia LineaId: mover la cámara de línea no cambia el histórico.
        punto.LineaId = request.LineaId;
        punto.Codigo = CalidadReglas.NormalizarCodigo(request.Codigo);
        punto.Nombre = request.Nombre.Trim();
        punto.Descripcion = Limpiar(request.Descripcion);
        punto.Activo = request.Activo;
        punto.ModificadoPorUsuarioId = usuarioId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Punto de control de calidad {Codigo} actualizado por usuario {UsuarioId}", punto.Codigo, usuarioId);

        punto.Linea = validacion.Value!;
        return Result<PuntoControlCalidadDto>.Success(ADto(punto));
    }

    private async Task<Result<Linea>> ValidarPuntoAsync(
        GuardarPuntoControlCalidadRequest request, int? idActual, CancellationToken cancellationToken)
    {
        var faltan = new List<string>();
        if (request.LineaId <= 0) faltan.Add("línea");
        if (string.IsNullOrWhiteSpace(request.Codigo)) faltan.Add("código");
        if (string.IsNullOrWhiteSpace(request.Nombre)) faltan.Add("nombre");
        if (faltan.Count > 0)
            return Result<Linea>.Failure("VALIDATION", "Falta completar: " + string.Join(", ", faltan) + ".");

        var motivo = MotivoLargos(request.Codigo, request.Nombre, request.Descripcion);
        if (motivo != null)
            return Result<Linea>.Failure("VALIDATION", motivo);

        var linea = await _unitOfWork.Lineas.GetByIdAsync(request.LineaId, cancellationToken);
        if (linea == null)
            return Result<Linea>.Failure("VALIDATION", "La línea no existe.");

        var codigo = CalidadReglas.NormalizarCodigo(request.Codigo);
        var existente = await _unitOfWork.PuntosControlCalidad.GetByCodigoAsync(codigo, cancellationToken);
        if (existente != null && existente.PuntoControlCalidadId != idActual)
            return Result<Linea>.Failure("VALIDATION", $"Ya hay un punto de control con el código {codigo} ({existente.Nombre}).");

        return Result<Linea>.Success(linea);
    }

    // ===================== Tipos de defecto =====================

    public async Task<IReadOnlyList<TipoDefectoCalidadDto>> ObtenerTiposDefectoAsync(
        bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var tipos = await _unitOfWork.TiposDefectoCalidad.GetListadoAsync(incluirInactivos, cancellationToken);
        return tipos.Select(ADto).ToList();
    }

    public async Task<Result<TipoDefectoCalidadDto>> CrearTipoDefectoAsync(
        GuardarTipoDefectoCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<TipoDefectoCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var motivo = await MotivoTipoInvalidoAsync(request, null, cancellationToken);
        if (motivo != null)
            return Result<TipoDefectoCalidadDto>.Failure("VALIDATION", motivo);

        var tipo = new TipoDefectoCalidad
        {
            Codigo = CalidadReglas.NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            Descripcion = Limpiar(request.Descripcion),
            UnidadMedida = Limpiar(request.UnidadMedida),
            Activo = request.Activo,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };
        await _unitOfWork.TiposDefectoCalidad.AddAsync(tipo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tipo de defecto de calidad {Codigo} creado por usuario {UsuarioId}", tipo.Codigo, usuarioId);
        return Result<TipoDefectoCalidadDto>.Success(ADto(tipo));
    }

    public async Task<Result<TipoDefectoCalidadDto>> ActualizarTipoDefectoAsync(
        int tipoDefectoCalidadId, GuardarTipoDefectoCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<TipoDefectoCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var tipo = await _unitOfWork.TiposDefectoCalidad.GetByIdAsync(tipoDefectoCalidadId, cancellationToken);
        if (tipo == null)
            return Result<TipoDefectoCalidadDto>.Failure("NOT_FOUND", "Tipo de defecto no encontrado.");

        var motivo = await MotivoTipoInvalidoAsync(request, tipoDefectoCalidadId, cancellationToken);
        if (motivo != null)
            return Result<TipoDefectoCalidadDto>.Failure("VALIDATION", motivo);

        // La unidad de los registros ya guardados es un snapshot: cambiarla acá no los toca.
        tipo.Codigo = CalidadReglas.NormalizarCodigo(request.Codigo);
        tipo.Nombre = request.Nombre.Trim();
        tipo.Descripcion = Limpiar(request.Descripcion);
        tipo.UnidadMedida = Limpiar(request.UnidadMedida);
        tipo.Activo = request.Activo;
        tipo.ModificadoPorUsuarioId = usuarioId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tipo de defecto de calidad {Codigo} actualizado por usuario {UsuarioId}", tipo.Codigo, usuarioId);
        return Result<TipoDefectoCalidadDto>.Success(ADto(tipo));
    }

    private async Task<string?> MotivoTipoInvalidoAsync(
        GuardarTipoDefectoCalidadRequest request, int? idActual, CancellationToken cancellationToken)
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Codigo)) faltan.Add("código");
        if (string.IsNullOrWhiteSpace(request.Nombre)) faltan.Add("nombre");
        if (faltan.Count > 0)
            return "Falta completar: " + string.Join(", ", faltan) + ".";

        var motivo = MotivoLargos(request.Codigo, request.Nombre, request.Descripcion);
        if (motivo != null)
            return motivo;

        if (request.UnidadMedida?.Trim().Length > CalidadReglas.LargoMaximoUnidad)
            return $"La unidad admite hasta {CalidadReglas.LargoMaximoUnidad} caracteres.";

        var codigo = CalidadReglas.NormalizarCodigo(request.Codigo);
        var existente = await _unitOfWork.TiposDefectoCalidad.GetByCodigoAsync(codigo, cancellationToken);
        if (existente != null && existente.TipoDefectoCalidadId != idActual)
            return $"Ya hay un tipo de defecto con el código {codigo} ({existente.Nombre}).";

        return null;
    }

    // ===================== Ayudas =====================

    private static string? MotivoLargos(string codigo, string nombre, string? descripcion)
    {
        if (codigo.Trim().Length > LargoCodigo)
            return $"El código admite hasta {LargoCodigo} caracteres.";
        if (codigo.Trim().Any(char.IsWhiteSpace))
            return "El código no puede tener espacios (ej.: T2-CAM1).";
        if (nombre.Trim().Length > LargoNombre)
            return $"El nombre admite hasta {LargoNombre} caracteres.";
        if (descripcion?.Trim().Length > LargoDescripcion)
            return $"La descripción admite hasta {LargoDescripcion} caracteres.";
        return null;
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static PuntoControlCalidadDto ADto(PuntoControlCalidad p) => new()
    {
        PuntoControlCalidadId = p.PuntoControlCalidadId,
        LineaId = p.LineaId,
        LineaNombre = p.Linea?.Nombre ?? string.Empty,
        Codigo = p.Codigo,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Activo = p.Activo
    };

    private static TipoDefectoCalidadDto ADto(TipoDefectoCalidad t) => new()
    {
        TipoDefectoCalidadId = t.TipoDefectoCalidadId,
        Codigo = t.Codigo,
        Nombre = t.Nombre,
        Descripcion = t.Descripcion,
        UnidadMedida = t.UnidadMedida,
        Activo = t.Activo
    };
}
