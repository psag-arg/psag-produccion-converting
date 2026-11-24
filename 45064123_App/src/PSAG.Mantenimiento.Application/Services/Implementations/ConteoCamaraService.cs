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
/// Conteo de producción y paradas por cámara (T-610). Todas las paradas quedan en CAM_Paradas (log, incluye
/// microcortes). Si la cámara tiene "crea detenciones" en Maestros → Cámaras, la parada que llega al umbral (5 min
/// por defecto) da de alta una detención pendiente de declaración; idempotente por identificador de parada.
/// El cruce con detenciones se calcula al consultar. T-798 etapa 4: todo cuelga de la cámara (CAM_Camaras) y del
/// turno de producción (AsegurarTurnoAsync); sólo se aceptan datos de cámaras activas y habilitadas.
/// </summary>
public class ConteoCamaraService : IConteoCamaraService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITurnoProduccionService _turnosProduccion;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<ConteoCamaraService> _logger;

    public ConteoCamaraService(IUnitOfWork unitOfWork, ITurnoProduccionService turnosProduccion, TimeZoneInfo zonaPlanta,
        ILogger<ConteoCamaraService> logger)
    {
        _unitOfWork = unitOfWork;
        _turnosProduccion = turnosProduccion;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
    }

    // ===================== Ingesta =====================

    public async Task<Result<IngestaConteoCamaraRespuesta>> IngestarAsync(
        IngestaConteoCamaraRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoCamara))
            return Fallo("Falta camara.");
        var minutos = request.Minutos ?? [];
        var paradas = request.Paradas ?? [];
        if (minutos.Count > ConteoCamaraReglas.MaximoMinutosPorLote || paradas.Count > ConteoCamaraReglas.MaximoParadasPorLote)
            return Fallo($"El lote admite hasta {ConteoCamaraReglas.MaximoMinutosPorLote} minutos y {ConteoCamaraReglas.MaximoParadasPorLote} paradas.");

        // T-798 etapa 4: la cámara del maestro único. Transición: el formato viejo manda "puntoControl" con el mismo código.
        var codigo = CalidadReglas.NormalizarCodigo(request.CodigoCamara);
        var camara = await _unitOfWork.Camaras.GetByCodigoAsync(codigo, cancellationToken);
        if (camara == null || !camara.Activo)
            return Fallo($"No existe la cámara {codigo} o está inactiva. Cargala en Maestros → Cámaras.");
        if (!camara.Habilitada)
            return Fallo($"La cámara {codigo} no está habilitada en Maestros → Cámaras: no se aceptan sus datos.");
        if (!camara.EsConteo)
            return Fallo($"La cámara {codigo} es de calidad: no cuenta producción.");

        var ahora = DateTimeOffset.UtcNow;

        // Validación completa antes de guardar: el lote entra entero o no entra.
        foreach (var m in minutos)
        {
            if (!Enum.TryParse<EstadoCarrilCamara>(m.Estado, ignoreCase: true, out var estado) || !Enum.IsDefined(estado))
                return Fallo($"Estado de minuto no válido: {m.Estado}.");
            if (m.CantidadEfectiva is not { } cantidad)
                return Fallo($"Minuto {m.Minuto:yyyy-MM-dd HH:mm} carril {m.Carril}: falta la cantidad.");
            var motivo = ConteoCamaraReglas.MotivoMinutoInvalido(m.Minuto, m.Carril, cantidad,
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

        // Turnos de producción de los minutos y paradas, ANTES de agregar nada al contexto: AsegurarTurnoAsync guarda
        // (y abre por SISTEMA el turno que falte), y no tiene que arrastrar minutos a medio validar.
        var turnosProduccion = await AsegurarTurnosAsync(
            camara.LineaId, minutos.Select(m => m.Minuto).Concat(paradas.Select(p => p.Inicio)), cancellationToken);

        // Minutos: únicos por cámara + minuto + carril (también dentro del mismo lote).
        if (minutos.Count > 0)
        {
            var existentes = await _unitOfWork.ConteosCamaraMinuto.GetClavesExistentesAsync(
                camara.CamaraId, minutos.Min(m => m.Minuto), minutos.Max(m => m.Minuto), cancellationToken);
            foreach (var m in minutos)
            {
                var clave = (m.Minuto.ToUniversalTime(), (byte)m.Carril);
                if (!existentes.Add(clave))
                {
                    respuesta.MinutosDuplicados++;
                    continue;
                }
                var turno = turnosProduccion(m.Minuto);
                if (turno == null) respuesta.MinutosSinTurno++;
                await _unitOfWork.ConteosCamaraMinuto.AddAsync(new ConteoCamaraMinuto
                {
                    Minuto = m.Minuto,
                    CamaraId = camara.CamaraId,
                    TurnoProduccionId = turno?.TurnoProduccionId,
                    Carril = (byte)m.Carril,
                    Cantidad = Math.Round(m.CantidadEfectiva!.Value, 1),
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

        // Paradas: únicas por identificador externo. Todas, micro y grandes (T-784), marcadas con el umbral de hoy.
        if (paradas.Count > 0)
        {
            var umbralSegundos = DetencionCamaraReglas.UmbralSegundos(camara);
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
                await _unitOfWork.ParadasCamara.AddAsync(new ParadaCamara
                {
                    CamaraId = camara.CamaraId,
                    TurnoProduccionId = turnosProduccion(p.Inicio)?.TurnoProduccionId,
                    FechaHoraInicio = p.Inicio,
                    FechaHoraFin = p.Fin,
                    DuracionSegundos = (int)Math.Round((p.Fin - p.Inicio).TotalSeconds),
                    Tipo = Enum.Parse<TipoParadaCamara>(p.Tipo, ignoreCase: true),
                    Carriles = ConteoCamaraReglas.CarrilesTexto(p.Carriles),
                    InicioIncierto = p.InicioIncierto,
                    FinIncierto = p.FinIncierto,
                    IdentificadorExterno = id,
                    UmbralSegundos = umbralSegundos,
                    SuperaUmbral = DetencionCamaraReglas.SuperaUmbral(p.Inicio, p.Fin, umbralSegundos),
                    FechaHoraCreacion = ahora
                }, cancellationToken);
                respuesta.ParadasNuevas++;
            }
        }

        var alertasCambiadas = await GuardarAlertasAsync(camara, codigo, alertas, ahora, respuesta, cancellationToken);

        // Llegaron minutos: la cámara ya no está "sin datos" ni "sin configuración" (un minuto en 0 también es un dato).
        if (minutos.Count > 0)
            alertasCambiadas += await CerrarAlertasDeFaltaDeDatosAsync(camara, ahora, respuesta, cancellationToken);

        var huboDatos = minutos.Count > 0 || paradas.Count > 0 || enCurso.Count > 0;
        if (huboDatos)
            camara.UltimoDatoRecibido = ahora;

        if (huboDatos || alertasCambiadas > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (camara.CreaDetenciones && camara.DetectaParadas)
            await CrearDetencionesAsync(camara, paradas, enCurso, respuesta, cancellationToken);

        _logger.LogInformation(
            "Conteo por cámara {Camara}: {MinNuevos} minutos nuevos ({MinDup} repetidos, {MinSinTurno} sin turno), {ParNuevas} paradas nuevas ({ParDup} repetidas), {DetNuevas} detenciones automáticas, {AlNuevas} alertas nuevas, {AlCerradas} cerradas",
            codigo, respuesta.MinutosNuevos, respuesta.MinutosDuplicados, respuesta.MinutosSinTurno, respuesta.ParadasNuevas, respuesta.ParadasDuplicadas,
            respuesta.DetencionesCreadas, respuesta.AlertasNuevas, respuesta.AlertasCerradas);
        return Result<IngestaConteoCamaraRespuesta>.Success(respuesta);
    }

    /// <summary>
    /// Asegura el turno de producción de cada instante (uno por turno de planta y fecha de producción, aunque haya
    /// cientos de minutos) y devuelve con qué buscarlo. Si no había, lo crea el SISTEMA (TurnoProduccionService).
    /// Null para un instante que ningún turno de planta cubre: el dato se guarda igual, sin turno.
    /// </summary>
    private async Task<Func<DateTimeOffset, TurnoProduccion?>> AsegurarTurnosAsync(
        int lineaId, IEnumerable<DateTimeOffset> instantes, CancellationToken ct)
    {
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(ct)).ToList();
        var porClave = new Dictionary<(int TurnoId, DateOnly Fecha), TurnoProduccion?>();
        (int, DateOnly)? Clave(DateTimeOffset instante) =>
            TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.En(instante, _zonaPlanta)!.Value.DateTime) is var (turno, fecha)
                ? (turno.TurnoId, fecha)
                : null;

        foreach (var instante in instantes.OrderBy(i => i))
        {
            if (Clave(instante) is not { } clave || porClave.ContainsKey(clave))
                continue;
            // Si otro proceso crea el mismo turno a la vez (índice único), la ingesta falla y el servicio reenvía el lote
            // (idempotente): en el reenvío el turno ya existe.
            porClave[clave] = await _turnosProduccion.AsegurarTurnoAsync(lineaId, instante, ct);
        }
        return instante => Clave(instante) is { } clave ? porClave.GetValueOrDefault(clave) : null;
    }

    /// <summary>
    /// Detenciones automáticas: una por parada (abierta o cerrada) que llegó al umbral de la cámara. Si ya existe
    /// (reenvío, o la parada en curso ya la había creado), no se duplica; con la parada cerrada se guarda la hora de
    /// arranque detectada como sugerencia. Cada alta se guarda por separado: el código DET-... sale de la base.
    /// T-798 etapa 3: la detención cuelga del turno de producción del inicio de la parada (si no había, lo crea el
    /// SISTEMA) y el operador es el del turno a esa hora (el del relevo si paró después del relevo). Sin operador a
    /// esa hora queda null y se completa al declarar.
    /// </summary>
    private async Task CrearDetencionesAsync(
        Camara camara, List<ParadaCamaraIngesta> cerradas, List<ParadaEnCursoIngesta> enCurso,
        IngestaConteoCamaraRespuesta respuesta, CancellationToken cancellationToken)
    {
        var umbral = DetencionCamaraReglas.UmbralSegundos(camara);
        // Fin: el de la parada cerrada (null si sigue). Arranque: la sugerencia para el operador (null si el fin es incierto).
        // Hasta: hasta cuándo se sabe que estuvo parada (el "hasta" de la parada en curso; el fin de la cerrada).
        var candidatas = new Dictionary<string, (DateTimeOffset Inicio, DateTimeOffset? Fin, DateTimeOffset? Arranque, DateTimeOffset Hasta, bool InicioIncierto)>(StringComparer.Ordinal);
        foreach (var p in enCurso.Where(p => DetencionCamaraReglas.SuperaUmbral(p.Inicio, p.Hasta, umbral)))
            candidatas[p.IdentificadorExterno.Trim()] = (p.Inicio, null, null, p.Hasta, p.InicioIncierto);
        foreach (var p in cerradas.Where(p => DetencionCamaraReglas.SuperaUmbral(p.Inicio, p.Fin, umbral)))
            candidatas[p.IdentificadorExterno.Trim()] = (p.Inicio, p.Fin, p.FinIncierto ? null : p.Fin, p.Fin, p.InicioIncierto);
        if (candidatas.Count == 0)
            return;

        var existentes = (await _unitOfWork.Detenciones.GetPorParadasCamaraAsync(candidatas.Keys.ToList(), cancellationToken))
            .ToDictionary(d => d.IdentificadorParadaCamara!, StringComparer.Ordinal);

        foreach (var (id, c) in candidatas.OrderBy(c => c.Value.Inicio))
        {
            if (existentes.TryGetValue(id, out var existente))
            {
                respuesta.DetencionesExistentes++;
                // Turno automático (D.12): el arranque (sugerencia para el operador; no toca FechaHoraArranque ni los minutos)
                // va a la ÚLTIMA parte de la parada; si cruzó el fin del turno, se corta y sigue en el turno siguiente.
                await _turnosProduccion.ContinuarDetencionAsync(existente, c.Fin, c.Arranque, c.Hasta, cancellationToken);
                continue;
            }

            // Si otro proceso crea el mismo turno a la vez (índice único), la ingesta falla y el servicio reenvía el lote
            // (idempotente): en el reenvío el turno ya existe.
            var turnoProduccion = await _turnosProduccion.AsegurarTurnoAsync(camara.LineaId, c.Inicio, cancellationToken);
            if (turnoProduccion == null)
            {
                respuesta.DetencionesSinTurno++;
                _logger.LogWarning("La parada {Id} de la cámara {Camara} llegó al umbral pero no hay turno para esa hora: no se creó la detención", id, camara.Codigo);
                continue;
            }

            var operador = TurnoProduccionReglas.OperadorALaHora(turnoProduccion.Operadores, c.Inicio);
            var motivoOperador = operador != null
                ? "Operador: el del turno a esa hora."
                : "Sin operador en el turno a esa hora: lo completa quien declara.";
            var inicioPlanta = HoraPlanta.En(c.Inicio, _zonaPlanta)!.Value;
            var sistema = DetencionCamaraReglas.UsuarioSistemaCamara;

            var detencion = new Detencion
            {
                Codigo = await _unitOfWork.Detenciones.GenerarCodigoDetencionAsync(cancellationToken),
                LineaId = camara.LineaId,
                TurnoProduccionId = turnoProduccion.TurnoProduccionId,
                OperadorUsuarioId = operador?.OperadorUsuarioId,
                FechaHoraParada = c.Inicio,
                // Sin hora de fin ni minutos: los declara el operador. 0 hasta entonces (CHECK >= 0).
                OrigenTiempo = OrigenTiempo.CALCULADO,
                MinutosEfectivos = 0,
                Descripcion = string.Empty,
                EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION,
                OrigenRegistro = OrigenRegistroDetencion.CAMARA,
                IdentificadorParadaCamara = id,
                CamaraId = camara.CamaraId,
                CreadoPorUsuarioId = sistema,
                ModificadoPorUsuarioId = sistema
            };
            detencion.HistorialEstados.Add(new DetencionEstadoHistorial
            {
                EstadoAnterior = null,
                EstadoNuevo = EstadoDetencion.PENDIENTE_JUSTIFICACION,
                FechaHora = DateTimeOffset.UtcNow,
                UsuarioId = sistema,
                Observacion = DetencionCamaraReglas.ObservacionAlta(camara.Codigo, inicioPlanta, c.InicioIncierto) + " " + motivoOperador,
                OrigenAccion = "CAMARA"
            });

            await _unitOfWork.Detenciones.AddAsync(detencion, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            existentes[id] = detencion;
            respuesta.DetencionesCreadas++;
            // Turno automático (D.12): si la parada ya abarca el fin del turno (llegó tarde), se corta en partes por turno;
            // si no, el arranque detectado queda en esta.
            respuesta.DetencionesCreadas += await _turnosProduccion.ContinuarDetencionAsync(detencion, c.Fin, c.Arranque, c.Hasta, cancellationToken);
            _logger.LogInformation("Detención automática {Codigo} creada por la cámara {Camara} (parada desde {Inicio:HH:mm:ss})",
                detencion.Codigo, camara.Codigo, inicioPlanta);
        }
    }

    /// <summary>
    /// Alta o cierre de alertas de cámara. Devuelve cuántas cambiaron (para saber si hay que guardar). Las de falta de
    /// datos (SIN_CONFIGURACION, SIN_DATOS) no se duplican: si ya hay una abierta del mismo tipo, la nueva se ignora.
    /// </summary>
    private async Task<int> GuardarAlertasAsync(
        Camara camara, string codigo, List<AlertaCamaraIngesta> alertas, DateTimeOffset ahora,
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
        var abiertasFaltaDatos = (await _unitOfWork.AlertasCamara.GetAbiertasAsync(camara.CamaraId, AlertasDeFaltaDeDatos, cancellationToken))
            .Select(a => a.Tipo).ToHashSet();

        var cambios = 0;
        foreach (var (id, a) in porId)
        {
            var tipo = Enum.Parse<TipoAlertaCamara>(a.Tipo, true);
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
            if (a.Fin == null && AlertasDeFaltaDeDatos.Contains(tipo) && !abiertasFaltaDatos.Add(tipo))
                continue;   // ya hay una abierta de ese tipo (el servicio la repite al reiniciar)
            await _unitOfWork.AlertasCamara.AddAsync(new AlertaCamara
            {
                CamaraId = camara.CamaraId,
                Tipo = tipo,
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
            _logger.LogWarning("Alerta de cámara {Camara}: {Tipo} desde {Inicio:HH:mm:ss}", codigo, a.Tipo, HoraPlanta.En(a.Inicio, _zonaPlanta));
        }
        return cambios;
    }

    /// <summary>Tipos de alerta que se cierran solos cuando llegan minutos de la cámara.</summary>
    private static readonly TipoAlertaCamara[] AlertasDeFaltaDeDatos = [TipoAlertaCamara.SIN_DATOS, TipoAlertaCamara.SIN_CONFIGURACION];

    private async Task<int> CerrarAlertasDeFaltaDeDatosAsync(
        Camara camara, DateTimeOffset ahora, IngestaConteoCamaraRespuesta respuesta, CancellationToken ct)
    {
        var abiertas = await _unitOfWork.AlertasCamara.GetAbiertasAsync(camara.CamaraId, AlertasDeFaltaDeDatos, ct);
        foreach (var alerta in abiertas)
        {
            alerta.FechaHoraFin = ahora < alerta.FechaHoraInicio ? alerta.FechaHoraInicio : ahora;
            alerta.FechaHoraActualizacion = ahora;
            respuesta.AlertasCerradas++;
            _logger.LogInformation("Alerta {Tipo} de la cámara {Camara} cerrada: llegaron minutos", alerta.Tipo, camara.Codigo);
        }
        return abiertas.Count;
    }

    // ===================== Vigilancia SIN_DATOS (job) =====================

    public async Task<int> VigilarSinDatosAsync(DateTimeOffset ahora, int minutosSinDatos, CancellationToken cancellationToken = default)
    {
        var abiertos = (await _unitOfWork.TurnosProduccion.GetAbiertosAsync(cancellationToken))
            .GroupBy(t => t.LineaId).ToDictionary(g => g.Key, g => g.First());
        if (abiertos.Count == 0)
            return 0;

        var nuevas = 0;
        foreach (var camara in (await _unitOfWork.Camaras.GetOficialesAsync(cancellationToken)).Where(c => c.Habilitada))
        {
            if (!abiertos.TryGetValue(camara.LineaId, out var turno))
                continue;
            var ultimoMinuto = await _unitOfWork.ConteosCamaraMinuto.GetUltimoMinutoAsync(camara.CamaraId, cancellationToken);
            var inicioTurno = turno.InicioReal ?? turno.InicioProgramado;
            // Lo último que se sabe: el fin del último minuto recibido o, si es posterior, el inicio del turno.
            var referencia = ultimoMinuto is { } u && u.AddMinutes(1) > inicioTurno ? u.AddMinutes(1) : inicioTurno;
            if (!DetencionCamaraReglas.CorrespondeAlertaSinDatos(referencia, ahora, minutosSinDatos))
                continue;
            if ((await _unitOfWork.AlertasCamara.GetAbiertasAsync(camara.CamaraId, [TipoAlertaCamara.SIN_DATOS], cancellationToken)).Count > 0)
                continue;

            var desde = HoraPlanta.En(referencia, _zonaPlanta)!.Value;
            await _unitOfWork.AlertasCamara.AddAsync(new AlertaCamara
            {
                CamaraId = camara.CamaraId,
                Tipo = TipoAlertaCamara.SIN_DATOS,
                FechaHoraInicio = referencia,
                // Clave con la hora de detección: una alerta cerrada y vuelta a abrir no choca con la anterior.
                IdentificadorExterno = DetencionCamaraReglas.IdentificadorAlerta(camara.Codigo, TipoAlertaCamara.SIN_DATOS, ahora),
                Detalle = $"Turno abierto y la cámara no manda minutos desde las {desde:HH:mm} (más de {Math.Max(1, minutosSinDatos)} min). Sin datos no es producción cero.",
                FechaHoraCreacion = ahora,
                FechaHoraActualizacion = ahora
            }, cancellationToken);
            nuevas++;
            _logger.LogWarning("Alerta SIN_DATOS de la cámara {Camara}: sin minutos desde {Desde:HH:mm} con el turno abierto", camara.Codigo, desde);
        }
        if (nuevas > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        return nuevas;
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
        if (filtro.CamaraId is { } camaraId)
            paradas = paradas.Where(p => p.CamaraId == camaraId).ToList();
        if (filtro.SuperaUmbral is { } supera)
            paradas = paradas.Where(p => DetencionCamaraReglas.SuperaUmbral(p) == supera).ToList();

        // Detenciones de un día antes y después (por fecha de producción del turno), con su turno cargado.
        var desdeProduccion = desde.AddDays(-1);
        var hastaProduccion = hasta.AddDays(1);
        var detenciones = (await _unitOfWork.Detenciones.FindAsync(
                d => !d.Eliminada && d.TurnoProduccion.FechaProduccion >= desdeProduccion && d.TurnoProduccion.FechaProduccion <= hastaProduccion, cancellationToken))
            .ToList();

        var items = new List<ParadaCamaraDto>();
        foreach (var p in paradas)
        {
            var (cruce, codigos) = ConteoCamaraReglas.Cruzar(p, p.Camara.LineaId, detenciones);
            if (filtro.SoloSinDetencion && cruce != CruceDetencionCamara.SIN_DETENCION)
                continue;
            items.Add(new ParadaCamaraDto
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
                SuperaUmbral = DetencionCamaraReglas.SuperaUmbral(p),
                CreaDetencion = p.Camara.CreaDetenciones && DetencionCamaraReglas.SuperaUmbral(p),
                UmbralTexto = DetencionCamaraReglas.UmbralSegundos(p) is { } umbral ? DetencionCamaraReglas.TextoUmbral(umbral) : null,
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
        DateOnly fecha, int? lineaId, int? camaraId = null, CancellationToken cancellationToken = default)
    {
        // Ventana del día de planta: del inicio del primer turno al fin del último (el nocturno es del día en que empieza).
        var turnos = (await _unitOfWork.Turnos.GetAllAsync(cancellationToken))
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden).ThenBy(t => t.HoraInicio)
            .ToList();
        var ventanas = turnos
            .Select(t =>
            {
                var (inicioPared, finPared) = EficienciaCamaraCalculador.VentanaTurno(fecha, t);
                return (Turno: t, Inicio: HoraPlanta.DesdeHoraDePared(inicioPared, _zonaPlanta), Fin: HoraPlanta.DesdeHoraDePared(finPared, _zonaPlanta));
            })
            .ToList();
        var inicioDia = ventanas.Count > 0 ? ventanas.Min(v => v.Inicio) : InicioDelDia(fecha);
        var finDia = ventanas.Count > 0 ? ventanas.Max(v => v.Fin) : InicioDelDia(fecha.AddDays(1));

        var minutos = await _unitOfWork.ConteosCamaraMinuto.GetRangoAsync(inicioDia, finDia, lineaId, cancellationToken);
        if (camaraId is { } id)
            minutos = minutos.Where(m => m.CamaraId == id).ToList();
        var camaras = (await _unitOfWork.Camaras.GetListadoAsync(lineaId, incluirInactivas: true, cancellationToken))
            .ToDictionary(c => c.CamaraId);
        var turnosPorId = turnos.ToDictionary(t => t.TurnoId);

        var resultado = new ProduccionCamaraDto
        {
            Fecha = fecha,
            LineaId = lineaId,
            Inicio = HoraPlanta.En(inicioDia, _zonaPlanta)!.Value,
            Fin = HoraPlanta.En(finDia, _zonaPlanta)!.Value
        };

        // Una entrada por cámara: rollos (09), mangas (10) y bolsones (8) no se suman, ni se mezcla el carril 1 de ellas.
        foreach (var grupo in minutos.GroupBy(m => m.CamaraId))
        {
            var minutosCamara = grupo.ToList();
            var camara = camaras.GetValueOrDefault(grupo.Key);
            var carriles = minutosCamara.Select(m => (int)m.Carril).Distinct().OrderBy(c => c).ToList();

            // Por turno de producción del día (los minutos cuelgan del turno: no se recalcula).
            var turnosCamara = new List<ProduccionCamaraTurnoDto>();
            // Por id: sin seguimiento cada minuto trae su propia instancia del turno.
            foreach (var delTurno in minutosCamara
                         .Where(m => m.TurnoProduccion is { } tp && tp.FechaProduccion == fecha)
                         .GroupBy(m => m.TurnoProduccionId!.Value)
                         .OrderBy(g => turnosPorId.GetValueOrDefault(g.First().TurnoProduccion!.TurnoId)?.Orden ?? int.MaxValue)
                         .ThenBy(g => g.First().TurnoProduccion!.InicioProgramado))
            {
                var tp = delTurno.First().TurnoProduccion!;
                turnosCamara.Add(TotalTurno(tp.TurnoProduccionId, tp.TurnoId, turnosPorId.GetValueOrDefault(tp.TurnoId)?.Nombre ?? $"Turno {tp.TurnoId}",
                    HoraPlanta.En(tp.InicioProgramado, _zonaPlanta), HoraPlanta.En(tp.FinProgramado, _zonaPlanta), delTurno.ToList()));
            }
            var sinTurno = minutosCamara.Where(m => m.TurnoProduccion is not { } tp || tp.FechaProduccion != fecha).ToList();
            if (sinTurno.Count > 0)
                turnosCamara.Add(TotalTurno(null, null, "Sin turno", null, null, sinTurno));

            resultado.Camaras.Add(new ProduccionCamaraPuntoDto
            {
                CamaraId = grupo.Key,
                Codigo = camara?.Codigo ?? $"Cámara {grupo.Key}",
                Nombre = camara?.Nombre ?? string.Empty,
                LineaNombre = camara?.Linea?.Nombre ?? string.Empty,
                Magnitud = camara?.Magnitud,
                CuentaProduccionOficial = camara?.CuentaProduccionOficial == true,
                Unidad = EficienciaCamaraCalculador.Unidad(camara?.Magnitud),
                Carriles = carriles,
                Horas = minutosCamara
                    .GroupBy(m =>
                    {
                        var local = HoraPlanta.En(m.Minuto, _zonaPlanta)!.Value;
                        return new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
                    })
                    .OrderBy(g => g.Key)
                    .Select(g => new ProduccionCamaraHoraDto
                    {
                        Hora = g.Key,
                        CantidadPorCarril = g.GroupBy(m => (int)m.Carril).ToDictionary(c => c.Key, c => Math.Round(c.Sum(m => m.Cantidad))),
                        Total = Math.Round(g.Sum(m => m.Cantidad)),
                        MinutosCarrilMarcha = g.Count(m => m.Estado == EstadoCarrilCamara.MARCHA),
                        MinutosCarrilConDatos = g.Count(m => m.Estado != EstadoCarrilCamara.SIN_SENAL)
                    })
                    .ToList(),
                Total = Math.Round(minutosCamara.Sum(m => m.Cantidad)),
                MinutosConDatos = MinutosConDatos(minutosCamara),
                MinutosMarcha = MinutosMarcha(minutosCamara),
                Turnos = turnosCamara
            });
        }

        resultado.Camaras = resultado.Camaras
            .OrderBy(c => camaras.GetValueOrDefault(c.CamaraId)?.Linea?.Orden ?? 0)
            .ThenBy(c => c.LineaNombre)
            .ThenBy(c => camaras.GetValueOrDefault(c.CamaraId)?.NumeroCanalGrabador ?? int.MaxValue)
            .ThenBy(c => c.Codigo)
            .ToList();
        return Result<ProduccionCamaraDto>.Success(resultado);
    }

    private static ProduccionCamaraTurnoDto TotalTurno(
        long? turnoProduccionId, int? turnoId, string nombre, DateTimeOffset? inicio, DateTimeOffset? fin,
        IReadOnlyCollection<ConteoCamaraMinuto> minutos) => new()
    {
        TurnoProduccionId = turnoProduccionId,
        TurnoId = turnoId,
        TurnoNombre = nombre,
        Inicio = inicio,
        Fin = fin,
        Total = Math.Round(minutos.Sum(m => m.Cantidad)),
        MinutosConDatos = MinutosConDatos(minutos),
        MinutosMarcha = MinutosMarcha(minutos)
    };

    /// <summary>Minutos (no minutos-carril) con datos de algún carril: lo mismo que suma la producción por turno.</summary>
    private static int MinutosConDatos(IEnumerable<ConteoCamaraMinuto> minutos) =>
        minutos.GroupBy(m => m.Minuto).Count(x => x.Any(m => m.Estado != EstadoCarrilCamara.SIN_SENAL));

    /// <summary>Minutos con todos los carriles en marcha.</summary>
    private static int MinutosMarcha(IEnumerable<ConteoCamaraMinuto> minutos) =>
        minutos.GroupBy(m => m.Minuto).Count(x => x.All(m => m.Estado == EstadoCarrilCamara.MARCHA));

    public async Task<IReadOnlyList<AlertaCamaraDto>> ObtenerAlertasAsync(int? lineaId, int horas = 24, CancellationToken cancellationToken = default)
    {
        var desde = DateTimeOffset.UtcNow.AddHours(-Math.Clamp(horas, 1, 24 * 31));
        var alertas = await _unitOfWork.AlertasCamara.GetRecientesAsync(desde, lineaId, cancellationToken);
        return alertas.Select(a => new AlertaCamaraDto
        {
            AlertaCamaraId = a.AlertaCamaraId,
            CamaraId = a.CamaraId,
            CamaraCodigo = a.Camara?.Codigo ?? string.Empty,
            CamaraNombre = a.Camara?.Nombre ?? string.Empty,
            LineaId = a.Camara?.LineaId ?? 0,
            LineaNombre = a.Camara?.Linea?.Nombre ?? string.Empty,
            Tipo = a.Tipo.ToString(),
            TipoDescripcion = DetencionCamaraReglas.DescripcionAlerta(a.Tipo),
            FechaHoraInicio = HoraPlanta.En(a.FechaHoraInicio, _zonaPlanta)!.Value,
            FechaHoraFin = HoraPlanta.En(a.FechaHoraFin, _zonaPlanta),
            Activa = a.FechaHoraFin == null,
            Detalle = a.Detalle
        }).ToList();
    }

    private DateTimeOffset InicioDelDia(DateOnly fecha) =>
        HoraPlanta.DesdeHoraDePared(fecha.ToDateTime(TimeOnly.MinValue), _zonaPlanta);
}
