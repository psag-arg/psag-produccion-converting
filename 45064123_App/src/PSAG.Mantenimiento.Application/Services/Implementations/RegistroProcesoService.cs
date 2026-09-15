using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;

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

        var producto = await _unitOfWork.Productos.GetByIdAsync(request.ProductoId, cancellationToken);
        if (producto == null)
            return Result<IniciarRegistroProcesoResponse>.Failure("NOT_FOUND", "Producto no encontrado.");

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

        // Obtener grupos aplicables con estándares
        var estandares = await _unitOfWork.EstandaresParametro.GetByLineaProductoAsync(
            registro.LineaId, registro.ProductoId, cancellationToken);

        var grupos = estandares
            .GroupBy(e => e.Parametro.GrupoParametro)
            .OrderBy(g => g.Key.Orden)
            .Select(g => new GrupoConDetallesDto
            {
                GrupoParametroId = g.Key.GrupoParametroId,
                Nombre = g.Key.Nombre,
                Orden = g.Key.Orden,
                Detalles = g.OrderBy(e => e.Parametro.Seccion).ThenBy(e => e.Parametro.Orden)
                    .Select(e =>
                    {
                        var detalle = registro.Detalles.FirstOrDefault(d => d.ParametroId == e.ParametroId);
                        return new RegistroProcesoDetalleDto
                        {
                            RegistroProcesoDetalleId = detalle?.RegistroProcesoDetalleId ?? 0,
                            RegistroProcesoId = registroProcesoId,
                            ParametroId = e.ParametroId,
                            ParametroCodigo = e.Parametro.Codigo,
                            ParametroNombre = e.Parametro.Nombre,
                            Seccion = e.Parametro.Seccion,
                            UnidadMedida = e.Parametro.UnidadMedida,
                            TipoDato = e.Parametro.TipoDato.ToString(),
                            ValorEstandarSnapshot = detalle?.ValorEstandarSnapshot ?? e.ValorEstandarTexto,
                            ValorReal = detalle?.ValorReal,
                            ValorRealTexto = detalle?.ValorRealTexto,
                            FueraDeEstandar = detalle?.FueraDeEstandar ?? false,
                            Observacion = detalle?.Observacion,
                            FechaHoraRegistro = detalle?.FechaHoraRegistro ?? DateTimeOffset.MinValue
                        };
                    }).ToList()
            }).ToList();

        var dto = new RegistroProcesoCompletoDto
        {
            RegistroProcesoId = registro.RegistroProcesoId,
            LineaId = registro.LineaId,
            LineaNombre = registro.Linea.Nombre,
            ProductoId = registro.ProductoId,
            ProductoNombre = registro.Producto.Nombre,
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

        // Obtener estándares para calcular desvíos
        var estandares = await _unitOfWork.EstandaresParametro.GetByLineaProductoAsync(
            registro.LineaId, registro.ProductoId, cancellationToken);

        foreach (var valor in request.Valores)
        {
            var estandar = estandares.FirstOrDefault(e => e.ParametroId == valor.ParametroId);
            if (estandar == null)
                continue;

            // Calcular si está fuera de estándar
            bool fueraDeEstandar = false;
            if (valor.ValorReal.HasValue)
            {
                fueraDeEstandar = !estandar.EstaEnEstandar(valor.ValorReal);
            }

            // Si está fuera de estándar, la observación es obligatoria
            if (fueraDeEstandar && string.IsNullOrWhiteSpace(valor.Observacion))
            {
                return Result.Failure("VALIDATION",
                    $"El parámetro '{estandar.Parametro.Nombre}' está fuera de estándar. La observación es obligatoria.");
            }

            // Buscar detalle existente o crear nuevo
            var detalle = registro.Detalles.FirstOrDefault(d => d.ParametroId == valor.ParametroId);
            if (detalle == null)
            {
                detalle = new RegistroProcesoDetalle
                {
                    RegistroProcesoId = registro.RegistroProcesoId,
                    ParametroId = valor.ParametroId,
                    ValorEstandarSnapshot = estandar.ValorEstandarTexto // SNAPSHOT
                };
                registro.Detalles.Add(detalle);
            }

            detalle.ValorReal = valor.ValorReal;
            detalle.ValorRealTexto = valor.ValorRealTexto;
            detalle.FueraDeEstandar = fueraDeEstandar;
            detalle.Observacion = valor.Observacion;
            detalle.FechaHoraRegistro = DateTimeOffset.UtcNow;
        }

        registro.ModificadoPorUsuarioId = usuarioId;
        registro.FechaHoraModificacion = DateTimeOffset.UtcNow;

        // Actualizar estado si todos los parámetros están completos
        var totalParametros = estandares.Count();
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
            ProductoNombre = r.Producto.Nombre,
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
        int productoId,
        CancellationToken cancellationToken = default)
    {
        var estandares = await _unitOfWork.EstandaresParametro.GetByLineaProductoAsync(
            lineaId, productoId, cancellationToken);

        var grupos = estandares
            .GroupBy(e => e.Parametro.GrupoParametro)
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
                Parametros = g.OrderBy(e => e.Parametro.Seccion).ThenBy(e => e.Parametro.Orden)
                    .Select(e => new ParametroDto
                    {
                        ParametroId = e.ParametroId,
                        GrupoParametroId = e.Parametro.GrupoParametroId,
                        Seccion = e.Parametro.Seccion,
                        Codigo = e.Parametro.Codigo,
                        Nombre = e.Parametro.Nombre,
                        UnidadMedida = e.Parametro.UnidadMedida,
                        TipoDato = e.Parametro.TipoDato.ToString(),
                        Orden = e.Parametro.Orden,
                        Activo = e.Parametro.Activo
                    }).ToList()
            }).ToList();

        return Result<IReadOnlyList<GrupoParametroDto>>.Success(grupos);
    }
}
