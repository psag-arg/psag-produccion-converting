using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Dashboard;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DashboardCompletoDto>> ObtenerDashboardCompletoAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        var dashboard = new DashboardCompletoDto();

        // Eje 1: Detenciones
        var resumenResult = await ObtenerResumenAsync(filtro, lineasPermitidas, cancellationToken);
        if (resumenResult.IsSuccess)
            dashboard.Resumen = resumenResult.Value!;

        var porLineaResult = await ObtenerDetencionesPorLineaAsync(filtro, lineasPermitidas, cancellationToken);
        if (porLineaResult.IsSuccess)
            dashboard.DetencionesPorLinea = porLineaResult.Value!;

        var porTipoResult = await ObtenerDetencionesPorTipoFallaAsync(filtro, lineasPermitidas, cancellationToken);
        if (porTipoResult.IsSuccess)
            dashboard.DetencionesPorTipoFalla = porTipoResult.Value!;

        var tendenciaResult = await ObtenerTendenciaDiariaAsync(filtro, lineasPermitidas, cancellationToken);
        if (tendenciaResult.IsSuccess)
            dashboard.TendenciaDiaria = tendenciaResult.Value!;

        var topDetallesResult = await ObtenerTopDetallesFallaAsync(filtro, lineasPermitidas, 10, cancellationToken);
        if (topDetallesResult.IsSuccess)
            dashboard.TopDetallesFalla = topDetallesResult.Value!;

        // Detenciones abiertas
        dashboard.DetencionesAbiertas = await ObtenerDetencionesAbiertasAsync(lineasPermitidas, cancellationToken);

        // Top equipos con mayor parada
        dashboard.TopEquiposConMayorParada = await ObtenerTopEquiposConMayorParadaAsync(filtro, lineasPermitidas, 5, cancellationToken);

        // Eje 2: Variables de Proceso
        dashboard.ResumenProceso = await ObtenerResumenProcesoAsync(filtro, lineasPermitidas, cancellationToken);
        dashboard.TopDesviaciones = await ObtenerTopDesviacionesAsync(filtro, lineasPermitidas, 10, cancellationToken);

        // Eje 3: Inspecciones
        dashboard.ResumenInspecciones = await ObtenerResumenInspeccionesAsync(filtro, lineasPermitidas, cancellationToken);
        dashboard.HallazgosPendientes = await ObtenerHallazgosPendientesAsync(lineasPermitidas, 10, cancellationToken);

        return Result<DashboardCompletoDto>.Success(dashboard);
    }

    public async Task<Result<DashboardResumenDto>> ObtenerResumenAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);

        var query = detenciones
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta);

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        if (filtro.EquipoId.HasValue)
            query = query.Where(d => d.EquipoId == filtro.EquipoId.Value);

        if (filtro.TipoFallaId.HasValue)
            query = query.Where(d => d.TipoFallaId == filtro.TipoFallaId.Value);

        if (filtro.TurnoId.HasValue)
            query = query.Where(d => d.TurnoId == filtro.TurnoId.Value);

        var lista = query.ToList();

        var resumen = new DashboardResumenDto
        {
            TotalDetenciones = lista.Count,
            DetencionesAbiertas = lista.Count(d => d.EstadoActual != EstadoDetencion.CERRADA),
            DetencionesCerradas = lista.Count(d => d.EstadoActual == EstadoDetencion.CERRADA),
            MinutosTotalesParada = lista.Sum(d => d.MinutosEfectivos),
            TiempoPromedioResolucionMinutos = lista.Count > 0 ? lista.Average(d => d.MinutosEfectivos) : 0
        };

        if (lista.Count > 0)
        {
            var tiempos = lista.Select(d => d.MinutosEfectivos).OrderBy(x => x).ToList();
            var mid = tiempos.Count / 2;
            resumen.MedianaTiempoResolucionMinutos = tiempos.Count % 2 == 0
                ? (tiempos[mid - 1] + tiempos[mid]) / 2.0
                : tiempos[mid];
        }

        return Result<DashboardResumenDto>.Success(resumen);
    }

    public async Task<Result<IReadOnlyList<DetencionesPorLineaDto>>> ObtenerDetencionesPorLineaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);

        var query = detenciones
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta);

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        var resultado = query
            .GroupBy(d => d.LineaId)
            .Select(g => new DetencionesPorLineaDto
            {
                LineaId = g.Key,
                LineaNombre = lineas.FirstOrDefault(l => l.LineaId == g.Key)?.Nombre ?? "Desconocida",
                CantidadDetenciones = g.Count(),
                MinutosTotalesParada = g.Sum(d => d.MinutosEfectivos)
            })
            .OrderByDescending(x => x.MinutosTotalesParada)
            .ToList();

        return Result<IReadOnlyList<DetencionesPorLineaDto>>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<DetencionesPorTipoFallaDto>>> ObtenerDetencionesPorTipoFallaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var tiposFalla = await _unitOfWork.TiposFalla.GetAllAsync(cancellationToken);

        var query = detenciones
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta && d.TipoFallaId.HasValue);

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        var total = query.Count();

        var resultado = query
            .GroupBy(d => d.TipoFallaId!.Value)
            .Select(g => new DetencionesPorTipoFallaDto
            {
                TipoFallaId = g.Key,
                TipoFallaNombre = tiposFalla.FirstOrDefault(t => t.TipoFallaId == g.Key)?.Nombre ?? "Desconocido",
                CantidadDetenciones = g.Count(),
                Porcentaje = total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0
            })
            .OrderByDescending(x => x.CantidadDetenciones)
            .ToList();

        return Result<IReadOnlyList<DetencionesPorTipoFallaDto>>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<DetencionesPorDetalleFallaDto>>> ObtenerTopDetallesFallaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var detallesFalla = await _unitOfWork.DetallesFalla.GetAllAsync(cancellationToken);

        var query = detenciones
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta && d.DetalleFallaId.HasValue);

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        var total = query.Count();

        var resultado = query
            .GroupBy(d => d.DetalleFallaId!.Value)
            .Select(g => new DetencionesPorDetalleFallaDto
            {
                DetalleFallaId = g.Key,
                DetalleFallaNombre = detallesFalla.FirstOrDefault(df => df.DetalleFallaId == g.Key)?.Nombre ?? "Desconocido",
                CantidadDetenciones = g.Count(),
                Porcentaje = total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0
            })
            .OrderByDescending(x => x.CantidadDetenciones)
            .Take(top)
            .ToList();

        return Result<IReadOnlyList<DetencionesPorDetalleFallaDto>>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<TendenciaDetencionesDto>>> ObtenerTendenciaDiariaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);

        var query = detenciones
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta);

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        var resultado = query
            .GroupBy(d => d.Fecha)
            .Select(g => new TendenciaDetencionesDto
            {
                Fecha = g.Key,
                CantidadDetenciones = g.Count(),
                MinutosTotalesParada = g.Sum(d => d.MinutosEfectivos)
            })
            .OrderBy(x => x.Fecha)
            .ToList();

        return Result<IReadOnlyList<TendenciaDetencionesDto>>.Success(resultado);
    }

    private async Task<IReadOnlyList<DetencionAbiertaDto>> ObtenerDetencionesAbiertasAsync(
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken)
    {
        var detenciones = await _unitOfWork.Detenciones.GetDetencionesAbiertasAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);
        var equipos = await _unitOfWork.Equipos.GetAllAsync(cancellationToken);
        var tiposFalla = await _unitOfWork.TiposFalla.GetAllAsync(cancellationToken);

        var query = detenciones.AsEnumerable();

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        return query
            .OrderByDescending(d => d.MinutosEfectivos)
            .Take(10)
            .Select(d => new DetencionAbiertaDto
            {
                DetencionId = d.DetencionId,
                Codigo = d.Codigo,
                LineaNombre = lineas.FirstOrDefault(l => l.LineaId == d.LineaId)?.Nombre ?? "Desconocida",
                EquipoNombre = d.EquipoId.HasValue
                    ? equipos.FirstOrDefault(e => e.EquipoId == d.EquipoId.Value)?.Nombre
                    : null,
                TipoFallaNombre = d.TipoFallaId.HasValue
                    ? tiposFalla.FirstOrDefault(t => t.TipoFallaId == d.TipoFallaId.Value)?.Nombre
                    : null,
                Estado = d.EstadoActual.ToString(),
                Fecha = d.Fecha,
                MinutosEfectivos = d.MinutosEfectivos,
                Prioridad = d.MinutosEfectivos > 60 ? "Alta" : d.MinutosEfectivos > 30 ? "Media" : "Baja"
            })
            .ToList();
    }

    private async Task<IReadOnlyList<DetencionesPorEquipoDto>> ObtenerTopEquiposConMayorParadaAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        int top,
        CancellationToken cancellationToken)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var equipos = await _unitOfWork.Equipos.GetAllAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);

        var query = detenciones
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta && d.EquipoId.HasValue);

        if (lineasPermitidas != null)
            query = query.Where(d => lineasPermitidas.Contains(d.LineaId));

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        return query
            .GroupBy(d => d.EquipoId!.Value)
            .Select(g =>
            {
                var equipo = equipos.FirstOrDefault(e => e.EquipoId == g.Key);
                return new DetencionesPorEquipoDto
                {
                    EquipoId = g.Key,
                    EquipoNombre = equipo?.Nombre ?? "Desconocido",
                    LineaNombre = equipo != null
                        ? lineas.FirstOrDefault(l => l.LineaId == equipo.LineaId)?.Nombre ?? "Desconocida"
                        : "Desconocida",
                    CantidadDetenciones = g.Count(),
                    MinutosTotalesParada = g.Sum(d => d.MinutosEfectivos)
                };
            })
            .OrderByDescending(x => x.MinutosTotalesParada)
            .Take(top)
            .ToList();
    }

    // Eje 2: Variables de Proceso
    private async Task<ResumenProcesoDto> ObtenerResumenProcesoAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // Usar paginado para obtener registros de hoy
        var (registros, _) = await _unitOfWork.RegistrosProceso.GetPaginadoAsync(
            page: 1,
            pageSize: 1000,
            lineaId: filtro.LineaId,
            fechaDesde: hoy,
            fechaHasta: hoy,
            cancellationToken: cancellationToken);

        var query = registros.AsEnumerable();

        if (lineasPermitidas != null)
            query = query.Where(r => lineasPermitidas.Contains(r.LineaId));

        var registrosHoy = query.ToList();
        var totalValores = 0;
        var valoresDentro = 0;
        var valoresFuera = 0;

        foreach (var registro in registrosHoy)
        {
            foreach (var detalle in registro.Detalles)
            {
                totalValores++;

                if (detalle.FueraDeEstandar)
                    valoresFuera++;
                else
                    valoresDentro++;
            }
        }

        return new ResumenProcesoDto
        {
            TotalRegistrosHoy = registrosHoy.Count,
            TotalValoresCapturados = totalValores,
            ValoresDentroEstandar = valoresDentro,
            ValoresFueraEstandar = valoresFuera,
            PorcentajeCumplimiento = totalValores > 0
                ? Math.Round((double)valoresDentro / totalValores * 100, 1)
                : 100
        };
    }

    private async Task<IReadOnlyList<DesviacionParametroDto>> ObtenerTopDesviacionesAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        int top,
        CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var (registros, _) = await _unitOfWork.RegistrosProceso.GetPaginadoAsync(
            page: 1,
            pageSize: 1000,
            lineaId: filtro.LineaId,
            fechaDesde: hoy,
            fechaHasta: hoy,
            cancellationToken: cancellationToken);

        var parametros = await _unitOfWork.Parametros.GetAllAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);
        var productos = await _unitOfWork.Productos.GetAllAsync(cancellationToken);

        var query = registros.AsEnumerable();

        if (lineasPermitidas != null)
            query = query.Where(r => lineasPermitidas.Contains(r.LineaId));

        var desviaciones = new List<DesviacionParametroDto>();

        foreach (var registro in query)
        {
            foreach (var detalle in registro.Detalles.Where(d => d.FueraDeEstandar && d.ValorReal.HasValue))
            {
                var parametro = parametros.FirstOrDefault(p => p.ParametroId == detalle.ParametroId);

                desviaciones.Add(new DesviacionParametroDto
                {
                    ParametroId = detalle.ParametroId,
                    ParametroNombre = parametro?.Nombre ?? "Desconocido",
                    LineaNombre = lineas.FirstOrDefault(l => l.LineaId == registro.LineaId)?.Nombre ?? "Desconocida",
                    ProductoNombre = productos.FirstOrDefault(p => p.ProductoId == registro.ProductoId)?.Nombre ?? "Desconocido",
                    ValorCapturado = detalle.ValorReal!.Value,
                    ValorEstandar = decimal.TryParse(detalle.ValorEstandarSnapshot, out var est) ? est : null,
                    ToleranciaInferior = null,
                    ToleranciaSuperior = null,
                    Desviacion = 0, // Simplificado - el campo FueraDeEstandar ya indica el desvio
                    FechaHora = detalle.FechaHoraRegistro
                });
            }
        }

        return desviaciones
            .OrderByDescending(d => d.FechaHora)
            .Take(top)
            .ToList();
    }

    // Eje 3: Inspecciones
    private async Task<ResumenInspeccionesDto> ObtenerResumenInspeccionesAsync(
        DashboardFiltroRequest filtro,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var inicioSemana = hoy.AddDays(-(int)hoy.DayOfWeek);

        // Obtener ejecuciones de hoy y de la semana
        var lineasIds = lineasPermitidas?.ToList();
        var lineaId = filtro.LineaId ?? (lineasIds?.Count == 1 ? lineasIds[0] : (int?)null);

        var (ejecucionesHoy, _) = await _unitOfWork.EjecucionesInspeccion.GetPaginadoAsync(
            page: 1,
            pageSize: 1000,
            lineaId: lineaId,
            fechaDesde: hoy,
            fechaHasta: hoy,
            cancellationToken: cancellationToken);

        var (ejecucionesSemana, _) = await _unitOfWork.EjecucionesInspeccion.GetPaginadoAsync(
            page: 1,
            pageSize: 1000,
            lineaId: lineaId,
            fechaDesde: inicioSemana,
            fechaHasta: hoy,
            cancellationToken: cancellationToken);

        // Obtener hallazgos pendientes
        var hallazgosPendientes = await _unitOfWork.HallazgosNoOk.GetAllPendientesAsync(cancellationToken);

        var queryHallazgos = hallazgosPendientes.AsEnumerable();
        if (lineasPermitidas != null)
            queryHallazgos = queryHallazgos.Where(h => lineasPermitidas.Contains(h.LineaId));

        if (filtro.LineaId.HasValue)
            queryHallazgos = queryHallazgos.Where(h => h.LineaId == filtro.LineaId.Value);

        // Calcular items OK de la semana
        var totalItemsSemana = ejecucionesSemana
            .Where(e => e.Completada)
            .SelectMany(e => e.Detalles)
            .Count();

        var itemsOkSemana = ejecucionesSemana
            .Where(e => e.Completada)
            .SelectMany(e => e.Detalles)
            .Count(d => d.Resultado == ResultadoInspeccion.OK);

        return new ResumenInspeccionesDto
        {
            InspeccionesCompletadasHoy = ejecucionesHoy.Count(e => e.Completada),
            InspeccionesEnProgreso = ejecucionesHoy.Count(e => !e.Completada),
            InspeccionesPendientesHoy = 0, // Se calcularia comparando con planes
            HallazgosNoOkPendientes = queryHallazgos.Count(),
            HallazgosResueltosSemana = 0, // Simplificado
            PorcentajeOkSemana = totalItemsSemana > 0
                ? Math.Round((double)itemsOkSemana / totalItemsSemana * 100, 1)
                : 100
        };
    }

    private async Task<IReadOnlyList<HallazgoPendienteDto>> ObtenerHallazgosPendientesAsync(
        IEnumerable<int>? lineasPermitidas,
        int top,
        CancellationToken cancellationToken)
    {
        var hallazgos = await _unitOfWork.HallazgosNoOk.GetAllPendientesAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);
        var equipos = await _unitOfWork.Equipos.GetAllAsync(cancellationToken);

        var query = hallazgos.AsEnumerable();

        if (lineasPermitidas != null)
            query = query.Where(h => lineasPermitidas.Contains(h.LineaId));

        var ahora = DateTimeOffset.Now;

        return query
            .OrderByDescending(h => h.FechaHoraHallazgo)
            .Take(top)
            .Select(h => new HallazgoPendienteDto
            {
                HallazgoNoOkId = h.HallazgoNoOkId,
                LineaNombre = lineas.FirstOrDefault(l => l.LineaId == h.LineaId)?.Nombre ?? "Desconocida",
                EquipoNombre = h.EquipoId.HasValue
                    ? equipos.FirstOrDefault(e => e.EquipoId == h.EquipoId.Value)?.Nombre
                    : null,
                PlanNombre = "Plan", // Simplificado
                ItemDescripcion = h.Descripcion,
                Descripcion = h.Descripcion,
                FechaHoraHallazgo = h.FechaHoraHallazgo,
                DiasAbierto = (int)(ahora - h.FechaHoraHallazgo).TotalDays,
                RequiereAccionCorrectiva = h.RequiereAccionCorrectiva
            })
            .ToList();
    }

    // ============ REPORTES DE INDISPONIBILIDAD, FALLOS MAYORES Y PARETO ============

    private static readonly string[] NombresMeses =
    {
        "", "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic"
    };

    private static readonly int MINUTOS_POR_TURNO = 480; // 8 horas

    public async Task<Result<IndisponibilidadReporteDto>> ObtenerIndisponibilidadAsync(
        ReporteFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var anio = filtro.Anio;
        var mesDesde = filtro.MesDesde ?? 1;
        var mesHasta = filtro.MesHasta ?? 12;

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var tiposFalla = await _unitOfWork.TiposFalla.GetAllAsync(cancellationToken);
        var turnos = await _unitOfWork.Turnos.GetAllAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);

        // Filtrar por linea si se especifica
        var query = detenciones.AsEnumerable();
        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        // Filtrar por anio
        query = query.Where(d => d.Fecha.Year == anio && d.Fecha.Month >= mesDesde && d.Fecha.Month <= mesHasta);

        var detencionesLista = query.ToList();

        // Calcular turnos trabajados por mes (aproximacion: dias del mes * 3 turnos)
        // En la realidad deberia venir de un registro de turnos trabajados
        int CalcularTurnosTrabajados(int mes)
        {
            var diasEnMes = DateTime.DaysInMonth(anio, mes);
            return diasEnMes * 3; // 3 turnos por dia aproximadamente
        }

        var reporte = new IndisponibilidadReporteDto
        {
            LineaId = filtro.LineaId,
            LineaNombre = filtro.LineaId.HasValue
                ? lineas.FirstOrDefault(l => l.LineaId == filtro.LineaId.Value)?.Nombre
                : null,
            Anio = anio
        };

        // Agrupar por mes
        for (int mes = mesDesde; mes <= mesHasta; mes++)
        {
            var detencionesDelMes = detencionesLista.Where(d => d.Fecha.Month == mes).ToList();
            var turnosTrabajados = CalcularTurnosTrabajados(mes);
            var minutosTotalesTurnos = turnosTrabajados * MINUTOS_POR_TURNO;

            var mensual = new IndisponibilidadMensualDto
            {
                Anio = anio,
                Mes = mes,
                MesNombre = $"{NombresMeses[mes]}-{anio % 100}",
                TurnosTrabajados = turnosTrabajados,
                MinutosTotalesTurnos = minutosTotalesTurnos,
                MinutosTotalesParada = detencionesDelMes.Sum(d => d.MinutosEfectivos)
            };

            // Calcular indisponibilidad total
            mensual.IndisponibilidadTotal = minutosTotalesTurnos > 0
                ? Math.Round((double)mensual.MinutosTotalesParada / minutosTotalesTurnos * 100, 1)
                : 0;

            // Agrupar por tipo de falla
            var porTipo = detencionesDelMes
                .Where(d => d.TipoFallaId.HasValue)
                .GroupBy(d => d.TipoFallaId!.Value)
                .Select(g =>
                {
                    var minutos = g.Sum(d => d.MinutosEfectivos);
                    return new IndisponibilidadTipoFallaItem
                    {
                        TipoFallaId = g.Key,
                        TipoFallaNombre = tiposFalla.FirstOrDefault(t => t.TipoFallaId == g.Key)?.Nombre ?? "Sin clasificar",
                        MinutosTotales = minutos,
                        Porcentaje = minutosTotalesTurnos > 0
                            ? Math.Round((double)minutos / minutosTotalesTurnos * 100, 1)
                            : 0
                    };
                })
                .OrderByDescending(x => x.MinutosTotales)
                .ToList();

            // Agregar "Sin clasificar" si hay detenciones sin tipo de falla
            var sinClasificar = detencionesDelMes.Where(d => !d.TipoFallaId.HasValue).Sum(d => d.MinutosEfectivos);
            if (sinClasificar > 0)
            {
                porTipo.Add(new IndisponibilidadTipoFallaItem
                {
                    TipoFallaId = 0,
                    TipoFallaNombre = "Sin clasificar",
                    MinutosTotales = sinClasificar,
                    Porcentaje = minutosTotalesTurnos > 0
                        ? Math.Round((double)sinClasificar / minutosTotalesTurnos * 100, 1)
                        : 0
                });
            }

            mensual.PorTipoFalla = porTipo;

            if (detencionesDelMes.Any())
                reporte.Meses.Add(mensual);
        }

        // Calcular promedio
        if (reporte.Meses.Any())
        {
            var totalTurnosTrabajados = reporte.Meses.Sum(m => m.TurnosTrabajados);
            var totalMinutosTurnos = reporte.Meses.Sum(m => m.MinutosTotalesTurnos);
            var totalMinutosParada = reporte.Meses.Sum(m => m.MinutosTotalesParada);

            reporte.Promedio = new IndisponibilidadMensualDto
            {
                MesNombre = $"PROM {anio % 100}",
                TurnosTrabajados = totalTurnosTrabajados / reporte.Meses.Count,
                MinutosTotalesTurnos = totalMinutosTurnos / reporte.Meses.Count,
                MinutosTotalesParada = totalMinutosParada / reporte.Meses.Count,
                IndisponibilidadTotal = totalMinutosTurnos > 0
                    ? Math.Round((double)totalMinutosParada / totalMinutosTurnos * 100, 1)
                    : 0
            };

            // Promedio por tipo de falla
            var tiposConDatos = reporte.Meses
                .SelectMany(m => m.PorTipoFalla)
                .GroupBy(t => t.TipoFallaId)
                .Select(g => new IndisponibilidadTipoFallaItem
                {
                    TipoFallaId = g.Key,
                    TipoFallaNombre = g.First().TipoFallaNombre,
                    MinutosTotales = g.Sum(x => x.MinutosTotales) / reporte.Meses.Count,
                    Porcentaje = Math.Round(g.Sum(x => x.Porcentaje) / reporte.Meses.Count, 1)
                })
                .OrderByDescending(x => x.MinutosTotales)
                .ToList();

            reporte.Promedio.PorTipoFalla = tiposConDatos;
        }

        return Result<IndisponibilidadReporteDto>.Success(reporte);
    }

    public async Task<Result<FallosMayoresReporteDto>> ObtenerFallosMayoresAsync(
        ReporteFiltroRequest filtro,
        int umbralMinutos = 60,
        CancellationToken cancellationToken = default)
    {
        var anio = filtro.Anio;
        var mesDesde = filtro.MesDesde ?? 1;
        var mesHasta = filtro.MesHasta ?? 12;

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var tiposFalla = await _unitOfWork.TiposFalla.GetAllAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);

        // Filtrar por linea y anio, y solo fallos mayores
        var query = detenciones.AsEnumerable()
            .Where(d => d.MinutosEfectivos >= umbralMinutos)
            .Where(d => d.Fecha.Year == anio && d.Fecha.Month >= mesDesde && d.Fecha.Month <= mesHasta);

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        var fallosMayores = query.ToList();

        // Calcular turnos trabajados por mes
        int CalcularTurnosTrabajados(int mes)
        {
            var diasEnMes = DateTime.DaysInMonth(anio, mes);
            return diasEnMes * 3;
        }

        var reporte = new FallosMayoresReporteDto
        {
            LineaId = filtro.LineaId,
            LineaNombre = filtro.LineaId.HasValue
                ? lineas.FirstOrDefault(l => l.LineaId == filtro.LineaId.Value)?.Nombre
                : null,
            Anio = anio,
            UmbralMinutos = umbralMinutos
        };

        // Agrupar por mes
        for (int mes = mesDesde; mes <= mesHasta; mes++)
        {
            var fallosDelMes = fallosMayores.Where(d => d.Fecha.Month == mes).ToList();
            var turnosTrabajados = CalcularTurnosTrabajados(mes);
            var minutosTotalesTurnos = turnosTrabajados * MINUTOS_POR_TURNO;

            var mensual = new FallosMayoresMensualDto
            {
                Anio = anio,
                Mes = mes,
                MesNombre = $"{NombresMeses[mes]}-{anio % 100}",
                TotalEventos = fallosDelMes.Count,
                MinutosTotalesParada = fallosDelMes.Sum(d => d.MinutosEfectivos),
                MinutosTotalesTurnos = minutosTotalesTurnos
            };

            // Calcular indicador %
            mensual.IndicadorPorcentaje = minutosTotalesTurnos > 0
                ? Math.Round((double)mensual.MinutosTotalesParada / minutosTotalesTurnos * 100, 1)
                : 0;

            // Agrupar por tipo de falla (solo PROCESO y EQUIPO para este reporte)
            var porTipo = fallosDelMes
                .Where(d => d.TipoFallaId.HasValue)
                .GroupBy(d => d.TipoFallaId!.Value)
                .Select(g => new FalloMayorTipoItem
                {
                    TipoFallaId = g.Key,
                    TipoFallaNombre = tiposFalla.FirstOrDefault(t => t.TipoFallaId == g.Key)?.Nombre ?? "Otro",
                    CantidadEventos = g.Count(),
                    MinutosTotales = g.Sum(d => d.MinutosEfectivos)
                })
                .OrderByDescending(x => x.CantidadEventos)
                .ToList();

            mensual.PorTipoFalla = porTipo;

            if (fallosDelMes.Any())
                reporte.Meses.Add(mensual);
        }

        // Calcular promedio
        if (reporte.Meses.Any())
        {
            var totalEventos = reporte.Meses.Sum(m => m.TotalEventos);
            var totalMinutosParada = reporte.Meses.Sum(m => m.MinutosTotalesParada);
            var totalMinutosTurnos = reporte.Meses.Sum(m => m.MinutosTotalesTurnos);

            reporte.Promedio = new FallosMayoresMensualDto
            {
                MesNombre = $"PROM {anio % 100}",
                TotalEventos = totalEventos / reporte.Meses.Count,
                MinutosTotalesParada = totalMinutosParada / reporte.Meses.Count,
                MinutosTotalesTurnos = totalMinutosTurnos / reporte.Meses.Count,
                IndicadorPorcentaje = totalMinutosTurnos > 0
                    ? Math.Round((double)totalMinutosParada / totalMinutosTurnos * 100, 1)
                    : 0
            };

            // Promedio por tipo de falla
            var tiposConDatos = reporte.Meses
                .SelectMany(m => m.PorTipoFalla)
                .GroupBy(t => t.TipoFallaId)
                .Select(g => new FalloMayorTipoItem
                {
                    TipoFallaId = g.Key,
                    TipoFallaNombre = g.First().TipoFallaNombre,
                    CantidadEventos = g.Sum(x => x.CantidadEventos) / reporte.Meses.Count,
                    MinutosTotales = g.Sum(x => x.MinutosTotales) / reporte.Meses.Count
                })
                .OrderByDescending(x => x.CantidadEventos)
                .ToList();

            reporte.Promedio.PorTipoFalla = tiposConDatos;
        }

        return Result<FallosMayoresReporteDto>.Success(reporte);
    }

    public async Task<Result<ParetoDetencionesReporteDto>> ObtenerParetoDetencionesAsync(
        DashboardFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var fechaDesde = filtro.FechaDesde ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fechaHasta = filtro.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today);

        var detenciones = await _unitOfWork.Detenciones.GetAllAsync(cancellationToken);
        var tiposFalla = await _unitOfWork.TiposFalla.GetAllAsync(cancellationToken);
        var equipos = await _unitOfWork.Equipos.GetAllAsync(cancellationToken);
        var detallesFalla = await _unitOfWork.DetallesFalla.GetAllAsync(cancellationToken);
        var lineas = await _unitOfWork.Lineas.GetAllAsync(cancellationToken);
        var turnos = await _unitOfWork.Turnos.GetAllAsync(cancellationToken);

        // Filtrar
        var query = detenciones.AsEnumerable()
            .Where(d => d.Fecha >= fechaDesde && d.Fecha <= fechaHasta);

        if (filtro.LineaId.HasValue)
            query = query.Where(d => d.LineaId == filtro.LineaId.Value);

        if (filtro.TurnoId.HasValue)
            query = query.Where(d => d.TurnoId == filtro.TurnoId.Value);

        var lista = query.ToList();
        var totalMinutos = lista.Sum(d => d.MinutosEfectivos);

        var reporte = new ParetoDetencionesReporteDto
        {
            LineaId = filtro.LineaId,
            LineaNombre = filtro.LineaId.HasValue
                ? lineas.FirstOrDefault(l => l.LineaId == filtro.LineaId.Value)?.Nombre
                : null,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            TurnoId = filtro.TurnoId,
            TurnoNombre = filtro.TurnoId.HasValue
                ? turnos.FirstOrDefault(t => t.TurnoId == filtro.TurnoId.Value)?.Nombre
                : "(Todas)",
            TotalMinutos = totalMinutos,
            TotalDetenciones = lista.Count
        };

        // 1. Resumen por Tipo de Falla con % acumulado
        var porTipoFalla = lista
            .GroupBy(d => d.TipoFallaId)
            .Select(g =>
            {
                var minutos = g.Sum(d => d.MinutosEfectivos);
                return new ParetoTipoFallaItem
                {
                    TipoFallaId = g.Key,
                    TipoFallaNombre = g.Key.HasValue
                        ? tiposFalla.FirstOrDefault(t => t.TipoFallaId == g.Key.Value)?.Nombre ?? "Sin clasificar"
                        : "Sin clasificar",
                    MinutosTotales = minutos,
                    PorcentajeDelTotal = totalMinutos > 0
                        ? Math.Round((double)minutos / totalMinutos * 100, 0)
                        : 0
                };
            })
            .OrderByDescending(x => x.MinutosTotales)
            .ToList();

        // Calcular % acumulado
        double acumulado = 0;
        foreach (var item in porTipoFalla)
        {
            acumulado += item.PorcentajeDelTotal;
            item.PorcentajeAcumulado = Math.Round(acumulado, 0);
        }

        reporte.ResumenPorTipoFalla = porTipoFalla;

        // 2. Detalle por Equipo > Detalle de Falla
        var porEquipo = lista
            .GroupBy(d => d.EquipoId)
            .Select(equipoGrupo =>
            {
                var minutosTotalEquipo = equipoGrupo.Sum(d => d.MinutosEfectivos);

                var detalles = equipoGrupo
                    .GroupBy(d => d.DetalleFallaId)
                    .Select(detalleGrupo =>
                    {
                        var minutosDetalle = detalleGrupo.Sum(d => d.MinutosEfectivos);
                        return new ParetoDetalleItem
                        {
                            EquipoId = equipoGrupo.Key,
                            EquipoNombre = equipoGrupo.Key.HasValue
                                ? equipos.FirstOrDefault(e => e.EquipoId == equipoGrupo.Key.Value)?.Nombre ?? "Sin equipo"
                                : "Sin equipo",
                            DetalleFallaId = detalleGrupo.Key,
                            DetalleFallaNombre = detalleGrupo.Key.HasValue
                                ? detallesFalla.FirstOrDefault(df => df.DetalleFallaId == detalleGrupo.Key.Value)?.Nombre ?? detalleGrupo.First().Descripcion
                                : detalleGrupo.First().Descripcion,
                            MinutosTotales = minutosDetalle,
                            PorcentajeDelTotal = totalMinutos > 0
                                ? Math.Round((double)minutosDetalle / totalMinutos * 100, 0)
                                : 0
                        };
                    })
                    .OrderByDescending(x => x.MinutosTotales)
                    .ToList();

                return new ParetoEquipoGrupo
                {
                    EquipoId = equipoGrupo.Key,
                    EquipoNombre = equipoGrupo.Key.HasValue
                        ? equipos.FirstOrDefault(e => e.EquipoId == equipoGrupo.Key.Value)?.Nombre ?? "Sin equipo"
                        : "MAQUINA",
                    MinutosTotales = minutosTotalEquipo,
                    PorcentajeDelTotal = totalMinutos > 0
                        ? Math.Round((double)minutosTotalEquipo / totalMinutos * 100, 0)
                        : 0,
                    Detalles = detalles
                };
            })
            .OrderByDescending(x => x.MinutosTotales)
            .ToList();

        reporte.DetallePorEquipo = porEquipo;

        return Result<ParetoDetencionesReporteDto>.Success(reporte);
    }
}
