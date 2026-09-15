using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class InspeccionService : IInspeccionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InspeccionService> _logger;

    public InspeccionService(IUnitOfWork unitOfWork, ILogger<InspeccionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<PlanInspeccionDto>>> ObtenerPlanesAsync(
        int lineaId,
        string puesto,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TipoPuesto>(puesto, true, out var tipoPuesto))
            return Result<IReadOnlyList<PlanInspeccionDto>>.Failure("INVALID_PUESTO", "Tipo de puesto no válido.");

        var planes = await _unitOfWork.PlanesInspeccion.GetByLineaYPuestoAsync(lineaId, tipoPuesto, cancellationToken);

        var dtos = planes.Select(p => MapToPlanDto(p)).ToList();

        return Result<IReadOnlyList<PlanInspeccionDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<PlanInspeccionConItemsDto>>> ObtenerPendientesHoyAsync(
        int lineaId,
        string puesto,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TipoPuesto>(puesto, true, out var tipoPuesto))
            return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Failure("INVALID_PUESTO", "Tipo de puesto no válido.");

        var planes = await _unitOfWork.PlanesInspeccion.GetPendientesHoyAsync(lineaId, tipoPuesto, cancellationToken);

        var dtos = planes.Select(p => MapToPlanConItemsDto(p, pendienteHoy: true)).ToList();

        return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Success(dtos);
    }

    public async Task<Result<PlanInspeccionConItemsDto>> ObtenerPlanConItemsAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdWithItemsAsync(planInspeccionId, cancellationToken);
        if (plan == null)
            return Result<PlanInspeccionConItemsDto>.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        var dto = MapToPlanConItemsDto(plan);
        return Result<PlanInspeccionConItemsDto>.Success(dto);
    }

    public async Task<Result<IniciarEjecucionResponse>> IniciarEjecucionAsync(
        IniciarEjecucionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdWithItemsAsync(request.PlanInspeccionId, cancellationToken);
        if (plan == null)
            return Result<IniciarEjecucionResponse>.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        var turno = await _unitOfWork.Turnos.GetTurnoActualAsync(cancellationToken);
        if (turno == null)
            return Result<IniciarEjecucionResponse>.Failure("NO_TURNO", "No hay turno configurado para la hora actual.");

        var ejecucion = new EjecucionInspeccion
        {
            PlanInspeccionId = plan.PlanInspeccionId,
            TurnoId = turno.TurnoId,
            UsuarioId = usuarioId,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            FechaHoraInicio = DateTimeOffset.UtcNow,
            Completada = false,
            CantidadOk = 0,
            CantidadNoOk = 0
        };

        await _unitOfWork.EjecucionesInspeccion.AddAsync(ejecucion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ejecución {EjecucionId} iniciada para Plan {PlanId} por usuario {UsuarioId}",
            ejecucion.EjecucionInspeccionId, plan.PlanInspeccionId, usuarioId);

        return Result<IniciarEjecucionResponse>.Success(new IniciarEjecucionResponse
        {
            EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
            PlanNombre = plan.Nombre,
            TurnoNombre = turno.Nombre,
            Fecha = ejecucion.Fecha,
            CantidadItems = plan.Items.Count,
            RowVersion = ejecucion.RowVersion
        });
    }

    public async Task<Result<EjecucionInspeccionCompletoDto>> ObtenerEjecucionAsync(
        long ejecucionInspeccionId,
        CancellationToken cancellationToken = default)
    {
        var ejecucion = await _unitOfWork.EjecucionesInspeccion.GetByIdWithDetallesAsync(ejecucionInspeccionId, cancellationToken);
        if (ejecucion == null)
            return Result<EjecucionInspeccionCompletoDto>.Failure("NOT_FOUND", "Ejecución de inspección no encontrada.");

        var dto = MapToEjecucionCompletoDto(ejecucion);
        return Result<EjecucionInspeccionCompletoDto>.Success(dto);
    }

    public async Task<Result> GuardarResultadosAsync(
        GuardarResultadosInspeccionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var ejecucion = await _unitOfWork.EjecucionesInspeccion.GetByIdWithDetallesAsync(request.EjecucionInspeccionId, cancellationToken);
        if (ejecucion == null)
            return Result.Failure("NOT_FOUND", "Ejecución de inspección no encontrada.");

        if (ejecucion.Completada)
            return Result.Failure("YA_COMPLETADA", "La ejecución ya está completada y no puede modificarse.");

        if (!ejecucion.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "La ejecución fue modificada por otro usuario.");

        // Validar y procesar resultados
        foreach (var resultado in request.Resultados)
        {
            if (!Enum.TryParse<ResultadoInspeccion>(resultado.Resultado, true, out var tipoResultado))
                return Result.Failure("INVALID_RESULTADO", $"Resultado '{resultado.Resultado}' no válido.");

            // R16: Si es NO_OK, la observación es obligatoria
            if (tipoResultado == ResultadoInspeccion.NO_OK && string.IsNullOrWhiteSpace(resultado.Observacion))
            {
                var item = ejecucion.PlanInspeccion.Items.FirstOrDefault(i => i.ItemPlanInspeccionId == resultado.ItemPlanInspeccionId);
                return Result.Failure("OBSERVACION_REQUERIDA",
                    $"El ítem '{item?.Descripcion ?? resultado.ItemPlanInspeccionId.ToString()}' tiene resultado NO OK. La observación es obligatoria.");
            }

            // Buscar o crear detalle
            var detalle = ejecucion.Detalles.FirstOrDefault(d => d.ItemPlanInspeccionId == resultado.ItemPlanInspeccionId);
            if (detalle == null)
            {
                detalle = new EjecucionInspeccionDetalle
                {
                    EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
                    ItemPlanInspeccionId = resultado.ItemPlanInspeccionId
                };
                ejecucion.Detalles.Add(detalle);
            }

            detalle.Resultado = tipoResultado;
            detalle.Observacion = resultado.Observacion;
            detalle.FechaHoraRegistro = DateTimeOffset.UtcNow;

            // Crear hallazgo NO OK si corresponde
            if (tipoResultado == ResultadoInspeccion.NO_OK)
            {
                var hallazgo = new HallazgoNoOk
                {
                    EjecucionInspeccionDetalleId = detalle.EjecucionInspeccionDetalleId,
                    LineaId = ejecucion.PlanInspeccion.LineaId,
                    EquipoId = ejecucion.PlanInspeccion.EquipoId,
                    UsuarioReporteId = usuarioId,
                    FechaHoraHallazgo = DateTimeOffset.UtcNow,
                    Descripcion = resultado.Observacion!,
                    Resuelto = false
                };
                await _unitOfWork.HallazgosNoOk.AddAsync(hallazgo, cancellationToken);
            }
        }

        // Actualizar contadores
        ejecucion.CantidadOk = ejecucion.Detalles.Count(d => d.Resultado == ResultadoInspeccion.OK);
        ejecucion.CantidadNoOk = ejecucion.Detalles.Count(d => d.Resultado == ResultadoInspeccion.NO_OK);
        ejecucion.ObservacionesGenerales = request.ObservacionesGenerales;

        // Completar si se solicita y todos los ítems están respondidos
        if (request.Completar)
        {
            var totalItems = ejecucion.PlanInspeccion.Items.Count;
            if (ejecucion.Detalles.Count >= totalItems)
            {
                ejecucion.Completada = true;
                ejecucion.FechaHoraFin = DateTimeOffset.UtcNow;
            }
            else
            {
                return Result.Failure("INCOMPLETA",
                    $"No se puede completar. Faltan {totalItems - ejecucion.Detalles.Count} ítems por responder.");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resultados guardados en ejecución {EjecucionId}. OK: {Ok}, NO_OK: {NoOk}, Completada: {Completada}",
            ejecucion.EjecucionInspeccionId, ejecucion.CantidadOk, ejecucion.CantidadNoOk, ejecucion.Completada);

        return Result.Success();
    }

    public async Task<Result<PagedResult<EjecucionInspeccionDto>>> ObtenerEjecucionesPaginadoAsync(
        EjecucionInspeccionFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _unitOfWork.EjecucionesInspeccion.GetPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            filtro.LineaId,
            filtro.PlanInspeccionId,
            filtro.FechaDesde,
            filtro.FechaHasta,
            filtro.SoloConNoOk,
            cancellationToken);

        var dtos = items.Select(e => MapToEjecucionDto(e)).ToList();

        return Result<PagedResult<EjecucionInspeccionDto>>.Success(new PagedResult<EjecucionInspeccionDto>
        {
            Items = dtos,
            Page = filtro.Page,
            PageSize = filtro.PageSize,
            TotalCount = totalCount
        });
    }

    public async Task<Result<IReadOnlyList<HallazgoNoOkDto>>> ObtenerHallazgosPendientesAsync(
        int? lineaId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HallazgoNoOk> hallazgos;

        if (lineaId.HasValue)
            hallazgos = await _unitOfWork.HallazgosNoOk.GetPendientesByLineaAsync(lineaId.Value, cancellationToken);
        else
            hallazgos = await _unitOfWork.HallazgosNoOk.GetAllPendientesAsync(cancellationToken);

        var dtos = hallazgos.Select(h => MapToHallazgoDto(h)).ToList();

        return Result<IReadOnlyList<HallazgoNoOkDto>>.Success(dtos);
    }

    public async Task<Result<PagedResult<HallazgoNoOkDto>>> ObtenerHallazgosPaginadoAsync(
        HallazgoNoOkFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _unitOfWork.HallazgosNoOk.GetPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            filtro.LineaId,
            filtro.EquipoId,
            filtro.SoloPendientes,
            filtro.FechaDesde,
            filtro.FechaHasta,
            cancellationToken);

        var dtos = items.Select(h => MapToHallazgoDto(h)).ToList();

        return Result<PagedResult<HallazgoNoOkDto>>.Success(new PagedResult<HallazgoNoOkDto>
        {
            Items = dtos,
            Page = filtro.Page,
            PageSize = filtro.PageSize,
            TotalCount = totalCount
        });
    }

    public async Task<Result> ResolverHallazgoAsync(
        ResolverHallazgoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var hallazgo = await _unitOfWork.HallazgosNoOk.GetByIdAsync(request.HallazgoNoOkId, cancellationToken);
        if (hallazgo == null)
            return Result.Failure("NOT_FOUND", "Hallazgo no encontrado.");

        if (hallazgo.Resuelto)
            return Result.Failure("YA_RESUELTO", "El hallazgo ya está resuelto.");

        // R17: Marcar como resuelto, no borrar
        hallazgo.Resuelto = true;
        hallazgo.UsuarioResolucionId = usuarioId;
        hallazgo.FechaHoraResolucion = DateTimeOffset.UtcNow;
        hallazgo.ObservacionResolucion = request.ObservacionResolucion;
        hallazgo.RequiereAccionCorrectiva = request.RequiereAccionCorrectiva;
        hallazgo.AccionCorrectivaDescripcion = request.AccionCorrectivaDescripcion;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hallazgo {HallazgoId} resuelto por usuario {UsuarioId}",
            hallazgo.HallazgoNoOkId, usuarioId);

        return Result.Success();
    }

    #region Mappers

    private PlanInspeccionDto MapToPlanDto(PlanInspeccion plan, bool pendienteHoy = false)
    {
        return new PlanInspeccionDto
        {
            PlanInspeccionId = plan.PlanInspeccionId,
            LineaId = plan.LineaId,
            LineaNombre = plan.Linea?.Nombre ?? string.Empty,
            EquipoId = plan.EquipoId,
            EquipoNombre = plan.Equipo?.Nombre,
            PuestoResponsable = plan.PuestoResponsable.ToString(),
            PuestoResponsableDescripcion = plan.PuestoResponsable.ObtenerDescripcion(),
            Codigo = plan.Codigo,
            Nombre = plan.Nombre,
            Descripcion = plan.Descripcion,
            FrecuenciaDias = plan.FrecuenciaDias,
            Orden = plan.Orden,
            Activo = plan.Activo,
            CantidadItems = plan.Items.Count,
            PendienteHoy = pendienteHoy
        };
    }

    private PlanInspeccionConItemsDto MapToPlanConItemsDto(PlanInspeccion plan, bool pendienteHoy = false)
    {
        var dto = new PlanInspeccionConItemsDto
        {
            PlanInspeccionId = plan.PlanInspeccionId,
            LineaId = plan.LineaId,
            LineaNombre = plan.Linea?.Nombre ?? string.Empty,
            EquipoId = plan.EquipoId,
            EquipoNombre = plan.Equipo?.Nombre,
            PuestoResponsable = plan.PuestoResponsable.ToString(),
            PuestoResponsableDescripcion = plan.PuestoResponsable.ObtenerDescripcion(),
            Codigo = plan.Codigo,
            Nombre = plan.Nombre,
            Descripcion = plan.Descripcion,
            FrecuenciaDias = plan.FrecuenciaDias,
            Orden = plan.Orden,
            Activo = plan.Activo,
            CantidadItems = plan.Items.Count,
            PendienteHoy = pendienteHoy,
            Items = plan.Items.Select(i => new ItemPlanInspeccionDto
            {
                ItemPlanInspeccionId = i.ItemPlanInspeccionId,
                PlanInspeccionId = i.PlanInspeccionId,
                NumeroItem = i.NumeroItem,
                Descripcion = i.Descripcion,
                CondicionEstandar = i.CondicionEstandar,
                Metodo = i.Metodo.ToString(),
                MetodoDescripcion = i.Metodo.ObtenerDescripcion(),
                Orden = i.Orden,
                Activo = i.Activo
            }).ToList()
        };

        return dto;
    }

    private EjecucionInspeccionDto MapToEjecucionDto(EjecucionInspeccion ejecucion)
    {
        return new EjecucionInspeccionDto
        {
            EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
            PlanInspeccionId = ejecucion.PlanInspeccionId,
            PlanNombre = ejecucion.PlanInspeccion?.Nombre ?? string.Empty,
            PlanCodigo = ejecucion.PlanInspeccion?.Codigo ?? string.Empty,
            LineaId = ejecucion.PlanInspeccion?.LineaId ?? 0,
            LineaNombre = ejecucion.PlanInspeccion?.Linea?.Nombre ?? string.Empty,
            EquipoId = ejecucion.PlanInspeccion?.EquipoId,
            EquipoNombre = ejecucion.PlanInspeccion?.Equipo?.Nombre,
            TurnoId = ejecucion.TurnoId,
            TurnoNombre = ejecucion.Turno?.Nombre ?? string.Empty,
            UsuarioId = ejecucion.UsuarioId,
            Fecha = ejecucion.Fecha,
            FechaHoraInicio = ejecucion.FechaHoraInicio,
            FechaHoraFin = ejecucion.FechaHoraFin,
            Completada = ejecucion.Completada,
            CantidadOk = ejecucion.CantidadOk,
            CantidadNoOk = ejecucion.CantidadNoOk,
            CantidadTotal = ejecucion.CantidadOk + ejecucion.CantidadNoOk,
            ObservacionesGenerales = ejecucion.ObservacionesGenerales,
            RowVersion = ejecucion.RowVersion
        };
    }

    private EjecucionInspeccionCompletoDto MapToEjecucionCompletoDto(EjecucionInspeccion ejecucion)
    {
        var dto = new EjecucionInspeccionCompletoDto
        {
            EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
            PlanInspeccionId = ejecucion.PlanInspeccionId,
            PlanNombre = ejecucion.PlanInspeccion?.Nombre ?? string.Empty,
            PlanCodigo = ejecucion.PlanInspeccion?.Codigo ?? string.Empty,
            LineaId = ejecucion.PlanInspeccion?.LineaId ?? 0,
            LineaNombre = ejecucion.PlanInspeccion?.Linea?.Nombre ?? string.Empty,
            EquipoId = ejecucion.PlanInspeccion?.EquipoId,
            EquipoNombre = ejecucion.PlanInspeccion?.Equipo?.Nombre,
            TurnoId = ejecucion.TurnoId,
            TurnoNombre = ejecucion.Turno?.Nombre ?? string.Empty,
            UsuarioId = ejecucion.UsuarioId,
            Fecha = ejecucion.Fecha,
            FechaHoraInicio = ejecucion.FechaHoraInicio,
            FechaHoraFin = ejecucion.FechaHoraFin,
            Completada = ejecucion.Completada,
            CantidadOk = ejecucion.CantidadOk,
            CantidadNoOk = ejecucion.CantidadNoOk,
            CantidadTotal = ejecucion.PlanInspeccion?.Items.Count ?? 0,
            ObservacionesGenerales = ejecucion.ObservacionesGenerales,
            RowVersion = ejecucion.RowVersion
        };

        // Mapear todos los ítems del plan con sus resultados (si existen)
        if (ejecucion.PlanInspeccion?.Items != null)
        {
            dto.Detalles = ejecucion.PlanInspeccion.Items
                .OrderBy(i => i.Orden)
                .Select(item =>
                {
                    var detalle = ejecucion.Detalles.FirstOrDefault(d => d.ItemPlanInspeccionId == item.ItemPlanInspeccionId);
                    return new EjecucionInspeccionDetalleDto
                    {
                        EjecucionInspeccionDetalleId = detalle?.EjecucionInspeccionDetalleId ?? 0,
                        EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
                        ItemPlanInspeccionId = item.ItemPlanInspeccionId,
                        NumeroItem = item.NumeroItem,
                        ItemDescripcion = item.Descripcion,
                        CondicionEstandar = item.CondicionEstandar,
                        Metodo = item.Metodo.ObtenerDescripcion(),
                        Resultado = detalle?.Resultado.ToString(),
                        ResultadoDescripcion = detalle?.Resultado.ObtenerDescripcion(),
                        Observacion = detalle?.Observacion,
                        FechaHoraRegistro = detalle?.FechaHoraRegistro
                    };
                }).ToList();
        }

        return dto;
    }

    private HallazgoNoOkDto MapToHallazgoDto(HallazgoNoOk hallazgo)
    {
        return new HallazgoNoOkDto
        {
            HallazgoNoOkId = hallazgo.HallazgoNoOkId,
            EjecucionInspeccionDetalleId = hallazgo.EjecucionInspeccionDetalleId,
            LineaId = hallazgo.LineaId,
            LineaNombre = hallazgo.Linea?.Nombre ?? string.Empty,
            EquipoId = hallazgo.EquipoId,
            EquipoNombre = hallazgo.Equipo?.Nombre,
            UsuarioReporteId = hallazgo.UsuarioReporteId,
            FechaHoraHallazgo = hallazgo.FechaHoraHallazgo,
            Descripcion = hallazgo.Descripcion,
            NivelCriticidad = hallazgo.NivelCriticidad,
            Resuelto = hallazgo.Resuelto,
            UsuarioResolucionId = hallazgo.UsuarioResolucionId,
            FechaHoraResolucion = hallazgo.FechaHoraResolucion,
            ObservacionResolucion = hallazgo.ObservacionResolucion,
            RequiereAccionCorrectiva = hallazgo.RequiereAccionCorrectiva,
            AccionCorrectivaDescripcion = hallazgo.AccionCorrectivaDescripcion,
            ItemDescripcion = hallazgo.EjecucionInspeccionDetalle?.ItemPlanInspeccion?.Descripcion ?? string.Empty,
            PlanNombre = hallazgo.EjecucionInspeccionDetalle?.EjecucionInspeccion?.PlanInspeccion?.Nombre ?? string.Empty
        };
    }

    #endregion
}
