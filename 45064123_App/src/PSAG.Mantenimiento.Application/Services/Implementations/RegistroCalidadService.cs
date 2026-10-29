using System.Globalization;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Registros de calidad (T-611). Ante un defecto el sistema SOLO registra: no rechaza ni alerta.
/// </summary>
public class RegistroCalidadService : IRegistroCalidadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAlmacenImagenesCalidad _almacen;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<RegistroCalidadService> _logger;

    public RegistroCalidadService(
        IUnitOfWork unitOfWork,
        IAlmacenImagenesCalidad almacen,
        TimeZoneInfo zonaPlanta,
        ILogger<RegistroCalidadService> logger)
    {
        _unitOfWork = unitOfWork;
        _almacen = almacen;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
    }

    // ===================== Consultas =====================

    public async Task<Result<PagedResult<RegistroCalidadDto>>> ObtenerPaginadoAsync(
        RegistrosCalidadFiltroRequest filtro, CancellationToken cancellationToken = default)
    {
        if (!TryParsearOpcional<EstadoRevisionCalidad>(filtro.EstadoRevision, out var estado))
            return Result<PagedResult<RegistroCalidadDto>>.Failure("VALIDATION", "Estado de revisión no válido.");
        if (!TryParsearOpcional<OrigenRegistroCalidad>(filtro.Origen, out var origen))
            return Result<PagedResult<RegistroCalidadDto>>.Failure("VALIDATION", "Origen no válido.");
        if (filtro.FechaDesde > filtro.FechaHasta)
            return Result<PagedResult<RegistroCalidadDto>>.Failure("VALIDATION", "La fecha desde no puede ser posterior a la fecha hasta.");

        var (items, total) = await _unitOfWork.RegistrosCalidad.GetPaginadoAsync(
            new FiltroRegistrosCalidad(
                InicioDelDia(filtro.FechaDesde),
                InicioDelDia(filtro.FechaHasta?.AddDays(1)),
                filtro.TurnoId,
                filtro.LineaId,
                filtro.PuntoControlCalidadId,
                filtro.TipoDefectoCalidadId,
                estado,
                origen),
            filtro.Page,
            filtro.PageSize,
            cancellationToken);

        return Result<PagedResult<RegistroCalidadDto>>.Success(new PagedResult<RegistroCalidadDto>
        {
            Items = items.Select(r => ADto(r, conRevisiones: false)).ToList(),
            TotalCount = total,
            Page = filtro.Page,
            PageSize = filtro.PageSize
        });
    }

    public async Task<Result<RegistroCalidadDto>> ObtenerAsync(long registroCalidadId, CancellationToken cancellationToken = default)
    {
        var registro = await _unitOfWork.RegistrosCalidad.GetByIdConDetalleAsync(registroCalidadId, cancellationToken);
        return registro == null
            ? Result<RegistroCalidadDto>.Failure("NOT_FOUND", "Registro de calidad no encontrado.")
            : Result<RegistroCalidadDto>.Success(ADto(registro, conRevisiones: true));
    }

    public async Task<Result<AdjuntoArchivo>> AbrirImagenAsync(long registroCalidadId, CancellationToken cancellationToken = default)
    {
        var registro = await _unitOfWork.RegistrosCalidad.GetByIdAsync(registroCalidadId, cancellationToken);
        if (registro == null || !registro.TieneImagen)
            return Result<AdjuntoArchivo>.Failure("NOT_FOUND", "El registro no tiene imagen.");

        var stream = _almacen.Abrir(registro.ImagenNombreArchivo!);
        if (stream == null)
        {
            _logger.LogWarning("Falta en disco la imagen {NombreArchivo} del registro de calidad {RegistroId}",
                registro.ImagenNombreArchivo, registroCalidadId);
            return Result<AdjuntoArchivo>.Failure("NOT_FOUND", "No se encontró el archivo de la imagen.");
        }

        return Result<AdjuntoArchivo>.Success(new AdjuntoArchivo(
            stream, registro.ImagenContentType!, registro.ImagenNombreOriginal ?? registro.ImagenNombreArchivo!));
    }

    // ===================== Alta =====================

    public async Task<Result<RegistroCalidadDto>> CrearManualAsync(
        CrearRegistroCalidadManualRequest request,
        ImagenEntrante? imagen,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeCargarManual(roles))
            return Result<RegistroCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoCargaManual);

        var faltan = new List<string>();
        if (request.PuntoControlCalidadId <= 0) faltan.Add("punto de control");
        if (request.TipoDefectoCalidadId <= 0) faltan.Add("defecto");
        if (faltan.Count > 0)
            return Result<RegistroCalidadDto>.Failure("VALIDATION", "Falta completar: " + string.Join(", ", faltan) + ".");

        var punto = await _unitOfWork.PuntosControlCalidad.GetByIdAsync(request.PuntoControlCalidadId, cancellationToken);
        var tipo = await _unitOfWork.TiposDefectoCalidad.GetByIdAsync(request.TipoDefectoCalidadId, cancellationToken);

        Producto? producto = null;
        if (request.ProductoId is { } productoId)
        {
            producto = await _unitOfWork.Productos.GetByIdAsync(productoId, cancellationToken);
            if (producto == null)
                return Result<RegistroCalidadDto>.Failure("VALIDATION", "El producto no existe.");
        }

        if (!TryParsearDecimal(request.ValorMedido, out var valor))
            return Result<RegistroCalidadDto>.Failure("VALIDATION", "El valor medido no es un número válido.");

        var resultado = await ArmarYGuardarAsync(new DatosRegistro(
            punto, tipo, request.FechaHora, request.TurnoId, Confianza: null, valor, request.UnidadMedida, producto,
            request.CodigoDetencion, request.Observaciones, IdentificadorExterno: null),
            imagen, OrigenRegistroCalidad.MANUAL, usuarioId, cancellationToken);
        if (resultado.IsFailure)
            return Result<RegistroCalidadDto>.Failure(resultado.ErrorCode!, resultado.ErrorMessage!);

        return Result<RegistroCalidadDto>.Success(ADto(resultado.Value!, conRevisiones: true));
    }

    public async Task<Result<IngestaCalidadRespuesta>> IngestarAsync(
        IngestaCalidadRequest request,
        ImagenEntrante? imagen,
        CancellationToken cancellationToken = default)
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(request.PuntoControl)) faltan.Add("puntoControl");
        if (string.IsNullOrWhiteSpace(request.TipoDefecto)) faltan.Add("tipoDefecto");
        if (imagen == null) faltan.Add("imagen");
        if (faltan.Count > 0)
            return Result<IngestaCalidadRespuesta>.Failure("VALIDATION", "Faltan campos obligatorios: " + string.Join(", ", faltan) + ".");

        var identificador = string.IsNullOrWhiteSpace(request.IdentificadorExterno) ? null : request.IdentificadorExterno.Trim();
        if (identificador?.Length > CalidadReglas.LargoMaximoIdentificadorExterno)
            return Result<IngestaCalidadRespuesta>.Failure("VALIDATION",
                $"identificadorExterno admite hasta {CalidadReglas.LargoMaximoIdentificadorExterno} caracteres.");

        if (identificador != null)
        {
            var existente = await _unitOfWork.RegistrosCalidad.GetByIdentificadorExternoAsync(identificador, cancellationToken);
            if (existente != null)
                return Result<IngestaCalidadRespuesta>.Success(ARespuesta(existente, duplicado: true));
        }

        var codigoPunto = CalidadReglas.NormalizarCodigo(request.PuntoControl);
        var codigoTipo = CalidadReglas.NormalizarCodigo(request.TipoDefecto);
        var punto = await _unitOfWork.PuntosControlCalidad.GetByCodigoAsync(codigoPunto, cancellationToken);
        if (punto == null)
            return Result<IngestaCalidadRespuesta>.Failure("VALIDATION", $"No existe el punto de control {codigoPunto}. Cargalo en Maestros → Calidad.");
        var tipo = await _unitOfWork.TiposDefectoCalidad.GetByCodigoAsync(codigoTipo, cancellationToken);
        if (tipo == null)
            return Result<IngestaCalidadRespuesta>.Failure("VALIDATION", $"No existe el tipo de defecto {codigoTipo}. Cargalo en Maestros → Calidad.");

        Producto? producto = null;
        if (!string.IsNullOrWhiteSpace(request.Producto))
        {
            var codigoProducto = request.Producto.Trim();
            producto = (await _unitOfWork.Productos.FindAsync(p => p.Codigo == codigoProducto, cancellationToken)).FirstOrDefault();
            if (producto == null)
                return Result<IngestaCalidadRespuesta>.Failure("VALIDATION", $"No existe el producto {codigoProducto}.");
        }

        if (!TryParsearDecimal(request.Confianza, out var confianza))
            return Result<IngestaCalidadRespuesta>.Failure("VALIDATION", "confianza no es un número válido (usar punto decimal, ej.: 0.87).");
        if (!TryParsearDecimal(request.ValorMedido, out var valor))
            return Result<IngestaCalidadRespuesta>.Failure("VALIDATION", "valorMedido no es un número válido (usar punto decimal, ej.: 14.5).");

        var resultado = await ArmarYGuardarAsync(new DatosRegistro(
            punto, tipo, request.FechaHora, request.TurnoId, confianza, valor, request.UnidadMedida, producto,
            request.CodigoDetencion, request.Observaciones, identificador),
            imagen, OrigenRegistroCalidad.AUTOMATICO, RegistroCalidad.UsuarioDetector, cancellationToken);
        if (resultado.IsFailure)
            return Result<IngestaCalidadRespuesta>.Failure(resultado.ErrorCode!, resultado.ErrorMessage!);

        return Result<IngestaCalidadRespuesta>.Success(ARespuesta(resultado.Value!, duplicado: false));
    }

    private sealed record DatosRegistro(
        PuntoControlCalidad? Punto,
        TipoDefectoCalidad? Tipo,
        string? FechaHora,
        int? TurnoId,
        decimal? Confianza,
        decimal? ValorMedido,
        string? UnidadMedida,
        Producto? Producto,
        string? CodigoDetencion,
        string? Observaciones,
        string? IdentificadorExterno);

    /// <summary>
    /// Validaciones comunes a la carga manual y a la ingesta, guardado de la imagen y del registro.
    /// Si falla la base, la imagen guardada se borra.
    /// </summary>
    private async Task<Result<RegistroCalidad>> ArmarYGuardarAsync(
        DatosRegistro datos,
        ImagenEntrante? imagen,
        OrigenRegistroCalidad origen,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        if (datos.Punto == null || !datos.Punto.Activo)
            return Result<RegistroCalidad>.Failure("VALIDATION", "El punto de control no existe o está inactivo.");
        if (datos.Tipo == null || !datos.Tipo.Activo)
            return Result<RegistroCalidad>.Failure("VALIDATION", "El tipo de defecto no existe o está inactivo.");

        var ahora = DateTimeOffset.UtcNow;
        if (!TryParsearFecha(datos.FechaHora, ahora, out var fechaHora))
            return Result<RegistroCalidad>.Failure("VALIDATION", "La fecha y hora no tiene un formato válido (ISO 8601, ej.: 2026-10-29T14:35:00-03:00).");
        if (CalidadReglas.EsFechaFutura(fechaHora, ahora))
            return Result<RegistroCalidad>.Failure("VALIDATION", "La fecha y hora de la detección no puede ser posterior a ahora.");

        var unidad = string.IsNullOrWhiteSpace(datos.UnidadMedida) ? datos.Tipo.UnidadMedida : datos.UnidadMedida.Trim();
        var motivo = CalidadReglas.MotivoDatosInvalidos(datos.Confianza, datos.ValorMedido, unidad);
        if (motivo != null)
            return Result<RegistroCalidad>.Failure("VALIDATION", motivo);

        var observaciones = string.IsNullOrWhiteSpace(datos.Observaciones) ? null : datos.Observaciones.Trim();
        if (observaciones?.Length > CalidadReglas.LargoMaximoObservaciones)
            return Result<RegistroCalidad>.Failure("VALIDATION", $"Las observaciones admiten hasta {CalidadReglas.LargoMaximoObservaciones} caracteres.");

        // Turno: el que viene, o el de la hora de la detección en la planta (igual que la detención con hora de inicio).
        Turno? turno;
        if (datos.TurnoId is { } turnoId)
        {
            turno = await _unitOfWork.Turnos.GetByIdAsync(turnoId, cancellationToken);
            if (turno == null || !turno.Activo)
                return Result<RegistroCalidad>.Failure("VALIDATION", "El turno no existe o está inactivo.");
        }
        else
        {
            var horaPlanta = TimeOnly.FromDateTime(HoraPlanta.En(fechaHora, _zonaPlanta)!.Value.DateTime);
            turno = await _unitOfWork.Turnos.GetTurnoPorHoraAsync(horaPlanta, cancellationToken);
            if (turno == null)
                _logger.LogWarning("Registro de calidad sin turno: no hay turno activo para las {Hora}", horaPlanta);
        }

        Detencion? detencion = null;
        if (!string.IsNullOrWhiteSpace(datos.CodigoDetencion))
        {
            var (encontrada, error) = await BuscarDetencionAsync(datos.CodigoDetencion, datos.Punto.LineaId, cancellationToken);
            if (error != null)
                return Result<RegistroCalidad>.Failure("VALIDATION", error);
            detencion = encontrada;
        }

        var registro = new RegistroCalidad
        {
            FechaHoraDeteccion = fechaHora,
            LineaId = datos.Punto.LineaId,
            PuntoControlCalidadId = datos.Punto.PuntoControlCalidadId,
            TurnoId = turno?.TurnoId,
            TipoDefectoCalidadId = datos.Tipo.TipoDefectoCalidadId,
            Origen = origen,
            Confianza = datos.Confianza,
            ValorMedido = datos.ValorMedido,
            UnidadMedida = datos.ValorMedido.HasValue ? unidad : null,
            ProductoId = datos.Producto?.ProductoId,
            DetencionId = detencion?.DetencionId,
            Observaciones = observaciones,
            IdentificadorExterno = datos.IdentificadorExterno,
            EstadoRevision = EstadoRevisionCalidad.PENDIENTE,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };

        if (imagen != null)
        {
            var motivoImagen = await PrepararImagenAsync(imagen, registro, cancellationToken);
            if (motivoImagen != null)
                return Result<RegistroCalidad>.Failure("VALIDATION", motivoImagen);
        }

        // Quien lo carga a mano lo vio: nace confirmado por esa persona y queda en el historial.
        if (origen == OrigenRegistroCalidad.MANUAL)
            registro.Revisar(EstadoRevisionCalidad.CONFIRMADO, usuarioId, ahora, "Carga manual");

        await _unitOfWork.RegistrosCalidad.AddAsync(registro, cancellationToken);

        if (registro.TieneImagen)
            await _almacen.GuardarAsync(registro.ImagenNombreArchivo!, imagen!.Contenido, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (registro.TieneImagen)
                _almacen.Eliminar(registro.ImagenNombreArchivo!);
            throw;
        }

        _logger.LogInformation(
            "Registro de calidad {RegistroId} ({Origen}) creado por {UsuarioId}: punto {Punto}, defecto {Defecto}",
            registro.RegistroCalidadId, origen, usuarioId, datos.Punto.Codigo, datos.Tipo.Codigo);

        registro.Linea = await _unitOfWork.Lineas.GetByIdAsync(registro.LineaId, cancellationToken) ?? registro.Linea;
        registro.PuntoControl = datos.Punto;
        registro.TipoDefecto = datos.Tipo;
        registro.Turno = turno;
        registro.Producto = datos.Producto;
        registro.Detencion = detencion;
        return Result<RegistroCalidad>.Success(registro);
    }

    /// <summary>
    /// Valida la imagen por contenido (JPG o PNG real, hasta 10 MB) y completa los datos en el registro.
    /// Deja el stream al principio para guardarlo. Devuelve el motivo de rechazo o null.
    /// </summary>
    private static async Task<string?> PrepararImagenAsync(ImagenEntrante imagen, RegistroCalidad registro, CancellationToken cancellationToken)
    {
        var contenido = imagen.Contenido;
        if (!contenido.CanSeek)
            throw new ArgumentException("El contenido de la imagen tiene que poder rebobinarse.", nameof(imagen));

        var inicio = contenido.Position;
        var tamanoReal = Math.Max(imagen.TamanoBytes, contenido.Length - inicio);
        var cabecera = new byte[AdjuntoImagenReglas.BytesCabecera];
        var leidos = await contenido.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, cancellationToken);

        var motivo = AdjuntoImagenReglas.MotivoRechazoImagen(imagen.NombreOriginal, tamanoReal, cabecera.AsSpan(0, leidos));
        if (motivo != null)
            return motivo;

        contenido.Position = inicio;
        var contentType = AdjuntoImagenReglas.DetectarTipo(cabecera)!;
        registro.ImagenNombreArchivo = $"{Guid.NewGuid():N}{AdjuntoImagenReglas.Extension(contentType)}";
        registro.ImagenNombreOriginal = NombreParaMostrar(imagen.NombreOriginal);
        registro.ImagenContentType = contentType;
        registro.ImagenTamanoBytes = tamanoReal;
        return null;
    }

    // ===================== Revisión =====================

    public async Task<Result<RegistroCalidadDto>> RevisarAsync(
        long registroCalidadId,
        RevisarRegistroCalidadRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeRevisar(roles))
            return Result<RegistroCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoRevision);

        if (!Enum.TryParse<EstadoRevisionCalidad>(request.Estado, ignoreCase: true, out var nuevo)
            || !Enum.IsDefined(nuevo)
            || nuevo == EstadoRevisionCalidad.PENDIENTE)
            return Result<RegistroCalidadDto>.Failure("VALIDATION", "Elegí si el defecto se confirma o es un falso positivo.");

        var comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim();
        if (comentario?.Length > CalidadReglas.LargoMaximoComentario)
            return Result<RegistroCalidadDto>.Failure("VALIDATION", $"El comentario admite hasta {CalidadReglas.LargoMaximoComentario} caracteres.");

        var registro = await _unitOfWork.RegistrosCalidad.GetByIdConDetalleAsync(registroCalidadId, cancellationToken);
        if (registro == null)
            return Result<RegistroCalidadDto>.Failure("NOT_FOUND", "Registro de calidad no encontrado.");

        if (!registro.RowVersion.SequenceEqual(request.RowVersion))
            return Result<RegistroCalidadDto>.Failure("CONCURRENCY", "Alguien revisó este registro mientras lo mirabas. Recargá y volvé a intentar.");

        if (!registro.EstadoRevision.PuedeTransicionarA(nuevo))
            return Result<RegistroCalidadDto>.Failure("VALIDATION", $"El registro ya está marcado como {registro.EstadoRevision.ObtenerDescripcion().ToLowerInvariant()}.");

        registro.Revisar(nuevo, usuarioId, DateTimeOffset.UtcNow, comentario);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registro de calidad {RegistroId} marcado {Estado} por usuario {UsuarioId}",
            registroCalidadId, nuevo, usuarioId);

        return Result<RegistroCalidadDto>.Success(ADto(registro, conRevisiones: true));
    }

    public async Task<Result<RegistroCalidadDto>> VincularDetencionAsync(
        long registroCalidadId,
        VincularDetencionCalidadRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!CalidadReglas.PuedeRevisar(roles))
            return Result<RegistroCalidadDto>.Failure("FORBIDDEN", CalidadReglas.MensajeSinPermisoRevision);

        var registro = await _unitOfWork.RegistrosCalidad.GetByIdConDetalleAsync(registroCalidadId, cancellationToken);
        if (registro == null)
            return Result<RegistroCalidadDto>.Failure("NOT_FOUND", "Registro de calidad no encontrado.");

        Detencion? detencion = null;
        if (!string.IsNullOrWhiteSpace(request.CodigoDetencion))
        {
            var (encontrada, error) = await BuscarDetencionAsync(request.CodigoDetencion, registro.LineaId, cancellationToken);
            if (error != null)
                return Result<RegistroCalidadDto>.Failure("VALIDATION", error);
            detencion = encontrada;
        }

        registro.DetencionId = detencion?.DetencionId;
        registro.Detencion = detencion;
        registro.ModificadoPorUsuarioId = usuarioId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registro de calidad {RegistroId} relacionado con la detención {Detencion} por usuario {UsuarioId}",
            registroCalidadId, detencion?.Codigo ?? "(ninguna)", usuarioId);

        return Result<RegistroCalidadDto>.Success(ADto(registro, conRevisiones: true));
    }

    // ===================== Ayudas =====================

    private async Task<(Detencion? Detencion, string? Error)> BuscarDetencionAsync(string codigo, int lineaId, CancellationToken cancellationToken)
    {
        var limpio = codigo.Trim().ToUpperInvariant();
        var detencion = await _unitOfWork.Detenciones.GetByCodigoAsync(limpio, cancellationToken);
        if (detencion == null || detencion.Eliminada)
            return (null, $"No existe la detención {limpio}.");
        if (detencion.LineaId != lineaId)
            return (null, $"La detención {limpio} es de otra línea.");
        return (detencion, null);
    }

    /// <summary>
    /// Vacío = ahora. Con offset (Z, -03:00) se respeta; sin offset es hora de pared de la planta.
    /// </summary>
    private bool TryParsearFecha(string? texto, DateTimeOffset ahora, out DateTimeOffset fecha)
    {
        fecha = ahora;
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        if (!DateTime.TryParse(texto.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var sinZona))
            return false;

        if (sinZona.Kind == DateTimeKind.Unspecified)
        {
            fecha = HoraPlanta.DesdeHoraDePared(sinZona, _zonaPlanta);
            return true;
        }

        return DateTimeOffset.TryParse(texto.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }

    /// <summary>
    /// Número con punto decimal (también acepta coma si no hay punto). Vacío = sin valor.
    /// No depende de la cultura del servidor.
    /// </summary>
    public static bool TryParsearDecimal(string? texto, out decimal? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        var normalizado = texto.Trim();
        if (!normalizado.Contains('.') && normalizado.Count(c => c == ',') == 1)
            normalizado = normalizado.Replace(',', '.');

        if (!decimal.TryParse(normalizado, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parseado))
            return false;

        valor = parseado;
        return true;
    }

    private static bool TryParsearOpcional<T>(string? texto, out T? valor) where T : struct, Enum
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto))
            return true;
        if (!Enum.TryParse<T>(texto, ignoreCase: true, out var parseado) || !Enum.IsDefined(parseado))
            return false;
        valor = parseado;
        return true;
    }

    private DateTimeOffset? InicioDelDia(DateOnly? fecha) =>
        fecha is { } f ? HoraPlanta.DesdeHoraDePared(f.ToDateTime(TimeOnly.MinValue), _zonaPlanta) : null;

    private static string NombreParaMostrar(string? nombreOriginal)
    {
        var nombre = Path.GetFileName((nombreOriginal ?? string.Empty).Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(nombre))
            nombre = "imagen";
        return nombre.Length > 255 ? nombre[^255..] : nombre;
    }

    private static IngestaCalidadRespuesta ARespuesta(RegistroCalidad r, bool duplicado) => new()
    {
        RegistroCalidadId = r.RegistroCalidadId,
        Duplicado = duplicado,
        EstadoRevision = r.EstadoRevision.ToString(),
        LineaNombre = r.Linea?.Nombre ?? string.Empty,
        TurnoNombre = r.Turno?.Nombre,
        FechaHoraDeteccion = r.FechaHoraDeteccion
    };

    private static RegistroCalidadDto ADto(RegistroCalidad r, bool conRevisiones) => new()
    {
        RegistroCalidadId = r.RegistroCalidadId,
        FechaHoraDeteccion = r.FechaHoraDeteccion,
        LineaId = r.LineaId,
        LineaNombre = r.Linea?.Nombre ?? string.Empty,
        PuntoControlCalidadId = r.PuntoControlCalidadId,
        PuntoControlCodigo = r.PuntoControl?.Codigo ?? string.Empty,
        PuntoControlNombre = r.PuntoControl?.Nombre ?? string.Empty,
        TurnoId = r.TurnoId,
        TurnoNombre = r.Turno?.Nombre,
        TipoDefectoCalidadId = r.TipoDefectoCalidadId,
        TipoDefectoCodigo = r.TipoDefecto?.Codigo ?? string.Empty,
        TipoDefectoNombre = r.TipoDefecto?.Nombre ?? string.Empty,
        Origen = r.Origen.ToString(),
        OrigenDescripcion = r.Origen.ObtenerDescripcion(),
        Confianza = r.Confianza,
        ValorMedido = r.ValorMedido,
        UnidadMedida = r.UnidadMedida,
        ProductoId = r.ProductoId,
        ProductoCodigo = r.Producto?.Codigo,
        ProductoNombre = r.Producto?.Nombre,
        DetencionId = r.DetencionId,
        DetencionCodigo = r.Detencion?.Codigo,
        Observaciones = r.Observaciones,
        IdentificadorExterno = r.IdentificadorExterno,
        TieneImagen = r.TieneImagen,
        EstadoRevision = r.EstadoRevision.ToString(),
        EstadoRevisionDescripcion = r.EstadoRevision.ObtenerDescripcion(),
        RevisadoPorUsuarioId = r.RevisadoPorUsuarioId,
        FechaHoraRevision = r.FechaHoraRevision,
        CreadoPorUsuarioId = r.CreadoPorUsuarioId,
        FechaHoraCreacion = r.FechaHoraCreacion,
        RowVersion = r.RowVersion,
        Revisiones = conRevisiones
            ? r.Revisiones
                .OrderByDescending(v => v.FechaHora)
                .ThenByDescending(v => v.RegistroCalidadRevisionId)
                .Select(v => new RegistroCalidadRevisionDto
                {
                    EstadoAnterior = v.EstadoAnterior.ToString(),
                    EstadoAnteriorDescripcion = v.EstadoAnterior.ObtenerDescripcion(),
                    EstadoNuevo = v.EstadoNuevo.ToString(),
                    EstadoNuevoDescripcion = v.EstadoNuevo.ObtenerDescripcion(),
                    Comentario = v.Comentario,
                    UsuarioId = v.UsuarioId,
                    FechaHora = v.FechaHora
                })
                .ToList()
            : new()
    };
}
