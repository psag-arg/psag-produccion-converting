using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Conteo de producción y paradas por cámara (T-610). Todas las paradas quedan en ParadasCamara (log, incluye
/// microcortes). Si la cámara tiene "crea detenciones" en Maestros, la parada que llega al umbral (5 min por
/// defecto) da de alta una detención pendiente de declaración; idempotente por identificador de parada.
/// El cruce con detenciones se calcula al consultar.
/// </summary>
public class ConteoCamaraService : IConteoCamaraService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<ConteoCamaraService> _logger;

    public ConteoCamaraService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<ConteoCamaraService> logger)
    {
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
    }

    // ===================== Ingesta =====================

    public async Task<Result<IngestaConteoCamaraRespuesta>> IngestarAsync(
        IngestaConteoCamaraRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PuntoControl))
            return Fallo("Falta puntoControl.");
        var minutos = request.Minutos ?? [];
        var paradas = request.Paradas ?? [];
        if (minutos.Count > ConteoCamaraReglas.MaximoMinutosPorLote || paradas.Count > ConteoCamaraReglas.MaximoParadasPorLote)
            return Fallo($"El lote admite hasta {ConteoCamaraReglas.MaximoMinutosPorLote} minutos y {ConteoCamaraReglas.MaximoParadasPorLote} paradas.");

        var codigo = CalidadReglas.NormalizarCodigo(request.PuntoControl);
        var punto = await _unitOfWork.PuntosControlCalidad.GetByCodigoAsync(codigo, cancellationToken);
        if (punto == null || !punto.Activo)
            return Fallo($"No existe el punto de control {codigo} o está inactivo. Cargalo en Maestros → Calidad.");

        var ahora = DateTimeOffset.UtcNow;

        // Validación completa antes de guardar: el lote entra entero o no entra.
        foreach (var m in minutos)
        {
            if (!Enum.TryParse<EstadoCarrilCamara>(m.Estado, ignoreCase: true, out var estado) || !Enum.IsDefined(estado))
                return Fallo($"Estado de minuto no válido: {m.Estado}.");
            var motivo = ConteoCamaraReglas.MotivoMinutoInvalido(m.Minuto, m.Carril, m.Rollos,
                m.SegundosMarcha, m.SegundosParada, m.SegundosAcumulacion, m.SegundosSinSenal);
            if (motivo != null)
                return Fallo($"Minuto {m.Minuto:yyyy-MM-dd HH:mm} carril {m.Carril}: {motivo}");
            if (CalidadReglas.EsFechaFutura(m.Minuto, ahora))
                return Fallo($"Minuto {m.Minuto:yyyy-MM-dd HH:mm}: es posterior a ahora (revisar el reloj del servidor).");
        }
        foreach (var p in paradas)
        {
            if (!Enum.TryParse<TipoParadaCamara>(p.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
                return Fallo($"Tipo de parada no válido: {p.Tipo}.");
            var motivo = ConteoCamaraReglas.MotivoParadaInvalida(p.Inicio, p.Fin, p.Carriles ?? [], p.IdentificadorExterno);
            if (motivo != null)
                return Fallo($"Parada {p.IdentificadorExterno}: {motivo}");
            if (CalidadReglas.EsFechaFutura(p.Fin, ahora))
                return Fallo($"Parada {p.IdentificadorExterno}: termina después de ahora (revisar el reloj del servidor).");
        }

        var enCurso = request.ParadasEnCurso ?? [];
        var alertas = request.Alertas ?? [];
        if (enCurso.Count > ConteoCamaraReglas.MaximoParadasPorLote || alertas.Count > ConteoCamaraReglas.MaximoParadasPorLote)
            return Fallo($"El lote admite hasta {ConteoCamaraReglas.MaximoParadasPorLote} paradas en curso y alertas.");
        foreach (var p in enCurso)
        {
            if (!Enum.TryParse<TipoParadaCamara>(p.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
                return Fallo($"Tipo de parada en curso no válido: {p.Tipo}.");
            var motivo = ConteoCamaraReglas.MotivoParadaInvalida(p.Inicio, p.Hasta, [1], p.IdentificadorExterno);
            if (motivo != null)
                return Fallo($"Parada en curso {p.IdentificadorExterno}: {motivo}");
            if (CalidadReglas.EsFechaFutura(p.Hasta, ahora))
                return Fallo($"Parada en curso {p.IdentificadorExterno}: es posterior a ahora (revisar el reloj del servidor).");
        }
        foreach (var a in alertas)
        {
            if (!Enum.TryParse<TipoAlertaCamara>(a.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
                return Fallo($"Tipo de alerta no válido: {a.Tipo}.");
            if (a.Fin is { } fin && fin < a.Inicio)
                return Fallo($"Alerta {a.Tipo}: el fin es anterior al inicio.");
            if (CalidadReglas.EsFechaFutura(a.Fin ?? a.Inicio, ahora))
                return Fallo($"Alerta {a.Tipo}: es posterior a ahora (revisar el reloj del servidor).");
            if (a.Detalle?.Length > 500)
                return Fallo("El detalle de la alerta admite hasta 500 caracteres.");
        }

        var respuesta = new IngestaConteoCamaraRespuesta();
        var turnos = new Dictionary<TimeOnly, Turno?>();

        // Minutos: únicos por punto + minuto + carril (también dentro del mismo lote).
        if (minutos.Count > 0)
        {
            var existentes = await _unitOfWork.ConteosCamaraMinuto.GetClavesExistentesAsync(
                punto.PuntoControlCalidadId, minutos.Min(m => m.Minuto), minutos.Max(m => m.Minuto), cancellationToken);
            foreach (var m in minutos)
            {
                var clave = (m.Minuto.ToUniversalTime(), (byte)m.Carril);
                if (!existentes.Add(clave))
                {
                    respuesta.MinutosDuplicados++;
                    continue;
                }
                var turno = await TurnoAsync(m.Minuto, turnos, cancellationToken);
                await _unitOfWork.ConteosCamaraMinuto.AddAsync(new ConteoCamaraMinuto
                {
                    Minuto = m.Minuto,
                    LineaId = punto.LineaId,
                    PuntoControlCalidadId = punto.PuntoControlCalidadId,
                    TurnoId = turno?.TurnoId,
                    Carril = (byte)m.Carril,
                    Rollos = Math.Round(m.Rollos, 1),
                    SegundosMarcha = (short)m.SegundosMarcha,
                    SegundosParada = (short)m.SegundosParada,
                    SegundosAcumulacion = (short)m.SegundosAcumulacion,
                    SegundosSinSenal = (short)m.SegundosSinSenal,
                    Estado = Enum.Parse<EstadoCarrilCamara>(m.Estado, ignoreCase: true),
                    FechaHoraCreacion = ahora
                }, cancellationToken);
                respuesta.MinutosNuevos++;
            }
        }

        // Paradas: únicas por identificador externo.
        if (paradas.Count > 0)
        {
            var ids = paradas.Select(p => p.IdentificadorExterno.Trim()).ToList();
            var existentes = await _unitOfWork.ParadasCamara.GetIdentificadoresExistentesAsync(ids, cancellationToken);
            foreach (var p in paradas)
            {
                var id = p.IdentificadorExterno.Trim();
                if (!existentes.Add(id))
                {
                    respuesta.ParadasDuplicadas++;
                    continue;
                }
                var turno = await TurnoAsync(p.Inicio, turnos, cancellationToken);
                await _unitOfWork.ParadasCamara.AddAsync(new ParadaCamara
                {
                    LineaId = punto.LineaId,
                    PuntoControlCalidadId = punto.PuntoControlCalidadId,
                    TurnoId = turno?.TurnoId,
                    FechaHoraInicio = p.Inicio,
                    FechaHoraFin = p.Fin,
                    DuracionSegundos = (int)Math.Round((p.Fin - p.Inicio).TotalSeconds),
                    Tipo = Enum.Parse<TipoParadaCamara>(p.Tipo, ignoreCase: true),
                    Carriles = ConteoCamaraReglas.CarrilesTexto(p.Carriles),
                    InicioIncierto = p.InicioIncierto,
                    FinIncierto = p.FinIncierto,
                    IdentificadorExterno = id,
                    FechaHoraCreacion = ahora
                }, cancellationToken);
                respuesta.ParadasNuevas++;
            }
        }

        var alertasCambiadas = await GuardarAlertasAsync(punto, codigo, alertas, ahora, respuesta, cancellationToken);

        if (respuesta.MinutosNuevos + respuesta.ParadasNuevas + alertasCambiadas > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (punto.CreaDetenciones)
            await CrearDetencionesAsync(punto, paradas, enCurso, turnos, respuesta, cancellationToken);

        _logger.LogInformation(
            "Conteo por cámara {Punto}: {MinNuevos} minutos nuevos ({MinDup} repetidos), {ParNuevas} paradas nuevas ({ParDup} repetidas), {DetNuevas} detenciones automáticas, {AlNuevas} alertas nuevas, {AlCerradas} cerradas",
            codigo, respuesta.MinutosNuevos, respuesta.MinutosDuplicados, respuesta.ParadasNuevas, respuesta.ParadasDuplicadas,
            respuesta.DetencionesCreadas, respuesta.AlertasNuevas, respuesta.AlertasCerradas);
        return Result<IngestaConteoCamaraRespuesta>.Success(respuesta);
    }

    /// <summary>
    /// Detenciones automáticas: una por parada (abierta o cerrada) que llegó al umbral de la cámara. Si ya existe
    /// (reenvío, o la parada en curso ya la había creado), no se duplica; con la parada cerrada se guarda la hora de
    /// arranque detectada como sugerencia. Cada alta se guarda por separado: el código DET-... sale de la base.
    /// </summary>
    private async Task CrearDetencionesAsync(
        PuntoControlCalidad punto, List<ParadaCamaraIngesta> cerradas, List<ParadaEnCursoIngesta> enCurso,
        Dictionary<TimeOnly, Turno?> turnos, IngestaConteoCamaraRespuesta respuesta, CancellationToken cancellationToken)
    {
        var umbral = punto.MinutosUmbralDetencion;
        var candidatas = new Dictionary<string, (DateTimeOffset Inicio, DateTimeOffset? Arranque, bool InicioIncierto)>(StringComparer.Ordinal);
        foreach (var p in enCurso.Where(p => DetencionCamaraReglas.SuperaUmbral(p.Inicio, p.Hasta, umbral)))
            candidatas[p.IdentificadorExterno.Trim()] = (p.Inicio, null, p.InicioIncierto);
        foreach (var p in cerradas.Where(p => DetencionCamaraReglas.SuperaUmbral(p.Inicio, p.Fin, umbral)))
            candidatas[p.IdentificadorExterno.Trim()] = (p.Inicio, p.FinIncierto ? null : p.Fin, p.InicioIncierto);
        if (candidatas.Count == 0)
            return;

        var existentes = (await _unitOfWork.Detenciones.GetPorParadasCamaraAsync(candidatas.Keys.ToList(), cancellationToken))
            .ToDictionary(d => d.IdentificadorParadaCamara!, StringComparer.Ordinal);
        List<UsuarioLinea>? asignaciones = null;

        foreach (var (id, c) in candidatas.OrderBy(c => c.Value.Inicio))
        {
            if (existentes.TryGetValue(id, out var existente))
            {
                respuesta.DetencionesExistentes++;
                if (c.Arranque is { } arranque && existente.FechaHoraArranqueDetectada == null)
                {
                    // Sugerencia para el operador; no toca FechaHoraArranque ni los minutos (los declara él).
                    existente.FechaHoraArranqueDetectada = arranque;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                continue;
            }

            var turno = await TurnoAsync(c.Inicio, turnos, cancellationToken);
            if (turno == null)
            {
                respuesta.DetencionesSinTurno++;
                _logger.LogWarning("La parada {Id} de la cámara {Punto} llegó al umbral pero no hay turno para esa hora: no se creó la detención", id, punto.Codigo);
                continue;
            }

            asignaciones ??= (await _unitOfWork.UsuariosLineas.GetByLineaIdAsync(punto.LineaId, cancellationToken)).ToList();
            var maquinista = DetencionCamaraReglas.ElegirMaquinista(asignaciones, turno.TurnoId);
            var inicioPlanta = HoraPlanta.En(c.Inicio, _zonaPlanta)!.Value;
            var sistema = DetencionCamaraReglas.UsuarioSistemaCamara;

            var detencion = new Detencion
            {
                Codigo = await _unitOfWork.Detenciones.GenerarCodigoDetencionAsync(cancellationToken),
                LineaId = punto.LineaId,
                TurnoId = turno.TurnoId,
                UsuarioLineaId = maquinista.Elegido?.UsuarioLineaId,
                Fecha = DateOnly.FromDateTime(inicioPlanta.DateTime),
                FechaHoraParada = c.Inicio,
                FechaHoraArranqueDetectada = c.Arranque,
                // Sin hora de fin ni minutos: los declara el operador. 0 hasta entonces (CHECK >= 0).
                OrigenTiempo = OrigenTiempo.CALCULADO,
                MinutosEfectivos = 0,
                Descripcion = string.Empty,
                EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION,
                OrigenRegistro = OrigenRegistroDetencion.CAMARA,
                IdentificadorParadaCamara = id,
                PuntoControlCalidadId = punto.PuntoControlCalidadId,
                CreadoPorUsuarioId = sistema,
                ModificadoPorUsuarioId = sistema
            };
            detencion.HistorialEstados.Add(new DetencionEstadoHistorial
            {
                EstadoAnterior = null,
                EstadoNuevo = EstadoDetencion.PENDIENTE_JUSTIFICACION,
                FechaHora = DateTimeOffset.UtcNow,
                UsuarioId = sistema,
                Observacion = DetencionCamaraReglas.ObservacionAlta(punto.Codigo, inicioPlanta, c.InicioIncierto) + " " + maquinista.Motivo,
                OrigenAccion = "CAMARA"
            });

            await _unitOfWork.Detenciones.AddAsync(detencion, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            existentes[id] = detencion;
            respuesta.DetencionesCreadas++;
            _logger.LogInformation("Detención automática {Codigo} creada por la cámara {Punto} (parada desde {Inicio:HH:mm:ss})",
                detencion.Codigo, punto.Codigo, inicioPlanta);
        }
    }

    /// <summary>Alta o cierre de alertas de cámara. Devuelve cuántas cambiaron (para saber si hay que guardar).</summary>
    private async Task<int> GuardarAlertasAsync(
        PuntoControlCalidad punto, string codigo, List<AlertaCamaraIngesta> alertas, DateTimeOffset ahora,
        IngestaConteoCamaraRespuesta respuesta, CancellationToken cancellationToken)
    {
        if (alertas.Count == 0)
            return 0;
        // La misma alerta puede venir abierta y cerrada en el mismo lote: gana la versión con fin.
        var porId = alertas
            .Select(a => (Id: DetencionCamaraReglas.IdentificadorAlerta(codigo, Enum.Parse<TipoAlertaCamara>(a.Tipo, true), a.Inicio), Alerta: a))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Alerta.Fin.HasValue).Last().Alerta);
        var existentes = (await _unitOfWork.AlertasCamara.GetPorIdentificadoresAsync(porId.Keys.ToList(), cancellationToken))
            .ToDictionary(a => a.IdentificadorExterno, StringComparer.Ordinal);

        var cambios = 0;
        foreach (var (id, a) in porId)
        {
            if (existentes.TryGetValue(id, out var existente))
            {
                if (a.Fin is { } fin && existente.FechaHoraFin == null)
                {
                    existente.FechaHoraFin = fin;
                    existente.FechaHoraActualizacion = ahora;
                    respuesta.AlertasCerradas++;
                    cambios++;
                }
                continue;
            }
            await _unitOfWork.AlertasCamara.AddAsync(new AlertaCamara
            {
                PuntoControlCalidadId = punto.PuntoControlCalidadId,
                LineaId = punto.LineaId,
                Tipo = Enum.Parse<TipoAlertaCamara>(a.Tipo, true),
                FechaHoraInicio = a.Inicio,
                FechaHoraFin = a.Fin,
                IdentificadorExterno = id,
                Detalle = string.IsNullOrWhiteSpace(a.Detalle) ? null : a.Detalle.Trim(),
                FechaHoraCreacion = ahora,
                FechaHoraActualizacion = ahora
            }, cancellationToken);
            respuesta.AlertasNuevas++;
            if (a.Fin != null) respuesta.AlertasCerradas++;
            cambios++;
            _logger.LogWarning("Alerta de cámara {Punto}: {Tipo} desde {Inicio:HH:mm:ss}", codigo, a.Tipo, HoraPlanta.En(a.Inicio, _zonaPlanta));
        }
        return cambios;
    }

    /// <summary>Turno de la hora de planta, como en detenciones y calidad. Null si no hay turno activo: el dato no se pierde.</summary>
    private async Task<Turno?> TurnoAsync(DateTimeOffset instante, Dictionary<TimeOnly, Turno?> cache, CancellationToken ct)
    {
        var hora = TimeOnly.FromDateTime(HoraPlanta.En(instante, _zonaPlanta)!.Value.DateTime);
        hora = new TimeOnly(hora.Hour, hora.Minute);
        if (!cache.TryGetValue(hora, out var turno))
            cache[hora] = turno = await _unitOfWork.Turnos.GetTurnoPorHoraAsync(hora, ct);
        return turno;
    }

    private static Result<IngestaConteoCamaraRespuesta> Fallo(string mensaje) =>
        Result<IngestaConteoCamaraRespuesta>.Failure("VALIDATION", mensaje);

    // ===================== Consultas =====================

    public async Task<Result<ParadasCamaraResultado>> ObtenerParadasAsync(
        ParadasCamaraFiltroRequest filtro, CancellationToken cancellationToken = default)
    {
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _zonaPlanta).Date);
        var hasta = filtro.FechaHasta ?? hoy;
        var desde = filtro.FechaDesde ?? hasta.AddDays(-7);
        if (desde > hasta)
            return Result<ParadasCamaraResultado>.Failure("VALIDATION", "La fecha desde no puede ser posterior a la fecha hasta.");
        if (hasta.DayNumber - desde.DayNumber > 92)
            return Result<ParadasCamaraResultado>.Failure("VALIDATION", "El rango admite hasta 3 meses.");

        var paradas = await _unitOfWork.ParadasCamara.GetRangoAsync(
            InicioDelDia(desde), InicioDelDia(hasta.AddDays(1)), filtro.LineaId, cancellationToken);
        if (filtro.SegundosMinimos > 0)
            paradas = paradas.Where(p => p.DuracionSegundos >= filtro.SegundosMinimos).ToList();
        if (filtro.PuntoControlCalidadId is { } puntoId)
            paradas = paradas.Where(p => p.PuntoControlCalidadId == puntoId).ToList();

        // Detenciones de un día antes y después: una parada de noche puede estar cargada con la fecha del turno.
        var detenciones = (await _unitOfWork.Detenciones.FindAsync(
                d => !d.Eliminada && d.Fecha >= desde.AddDays(-1) && d.Fecha <= hasta.AddDays(1), cancellationToken))
            .ToList();

        var items = new List<ParadaCamaraDto>();
        foreach (var p in paradas)
        {
            var dia = DateOnly.FromDateTime(HoraPlanta.En(p.FechaHoraInicio, _zonaPlanta)!.Value.DateTime);
            var (cruce, codigos) = ConteoCamaraReglas.Cruzar(p, dia, detenciones);
            if (filtro.SoloSinDetencion && cruce != CruceDetencionCamara.SIN_DETENCION)
                continue;
            items.Add(new ParadaCamaraDto
            {
                ParadaCamaraId = p.ParadaCamaraId,
                LineaId = p.LineaId,
                LineaNombre = p.Linea?.Nombre ?? string.Empty,
                PuntoControlCodigo = p.PuntoControl?.Codigo ?? string.Empty,
                TurnoNombre = p.Turno?.Nombre,
                FechaHoraInicio = HoraPlanta.En(p.FechaHoraInicio, _zonaPlanta)!.Value,
                FechaHoraFin = HoraPlanta.En(p.FechaHoraFin, _zonaPlanta)!.Value,
                DuracionSegundos = p.DuracionSegundos,
                Tipo = p.Tipo.ToString(),
                Carriles = p.Carriles,
                InicioIncierto = p.InicioIncierto,
                FinIncierto = p.FinIncierto,
                SuperaUmbral = p.PuntoControl?.CreaDetenciones == true
                    && DetencionCamaraReglas.SuperaUmbral(p.FechaHoraInicio, p.FechaHoraFin, p.PuntoControl.MinutosUmbralDetencion),
                Cruce = cruce.ToString(),
                CodigosDetencion = codigos.ToList()
            });
        }

        var sin = items.Where(i => i.Cruce == nameof(CruceDetencionCamara.SIN_DETENCION)).ToList();
        return Result<ParadasCamaraResultado>.Success(new ParadasCamaraResultado
        {
            Items = items,
            Total = items.Count,
            SinDetencion = sin.Count,
            MinutosSinDetencion = (int)Math.Round(sin.Sum(i => i.DuracionSegundos) / 60.0)
        });
    }

    public async Task<Result<ProduccionCamaraDto>> ObtenerProduccionAsync(
        DateOnly fecha, int? lineaId, int? puntoControlCalidadId = null, CancellationToken cancellationToken = default)
    {
        var minutos = await _unitOfWork.ConteosCamaraMinuto.GetRangoAsync(
            InicioDelDia(fecha), InicioDelDia(fecha.AddDays(1)), lineaId, cancellationToken);
        if (puntoControlCalidadId is { } puntoId)
            minutos = minutos.Where(m => m.PuntoControlCalidadId == puntoId).ToList();
        var nombresTurno = (await _unitOfWork.Turnos.GetAllAsync(cancellationToken)).ToDictionary(t => t.TurnoId, t => t.Nombre);

        var carriles = minutos.Select(m => (int)m.Carril).Distinct().OrderBy(c => c).ToList();
        var horas = minutos
            .GroupBy(m =>
            {
                var local = HoraPlanta.En(m.Minuto, _zonaPlanta)!.Value;
                return new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
            })
            .OrderBy(g => g.Key)
            .Select(g => new ProduccionCamaraHoraDto
            {
                Hora = g.Key,
                RollosPorCarril = g.GroupBy(m => (int)m.Carril).ToDictionary(c => c.Key, c => Math.Round(c.Sum(m => m.Rollos))),
                TotalRollos = Math.Round(g.Sum(m => m.Rollos)),
                MinutosCarrilMarcha = g.Count(m => m.Estado == EstadoCarrilCamara.MARCHA),
                MinutosCarrilConDatos = g.Count(m => m.Estado != EstadoCarrilCamara.SIN_SENAL)
            })
            .ToList();

        var porMinuto = minutos.GroupBy(m => m.Minuto).ToList();
        return Result<ProduccionCamaraDto>.Success(new ProduccionCamaraDto
        {
            Fecha = fecha,
            LineaId = lineaId,
            Carriles = carriles,
            Horas = horas,
            TotalRollos = Math.Round(minutos.Sum(m => m.Rollos)),
            MinutosConDatos = porMinuto.Count(g => g.Any(m => m.Estado != EstadoCarrilCamara.SIN_SENAL)),
            MinutosMarcha = porMinuto.Count(g => g.All(m => m.Estado == EstadoCarrilCamara.MARCHA)),
            Turnos = minutos
                .GroupBy(m => m.TurnoId)
                .Select(g => new ProduccionCamaraTurnoDto
                {
                    TurnoId = g.Key,
                    TurnoNombre = g.Key is { } t ? nombresTurno.GetValueOrDefault(t, $"Turno {t}") : "Sin turno",
                    Total = Math.Round(g.Sum(m => m.Rollos)),
                    MinutosConDatos = MinutosConDatos(g),
                    MinutosMarcha = MinutosMarcha(g)
                })
                .OrderBy(t => t.TurnoNombre)
                .ToList()
        });
    }

    /// <summary>Minutos (no minutos-carril) con datos de algún carril: lo mismo que suma la producción por turno.</summary>
    private static int MinutosConDatos(IEnumerable<ConteoCamaraMinuto> minutos) =>
        minutos.GroupBy(m => m.Minuto).Count(x => x.Any(m => m.Estado != EstadoCarrilCamara.SIN_SENAL));

    /// <summary>Minutos con todos los carriles en marcha.</summary>
    private static int MinutosMarcha(IEnumerable<ConteoCamaraMinuto> minutos) =>
        minutos.GroupBy(m => m.Minuto).Count(x => x.All(m => m.Estado == EstadoCarrilCamara.MARCHA));

    // ===================== Eficiencia por turno =====================

    public async Task<Result<EficienciaCamaraDiaDto>> ObtenerEficienciaAsync(
        DateOnly fecha, int? lineaId, DateTimeOffset? ahora = null, CancellationToken cancellationToken = default)
    {
        var momento = ahora ?? DateTimeOffset.UtcNow;
        var turnos = (await _unitOfWork.Turnos.GetAllAsync(cancellationToken))
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden).ThenBy(t => t.HoraInicio)
            .ToList();

        // Los turnos del día pueden terminar al día siguiente (nocturno): se leen dos días. Las paradas, desde el
        // día anterior, para recortar las que vienen del turno de antes.
        var desde = InicioDelDia(fecha);
        var hasta = InicioDelDia(fecha.AddDays(2));
        var minutos = await _unitOfWork.ConteosCamaraMinuto.GetRangoAsync(desde, hasta, lineaId, cancellationToken);
        var paradas = await _unitOfWork.ParadasCamara.GetRangoAsync(InicioDelDia(fecha.AddDays(-1)), hasta, lineaId, cancellationToken);

        // Cámaras que cuentan producción: las que tienen datos en el rango o ya tienen objetivo o método cargado.
        // Los puntos de control de defectos de calidad, sin conteo, no aparecen.
        var conDatos = minutos.Select(m => m.PuntoControlCalidadId).ToHashSet();
        var puntos = (await _unitOfWork.PuntosControlCalidad.GetListadoAsync(lineaId, incluirInactivos: false, cancellationToken))
            .Where(p => conDatos.Contains(p.PuntoControlCalidadId) || p.ObjetivoPorHora != null || p.MetodoConteo != null)
            .OrderBy(p => p.Linea?.Orden ?? 0).ThenBy(p => p.Linea?.Nombre)
            .ThenBy(p => p.NumeroCamara ?? int.MaxValue).ThenBy(p => p.Codigo)
            .ToList();

        var calendarios = (await _unitOfWork.CalendariosLinea.GetActivosAsync(lineaId, cancellationToken)).ToList();
        var excepciones = (await _unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(fecha, fecha, lineaId, cancellationToken)).ToList();

        var resultado = new EficienciaCamaraDiaDto { Fecha = fecha };
        foreach (var punto in puntos)
        {
            var minutosPunto = minutos.Where(m => m.PuntoControlCalidadId == punto.PuntoControlCalidadId).ToList();
            var paradasPunto = paradas.Where(p => p.PuntoControlCalidadId == punto.PuntoControlCalidadId).ToList();
            var calendariosLinea = calendarios.Where(c => c.LineaId == punto.LineaId).ToList();
            var excepcionesLinea = excepciones.Where(e => e.LineaId == punto.LineaId).ToList();
            var carriles = minutosPunto.Select(m => m.Carril).Distinct().Count();

            var dto = new EficienciaCamaraPuntoDto
            {
                PuntoControlCalidadId = punto.PuntoControlCalidadId,
                Codigo = punto.Codigo,
                Nombre = punto.Nombre,
                LineaId = punto.LineaId,
                LineaNombre = punto.Linea?.Nombre ?? string.Empty,
                NumeroCamara = punto.NumeroCamara,
                MetodoConteo = punto.MetodoConteo,
                Unidad = EficienciaCamaraCalculador.Unidad(punto.MetodoConteo, carriles),
                ObjetivoPorHora = punto.ObjetivoPorHora,
                MinutosUmbralDetencion = DetencionCamaraReglas.UmbralValido(punto.MinutosUmbralDetencion)
            };

            foreach (var turno in turnos)
            {
                var (inicioPared, finPared) = EficienciaCamaraCalculador.VentanaTurno(fecha, turno);
                var inicio = HoraPlanta.DesdeHoraDePared(inicioPared, _zonaPlanta);
                var fin = HoraPlanta.DesdeHoraDePared(finPared, _zonaPlanta);

                // Turno materializado (no se recalcula): la ventana sólo elige de qué día es ese turno.
                var delTurno = minutosPunto
                    .Where(m => m.TurnoId == turno.TurnoId && m.Minuto >= inicio && m.Minuto < fin)
                    .ToList();
                var paradasVentana = paradasPunto.Where(p => p.FechaHoraFin > inicio && p.FechaHoraInicio < fin).ToList();
                var empezadas = paradasVentana.Where(p => p.FechaHoraInicio >= inicio).ToList();
                var largas = empezadas.Count(p =>
                    DetencionCamaraReglas.SuperaUmbral(p.FechaHoraInicio, p.FechaHoraFin, punto.MinutosUmbralDetencion));

                var minutosProgramados = EficienciaCamaraCalculador.MinutosProgramadosTurno(
                    fecha, turno, turnos, calendariosLinea, excepcionesLinea);
                var contadas = Math.Round(delTurno.Sum(m => m.Rollos));
                var objetivo = EficienciaCamaraCalculador.ObjetivoTurno(punto.ObjetivoPorHora, minutosProgramados);

                dto.Turnos.Add(new EficienciaTurnoDto
                {
                    TurnoId = turno.TurnoId,
                    TurnoCodigo = turno.Codigo,
                    TurnoNombre = turno.Nombre,
                    Inicio = inicio,
                    Fin = fin,
                    MinutosProgramados = minutosProgramados,
                    Contadas = contadas,
                    Objetivo = objetivo,
                    Porcentaje = EficienciaCamaraCalculador.Porcentaje(contadas, objetivo),
                    Estado = EficienciaCamaraCalculador.Estado(punto.ObjetivoPorHora, minutosProgramados),
                    Momento = EficienciaCamaraCalculador.Momento(momento, inicio, fin),
                    ObjetivoHastaAhora = EficienciaCamaraCalculador.ObjetivoHastaAhora(objetivo, momento, inicio, fin),
                    MinutosParada = EficienciaCamaraCalculador.MinutosParadaEnVentana(
                        paradasVentana.Select(p => (p.FechaHoraInicio, p.FechaHoraFin)), inicio, fin),
                    Microcortes = empezadas.Count - largas,
                    ParadasLargas = largas,
                    MinutosConDatos = MinutosConDatos(delTurno),
                    MinutosMarcha = MinutosMarcha(delTurno)
                });
            }

            resultado.Camaras.Add(dto);
        }

        return Result<EficienciaCamaraDiaDto>.Success(resultado);
    }

    public async Task<IReadOnlyList<AlertaCamaraDto>> ObtenerAlertasAsync(int? lineaId, int horas = 24, CancellationToken cancellationToken = default)
    {
        var desde = DateTimeOffset.UtcNow.AddHours(-Math.Clamp(horas, 1, 24 * 31));
        var alertas = await _unitOfWork.AlertasCamara.GetRecientesAsync(desde, lineaId, cancellationToken);
        return alertas.Select(a => new AlertaCamaraDto
        {
            AlertaCamaraId = a.AlertaCamaraId,
            PuntoControlCodigo = a.PuntoControl?.Codigo ?? string.Empty,
            PuntoControlNombre = a.PuntoControl?.Nombre ?? string.Empty,
            LineaId = a.LineaId,
            LineaNombre = a.Linea?.Nombre ?? string.Empty,
            Tipo = a.Tipo.ToString(),
            TipoDescripcion = a.Tipo == TipoAlertaCamara.CONGELADA ? "Cámara congelada" : "Pérdida de conexión de cámara",
            FechaHoraInicio = HoraPlanta.En(a.FechaHoraInicio, _zonaPlanta)!.Value,
            FechaHoraFin = HoraPlanta.En(a.FechaHoraFin, _zonaPlanta),
            Activa = a.FechaHoraFin == null,
            Detalle = a.Detalle
        }).ToList();
    }

    private DateTimeOffset InicioDelDia(DateOnly fecha) =>
        HoraPlanta.DesdeHoraDePared(fecha.ToDateTime(TimeOnly.MinValue), _zonaPlanta);
}
