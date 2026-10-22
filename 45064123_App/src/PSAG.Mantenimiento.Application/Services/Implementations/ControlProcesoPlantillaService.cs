using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Maestro de la plantilla de Control de Proceso por línea (T-545).
/// Usa las tablas del Eje 2 (GruposParametro / Parametros): un grupo con LineaId es la plantilla de esa línea.
/// </summary>
public class ControlProcesoPlantillaService : IControlProcesoPlantillaService
{
    private const int LargoCodigo = 50;
    private const int LargoNombreGrupo = 100;
    private const int LargoNombrePunto = 200;
    private const int LargoSeccion = 100;
    private const int LargoUnidad = 50;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ControlProcesoPlantillaService> _logger;

    public ControlProcesoPlantillaService(IUnitOfWork unitOfWork, ILogger<ControlProcesoPlantillaService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<GrupoParametroDto>>> ObtenerPlantillaAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId, cancellationToken);
        if (linea == null)
            return Result<IReadOnlyList<GrupoParametroDto>>.Failure("NOT_FOUND", "Línea no encontrada.");

        var grupos = await _unitOfWork.GruposParametro.GetPlantillaLineaConPuntosAsync(lineaId, cancellationToken);
        var dtos = grupos.Select(g => new GrupoParametroDto
        {
            GrupoParametroId = g.GrupoParametroId,
            LineaId = g.LineaId,
            Codigo = g.Codigo,
            Nombre = g.Nombre,
            Descripcion = g.Descripcion,
            Orden = g.Orden,
            Activo = g.Activo,
            Parametros = g.Parametros
                .OrderBy(p => p.Seccion).ThenBy(p => p.Orden).ThenBy(p => p.Nombre)
                .Select(ControlProcesoMapeo.APunto)
                .ToList()
        }).ToList();

        return Result<IReadOnlyList<GrupoParametroDto>>.Success(dtos);
    }

    public async Task<Result<int>> CrearGrupoAsync(GuardarGrupoControlRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(request.LineaId, cancellationToken);
        if (linea == null)
            return Result<int>.Failure("NOT_FOUND", "Línea no encontrada.");

        var error = ValidarGrupo(request);
        if (error != null)
            return Result<int>.Failure("VALIDATION", error);

        var existentes = await _unitOfWork.GruposParametro.CountAsync(g => g.LineaId == request.LineaId, cancellationToken);
        var grupo = new GrupoParametro
        {
            LineaId = request.LineaId,
            Codigo = $"CP-L{request.LineaId}-G{existentes + 1:00}",
            Nombre = request.Nombre.Trim(),
            Descripcion = Recortar(request.Descripcion),
            Orden = request.Orden,
            Activo = request.Activo,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };

        await _unitOfWork.GruposParametro.AddAsync(grupo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grupo de control de proceso {GrupoId} creado en línea {LineaId} por {UsuarioId}",
            grupo.GrupoParametroId, request.LineaId, usuarioId);

        return Result<int>.Success(grupo.GrupoParametroId);
    }

    public async Task<Result> ActualizarGrupoAsync(int grupoParametroId, GuardarGrupoControlRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var grupo = await _unitOfWork.GruposParametro.GetByIdAsync(grupoParametroId, cancellationToken);
        if (grupo == null || grupo.LineaId == null)
            return Result.Failure("NOT_FOUND", "Grupo de control de proceso no encontrado.");

        var error = ValidarGrupo(request);
        if (error != null)
            return Result.Failure("VALIDATION", error);

        grupo.Nombre = request.Nombre.Trim();
        grupo.Descripcion = Recortar(request.Descripcion);
        grupo.Orden = request.Orden;
        grupo.Activo = request.Activo;
        grupo.ModificadoPorUsuarioId = usuarioId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<int>> CrearPuntoAsync(GuardarPuntoControlRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var grupo = await _unitOfWork.GruposParametro.GetByIdAsync(request.GrupoParametroId, cancellationToken);
        if (grupo?.LineaId == null)
            return Result<int>.Failure("NOT_FOUND", "Grupo de control de proceso no encontrado.");

        var (error, tipo, frecuencia) = ValidarPunto(request);
        if (error != null)
            return Result<int>.Failure("VALIDATION", error);

        var codigo = Recortar(request.Codigo);
        if (codigo == null)
        {
            var cantidad = await _unitOfWork.Parametros.CountByLineaAsync(grupo.LineaId.Value, cancellationToken);
            codigo = $"CP-L{grupo.LineaId}-{cantidad + 1:000}";
        }

        var punto = new Parametro
        {
            GrupoParametroId = grupo.GrupoParametroId,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };
        Aplicar(punto, request, codigo, tipo, frecuencia);

        await _unitOfWork.Parametros.AddAsync(punto, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Punto de control {ParametroId} creado en grupo {GrupoId} por {UsuarioId}",
            punto.ParametroId, grupo.GrupoParametroId, usuarioId);

        return Result<int>.Success(punto.ParametroId);
    }

    public async Task<Result> ActualizarPuntoAsync(int parametroId, GuardarPuntoControlRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var punto = await _unitOfWork.Parametros.GetByIdAsync(parametroId, cancellationToken);
        if (punto == null)
            return Result.Failure("NOT_FOUND", "Punto de control no encontrado.");

        var grupoActual = await _unitOfWork.GruposParametro.GetByIdAsync(punto.GrupoParametroId, cancellationToken);
        var grupoNuevo = request.GrupoParametroId == punto.GrupoParametroId
            ? grupoActual
            : await _unitOfWork.GruposParametro.GetByIdAsync(request.GrupoParametroId, cancellationToken);
        if (grupoActual?.LineaId == null || grupoNuevo?.LineaId != grupoActual.LineaId)
            return Result.Failure("NOT_FOUND", "El grupo tiene que ser de la plantilla de la misma línea.");

        var (error, tipo, frecuencia) = ValidarPunto(request);
        if (error != null)
            return Result.Failure("VALIDATION", error);

        // Los valores ya cargados se leen con el tipo del punto: cambiarlo rompería el histórico.
        if (tipo != punto.TipoDato && await _unitOfWork.Parametros.TieneCargasAsync(parametroId, cancellationToken))
            return Result.Failure("VALIDATION",
                "El punto ya tiene valores cargados: no se puede cambiar el tipo de dato. Desactivalo y creá uno nuevo.");

        punto.GrupoParametroId = grupoNuevo.GrupoParametroId;
        Aplicar(punto, request, Recortar(request.Codigo) ?? punto.Codigo, tipo, frecuencia);
        punto.ModificadoPorUsuarioId = usuarioId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static void Aplicar(Parametro punto, GuardarPuntoControlRequest request, string codigo, TipoDatoParametro tipo, FrecuenciaControl frecuencia)
    {
        var numerico = tipo.EsNumerico();
        punto.Codigo = codigo;
        punto.Nombre = request.Nombre.Trim();
        punto.Seccion = Recortar(request.Seccion);
        punto.TipoDato = tipo;
        punto.UnidadMedida = numerico ? Recortar(request.UnidadMedida) : null;
        punto.ValorMin = numerico ? request.ValorMin : null;
        punto.ValorMax = numerico ? request.ValorMax : null;
        punto.Frecuencia = frecuencia;
        punto.Orden = request.Orden;
        punto.Activo = request.Activo;
    }

    private static string? ValidarGrupo(GuardarGrupoControlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre del grupo es obligatorio.";
        if (request.Nombre.Trim().Length > LargoNombreGrupo)
            return $"El nombre del grupo no puede superar {LargoNombreGrupo} caracteres.";
        return null;
    }

    private static (string? Error, TipoDatoParametro Tipo, FrecuenciaControl Frecuencia) ValidarPunto(GuardarPuntoControlRequest request)
    {
        if (!Enum.TryParse<TipoDatoParametro>(request.TipoDato, true, out var tipo) || !Enum.IsDefined(tipo))
            return ("El tipo de dato no es válido (NUMERICO, OK_NOOK o TEXTO).", default, default);
        if (!Enum.TryParse<FrecuenciaControl>(request.Frecuencia, true, out var frecuencia) || !Enum.IsDefined(frecuencia))
            return ("La frecuencia no es válida.", default, default);

        var error = PuntoControlValidador.MotivoDefinicionInvalida(request.Nombre, tipo, request.ValorMin, request.ValorMax)
            ?? Largo(request.Nombre, LargoNombrePunto, "El nombre")
            ?? Largo(request.Codigo, LargoCodigo, "El código")
            ?? Largo(request.Seccion, LargoSeccion, "La sección")
            ?? Largo(request.UnidadMedida, LargoUnidad, "La unidad");

        return (error, tipo, frecuencia);
    }

    private static string? Largo(string? valor, int maximo, string campo) =>
        valor != null && valor.Trim().Length > maximo ? $"{campo} no puede superar {maximo} caracteres." : null;

    private static string? Recortar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Entidad → DTO de los puntos de control, compartido por el maestro y la carga.
/// </summary>
internal static class ControlProcesoMapeo
{
    public static ParametroDto APunto(Parametro p) => new()
    {
        ParametroId = p.ParametroId,
        GrupoParametroId = p.GrupoParametroId,
        Seccion = p.Seccion,
        Codigo = p.Codigo,
        Nombre = p.Nombre,
        UnidadMedida = p.UnidadMedida,
        TipoDato = p.TipoDato.ToString(),
        TipoDatoDescripcion = p.TipoDato.ObtenerDescripcion(),
        ValorMin = p.ValorMin,
        ValorMax = p.ValorMax,
        Frecuencia = p.Frecuencia.ToString(),
        FrecuenciaDescripcion = p.Frecuencia.ObtenerDescripcion(),
        Orden = p.Orden,
        Activo = p.Activo
    };
}
