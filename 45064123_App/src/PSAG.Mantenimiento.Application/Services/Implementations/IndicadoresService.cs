using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Indicadores de producción (T-798 2.10, etapa 5). Arma, por cada turno de producción, sus tramos con el objetivo
/// prorrateado (snapshot), el real (<see cref="ProduccionOficialReglas.RealParaIndicadores"/>; parcial al vuelo si el
/// tramo está abierto) y la velocidad, y los agrega sumando (nunca promediando %) con
/// <see cref="IndicadoresProduccionCalculador"/>. Los minutos de parada son los de las detenciones que cuentan
/// (<see cref="DetencionIndicadores"/>) y los programados, los del turno (<see cref="MinutosProgramadosTurnos"/>).
/// <para>Decisiones (no estaban especificadas):</para>
/// <list type="bullet">
/// <item>El % de un grupo se calcula sólo con los tramos que tienen objetivo y real; los que tienen objetivo sin real se
/// informan en TramosSinDatos (el objetivo total igual los suma).</item>
/// <item>Si los tramos de un turno suman más minutos que los programados (turno cortado por excepción, cierre tarde),
/// el objetivo se prorratea sobre los programados. Fuera de programación (0) se usa la duración del tramo.</item>
/// <item>Al agrupar o filtrar por producto no se informan paradas ni indisponibilidad (son del turno, no del tramo).</item>
/// <item>T-803 · Microcortes (paradas de cámara que no llegaron al umbral de detención, <see cref="DetencionCamaraReglas.SuperaUmbral(ParadaCamara)"/>,
/// por su <c>TurnoProduccionId</c>): por turno, día, línea y en el total, todos los del turno; al agrupar o filtrar por
/// producto, los que EMPEZARON dentro de un tramo de ese producto (las paradas tienen hora, a diferencia de las
/// detenciones); "Sin producto (a completar)" lleva los de los turnos sin tramos. Null (no 0) si ninguna línea de la fila
/// tiene cámara de conteo.</item>
/// </list>
/// </summary>
public class IndicadoresService : IIndicadoresService
{
    /// <summary>Período máximo de una consulta de presupuesto vs. real (días).</summary>
    public const int DiasMaximos = 366;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly Func<DateTimeOffset> _ahora;

    public IndicadoresService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta)
        : this(unitOfWork, zonaPlanta, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Con reloj inyectable, para los tests.</summary>
    public IndicadoresService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, Func<DateTimeOffset> ahora)
    {
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _ahora = ahora;
    }

    // ===================== Modelo de cálculo =====================

    private sealed record MagnitudCalculada(string Magnitud, decimal? Objetivo, RealProduccion Real, ProduccionTurno? Vigente, bool TieneCamara);

    private sealed record TramoCalculado(
        TurnoProduccionProducto Tramo, bool Abierto, decimal MinutosTramo, decimal MinutosObjetivo,
        IReadOnlyDictionary<string, MagnitudCalculada> Magnitudes, VelocidadTramo? Velocidad, DateTimeOffset Fin)
    {
        public bool Contiene(DateTimeOffset momento) => momento >= Tramo.Desde && momento < Fin;
    }

    /// <summary>Microcortes: inicio y duración de cada parada de cámara del turno que no llegó al umbral.</summary>
    private sealed record TurnoCalculado(
        TurnoProduccion Turno, int MinutosProgramados, int MinutosParada, IReadOnlyList<TramoCalculado> Tramos,
        bool ConCamara, IReadOnlyList<(DateTimeOffset Inicio, int Segundos)> Microcortes);

    private async Task<List<TurnoCalculado>> CalcularAsync(IReadOnlyList<TurnoProduccion> turnos, CancellationToken cancellationToken)
    {
        if (turnos.Count == 0)
            return [];
        var ahora = _ahora();
        var lineas = turnos.Select(t => t.LineaId).ToHashSet();
        var camaras = (await _unitOfWork.Camaras.GetOficialesAsync(cancellationToken))
            .Where(c => lineas.Contains(c.LineaId) && c.Magnitud != null)
            .GroupBy(c => (c.LineaId, c.Magnitud!))
            .ToDictionary(g => g.Key, g => g.First());

        var tramosVivos = turnos.SelectMany(t => t.Productos.Where(p => !p.Anulado).Select(p => (Turno: t, Tramo: p))).ToList();
        IReadOnlyList<MinutoConteo> minutos = [];
        if (tramosVivos.Count > 0 && camaras.Count > 0)
        {
            minutos = await _unitOfWork.ConteosCamaraMinuto.GetMinutosAsync(
                camaras.Values.Select(c => c.CamaraId).ToList(),
                tramosVivos.Min(x => x.Tramo.Desde), tramosVivos.Max(x => FinTramo(x.Turno, x.Tramo, ahora)), cancellationToken);
        }
        var paradas = await _unitOfWork.Detenciones.GetMinutosQueCuentanPorTurnoAsync(turnos.Select(t => t.TurnoProduccionId).ToList(), cancellationToken);
        var programados = await MinutosProgramadosTurnos.CalcularAsync(_unitOfWork, turnos, cancellationToken);
        var lineasConCamara = (await _unitOfWork.Camaras.GetListadoAsync(null, incluirInactivas: true, cancellationToken))
            .Where(c => c.EsConteo).Select(c => c.LineaId).ToHashSet();
        var microcortes = (await _unitOfWork.ParadasCamara.GetPorTurnosAsync(turnos.Select(t => t.TurnoProduccionId).ToList(), cancellationToken))
            .Where(p => !DetencionCamaraReglas.SuperaUmbral(p))
            .GroupBy(p => p.TurnoProduccionId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<(DateTimeOffset, int)>)g.Select(p => (p.FechaHoraInicio, p.DuracionSegundos)).ToList());

        var resultado = new List<TurnoCalculado>();
        foreach (var tp in turnos)
        {
            var minutosProgramados = programados.GetValueOrDefault(tp.TurnoProduccionId);
            var vivos = tp.Productos.Where(p => !p.Anulado).OrderBy(p => p.Desde).ToList();
            var duraciones = vivos.Select(p => ProduccionOficialReglas.MinutosTramo(p.Desde, FinTramo(tp, p, ahora))).ToList();
            var paraObjetivo = IndicadoresProduccionCalculador.MinutosParaObjetivo(duraciones, minutosProgramados);

            var tramos = new List<TramoCalculado>();
            for (var i = 0; i < vivos.Count; i++)
            {
                var tramo = vivos[i];
                var abierto = tramo.Hasta == null;
                var fin = FinTramo(tp, tramo, ahora);
                var magnitudes = new Dictionary<string, MagnitudCalculada>();
                foreach (var magnitud in IndicadoresProduccionCalculador.MagnitudesConPresupuesto)
                {
                    var objetivoTurno = magnitud == MagnitudProduccion.Mangas ? tramo.MangasObjetivoTurno : tramo.BolsonesObjetivoTurno;
                    var objetivo = IndicadoresProduccionCalculador.ObjetivoTramo(objetivoTurno, tramo.MinutosTurnoReferencia, paraObjetivo[i]);
                    var camara = camaras.GetValueOrDefault((tp.LineaId, magnitud));
                    ProduccionTurno? vigente = null;
                    RealProduccion real;
                    if (abierto)
                    {
                        if (camara == null)
                        {
                            real = RealProduccion.SinDatos;
                        }
                        else
                        {
                            var (cantidad, conDatos) = ProduccionOficialReglas.ConteoDelTramo(minutos, camara.CamaraId, tramo.Desde, fin);
                            real = ProduccionOficialReglas.Parcial(cantidad, conDatos);
                        }
                    }
                    else
                    {
                        vigente = ProduccionOficialReglas.Vigente(tramo.Producciones, magnitud);
                        real = ProduccionOficialReglas.RealParaIndicadores(vigente);
                    }
                    magnitudes[magnitud] = new MagnitudCalculada(magnitud, objetivo, real, vigente, camara != null);
                }

                var camaraMangas = camaras.GetValueOrDefault((tp.LineaId, MagnitudProduccion.Mangas));
                var velocidad = camaraMangas == null
                    ? null
                    : IndicadoresProduccionCalculador.VelocidadDelTramo(minutos, camaraMangas.CamaraId, tramo.Desde, fin, tramo.MetrosPorManga);
                tramos.Add(new TramoCalculado(tramo, abierto, duraciones[i], paraObjetivo[i], magnitudes, velocidad, fin));
            }
            resultado.Add(new TurnoCalculado(tp, minutosProgramados, paradas.GetValueOrDefault(tp.TurnoProduccionId), tramos,
                lineasConCamara.Contains(tp.LineaId), microcortes.GetValueOrDefault(tp.TurnoProduccionId) ?? []));
        }
        return resultado;
    }

    /// <summary>Fin del tramo para el cálculo: su Hasta; abierto, ahora (o el fin real del turno si ya cerró).</summary>
    private static DateTimeOffset FinTramo(TurnoProduccion tp, TurnoProduccionProducto tramo, DateTimeOffset ahora)
    {
        var fin = tramo.Hasta ?? tp.FinReal ?? ahora;
        return fin > tramo.Desde ? fin : tramo.Desde;
    }

    private static ComparacionProduccionDto Dto(string magnitud, ComparacionProduccion c) => new()
    {
        Magnitud = magnitud,
        Objetivo = c.Objetivo,
        Real = c.Real,
        Porcentaje = c.Porcentaje,
        Estado = c.Estado,
        Provisorio = c.Provisorio,
        TramosSinDatos = c.TramosSinDatos,
        TramosSinObjetivo = c.TramosSinObjetivo
    };

    /// <summary>
    /// Real vs. objetivo de una magnitud sobre los tramos elegidos de los turnos. Un turno sin tramos ("a completar")
    /// entra como un ítem sin objetivo, salvo que se pida sólo un producto.
    /// </summary>
    private static ComparacionProduccion Comparar(
        IEnumerable<TurnoCalculado> turnos, string magnitud, Func<TramoCalculado, bool> tramoIncluido, bool incluirSinTramos)
    {
        var items = new List<(decimal? Objetivo, RealProduccion Real)>();
        foreach (var t in turnos)
        {
            if (t.Tramos.Count == 0)
            {
                if (incluirSinTramos)
                    items.Add((null, RealProduccion.SinDatos));
                continue;
            }
            items.AddRange(t.Tramos.Where(tramoIncluido).Select(x => (x.Magnitudes[magnitud].Objetivo, x.Magnitudes[magnitud].Real)));
        }
        return IndicadoresProduccionCalculador.Comparar(items);
    }

    private static FilaPresupuestoRealDto Fila(
        string clave, string etiqueta, IReadOnlyCollection<TurnoCalculado> turnos, Func<TramoCalculado, bool> tramoIncluido,
        bool incluirSinTramos, bool incluirParadas)
    {
        var mangas = Dto(MagnitudProduccion.Mangas, Comparar(turnos, MagnitudProduccion.Mangas, tramoIncluido, incluirSinTramos));
        var fila = new FilaPresupuestoRealDto
        {
            Clave = clave,
            Etiqueta = etiqueta,
            Turnos = turnos.Count,
            IncluyeAbiertos = turnos.Any(t => t.Turno.EstaAbierto),
            Mangas = mangas,
            Bolsones = Dto(MagnitudProduccion.Bolsones, Comparar(turnos, MagnitudProduccion.Bolsones, tramoIncluido, incluirSinTramos)),
            Eficiencia = mangas,
            VelocidadMetrosMinuto = IndicadoresProduccionCalculador.Velocidad(
                turnos.SelectMany(t => t.Tramos.Where(tramoIncluido)).Where(x => x.Velocidad != null).Select(x => x.Velocidad!))
        };
        if (incluirParadas)
        {
            fila.MinutosProgramados = turnos.Sum(t => t.MinutosProgramados);
            fila.MinutosParadaQueCuentan = turnos.Sum(t => t.MinutosParada);
            fila.Indisponibilidad = IndicadoresProduccionCalculador.Indisponibilidad(fila.MinutosParadaQueCuentan.Value, fila.MinutosProgramados.Value);
        }
        if (turnos.Any(t => t.ConCamara))
        {
            // Todo el turno, o (por producto) los que empezaron en un tramo incluido; los turnos sin tramos, si se piden.
            var lista = turnos.SelectMany(t =>
                incluirParadas || (t.Tramos.Count == 0 && incluirSinTramos)
                    ? t.Microcortes
                    : t.Microcortes.Where(m => t.Tramos.Where(tramoIncluido).Any(x => x.Contiene(m.Inicio)))).ToList();
            fila.Microcortes = lista.Count;
            fila.SegundosMicrocortes = lista.Sum(m => m.Segundos);
            fila.MinutosMicrocortes = (int)Math.Round(fila.SegundosMicrocortes.Value / 60m, MidpointRounding.AwayFromZero);
        }
        return fila;
    }

    // ===================== Detalle del turno =====================

    public async Task<Result<IndicadoresTurnoDto>> ObtenerTurnoAsync(long turnoProduccionId, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return Result<IndicadoresTurnoDto>.Failure("NOT_FOUND", "El turno de producción no existe.");

        var calculado = (await CalcularAsync([tp], cancellationToken)).Single();
        var fila = Fila(tp.TurnoProduccionId.ToString(), string.Empty, [calculado], _ => true, incluirSinTramos: true, incluirParadas: true);

        var dto = new IndicadoresTurnoDto
        {
            TurnoProduccionId = tp.TurnoProduccionId,
            LineaId = tp.LineaId,
            LineaNombre = tp.Linea?.Nombre ?? string.Empty,
            TurnoId = tp.TurnoId,
            TurnoCodigo = tp.Turno?.Codigo ?? string.Empty,
            TurnoNombre = tp.Turno?.Nombre ?? string.Empty,
            FechaProduccion = tp.FechaProduccion,
            Estado = tp.Estado.ToString(),
            Abierto = tp.EstaAbierto,
            InicioProgramado = HoraPlanta.En(tp.InicioProgramado, _zonaPlanta)!.Value,
            FinProgramado = HoraPlanta.En(tp.FinProgramado, _zonaPlanta)!.Value,
            MinutosProgramados = calculado.MinutosProgramados,
            MinutosParadaQueCuentan = calculado.MinutosParada,
            Indisponibilidad = fila.Indisponibilidad,
            ACompletar = calculado.Tramos.Count == 0,
            Mangas = fila.Mangas,
            Bolsones = fila.Bolsones,
            Eficiencia = fila.Eficiencia,
            VelocidadMetrosMinuto = fila.VelocidadMetrosMinuto
        };
        foreach (var t in calculado.Tramos)
        {
            var tramo = t.Tramo;
            dto.Tramos.Add(new TramoIndicadorDto
            {
                TurnoProduccionProductoId = tramo.TurnoProduccionProductoId,
                ProductoMaestroId = tramo.ProductoMaestroId,
                ProductoCodigo = tramo.ProductoMaestro?.Codigo ?? string.Empty,
                ProductoNombre = tramo.ProductoMaestro?.Nombre ?? string.Empty,
                Desde = HoraPlanta.En(tramo.Desde, _zonaPlanta)!.Value,
                Hasta = HoraPlanta.En(tramo.Hasta, _zonaPlanta),
                Abierto = t.Abierto,
                MinutosTramo = Math.Round(t.MinutosTramo, 1),
                MinutosObjetivo = Math.Round(t.MinutosObjetivo, 1),
                MangasObjetivoTurno = tramo.MangasObjetivoTurno,
                BolsonesObjetivoTurno = tramo.BolsonesObjetivoTurno,
                MinutosTurnoReferencia = tramo.MinutosTurnoReferencia,
                MetrosPorManga = tramo.MetrosPorManga,
                Mangas = MagnitudDto(t.Magnitudes[MagnitudProduccion.Mangas]),
                Bolsones = MagnitudDto(t.Magnitudes[MagnitudProduccion.Bolsones]),
                Eficiencia = Dto(MagnitudProduccion.Mangas, IndicadoresProduccionCalculador.Comparar(
                    [(t.Magnitudes[MagnitudProduccion.Mangas].Objetivo, t.Magnitudes[MagnitudProduccion.Mangas].Real)])),
                VelocidadMetrosMinuto = t.Velocidad?.MetrosPorMinuto,
                MinutosMarcha = t.Velocidad?.MinutosMarcha ?? 0,
                Producciones = tramo.Producciones
                    .OrderBy(p => p.FechaHoraRegistro).ThenBy(p => p.ProduccionTurnoId)
                    .Select(p => ProduccionTurnoDto.De(p, _zonaPlanta))
                    .ToList()
            });
        }
        return Result<IndicadoresTurnoDto>.Success(dto);
    }

    private MagnitudTramoDto MagnitudDto(MagnitudCalculada m) => new()
    {
        Magnitud = m.Magnitud,
        Objetivo = m.Objetivo is { } o ? Math.Round(o, 1, MidpointRounding.AwayFromZero) : null,
        Real = m.Real.Cantidad,
        EstadoReal = m.Real.Estado,
        Provisorio = m.Real.Provisorio,
        TieneCamaraOficial = m.TieneCamara,
        Vigente = m.Vigente == null ? null : ProduccionTurnoDto.De(m.Vigente, _zonaPlanta)
    };

    // ===================== Presupuesto vs. real =====================

    public async Task<Result<PresupuestoRealDto>> ObtenerProduccionAsync(IndicadoresProduccionFiltro filtro, CancellationToken cancellationToken = default)
    {
        var agrupar = string.IsNullOrWhiteSpace(filtro.Agrupar) ? AgrupacionIndicadores.Turno : filtro.Agrupar.Trim().ToLowerInvariant();
        if (!AgrupacionIndicadores.Todas.Contains(agrupar))
            return Result<PresupuestoRealDto>.Failure("VALIDATION", "agrupar tiene que ser turno, dia, producto o linea.");
        if (filtro.Desde == default || filtro.Hasta == default)
            return Result<PresupuestoRealDto>.Failure("VALIDATION", "Indicá desde y hasta (fecha de producción).");
        if (filtro.Hasta < filtro.Desde)
            return Result<PresupuestoRealDto>.Failure("VALIDATION", "La fecha hasta no puede ser anterior a la fecha desde.");
        if (filtro.Hasta.DayNumber - filtro.Desde.DayNumber + 1 > DiasMaximos)
            return Result<PresupuestoRealDto>.Failure("VALIDATION", $"El período no puede pasar de {DiasMaximos} días.");

        var turnos = await _unitOfWork.TurnosProduccion.GetPorFechasAsync(
            filtro.Desde, filtro.Hasta, filtro.LineaId is { } l ? [l] : null, cancellationToken);
        var productoId = filtro.ProductoMaestroId;
        if (productoId != null)
            turnos = turnos.Where(t => t.Productos.Any(p => !p.Anulado && p.ProductoMaestroId == productoId)).ToList();
        var ordenados = turnos
            .OrderBy(t => t.FechaProduccion).ThenBy(t => t.Linea?.Orden).ThenBy(t => t.Linea?.Nombre)
            .ThenBy(t => t.Turno?.Orden).ThenBy(t => t.Turno?.HoraInicio)
            .ToList();
        var calculados = await CalcularAsync(ordenados, cancellationToken);

        Func<TramoCalculado, bool> delProducto = productoId == null ? _ => true : x => x.Tramo.ProductoMaestroId == productoId;
        var sinProducto = productoId == null;
        var conParadas = productoId == null && agrupar != AgrupacionIndicadores.Producto;

        var dto = new PresupuestoRealDto
        {
            Desde = filtro.Desde,
            Hasta = filtro.Hasta,
            LineaId = filtro.LineaId,
            ProductoMaestroId = productoId,
            Agrupar = agrupar
        };

        switch (agrupar)
        {
            case AgrupacionIndicadores.Turno:
                foreach (var t in calculados)
                {
                    var tp = t.Turno;
                    var fila = Fila(tp.TurnoProduccionId.ToString(), $"{tp.FechaProduccion:dd/MM/yyyy} · {tp.Linea?.Nombre} · {tp.Turno?.Nombre}",
                        [t], delProducto, sinProducto, conParadas);
                    fila.TurnoProduccionId = tp.TurnoProduccionId;
                    fila.LineaId = tp.LineaId;
                    fila.LineaNombre = tp.Linea?.Nombre;
                    fila.FechaProduccion = tp.FechaProduccion;
                    fila.TurnoCodigo = tp.Turno?.Codigo;
                    fila.TurnoNombre = tp.Turno?.Nombre;
                    dto.Filas.Add(fila);
                }
                break;
            case AgrupacionIndicadores.Dia:
                foreach (var g in calculados.GroupBy(t => t.Turno.FechaProduccion).OrderBy(g => g.Key))
                {
                    var fila = Fila(g.Key.ToString("yyyy-MM-dd"), g.Key.ToString("dd/MM/yyyy"), g.ToList(), delProducto, sinProducto, conParadas);
                    fila.FechaProduccion = g.Key;
                    dto.Filas.Add(fila);
                }
                break;
            case AgrupacionIndicadores.Linea:
                foreach (var g in calculados.GroupBy(t => t.Turno.LineaId)
                             .OrderBy(g => g.First().Turno.Linea?.Orden).ThenBy(g => g.First().Turno.Linea?.Nombre))
                {
                    var linea = g.First().Turno.Linea;
                    var fila = Fila(g.Key.ToString(), linea?.Nombre ?? $"Línea {g.Key}", g.ToList(), delProducto, sinProducto, conParadas);
                    fila.LineaId = g.Key;
                    fila.LineaNombre = linea?.Nombre;
                    dto.Filas.Add(fila);
                }
                break;
            default:
                var productos = calculados
                    .SelectMany(t => t.Tramos.Select(x => x.Tramo))
                    .Where(p => productoId == null || p.ProductoMaestroId == productoId)
                    .GroupBy(p => p.ProductoMaestroId)
                    .Select(g => g.First().ProductoMaestro)
                    .OrderBy(p => p?.Orden).ThenBy(p => p?.Nombre)
                    .ToList();
                foreach (var producto in productos)
                {
                    var id = producto.ProductoMaestroId;
                    var conProducto = calculados.Where(t => t.Tramos.Any(x => x.Tramo.ProductoMaestroId == id)).ToList();
                    var fila = Fila(id.ToString(), producto.Nombre, conProducto, x => x.Tramo.ProductoMaestroId == id, false, false);
                    fila.ProductoMaestroId = id;
                    fila.ProductoNombre = producto.Nombre;
                    dto.Filas.Add(fila);
                }
                var aCompletar = calculados.Where(t => t.Tramos.Count == 0).ToList();
                if (sinProducto && aCompletar.Count > 0)
                    dto.Filas.Add(Fila("0", "Sin producto (a completar)", aCompletar, _ => true, true, false));
                break;
        }

        dto.Total = Fila("total", "Total", calculados, delProducto, sinProducto, productoId == null);
        return Result<PresupuestoRealDto>.Success(dto);
    }
}
