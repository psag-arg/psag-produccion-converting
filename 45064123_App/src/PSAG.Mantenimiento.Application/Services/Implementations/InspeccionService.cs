using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Inspecciones (Eje 3). Funciona igual que Control de Proceso (reunión 03/11/2026 [78:17]):
/// se elige línea + plan (equipo) + quién realiza, se retoma la del día si ya existe, cada punto se responde
/// OK / NO OK con autoguardado (NO OK pide observación y abre un hallazgo), y al final se cierra (firma).
/// </summary>
public class InspeccionService : IInspeccionService
{
    public const string EnCurso = "EN_CURSO";
    public const string Cerrada = "CERRADA";
    private const string CorregidoEnLaInspeccion = "Se cambió a OK en la misma inspección.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InspeccionService> _logger;

    public InspeccionService(IUnitOfWork unitOfWork, ILogger<InspeccionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    public async Task<Result<IReadOnlyList<PlanInspeccionDto>>> ObtenerPlanesAsync(
        int lineaId,
        string puesto,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TipoPuesto>(puesto, true, out var tipoPuesto))
            return Result<IReadOnlyList<PlanInspeccionDto>>.Failure("INVALID_PUESTO", "Tipo de puesto no válido.");

        var planes = await _unitOfWork.PlanesInspeccion.GetByLineaYPuestoAsync(lineaId, tipoPuesto, cancellationToken);

        var dtos = planes.Select(p => InspeccionMapeo.APlan(p)).ToList();

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

        var dtos = planes.Select(p => InspeccionMapeo.APlanConItems(p, p.Items, pendienteHoy: true)).ToList();

        return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Success(dtos);
    }

    public async Task<Result<PlanInspeccionConItemsDto>> ObtenerPlanConItemsAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdWithItemsAsync(planInspeccionId, cancellationToken);
        if (plan == null)
            return Result<PlanInspeccionConItemsDto>.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        return Result<PlanInspeccionConItemsDto>.Success(InspeccionMapeo.APlanConItems(plan, plan.Items));
    }

    public async Task<Result<IReadOnlyList<PlanInspeccionConItemsDto>>> ObtenerPlanesLineaAsync(
        int lineaId,
        CancellationToken cancellationToken = default)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId, cancellationToken);
        if (linea == null)
            return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Failure("NOT_FOUND", "Línea no encontrada.");

        var planes = await _unitOfWork.PlanesInspeccion.GetPlantillaLineaAsync(lineaId, incluirInactivos: false, cancellationToken);
        var hoy = Hoy;
        var dtos = new List<PlanInspeccionConItemsDto>();
        foreach (var plan in planes.Where(p => p.Items.Any()))
        {
            var ultimas = await _unitOfWork.EjecucionesInspeccion.GetUltimasRespuestasPorItemAsync(plan.PlanInspeccionId, hoy, cancellationToken);
            var dto = InspeccionMapeo.APlanConItems(plan, plan.Items);
            foreach (var item in dto.Items)
            {
                var ultima = ultimas.TryGetValue(item.ItemPlanInspeccionId, out var f) ? f : (DateOnly?)null;
                item.UltimaRespuesta = ultima;
                item.ProximoVencimiento = InspeccionReglas.ProximoVencimiento(ultima, item.FrecuenciaDias);
                item.VenceHoy = InspeccionReglas.VenceHoy(ultima, item.FrecuenciaDias, hoy);
            }
            dtos.Add(dto);
        }

        return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Success(dtos);
    }

    public async Task<Result<IniciarEjecucionResponse>> IniciarEjecucionAsync(
        IniciarEjecucionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdWithItemsAsync(request.PlanInspeccionId, cancellationToken);
        if (plan == null || !plan.Activo)
            return Result<IniciarEjecucionResponse>.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        if (!InspeccionReglas.TryLeerPuesto(request.Puesto, out var puesto))
            return Result<IniciarEjecucionResponse>.Failure("VALIDATION", "Quién realiza no es válido.");

        var items = plan.Items.Where(i => InspeccionReglas.AplicaAPuesto(i.QuienRealiza, puesto)).ToList();
        if (items.Count == 0)
            return Result<IniciarEjecucionResponse>.Failure("SIN_PLANTILLA",
                puesto == null
                    ? "El plan no tiene puntos de inspección cargados. Un administrador los carga desde Maestros > Planes de inspección."
                    : $"El plan no tiene puntos para {puesto.Value.ObtenerDescripcion()}.");

        var turno = await _unitOfWork.Turnos.GetTurnoActualAsync(cancellationToken);
        if (turno == null)
            return Result<IniciarEjecucionResponse>.Failure("NO_TURNO", "No hay turno configurado para la hora actual.");

        var hoy = Hoy;

        // Una por plan + día + quién realiza: si ya existe se retoma (como Control de Proceso).
        var existente = await _unitOfWork.EjecucionesInspeccion.GetByPlanFechaPuestoAsync(plan.PlanInspeccionId, hoy, puesto, cancellationToken);
        if (existente != null)
        {
            _logger.LogInformation("Retomando inspección {EjecucionId}", existente.EjecucionInspeccionId);
            return Result<IniciarEjecucionResponse>.Success(new IniciarEjecucionResponse
            {
                EjecucionInspeccionId = existente.EjecucionInspeccionId,
                PlanNombre = plan.Nombre,
                TurnoNombre = existente.Turno?.Nombre ?? turno.Nombre,
                Fecha = existente.Fecha,
                CantidadItems = items.Count,
                EsNuevo = false,
                Completada = existente.Completada,
                RowVersion = existente.RowVersion
            });
        }

        var ejecucion = new EjecucionInspeccion
        {
            PlanInspeccionId = plan.PlanInspeccionId,
            TurnoId = turno.TurnoId,
            UsuarioId = usuarioId,
            Puesto = puesto,
            Fecha = hoy,
            FechaHoraInicio = DateTimeOffset.UtcNow,
            Completada = false,
            CantidadOk = 0,
            CantidadNoOk = 0
        };

        await _unitOfWork.EjecucionesInspeccion.AddAsync(ejecucion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Inspección {EjecucionId} iniciada para Plan {PlanId} ({Puesto}) por usuario {UsuarioId}",
            ejecucion.EjecucionInspeccionId, plan.PlanInspeccionId, puesto?.ToString() ?? "todos", usuarioId);

        return Result<IniciarEjecucionResponse>.Success(new IniciarEjecucionResponse
        {
            EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
            PlanNombre = plan.Nombre,
            TurnoNombre = turno.Nombre,
            Fecha = ejecucion.Fecha,
            CantidadItems = items.Count,
            EsNuevo = true,
            RowVersion = ejecucion.RowVersion
        });
    }

    public async Task<Result<EjecucionInspeccionCompletoDto>> ObtenerEjecucionAsync(
        long ejecucionInspeccionId,
        CancellationToken cancellationToken = default)
    {
        var ejecucion = await _unitOfWork.EjecucionesInspeccion.GetByIdWithDetallesAsync(ejecucionInspeccionId, cancellationToken);
        if (ejecucion == null)
            return Result<EjecucionInspeccionCompletoDto>.Failure("NOT_FOUND", "Inspección no encontrada.");

        var ultimas = await _unitOfWork.EjecucionesInspeccion.GetUltimasRespuestasPorItemAsync(
            ejecucion.PlanInspeccionId, ejecucion.Fecha, cancellationToken);

        return Result<EjecucionInspeccionCompletoDto>.Success(ArmarCompleto(ejecucion, ultimas));
    }

    public async Task<Result> GuardarResultadosAsync(
        GuardarResultadosInspeccionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var ejecucion = await _unitOfWork.EjecucionesInspeccion.GetByIdWithDetallesAsync(request.EjecucionInspeccionId, cancellationToken);
        if (ejecucion == null)
            return Result.Failure("NOT_FOUND", "Inspección no encontrada.");

        if (ejecucion.Completada)
            return Result.Failure("YA_COMPLETADA", "La inspección ya está cerrada y no puede modificarse.");

        if (!ejecucion.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "La inspección fue modificada por otro usuario. Recargá la pantalla.");

        var plantilla = PuntosDe(ejecucion).ToDictionary(i => i.ItemPlanInspeccionId);

        // Primero se valida todo: si un punto no valida no se guarda ninguno (el operador corrige y reintenta).
        var errores = new List<string>();
        var aGuardar = new List<(ItemPlanInspeccion Item, ResultadoPuntoInspeccion Respuesta)>();
        foreach (var r in request.Resultados)
        {
            // Un punto que no es de esta inspección (otro puesto o desactivado) no se carga.
            if (!plantilla.TryGetValue(r.ItemPlanInspeccionId, out var item))
                continue;

            var respuesta = InspeccionReglas.ValidarRespuesta(r.Resultado, r.Observacion);
            if (!respuesta.EsValido)
                errores.Add($"{item.NumeroItem}. {item.ParteEquipo ?? item.Descripcion}: {respuesta.Error}");
            else if (!respuesta.SinValor)
                aGuardar.Add((item, respuesta));
        }

        if (errores.Count > 0)
            return Result.Failure("VALIDATION", string.Join(" | ", errores));

        var hallazgos = (await _unitOfWork.HallazgosNoOk.GetByEjecucionAsync(ejecucion.EjecucionInspeccionId, cancellationToken))
            .Where(h => !h.Resuelto)
            .ToDictionary(h => h.EjecucionInspeccionDetalleId);
        var ahora = DateTimeOffset.UtcNow;

        foreach (var (item, respuesta) in aGuardar)
        {
            var detalle = ejecucion.Detalles.FirstOrDefault(d => d.ItemPlanInspeccionId == item.ItemPlanInspeccionId);
            if (detalle == null)
            {
                detalle = new EjecucionInspeccionDetalle
                {
                    EjecucionInspeccionId = ejecucion.EjecucionInspeccionId,
                    ItemPlanInspeccionId = item.ItemPlanInspeccionId,
                    EstandarSnapshot = item.CondicionEstandar // SNAPSHOT
                };
                ejecucion.Detalles.Add(detalle);
            }

            var cambio = detalle.Resultado != respuesta.Resultado || detalle.Observacion != respuesta.Observacion;
            detalle.Resultado = respuesta.Resultado!.Value;
            detalle.Observacion = respuesta.Observacion;
            if (cambio || detalle.EjecucionInspeccionDetalleId == 0)
                detalle.FechaHoraRegistro = ahora;

            var abierto = detalle.EjecucionInspeccionDetalleId != 0 && hallazgos.TryGetValue(detalle.EjecucionInspeccionDetalleId, out var h) ? h : null;
            if (respuesta.Resultado == ResultadoInspeccion.NO_OK)
            {
                // Un hallazgo abierto por punto: si ya existe se actualiza la descripción.
                if (abierto != null)
                {
                    abierto.Descripcion = respuesta.Observacion!;
                }
                else
                {
                    await _unitOfWork.HallazgosNoOk.AddAsync(new HallazgoNoOk
                    {
                        EjecucionInspeccionDetalle = detalle,
                        LineaId = ejecucion.PlanInspeccion.LineaId,
                        EquipoId = ejecucion.PlanInspeccion.EquipoId,
                        UsuarioReporteId = usuarioId,
                        FechaHoraHallazgo = ahora,
                        Descripcion = respuesta.Observacion!,
                        Resuelto = false
                    }, cancellationToken);
                }
            }
            else if (abierto != null)
            {
                // Pasó de NO OK a OK antes de cerrar: el hallazgo no se borra, se da por resuelto.
                abierto.Resuelto = true;
                abierto.UsuarioResolucionId = usuarioId;
                abierto.FechaHoraResolucion = ahora;
                abierto.ObservacionResolucion = CorregidoEnLaInspeccion;
            }
        }

        ActualizarContadores(ejecucion);
        if (request.ObservacionesGenerales != null)
            ejecucion.ObservacionesGenerales = Recortar(request.ObservacionesGenerales, 1000);

        if (request.Completar)
        {
            var ultimas = await _unitOfWork.EjecucionesInspeccion.GetUltimasRespuestasPorItemAsync(ejecucion.PlanInspeccionId, ejecucion.Fecha, cancellationToken);
            var faltan = FaltanParaCerrar(ejecucion, ultimas);
            if (faltan > 0)
                return Result.Failure("INCOMPLETA", $"No se puede cerrar. Faltan {faltan} puntos que vencen hoy.");
            Cerrar(ejecucion, usuarioId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Respuestas guardadas en inspección {EjecucionId}. OK: {Ok}, NO_OK: {NoOk}",
            ejecucion.EjecucionInspeccionId, ejecucion.CantidadOk, ejecucion.CantidadNoOk);

        return Result.Success();
    }

    public async Task<Result> CerrarEjecucionAsync(
        CerrarEjecucionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var ejecucion = await _unitOfWork.EjecucionesInspeccion.GetByIdWithDetallesAsync(request.EjecucionInspeccionId, cancellationToken);
        if (ejecucion == null)
            return Result.Failure("NOT_FOUND", "Inspección no encontrada.");

        if (ejecucion.Completada)
            return Result.Failure("YA_COMPLETADA", "La inspección ya está cerrada.");

        if (!ejecucion.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "La inspección fue modificada por otro usuario. Recargá la pantalla.");

        var ultimas = await _unitOfWork.EjecucionesInspeccion.GetUltimasRespuestasPorItemAsync(ejecucion.PlanInspeccionId, ejecucion.Fecha, cancellationToken);
        var faltan = FaltanParaCerrar(ejecucion, ultimas);
        if (faltan > 0)
            return Result.Failure("INCOMPLETA", $"No se puede cerrar. Faltan {faltan} puntos que vencen hoy.");
        if (ejecucion.Detalles.Count == 0)
            return Result.Failure("INCOMPLETA", "No se puede cerrar una inspección sin puntos respondidos.");

        ejecucion.ObservacionesGenerales = Recortar(request.ObservacionesGenerales, 1000) ?? ejecucion.ObservacionesGenerales;
        ActualizarContadores(ejecucion);
        Cerrar(ejecucion, usuarioId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Inspección {EjecucionId} cerrada (firmada) por usuario {UsuarioId}", ejecucion.EjecucionInspeccionId, usuarioId);
        return Result.Success();
    }

    public async Task<Result<PagedResult<EjecucionInspeccionDto>>> ObtenerEjecucionesPaginadoAsync(
        EjecucionInspeccionFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        TipoPuesto? puesto = null;
        if (!string.IsNullOrWhiteSpace(filtro.Puesto) && !InspeccionReglas.TryLeerPuesto(filtro.Puesto, out puesto))
            return Result<PagedResult<EjecucionInspeccionDto>>.Failure("VALIDATION", "Quién realiza no es válido.");

        bool? completada = filtro.Estado?.ToUpperInvariant() switch
        {
            Cerrada => true,
            EnCurso => false,
            _ => null
        };

        var page = Math.Max(1, filtro.Page);
        var pageSize = Math.Clamp(filtro.PageSize, 1, 100);

        var (items, totalCount) = await _unitOfWork.EjecucionesInspeccion.GetPaginadoAsync(
            page,
            pageSize,
            filtro.LineaId,
            filtro.PlanInspeccionId,
            filtro.FechaDesde,
            filtro.FechaHasta,
            filtro.SoloConNoOk,
            cancellationToken,
            filtro.EquipoId,
            puesto,
            filtro.FrecuenciaDias,
            completada);

        var dtos = items.Select(e =>
        {
            var dto = new EjecucionInspeccionDto();
            InspeccionMapeo.LlenarEncabezado(dto, e);
            return dto;
        }).ToList();

        return Result<PagedResult<EjecucionInspeccionDto>>.Success(new PagedResult<EjecucionInspeccionDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
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

        var dtos = hallazgos.Select(MapToHallazgoDto).ToList();

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

        var dtos = items.Select(MapToHallazgoDto).ToList();

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

    // ---------------------------------------------------------------------------------------------

    /// <summary>Puntos activos del plan que entran en esta inspección (según quién realiza).</summary>
    private static IEnumerable<ItemPlanInspeccion> PuntosDe(EjecucionInspeccion e) =>
        e.PlanInspeccion.Items
            .Where(i => i.Activo && InspeccionReglas.AplicaAPuesto(i.QuienRealiza, e.Puesto))
            .OrderBy(i => i.Orden).ThenBy(i => i.NumeroItem);

    private static int FaltanParaCerrar(EjecucionInspeccion e, IReadOnlyDictionary<int, DateOnly> ultimas)
    {
        var respondidos = e.Detalles.Select(d => d.ItemPlanInspeccionId).ToHashSet();
        return PuntosDe(e).Count(i =>
            InspeccionReglas.VenceHoy(ultimas.TryGetValue(i.ItemPlanInspeccionId, out var f) ? f : null, i.FrecuenciaDias, e.Fecha)
            && !respondidos.Contains(i.ItemPlanInspeccionId));
    }

    private static void ActualizarContadores(EjecucionInspeccion e)
    {
        e.CantidadOk = e.Detalles.Count(d => d.Resultado == ResultadoInspeccion.OK);
        e.CantidadNoOk = e.Detalles.Count(d => d.Resultado == ResultadoInspeccion.NO_OK);
    }

    private static void Cerrar(EjecucionInspeccion e, string usuarioId)
    {
        e.Completada = true;
        e.FechaHoraFin = DateTimeOffset.UtcNow;
        e.UsuarioCierreId = usuarioId;
    }

    private static string? Recortar(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var t = valor.Trim();
        return t.Length > maximo ? t[..maximo] : t;
    }

    private static EjecucionInspeccionCompletoDto ArmarCompleto(EjecucionInspeccion e, IReadOnlyDictionary<int, DateOnly> ultimas)
    {
        var dto = new EjecucionInspeccionCompletoDto { PlanDescripcion = e.PlanInspeccion?.Descripcion };
        InspeccionMapeo.LlenarEncabezado(dto, e);

        var puntos = PuntosDe(e).ToList();
        var filas = puntos
            .Select(item => ADetalle(e, item, e.Detalles.FirstOrDefault(d => d.ItemPlanInspeccionId == item.ItemPlanInspeccionId), ultimas, fueraDePlantilla: false))
            .ToList();

        // Datos viejos: un punto que salió del plan pero ya tenía respuesta se sigue viendo (solo lectura).
        var enPlantilla = puntos.Select(p => p.ItemPlanInspeccionId).ToHashSet();
        filas.AddRange(e.Detalles
            .Where(d => !enPlantilla.Contains(d.ItemPlanInspeccionId))
            .Select(d => ADetalle(e, d.ItemPlanInspeccion, d, ultimas, fueraDePlantilla: true)));

        dto.Detalles = filas.OrderBy(f => f.FueraDePlantilla).ThenBy(f => f.Orden).ThenBy(f => f.NumeroItem).ToList();
        dto.CantidadTotal = puntos.Count;
        var vencen = dto.Detalles.Where(d => !d.FueraDePlantilla && d.VenceHoy).ToList();
        dto.CantidadVencenHoy = vencen.Count;
        dto.CantidadVencenHoyRespondidos = vencen.Count(d => d.Resultado != null);
        dto.PuedeCerrar = !e.Completada && dto.CantidadVencenHoy == dto.CantidadVencenHoyRespondidos && e.Detalles.Count > 0;
        return dto;
    }

    private static EjecucionInspeccionDetalleDto ADetalle(
        EjecucionInspeccion e, ItemPlanInspeccion item, EjecucionInspeccionDetalle? detalle,
        IReadOnlyDictionary<int, DateOnly> ultimas, bool fueraDePlantilla)
    {
        var ultima = ultimas.TryGetValue(item.ItemPlanInspeccionId, out var f) ? f : (DateOnly?)null;
        return new EjecucionInspeccionDetalleDto
        {
            EjecucionInspeccionDetalleId = detalle?.EjecucionInspeccionDetalleId ?? 0,
            EjecucionInspeccionId = e.EjecucionInspeccionId,
            ItemPlanInspeccionId = item.ItemPlanInspeccionId,
            NumeroItem = item.NumeroItem,
            Codigo = item.Codigo,
            ParteEquipo = item.ParteEquipo,
            ItemDescripcion = item.Descripcion,
            // Con respuesta se muestra el snapshot: contra qué se inspeccionó ESE día.
            CondicionEstandar = detalle?.EstandarSnapshot ?? item.CondicionEstandar,
            Metodo = item.MetodoDetalle ?? item.Metodo.ObtenerDescripcion(),
            QuienRealiza = item.QuienRealiza.ToString(),
            QuienRealizaDescripcion = item.QuienRealiza.ObtenerDescripcion(),
            FrecuenciaDias = item.FrecuenciaDias,
            FrecuenciaDescripcion = InspeccionReglas.DescribirFrecuencia(item.FrecuenciaDias),
            Orden = item.Orden,
            UltimaRespuesta = ultima,
            ProximoVencimiento = InspeccionReglas.ProximoVencimiento(ultima, item.FrecuenciaDias),
            VenceHoy = InspeccionReglas.VenceHoy(ultima, item.FrecuenciaDias, e.Fecha),
            FueraDePlantilla = fueraDePlantilla,
            Resultado = detalle?.Resultado.ToString(),
            ResultadoDescripcion = detalle?.Resultado.ObtenerDescripcion(),
            Observacion = detalle?.Observacion,
            FechaHoraRegistro = detalle?.FechaHoraRegistro
        };
    }

    private static HallazgoNoOkDto MapToHallazgoDto(HallazgoNoOk hallazgo)
    {
        var item = hallazgo.EjecucionInspeccionDetalle?.ItemPlanInspeccion;
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
            ItemDescripcion = item == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(item.ParteEquipo) ? item.Descripcion : $"{item.NumeroItem}. {item.ParteEquipo}: {item.Descripcion}",
            PlanNombre = hallazgo.EjecucionInspeccionDetalle?.EjecucionInspeccion?.PlanInspeccion?.Nombre ?? string.Empty
        };
    }
}

/// <summary>
/// Entidad → DTO de planes e ítems de inspección, compartido por la carga y el maestro.
/// </summary>
internal static class InspeccionMapeo
{
    public static PlanInspeccionDto APlan(PlanInspeccion plan, bool pendienteHoy = false)
    {
        var dto = new PlanInspeccionDto();
        Llenar(dto, plan, plan.Items.Count, pendienteHoy);
        return dto;
    }

    public static PlanInspeccionConItemsDto APlanConItems(PlanInspeccion plan, IEnumerable<ItemPlanInspeccion> items, bool pendienteHoy = false)
    {
        var lista = items.OrderBy(i => i.Orden).ThenBy(i => i.NumeroItem).ToList();
        var dto = new PlanInspeccionConItemsDto { Items = lista.Select(AItem).ToList() };
        Llenar(dto, plan, lista.Count(i => i.Activo), pendienteHoy);
        return dto;
    }

    public static ItemPlanInspeccionDto AItem(ItemPlanInspeccion i) => new()
    {
        ItemPlanInspeccionId = i.ItemPlanInspeccionId,
        PlanInspeccionId = i.PlanInspeccionId,
        NumeroItem = i.NumeroItem,
        Codigo = i.Codigo,
        ParteEquipo = i.ParteEquipo,
        Descripcion = i.Descripcion,
        CondicionEstandar = i.CondicionEstandar,
        Metodo = i.Metodo.ToString(),
        MetodoDescripcion = i.Metodo.ObtenerDescripcion(),
        MetodoDetalle = i.MetodoDetalle,
        QuienRealiza = i.QuienRealiza.ToString(),
        QuienRealizaDescripcion = i.QuienRealiza.ObtenerDescripcion(),
        FrecuenciaDias = i.FrecuenciaDias,
        FrecuenciaDescripcion = InspeccionReglas.DescribirFrecuencia(i.FrecuenciaDias),
        Orden = i.Orden,
        Activo = i.Activo
    };

    public static void LlenarEncabezado(EjecucionInspeccionDto dto, EjecucionInspeccion e)
    {
        var plan = e.PlanInspeccion;
        dto.EjecucionInspeccionId = e.EjecucionInspeccionId;
        dto.PlanInspeccionId = e.PlanInspeccionId;
        dto.PlanNombre = plan?.Nombre ?? string.Empty;
        dto.PlanCodigo = plan?.Codigo ?? string.Empty;
        dto.LineaId = plan?.LineaId ?? 0;
        dto.LineaNombre = plan?.Linea?.Nombre ?? string.Empty;
        dto.EquipoId = plan?.EquipoId;
        dto.EquipoNombre = plan?.Equipo?.Nombre;
        dto.TurnoId = e.TurnoId;
        dto.TurnoNombre = e.Turno?.Nombre ?? string.Empty;
        dto.UsuarioId = e.UsuarioId;
        dto.Fecha = e.Fecha;
        dto.FechaHoraInicio = e.FechaHoraInicio;
        dto.FechaHoraFin = e.FechaHoraFin;
        dto.Completada = e.Completada;
        dto.Estado = e.Completada ? InspeccionService.Cerrada : InspeccionService.EnCurso;
        dto.EstadoDescripcion = e.Completada ? "Cerrada" : "En curso";
        dto.Puesto = e.Puesto?.ToString();
        dto.PuestoDescripcion = e.Puesto?.ObtenerDescripcion() ?? "Todos";
        dto.UsuarioCierreId = e.UsuarioCierreId;
        dto.CantidadOk = e.CantidadOk;
        dto.CantidadNoOk = e.CantidadNoOk;
        dto.CantidadTotal = e.CantidadOk + e.CantidadNoOk;
        dto.ObservacionesGenerales = e.ObservacionesGenerales;
        dto.RowVersion = e.RowVersion;
    }

    private static void Llenar(PlanInspeccionDto dto, PlanInspeccion plan, int cantidadItems, bool pendienteHoy)
    {
        dto.PlanInspeccionId = plan.PlanInspeccionId;
        dto.LineaId = plan.LineaId;
        dto.LineaNombre = plan.Linea?.Nombre ?? string.Empty;
        dto.EquipoId = plan.EquipoId;
        dto.EquipoNombre = plan.Equipo?.Nombre;
        dto.PuestoResponsable = plan.PuestoResponsable.ToString();
        dto.PuestoResponsableDescripcion = plan.PuestoResponsable.ObtenerDescripcion();
        dto.Codigo = plan.Codigo;
        dto.Nombre = plan.Nombre;
        dto.Descripcion = plan.Descripcion;
        dto.FrecuenciaDias = plan.FrecuenciaDias;
        dto.Orden = plan.Orden;
        dto.Activo = plan.Activo;
        dto.CantidadItems = cantidadItems;
        dto.PendienteHoy = pendienteHoy;
    }
}
