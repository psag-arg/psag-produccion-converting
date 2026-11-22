using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Camaras;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Maestro de cámaras (T-798 etapa 4, diseño 2.4). Sólo el administrador lo modifica; nunca se borra (se desactiva).
/// <see cref="Camara.Habilitada"/> es la única fuente de verdad del servicio de conteo.
/// </summary>
public class CamaraService : ICamaraService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<CamaraService> _logger;
    private readonly Func<DateTimeOffset> _ahora;

    public CamaraService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<CamaraService> logger)
        : this(unitOfWork, zonaPlanta, logger, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Con reloj inyectable, para los tests.</summary>
    public CamaraService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<CamaraService> logger, Func<DateTimeOffset> ahora)
    {
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
        _ahora = ahora;
    }

    public async Task<IReadOnlyList<CamaraDto>> ObtenerAsync(int? lineaId, bool incluirInactivas, CancellationToken cancellationToken = default)
    {
        var ahora = _ahora();
        return (await _unitOfWork.Camaras.GetListadoAsync(lineaId, incluirInactivas, cancellationToken))
            .Select(c => ADto(c, ahora)).ToList();
    }

    public async Task<CamaraDto?> ObtenerPorIdAsync(int camaraId, CancellationToken cancellationToken = default)
    {
        var camara = (await _unitOfWork.Camaras.GetListadoAsync(null, incluirInactivas: true, cancellationToken))
            .FirstOrDefault(c => c.CamaraId == camaraId);
        return camara == null ? null : ADto(camara, _ahora());
    }

    public async Task<Result<CamaraDto>> CrearAsync(
        GuardarCamaraRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<CamaraDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var camara = new Camara { CreadoPorUsuarioId = usuarioId };
        Aplicar(camara, request, usuarioId);
        var motivo = await MotivoInvalidaAsync(camara, null, cancellationToken);
        if (motivo != null)
            return Result<CamaraDto>.Failure("VALIDATION", motivo);

        await _unitOfWork.Camaras.AddAsync(camara, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cámara {Codigo} creada por usuario {UsuarioId} (habilitada: {Habilitada})", camara.Codigo, usuarioId, camara.Habilitada);
        return Result<CamaraDto>.Success((await ObtenerPorIdAsync(camara.CamaraId, cancellationToken))!);
    }

    public async Task<Result<CamaraDto>> ActualizarAsync(
        int camaraId, GuardarCamaraRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<CamaraDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var camara = await _unitOfWork.Camaras.GetByIdAsync(camaraId, cancellationToken);
        if (camara == null)
            return Result<CamaraDto>.Failure("NOT_FOUND", "Cámara no encontrada.");

        var lineaAnterior = camara.LineaId;
        Aplicar(camara, request, usuarioId);
        var motivo = await MotivoInvalidaAsync(camara, camaraId, cancellationToken);
        if (motivo == null && camara.LineaId != lineaAnterior && await TieneDatosAsync(camaraId, cancellationToken))
            motivo = "La cámara ya tiene conteos o paradas en su línea (la línea de esos datos sale de la cámara). " +
                     "Para otra línea, dá de alta otra cámara.";
        if (motivo != null)
            return Result<CamaraDto>.Failure("VALIDATION", motivo);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cámara {Codigo} actualizada por usuario {UsuarioId} (habilitada: {Habilitada}, activa: {Activo})",
            camara.Codigo, usuarioId, camara.Habilitada, camara.Activo);
        return Result<CamaraDto>.Success((await ObtenerPorIdAsync(camaraId, cancellationToken))!);
    }

    public async Task<Result<CamaraDto>> DesactivarAsync(
        int camaraId, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeAdministrarMaestros(roles))
            return Result<CamaraDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoMaestros);

        var camara = await _unitOfWork.Camaras.GetByIdAsync(camaraId, cancellationToken);
        if (camara == null)
            return Result<CamaraDto>.Failure("NOT_FOUND", "Cámara no encontrada.");

        if (await _unitOfWork.PuntosControlCalidad.ExistsAsync(p => p.CamaraId == camaraId && p.Activo, cancellationToken))
            return Result<CamaraDto>.Failure("VALIDATION",
                "La cámara la usa un punto de control de calidad activo. Cambiale la cámara o desactivalo primero en Maestros → Calidad.");

        DarDeBaja(camara);
        camara.ModificadoPorUsuarioId = usuarioId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cámara {Codigo} desactivada por usuario {UsuarioId}", camara.Codigo, usuarioId);
        return Result<CamaraDto>.Success((await ObtenerPorIdAsync(camaraId, cancellationToken))!);
    }

    public async Task<IReadOnlyList<CamaraConfiguracionDto>> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default) =>
        (await _unitOfWork.Camaras.GetHabilitadasAsync(cancellationToken))
            .Select(c => new CamaraConfiguracionDto
            {
                Codigo = c.Codigo,
                Nombre = c.Nombre,
                LineaId = c.LineaId,
                LineaNombre = c.Linea?.Nombre ?? string.Empty,
                Canal = c.NumeroCanalGrabador,
                Funcion = c.Funcion,
                Magnitud = c.Magnitud,
                MetodoDeteccion = c.MetodoDeteccion,
                CantidadCarriles = c.CantidadCarriles,
                DetectaParadas = c.DetectaParadas,
                CreaDetenciones = c.CreaDetenciones,
                UmbralDetencionSegundos = DetencionCamaraReglas.UmbralSegundos(c)
            })
            .ToList();

    // ===================== Ayudas =====================

    private static void Aplicar(Camara c, GuardarCamaraRequest r, string usuarioId)
    {
        c.Codigo = CalidadReglas.NormalizarCodigo(r.Codigo);
        c.Nombre = (r.Nombre ?? string.Empty).Trim();
        c.LineaId = r.LineaId;
        c.EquipoId = r.EquipoId;
        c.NumeroCanalGrabador = r.NumeroCanalGrabador;
        c.Funcion = CamaraReglas.Normalizar(r.Funcion) ?? string.Empty;
        if (c.Funcion == FuncionCamara.Calidad)
        {
            // Lo del conteo no aplica a una cámara de calidad: se limpia (la pantalla lo oculta).
            c.Magnitud = null;
            c.MetodoDeteccion = null;
            c.CantidadCarriles = null;
            c.DetectaParadas = false;
            c.CreaDetenciones = false;
            c.UmbralDetencionSegundos = null;
            c.CuentaProduccionOficial = false;
        }
        else
        {
            c.Magnitud = CamaraReglas.Normalizar(r.Magnitud);
            c.MetodoDeteccion = CamaraReglas.Normalizar(r.MetodoDeteccion);
            c.CantidadCarriles = r.CantidadCarriles is { } n ? (byte)Math.Clamp(n, 0, byte.MaxValue) : null;   // fuera de 1-16 lo rechaza la validación
            c.DetectaParadas = r.DetectaParadas;
            c.CreaDetenciones = r.CreaDetenciones;
            c.UmbralDetencionSegundos = r.UmbralDetencionSegundos;
            c.CuentaProduccionOficial = r.CuentaProduccionOficial;
        }
        c.Habilitada = r.Habilitada;
        c.Activo = r.Activo;
        if (!c.Activo)
            DarDeBaja(c);
        c.ModificadoPorUsuarioId = usuarioId;
    }

    /// <summary>Una cámara dada de baja no corre ni cuenta la producción oficial (libera el lugar de la línea y magnitud).</summary>
    private static void DarDeBaja(Camara c)
    {
        c.Activo = false;
        c.Habilitada = false;
        c.CuentaProduccionOficial = false;
    }

    private async Task<string?> MotivoInvalidaAsync(Camara c, int? idActual, CancellationToken ct)
    {
        if (CamaraReglas.MotivoInvalida(c) is { } motivo)
            return motivo;

        var linea = await _unitOfWork.Lineas.GetByIdAsync(c.LineaId, ct);
        if (linea == null)
            return "La línea no existe.";
        if (c.EquipoId is { } equipoId)
        {
            var equipo = await _unitOfWork.Equipos.GetByIdAsync(equipoId, ct);
            if (equipo == null || equipo.LineaId != c.LineaId)
                return "El equipo tiene que ser de la misma línea que la cámara.";
        }

        var existente = await _unitOfWork.Camaras.GetByCodigoAsync(c.Codigo, ct);
        if (existente != null && existente.CamaraId != idActual)
            return $"Ya hay una cámara con el código {c.Codigo} ({existente.Nombre}).";

        if (c.CuentaProduccionOficial && c.Magnitud != null
            && await _unitOfWork.Camaras.GetOficialAsync(c.LineaId, c.Magnitud, idActual, ct) is { } oficial)
            return $"La producción oficial de {EficienciaCamaraCalculador.Unidad(c.Magnitud)} de {linea.Nombre} ya la cuenta la cámara " +
                   $"{oficial.Codigo}. Una sola por línea y magnitud: sacale el tilde a esa primero.";
        return null;
    }

    private async Task<bool> TieneDatosAsync(int camaraId, CancellationToken ct) =>
        await _unitOfWork.ConteosCamaraMinuto.ExistsAsync(m => m.CamaraId == camaraId, ct)
        || await _unitOfWork.ParadasCamara.ExistsAsync(p => p.CamaraId == camaraId, ct);

    private CamaraDto ADto(Camara c, DateTimeOffset ahora)
    {
        var umbral = DetencionCamaraReglas.UmbralSegundos(c);
        return new CamaraDto
        {
            CamaraId = c.CamaraId,
            Codigo = c.Codigo,
            Nombre = c.Nombre,
            LineaId = c.LineaId,
            LineaNombre = c.Linea?.Nombre ?? string.Empty,
            EquipoId = c.EquipoId,
            EquipoNombre = c.Equipo?.Nombre,
            NumeroCanalGrabador = c.NumeroCanalGrabador,
            Funcion = c.Funcion,
            Magnitud = c.Magnitud,
            MetodoDeteccion = c.MetodoDeteccion,
            CantidadCarriles = c.CantidadCarriles,
            DetectaParadas = c.DetectaParadas,
            CreaDetenciones = c.CreaDetenciones,
            UmbralDetencionSegundos = c.UmbralDetencionSegundos,
            UmbralSegundos = umbral,
            UmbralTexto = DetencionCamaraReglas.TextoUmbral(umbral),
            CuentaProduccionOficial = c.CuentaProduccionOficial,
            Habilitada = c.Habilitada,
            Activo = c.Activo,
            UltimoDatoRecibido = HoraPlanta.En(c.UltimoDatoRecibido, _zonaPlanta),
            MinutosDesdeUltimoDato = c.UltimoDatoRecibido is { } u ? (int)Math.Max(0, Math.Floor((ahora - u).TotalMinutes)) : null,
            EstadoUltimoDato = CamaraReglas.EstadoUltimoDato(c.UltimoDatoRecibido, ahora)
        };
    }
}
