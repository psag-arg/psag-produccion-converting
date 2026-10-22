using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class RegistroProcesoService : IRegistroProcesoService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegistroProcesoService> _logger;

    public RegistroProcesoService(IUnitOfWork unitOfWork, ILogger<RegistroProcesoService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IniciarRegistroProcesoResponse>> IniciarRegistroAsync(
        IniciarRegistroProcesoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        // Validar línea y producto
        var linea = await _unitOfWork.Lineas.GetByIdAsync(request.LineaId, cancellationToken);
        if (linea == null)
            return Result<IniciarRegistroProcesoResponse>.Failure("NOT_FOUND", "Línea no encontrada.");

        if (request.ProductoId.HasValue)
        {
            var producto = await _unitOfWork.Productos.GetByIdAsync(request.ProductoId.Value, cancellationToken);
            if (producto == null)
                return Result<IniciarRegistroProcesoResponse>.Failure("NOT_FOUND", "Producto no encontrado.");
        }

        // Obtener turno actual
        var turno = await _unitOfWork.Turnos.GetTurnoActualAsync(cancellationToken);
        if (turno == null)
            return Result<IniciarRegistroProcesoResponse>.Failure("NO_TURNO", "No hay turno configurado para la hora actual.");

        var fechaHoy = DateOnly.FromDateTime(DateTime.Today);

        // Buscar registro existente para línea+producto+turno+fecha
        var registroExistente = await _unitOfWork.RegistrosProceso.GetByLineaProductoTurnoFechaAsync(
            request.LineaId, request.ProductoId, turno.TurnoId, fechaHoy, cancellationToken);

        if (registroExistente != null)
        {
            // Retomar registro existente
            _logger.LogInformation("Retomando registro {RegistroId} existente", registroExistente.RegistroProcesoId);

            return Result<IniciarRegistroProcesoResponse>.Success(new IniciarRegistroProcesoResponse
            {
                RegistroProcesoId = registroExistente.RegistroProcesoId,
                EsNuevo = false,
                Estado = registroExistente.Estado.ToString(),
                TurnoNombre = turno.Nombre,
                Fecha = registroExistente.Fecha,
                RowVersion = registroExistente.RowVersion
            });
        }

        var plantilla = await ArmarPlantillaAsync(request.LineaId, request.ProductoId, cancellationToken);
        if (plantilla.Count == 0)
            return Result<IniciarRegistroProcesoResponse>.Failure("SIN_PLANTILLA",
                "La línea no tiene puntos de control de proceso cargados. Un administrador los carga desde Maestros > Control de Proceso.");

        // Crear nuevo registro
        var nuevoRegistro = new RegistroProceso
        {
            LineaId = request.LineaId,
            ProductoId = request.ProductoId,
            TurnoId = turno.TurnoId,
            Fecha = fechaHoy,
            UsuarioId = usuarioId,
            FechaHoraInicio = DateTimeOffset.UtcNow,
            Estado = EstadoRegistroProceso.PARCIAL,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };

        await _unitOfWork.RegistrosProceso.AddAsync(nuevoRegistro, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registro de proceso {RegistroId} creado para Línea {LineaId}, Producto {ProductoId}, Turno {TurnoId}",
            nuevoRegistro.RegistroProcesoId, request.LineaId, request.ProductoId, turno.TurnoId);

        return Result<IniciarRegistroProcesoResponse>.Success(new IniciarRegistroProcesoResponse
        {
            RegistroProcesoId = nuevoRegistro.RegistroProcesoId,
            EsNuevo = true,
            Estado = nuevoRegistro.Estado.ToString(),
            TurnoNombre = turno.Nombre,
            Fecha = nuevoRegistro.Fecha,
            RowVersion = nuevoRegistro.RowVersion
        });
    }

    public async Task<Result<RegistroProcesoCompletoDto>> ObtenerCompletoAsync(
        long registroProcesoId,
        CancellationToken cancellationToken = default)
    {
        var registro = await _unitOfWork.RegistrosProceso.GetByIdWithDetailsAsync(registroProcesoId, cancellationToken);
        if (registro == null)
            return Result<RegistroProcesoCompletoDto>.Failure("NOT_FOUND", "Registro de proceso no encontrado.");

        var plantilla = await ArmarPlantillaAsync(registro.LineaId, registro.ProductoId, cancellationToken);

        var filas = plantilla
            .Select(punto => ADetalle(registroProcesoId, punto,
                registro.Detalles.FirstOrDefault(d => d.ParametroId == punto.Parametro.ParametroId), fueraDePlantilla: false))
            .ToList();

        // Datos viejos: un punto que salió de la plantilla pero ya tenía valor se sigue viendo (solo lectura).
        var enPlantilla = plantilla.Select(p => p.Parametro.ParametroId).ToHashSet();
        filas.AddRange(registro.Detalles
            .Where(d => !enPlantilla.Contains(d.ParametroId))
            .Select(d => ADetalle(registroProcesoId,
                new PuntoPlantilla(d.Parametro, new RangoControl(null, null), d.ValorEstandarSnapshot), d, fueraDePlantilla: true)));

        var grupos = filas
            .GroupBy(f => f.Grupo)
            .OrderBy(g => g.Key.Orden)
            .Select(g => new GrupoConDetallesDto
            {
                GrupoParametroId = g.Key.GrupoParametroId,
                Nombre = g.Key.Nombre,
                Orden = g.Key.Orden,
                Detalles = g.Select(f => f.Detalle).ToList()
            }).ToList();

        var dto = new RegistroProcesoCompletoDto
        {
            RegistroProcesoId = registro.RegistroProcesoId,
            LineaId = registro.LineaId,
            LineaNombre = registro.Linea.Nombre,
            ProductoId = registro.ProductoId,
            ProductoNombre = registro.Producto?.Nombre ?? SinProducto,
            TurnoId = registro.TurnoId,
            TurnoNombre = registro.Turno.Nombre,
            Fecha = registro.Fecha,
            UsuarioId = registro.UsuarioId,
            FechaHoraInicio = registro.FechaHoraInicio,
            FechaHoraFin = registro.FechaHoraFin,
            Estado = registro.Estado.ToString(),
            EstadoDescripcion = registro.Estado.ObtenerDescripcion(),
            CantidadParametros = grupos.Sum(g => g.Detalles.Count),
            CantidadCompletados = registro.CantidadCompletados,
            CantidadDesvios = registro.CantidadDesvios,
            RowVersion = registro.RowVersion,
            Grupos = grupos
        };

        return Result<RegistroProcesoCompletoDto>.Success(dto);
    }

    public async Task<Result> GuardarValoresAsync(
        GuardarValoresRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registro = await _unitOfWork.RegistrosProceso.GetByIdWithDetailsAsync(request.RegistroProcesoId, cancellationToken);
        if (registro == null)
            return Result.Failure("NOT_FOUND", "Registro de proceso no encontrado.");

        if (!registro.PuedeSerEditado)
            return Result.Failure("CERRADO", "No se puede editar un registro cerrado.");

        // Verificar concurrencia
        if (!registro.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "El registro fue modificado por otro usuario.");

        var plantilla = (await ArmarPlantillaAsync(registro.LineaId, registro.ProductoId, cancellationToken))
            .ToDictionary(p => p.Parametro.ParametroId);

        // Primero se valida todo; si un punto no valida no se guarda ninguno (el maquinista corrige y reintenta).
        var errores = new List<string>();
        var aGuardar = new List<(ValorParametroRequest Valor, PuntoPlantilla Punto, ResultadoPuntoControl Resultado)>();
        foreach (var valor in request.Valores)
        {
            // Un punto fuera de la plantilla (desactivado) no se carga más.
            if (!plantilla.TryGetValue(valor.ParametroId, out var punto))
                continue;

            var resultado = PuntoControlValidador.ValidarCarga(
                punto.Parametro.TipoDato, punto.Rango, valor.ValorReal, valor.ValorRealTexto, valor.Observacion);

            if (!resultado.EsValido)
                errores.Add($"{punto.Parametro.Nombre}: {resultado.Error}");
            else if (!resultado.SinValor)
                aGuardar.Add((valor, punto, resultado));
        }

        if (errores.Count > 0)
            return Result.Failure("VALIDATION", string.Join(" | ", errores));

        foreach (var (valor, punto, resultado) in aGuardar)
        {
            var detalle = registro.Detalles.FirstOrDefault(d => d.ParametroId == valor.ParametroId);
            if (detalle == null)
            {
                detalle = new RegistroProcesoDetalle
                {
                    RegistroProcesoId = registro.RegistroProcesoId,
                    ParametroId = valor.ParametroId,
                    ValorEstandarSnapshot = punto.EstandarTexto // SNAPSHOT
                };
                registro.Detalles.Add(detalle);
            }

            detalle.ValorReal = resultado.ValorReal;
            detalle.ValorRealTexto = resultado.ValorRealTexto;
            detalle.FueraDeEstandar = resultado.FueraDeEstandar;
            detalle.Observacion = string.IsNullOrWhiteSpace(valor.Observacion) ? null : valor.Observacion.Trim();
            detalle.FechaHoraRegistro = DateTimeOffset.UtcNow;
        }

        registro.ModificadoPorUsuarioId = usuarioId;
        registro.FechaHoraModificacion = DateTimeOffset.UtcNow;

        // Actualizar estado si todos los parámetros están completos
        var totalParametros = plantilla.Count;
        var completados = registro.Detalles.Count(d => d.ValorReal != null || !string.IsNullOrEmpty(d.ValorRealTexto));
        if (completados >= totalParametros && registro.Estado == EstadoRegistroProceso.PARCIAL)
        {
            registro.Estado = EstadoRegistroProceso.COMPLETO;
            registro.FechaHoraFin = DateTimeOffset.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Valores guardados en registro {RegistroId} por usuario {UsuarioId}",
            request.RegistroProcesoId, usuarioId);

        return Result.Success();
    }

    public async Task<Result> CerrarRegistroAsync(
        CerrarRegistroProcesoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registro = await _unitOfWork.RegistrosProceso.GetByIdAsync(request.RegistroProcesoId, cancellationToken);
        if (registro == null)
            return Result.Failure("NOT_FOUND", "Registro de proceso no encontrado.");

        if (registro.Estado == EstadoRegistroProceso.CERRADO)
            return Result.Failure("YA_CERRADO", "El registro ya está cerrado.");

        if (!registro.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "El registro fue modificado por otro usuario.");

        registro.Estado = EstadoRegistroProceso.CERRADO;
        registro.FechaHoraFin = DateTimeOffset.UtcNow;
        registro.ModificadoPorUsuarioId = usuarioId;
        registro.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registro {RegistroId} cerrado por usuario {UsuarioId}",
            request.RegistroProcesoId, usuarioId);

        return Result.Success();
    }

    public async Task<Result<PagedResult<RegistroProcesoDto>>> ObtenerPaginadoAsync(
        RegistroProcesoFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        EstadoRegistroProceso? estadoEnum = null;
        if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoRegistroProceso>(filtro.Estado, true, out var e))
            estadoEnum = e;

        var (items, totalCount) = await _unitOfWork.RegistrosProceso.GetPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            filtro.LineaId,
            filtro.ProductoId,
            filtro.TurnoId,
            estadoEnum,
            filtro.FechaDesde,
            filtro.FechaHasta,
            filtro.OrdenarPor,
            filtro.OrdenDescendente,
            cancellationToken);

        var dtos = items.Select(r => new RegistroProcesoDto
        {
            RegistroProcesoId = r.RegistroProcesoId,
            LineaId = r.LineaId,
            LineaNombre = r.Linea.Nombre,
            ProductoId = r.ProductoId,
            ProductoNombre = r.Producto?.Nombre ?? SinProducto,
            TurnoId = r.TurnoId,
            TurnoNombre = r.Turno.Nombre,
            Fecha = r.Fecha,
            UsuarioId = r.UsuarioId,
            FechaHoraInicio = r.FechaHoraInicio,
            FechaHoraFin = r.FechaHoraFin,
            Estado = r.Estado.ToString(),
            EstadoDescripcion = r.Estado.ObtenerDescripcion(),
            CantidadCompletados = r.CantidadCompletados,
            CantidadDesvios = r.CantidadDesvios,
            RowVersion = r.RowVersion
        }).ToList();

        return Result<PagedResult<RegistroProcesoDto>>.Success(new PagedResult<RegistroProcesoDto>
        {
            Items = dtos,
            Page = filtro.Page,
            PageSize = filtro.PageSize,
            TotalCount = totalCount
        });
    }

    public async Task<Result<IReadOnlyList<GrupoParametroDto>>> ObtenerGruposConEstandaresAsync(
        int lineaId,
        int? productoId,
        CancellationToken cancellationToken = default)
    {
        var plantilla = await ArmarPlantillaAsync(lineaId, productoId, cancellationToken);

        var grupos = plantilla
            .GroupBy(p => p.Parametro.GrupoParametro)
            .OrderBy(g => g.Key.Orden)
            .Select(g => new GrupoParametroDto
            {
                GrupoParametroId = g.Key.GrupoParametroId,
                LineaId = g.Key.LineaId,
                Codigo = g.Key.Codigo,
                Nombre = g.Key.Nombre,
                Descripcion = g.Key.Descripcion,
                Orden = g.Key.Orden,
                Activo = g.Key.Activo,
                Parametros = g.Select(p =>
                {
                    var dto = ControlProcesoMapeo.APunto(p.Parametro);
                    dto.ValorMin = p.Rango.Min;
                    dto.ValorMax = p.Rango.Max;
                    return dto;
                }).ToList()
            }).ToList();

        return Result<IReadOnlyList<GrupoParametroDto>>.Success(grupos);
    }

    private const string SinProducto = "Sin producto";

    /// <summary>
    /// Punto a cargar con el rango contra el que se valida y el texto que se guarda como snapshot.
    /// </summary>
    private sealed record PuntoPlantilla(Parametro Parametro, RangoControl Rango, string? EstandarTexto);

    /// <summary>
    /// Plantilla de Control de Proceso (T-545) = puntos activos de los grupos de la línea
    /// + puntos con estándar para línea + producto (Eje 2 original). Si un punto de la línea
    /// tiene estándar para el producto, manda el estándar del producto.
    /// </summary>
    private async Task<List<PuntoPlantilla>> ArmarPlantillaAsync(int lineaId, int? productoId, CancellationToken cancellationToken)
    {
        var estandares = productoId.HasValue
            ? (await _unitOfWork.EstandaresParametro.GetByLineaProductoAsync(lineaId, productoId.Value, cancellationToken))
                .ToDictionary(e => e.ParametroId)
            : new Dictionary<int, EstandarParametro>();

        var puntos = new Dictionary<int, PuntoPlantilla>();
        foreach (var parametro in await _unitOfWork.Parametros.GetPlantillaLineaAsync(lineaId, cancellationToken))
        {
            puntos[parametro.ParametroId] = estandares.TryGetValue(parametro.ParametroId, out var estandar)
                ? DesdeEstandar(estandar)
                : DesdePlantilla(parametro);
        }

        foreach (var estandar in estandares.Values.Where(e => !puntos.ContainsKey(e.ParametroId)))
            puntos[estandar.ParametroId] = DesdeEstandar(estandar);

        return puntos.Values
            .OrderBy(p => p.Parametro.GrupoParametro.Orden)
            .ThenBy(p => p.Parametro.GrupoParametroId)
            .ThenBy(p => p.Parametro.Seccion)
            .ThenBy(p => p.Parametro.Orden)
            .ThenBy(p => p.Parametro.Nombre)
            .ToList();
    }

    private static PuntoPlantilla DesdePlantilla(Parametro p)
    {
        var rango = new RangoControl(p.ValorMin, p.ValorMax);
        return new PuntoPlantilla(p, rango, PuntoControlValidador.DescribirEstandar(p.TipoDato, rango, p.UnidadMedida));
    }

    private static PuntoPlantilla DesdeEstandar(EstandarParametro e)
    {
        var rango = RangoControl.DesdeEstandar(e);
        return new PuntoPlantilla(e.Parametro, rango,
            e.ValorEstandarTexto ?? PuntoControlValidador.DescribirEstandar(e.Parametro.TipoDato, rango, e.Parametro.UnidadMedida));
    }

    private static (GrupoParametro Grupo, RegistroProcesoDetalleDto Detalle) ADetalle(
        long registroProcesoId, PuntoPlantilla punto, RegistroProcesoDetalle? detalle, bool fueraDePlantilla)
    {
        var p = punto.Parametro;
        return (p.GrupoParametro, new RegistroProcesoDetalleDto
        {
            RegistroProcesoDetalleId = detalle?.RegistroProcesoDetalleId ?? 0,
            RegistroProcesoId = registroProcesoId,
            ParametroId = p.ParametroId,
            ParametroCodigo = p.Codigo,
            ParametroNombre = p.Nombre,
            Seccion = p.Seccion,
            UnidadMedida = p.UnidadMedida,
            TipoDato = p.TipoDato.ToString(),
            TipoDatoDescripcion = p.TipoDato.ObtenerDescripcion(),
            Frecuencia = p.Frecuencia.ToString(),
            FrecuenciaDescripcion = p.Frecuencia.ObtenerDescripcion(),
            ValorMin = punto.Rango.Min,
            ValorMax = punto.Rango.Max,
            Orden = p.Orden,
            FueraDePlantilla = fueraDePlantilla,
            // Con valor cargado se muestra el snapshot: contra qué se comparó ESE día.
            ValorEstandarSnapshot = detalle != null ? detalle.ValorEstandarSnapshot : punto.EstandarTexto,
            ValorReal = detalle?.ValorReal,
            ValorRealTexto = detalle?.ValorRealTexto,
            FueraDeEstandar = detalle?.FueraDeEstandar ?? false,
            Observacion = detalle?.Observacion,
            FechaHoraRegistro = detalle?.FechaHoraRegistro ?? DateTimeOffset.MinValue
        });
    }
}
