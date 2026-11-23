using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// T-770 · Arma el tablero del turno. Los números de presupuesto vs. real son los de <see cref="IIndicadoresService"/>
/// (no se recalculan); las paradas de cámara se separan por la marca <c>SuperaUmbral</c> (T-784): las que llegaron al
/// umbral son "detenciones reales" y las demás, microcortes. Las detenciones del turno se leen por TurnoProduccionId.
/// </summary>
public class TableroService : ITableroService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIndicadoresService _indicadores;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly Func<DateTimeOffset> _ahora;

    public TableroService(IUnitOfWork unitOfWork, IIndicadoresService indicadores, TimeZoneInfo zonaPlanta)
        : this(unitOfWork, indicadores, zonaPlanta, () => DateTimeOffset.UtcNow)
    {
    }

    public TableroService(IUnitOfWork unitOfWork, IIndicadoresService indicadores, TimeZoneInfo zonaPlanta, Func<DateTimeOffset> ahora)
    {
        _unitOfWork = unitOfWork;
        _indicadores = indicadores;
        _zonaPlanta = zonaPlanta;
        _ahora = ahora;
    }

    public async Task<Result<TableroTurnoDto>> ObtenerAsync(long turnoProduccionId, bool soloLectura, CancellationToken cancellationToken = default)
    {
        var indicadores = await _indicadores.ObtenerTurnoAsync(turnoProduccionId, cancellationToken);
        if (indicadores.IsFailure)
            return Result<TableroTurnoDto>.Failure(indicadores.ErrorCode ?? "NOT_FOUND", indicadores.ErrorMessage ?? "El turno de producción no existe.");
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return Result<TableroTurnoDto>.Failure("NOT_FOUND", "El turno de producción no existe.");

        var ind = indicadores.Value!;
        var ahora = _ahora();
        var finTurno = tp.FinReal ?? (tp.EstaAbierto ? ahora : tp.FinProgramado);
        var inicioTurno = tp.InicioReal is { } ir && ir < tp.InicioProgramado ? ir : tp.InicioProgramado;

        var tramoAbierto = ind.Tramos.LastOrDefault(t => t.Abierto);
        var operador = tp.Operadores.Where(o => !o.Anulado).OrderBy(o => o.Desde).LastOrDefault(o => o.Hasta == null);

        var dto = new TableroTurnoDto
        {
            TurnoProduccionId = tp.TurnoProduccionId,
            LineaId = tp.LineaId,
            LineaNombre = ind.LineaNombre,
            TurnoCodigo = ind.TurnoCodigo,
            TurnoNombre = ind.TurnoNombre,
            FechaProduccion = ind.FechaProduccion,
            Estado = ind.Estado,
            Abierto = ind.Abierto,
            InicioProgramado = ind.InicioProgramado,
            FinProgramado = ind.FinProgramado,
            ProductoNombre = tramoAbierto?.ProductoNombre,
            ProductoDesde = tramoAbierto?.Desde,
            OperadorUsuarioId = operador?.OperadorUsuarioId,
            SoloLectura = soloLectura,
            GeneradoEn = HoraPlanta.En(ahora, _zonaPlanta)!.Value,
            Indicadores = ind
        };

        // Detenciones del turno (el filtro global ya saca las eliminadas)
        var detenciones = await _unitOfWork.Detenciones.GetPorTurnoProduccionAsync(turnoProduccionId, cancellationToken);
        dto.Detenciones = detenciones.Select(d => new DetencionTableroDto
        {
            DetencionId = d.DetencionId,
            Codigo = d.Codigo,
            FechaHoraParada = HoraPlanta.En(d.FechaHoraParada, _zonaPlanta),
            FechaHoraArranque = HoraPlanta.En(d.FechaHoraArranque, _zonaPlanta),
            MinutosEfectivos = d.MinutosEfectivos,
            EstadoActual = d.EstadoActual.ToString(),
            EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion(),
            EquipoNombre = d.Equipo?.Nombre,
            TipoFallaNombre = d.TipoFalla?.Nombre,
            OrigenRegistro = d.OrigenRegistro.ToString(),
            Cuenta = DetencionIndicadores.Cuenta(d),
            PendienteDeclaracion = d.EsPendienteDeclaracion,
            Descripcion = d.Descripcion
        }).ToList();

        // Cámaras de conteo de la línea
        var camaras = (await _unitOfWork.Camaras.GetListadoAsync(tp.LineaId, incluirInactivas: false, cancellationToken))
            .Where(c => c.EsConteo && c.Habilitada).ToList();
        var codigos = camaras.ToDictionary(c => c.CamaraId, c => c.Codigo);

        dto.PendientesDeclarar = detenciones
            .Where(d => d.EsPendienteDeclaracion)
            .Select(d => new PendienteDeclaracionDto
            {
                DetencionId = d.DetencionId,
                Codigo = d.Codigo,
                Fecha = tp.FechaProduccion,
                LineaNombre = ind.LineaNombre,
                TurnoNombre = ind.TurnoNombre,
                FechaHoraParada = HoraPlanta.En(d.FechaHoraParada, _zonaPlanta),
                FechaHoraArranque = HoraPlanta.En(d.FechaHoraArranque, _zonaPlanta),
                FechaHoraArranqueDetectada = HoraPlanta.En(d.FechaHoraArranqueDetectada, _zonaPlanta),
                OrigenRegistro = d.OrigenRegistro.ToString(),
                CamaraCodigo = d.CamaraId is { } c ? codigos.GetValueOrDefault(c) : null,
                MinutosEfectivos = d.MinutosEfectivos,
                EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion()
            })
            .ToList();

        // Paradas de cámara del turno: reales (superan el umbral) y microcortes
        var paradas = (await _unitOfWork.ParadasCamara.GetRangoAsync(
                inicioTurno.AddHours(-2), (finTurno > tp.FinProgramado ? finTurno : tp.FinProgramado).AddHours(2), tp.LineaId, cancellationToken))
            .Where(p => p.TurnoProduccionId == turnoProduccionId)
            .OrderBy(p => p.FechaHoraInicio)
            .ToList();
        var items = paradas.Select(p => ParadaDto(p, detenciones)).ToList();
        dto.DetencionesReales = Resumen(items.Where(p => p.SuperaUmbral));
        dto.Microcortes = Resumen(items.Where(p => !p.SuperaUmbral));

        // Mangas por hora (cámara oficial de mangas de la línea)
        var oficialMangas = (await _unitOfWork.Camaras.GetOficialesAsync(cancellationToken))
            .FirstOrDefault(c => c.LineaId == tp.LineaId && c.Magnitud == MagnitudProduccion.Mangas);
        var tramos = tp.Productos.Where(p => !p.Anulado)
            .Select(p => new TramoObjetivo(p.Desde, p.Hasta ?? finTurno, p.MangasObjetivoTurno, p.MinutosTurnoReferencia))
            .ToList();
        IReadOnlyList<MinutoConteo> minutos = oficialMangas == null || finTurno <= inicioTurno
            ? []
            : await _unitOfWork.ConteosCamaraMinuto.GetMinutosAsync([oficialMangas.CamaraId], inicioTurno, finTurno, cancellationToken);
        dto.MangasPorHora = TableroReglas.MangasPorHora(minutos, oficialMangas?.CamaraId, tramos, inicioTurno, finTurno, ahora, _zonaPlanta)
            .Select(h => new MangasHoraDto { Hora = h.Hora, Objetivo = h.Objetivo, Real = h.Real, MinutosConDatos = h.MinutosConDatos, EnCurso = h.EnCurso })
            .ToList();

        // Último dato de cámara y alertas abiertas de la línea
        var ultimo = camaras.Select(c => c.UltimoDatoRecibido).Where(u => u != null).DefaultIfEmpty(null).Max();
        var alertas = await _unitOfWork.AlertasCamara.GetRecientesAsync(ahora, tp.LineaId, cancellationToken);
        dto.Camara = new CamaraTableroDto
        {
            TieneCamara = camaras.Count > 0,
            UltimoDato = HoraPlanta.En(ultimo, _zonaPlanta),
            MinutosDesdeUltimoDato = TableroReglas.MinutosDesde(ultimo, ahora),
            SinDatos = camaras.Count > 0 && TableroReglas.SinDatos(tp.EstaAbierto, ultimo, ahora),
            Alertas = alertas.Where(a => a.FechaHoraFin == null).Select(a => new AlertaCamaraDto
            {
                AlertaCamaraId = a.AlertaCamaraId,
                CamaraId = a.CamaraId,
                CamaraCodigo = a.Camara?.Codigo ?? string.Empty,
                CamaraNombre = a.Camara?.Nombre ?? string.Empty,
                LineaId = a.Camara?.LineaId ?? tp.LineaId,
                LineaNombre = a.Camara?.Linea?.Nombre ?? ind.LineaNombre,
                Tipo = a.Tipo.ToString(),
                TipoDescripcion = DetencionCamaraReglas.DescripcionAlerta(a.Tipo),
                FechaHoraInicio = HoraPlanta.En(a.FechaHoraInicio, _zonaPlanta)!.Value,
                Activa = true,
                Detalle = a.Detalle
            }).ToList()
        };

        // Los 4 indicadores de Ferretti (T-770 iteración 2)
        var tramosVivos = tp.Productos.Where(p => !p.Anulado).OrderBy(p => p.Desde).ToList();
        dto.Eficiencia = Eficiencia(ind, tramosVivos, tp, ahora);
        dto.Produccion = Produccion(ind, tramosVivos, tp, ahora);
        dto.Velocidad = Velocidad(ind, tramosVivos, minutos, oficialMangas?.CamaraId, inicioTurno, finTurno, tp);
        var tipos = (await _unitOfWork.TiposFalla.GetAllAsync(cancellationToken))
            .Select(t => new TipoFallaTablero(t.TipoFallaId, t.Nombre, t.Orden, t.CuentaParaIndicadores, t.Activo));
        // Pendiente que sigue parada: hasta ahora con el turno abierto; hasta el fin del turno si ya cerró.
        var hastaSiSigue = tp.EstaAbierto ? ahora : finTurno;
        var resumen = TableroIndicadoresReglas.DetencionesPorTipo(
            detenciones.Select(d => d.EsPendienteDeclaracion
                ? new DetencionTablero(d.TipoFallaId, TableroIndicadoresReglas.MinutosPendiente(d.FechaHoraParada, d.FechaHoraArranqueDetectada, hastaSiSigue), true, true)
                : new DetencionTablero(d.TipoFallaId, d.MinutosEfectivos, false, DetencionIndicadores.Cuenta(d))),
            tipos);
        dto.DetencionesPorTipo = new DetencionesTipoTableroDto
        {
            Barras = resumen.Barras.Select(b => new BarraTipoFallaDto
            {
                TipoFallaId = b.TipoFallaId, Nombre = b.Nombre, Minutos = b.Minutos, Cantidad = b.Cantidad, Cuenta = b.Cuenta, SinDeclarar = b.SinDeclarar
            }).ToList(),
            MinutosTotal = resumen.MinutosTotal,
            MinutosPendientes = resumen.MinutosPendientes,
            CantidadPendientes = resumen.CantidadPendientes,
            MinutosNoCuentan = resumen.MinutosNoCuentan
        };

        return Result<TableroTurnoDto>.Success(dto);
    }

    private static decimal? ObjetivoTurno(IReadOnlyList<TurnoProduccionProducto> tramos, Func<TurnoProduccionProducto, int?> objetivo,
        TurnoProduccion tp, int minutosProgramados, DateTimeOffset ahora) =>
        TableroIndicadoresReglas.ObjetivoTurnoCompleto(
            tramos.Select(t => new TramoTurnoCompleto(t.Desde, t.Hasta, objetivo(t), t.MinutosTurnoReferencia)).ToList(),
            tp.FinProgramado, tp.FinReal, ahora, minutosProgramados);

    private static EficienciaTableroDto Eficiencia(IndicadoresTurnoDto ind, IReadOnlyList<TurnoProduccionProducto> tramos, TurnoProduccion tp, DateTimeOffset ahora)
    {
        var objetivoTurno = ObjetivoTurno(tramos, t => t.MangasObjetivoTurno, tp, ind.MinutosProgramados, ahora);
        return new EficienciaTableroDto
        {
            Porcentaje = ind.Eficiencia.Porcentaje,
            Estado = ind.Eficiencia.Estado,
            Nivel = TableroIndicadoresReglas.NivelEficiencia(ind.Eficiencia.Estado == EstadoIndicador.Ok ? ind.Eficiencia.Porcentaje : null),
            Provisorio = ind.Eficiencia.Provisorio,
            Real = ind.Mangas.Real,
            ObjetivoALaHora = ind.Mangas.Objetivo,
            ObjetivoTurno = objetivoTurno,
            PorcentajeTurno = TableroIndicadoresReglas.Porcentaje(ind.Mangas.Real, objetivoTurno),
            UmbralRojo = TableroIndicadoresReglas.EficienciaRojoDebajoDe,
            UmbralVerde = TableroIndicadoresReglas.EficienciaVerdeArribaDe
        };
    }

    private static ProduccionTableroDto Produccion(IndicadoresTurnoDto ind, IReadOnlyList<TurnoProduccionProducto> tramos, TurnoProduccion tp, DateTimeOffset ahora)
    {
        BarraProduccionDto Barra(ComparacionProduccionDto c, Func<TurnoProduccionProducto, int?> objetivo)
        {
            var objetivoTurno = ObjetivoTurno(tramos, objetivo, tp, ind.MinutosProgramados, ahora);
            var porcentajeHora = c.Estado == EstadoIndicador.Ok ? c.Porcentaje : null;
            return new BarraProduccionDto
            {
                Magnitud = c.Magnitud,
                Real = c.Real,
                ObjetivoTurno = objetivoTurno,
                ObjetivoALaHora = c.Objetivo,
                PorcentajeTurno = TableroIndicadoresReglas.Porcentaje(c.Real, objetivoTurno),
                PorcentajeALaHora = porcentajeHora,
                Estado = c.Real == null ? EstadoIndicador.SinDatos : objetivoTurno == null ? EstadoIndicador.SinObjetivo : EstadoIndicador.Ok,
                Nivel = TableroIndicadoresReglas.NivelEficiencia(porcentajeHora),
                Provisorio = c.Provisorio
            };
        }

        var vigente = ind.Tramos.LastOrDefault(t => t.Abierto) ?? ind.Tramos.LastOrDefault();
        return new ProduccionTableroDto
        {
            ProductoNombre = vigente?.ProductoNombre,
            Productos = ind.Tramos.Select(t => t.ProductoNombre).Where(n => !string.IsNullOrEmpty(n))
                .Aggregate(new List<string>(), (l, n) => { if (l.Count == 0 || l[^1] != n) l.Add(n); return l; }),
            Mangas = Barra(ind.Mangas, t => t.MangasObjetivoTurno),
            Bolsones = Barra(ind.Bolsones, t => t.BolsonesObjetivoTurno)
        };
    }

    private VelocidadTableroDto Velocidad(IndicadoresTurnoDto ind, IReadOnlyList<TurnoProduccionProducto> tramos, IReadOnlyList<MinutoConteo> minutos,
        int? camaraId, DateTimeOffset inicioTurno, DateTimeOffset finTurno, TurnoProduccion tp)
    {
        var serie = TableroIndicadoresReglas.VelocidadPorMinuto(
            minutos, camaraId, tramos.Select(t => new TramoMetraje(t.Desde, t.Hasta ?? finTurno, t.MetrosPorManga)).ToList(), inicioTurno, finTurno);
        var finEje = finTurno > tp.FinProgramado ? finTurno : tp.FinProgramado;
        return new VelocidadTableroDto
        {
            Desde = HoraPlanta.En(TableroIndicadoresReglas.Minuto(inicioTurno), _zonaPlanta)!.Value,
            MetrosPorMinuto = serie.ToList(),
            EjeDesde = HoraPlanta.En(inicioTurno, _zonaPlanta)!.Value,
            EjeHasta = HoraPlanta.En(finEje, _zonaPlanta)!.Value,
            Promedio = ind.VelocidadMetrosMinuto,
            Actual = serie.LastOrDefault(v => v != null),
            Referencia = TableroIndicadoresReglas.VelocidadReferenciaMetrosMinuto,
            FaltaMetraje = tramos.Any(t => t.MetrosPorManga == null),
            Tramos = tramos.Select(t => new TramoVelocidadDto
            {
                Desde = HoraPlanta.En(t.Desde, _zonaPlanta)!.Value,
                ProductoNombre = t.ProductoMaestro?.Nombre ?? string.Empty,
                MetrosPorManga = t.MetrosPorManga
            }).ToList()
        };
    }

    public async Task<TurnosTableroDto> ListarTurnosAsync(DateOnly fecha, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default)
    {
        var lineas = (await _unitOfWork.Lineas.GetActivasAsync(cancellationToken))
            .Where(l => TableroIndicadoresReglas.AlcanzaLinea(roles, lineasAsignadas, l.LineaId))
            .OrderBy(l => l.Orden).ThenBy(l => l.Nombre)
            .ToList();
        var ids = lineas.Select(l => l.LineaId).ToList();
        var turnos = ids.Count == 0 ? [] : await _unitOfWork.TurnosProduccion.GetPorFechasAsync(fecha, fecha, ids, cancellationToken);
        return new TurnosTableroDto
        {
            Fecha = fecha,
            Lineas = lineas.Select(l => new LineaTableroDto { LineaId = l.LineaId, Nombre = l.Nombre }).ToList(),
            Turnos = turnos
                .OrderBy(t => t.LineaId).ThenBy(t => t.InicioProgramado)
                .Select(t => new TurnoTableroItemDto
                {
                    TurnoProduccionId = t.TurnoProduccionId,
                    LineaId = t.LineaId,
                    TurnoCodigo = t.Turno?.Codigo ?? string.Empty,
                    TurnoNombre = t.Turno?.Nombre ?? string.Empty,
                    Estado = t.Estado.ToString(),
                    Abierto = t.EstaAbierto,
                    InicioProgramado = HoraPlanta.En(t.InicioProgramado, _zonaPlanta)!.Value,
                    FinProgramado = HoraPlanta.En(t.FinProgramado, _zonaPlanta)!.Value
                })
                .ToList()
        };
    }

    private ParadaCamaraDto ParadaDto(ParadaCamara p, IReadOnlyList<Detencion> detenciones)
    {
        var (cruce, codigos) = ConteoCamaraReglas.Cruzar(p, p.Camara.LineaId, detenciones);
        var supera = DetencionCamaraReglas.SuperaUmbral(p);
        return new ParadaCamaraDto
        {
            ParadaCamaraId = p.ParadaCamaraId,
            LineaId = p.Camara.LineaId,
            LineaNombre = p.Camara.Linea?.Nombre ?? string.Empty,
            CamaraId = p.CamaraId,
            CamaraCodigo = p.Camara.Codigo,
            TurnoProduccionId = p.TurnoProduccionId,
            TurnoNombre = p.TurnoProduccion?.Turno?.Nombre,
            FechaHoraInicio = HoraPlanta.En(p.FechaHoraInicio, _zonaPlanta)!.Value,
            FechaHoraFin = HoraPlanta.En(p.FechaHoraFin, _zonaPlanta)!.Value,
            DuracionSegundos = p.DuracionSegundos,
            Tipo = p.Tipo.ToString(),
            Carriles = p.Carriles,
            InicioIncierto = p.InicioIncierto,
            FinIncierto = p.FinIncierto,
            SuperaUmbral = supera,
            CreaDetencion = p.Camara.CreaDetenciones && supera,
            UmbralTexto = DetencionCamaraReglas.UmbralSegundos(p) is { } umbral ? DetencionCamaraReglas.TextoUmbral(umbral) : null,
            Cruce = cruce.ToString(),
            CodigosDetencion = codigos.ToList()
        };
    }

    private static ResumenParadasTableroDto Resumen(IEnumerable<ParadaCamaraDto> paradas)
    {
        var lista = paradas.ToList();
        var segundos = lista.Sum(p => p.DuracionSegundos);
        return new ResumenParadasTableroDto
        {
            Cantidad = lista.Count,
            Segundos = segundos,
            Minutos = (int)Math.Round(segundos / 60m, MidpointRounding.AwayFromZero),
            Items = lista
        };
    }
}
