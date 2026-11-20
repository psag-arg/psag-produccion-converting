using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Maquinista de cada turno (T-783). Cada confirmación o corrección es un registro nuevo en mnt.MaquinistasTurno
/// (INSERT-ONLY); el vigente es el último. Las detenciones automáticas toman al vigente al crearse
/// (<see cref="DetencionCamaraReglas.ElegirMaquinista"/>) y, al confirmar o corregir, las pendientes de
/// declaración de ese turno pasan a tomarlo, con una entrada en su historial. Las ya declaradas no se tocan.
/// </summary>
public class MaquinistaTurnoService : IMaquinistaTurnoService
{
    public const string OrigenAccionHistorial = "MAQUINISTA_TURNO";

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<MaquinistaTurnoService> _logger;
    private readonly Func<DateTimeOffset> _ahora;

    public MaquinistaTurnoService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<MaquinistaTurnoService> logger)
        : this(unitOfWork, zonaPlanta, logger, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Con reloj inyectable, para los tests.</summary>
    public MaquinistaTurnoService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<MaquinistaTurnoService> logger, Func<DateTimeOffset> ahora)
    {
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
        _ahora = ahora;
    }

    public async Task<PropuestaMaquinistaTurnoDto> ObtenerPropuestaAsync(
        string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var propuesta = new PropuestaMaquinistaTurnoDto();
        if (!roles.Contains(RolesSistema.Operario))
            return propuesta;

        var (turno, fechaTurno) = await TurnoEnCursoAsync(cancellationToken);
        if (turno == null)
            return propuesta;   // sin turno activo no hay nada que confirmar ni avisar
        propuesta.TurnoId = turno.TurnoId;
        propuesta.TurnoNombre = turno.Nombre;
        propuesta.FechaTurno = fechaTurno;

        var asignaciones = await _unitOfWork.UsuariosLineas.FindAsync(a => a.UsuarioId == usuarioId, cancellationToken);
        var lineas = (await _unitOfWork.Lineas.GetAllAsync(cancellationToken)).Where(l => l.Activo).ToDictionary(l => l.LineaId);
        var candidatas = MaquinistaTurnoReglas.Candidatas(asignaciones.Where(a => lineas.ContainsKey(a.LineaId)), turno.TurnoId);
        if (candidatas.Count == 0)
        {
            propuesta.Aviso = MaquinistaTurnoReglas.MensajeSinRelacion;
            propuesta.Preguntar = true;
            return propuesta;
        }

        foreach (var a in candidatas)
        {
            var vigente = await _unitOfWork.MaquinistasTurno.GetVigenteAsync(a.LineaId, turno.TurnoId, fechaTurno, cancellationToken);
            var esEl = vigente?.UsuarioLineaId == a.UsuarioLineaId;
            propuesta.Opciones.Add(new OpcionMaquinistaTurnoDto
            {
                LineaId = a.LineaId,
                LineaNombre = lineas[a.LineaId].Nombre,
                UsuarioLineaId = a.UsuarioLineaId,
                SinTurnoFijo = a.TurnoId == null,
                YaConfirmado = esEl,
                MaquinistaActualUsuarioId = esEl ? null : vigente?.UsuarioId,
                MaquinistaActualDesde = esEl ? null : vigente?.FechaHora,
                MaquinistaActualOrigen = esEl ? null : vigente?.Origen.ToString()
            });
        }
        propuesta.Preguntar = propuesta.Opciones.Any(o => !o.YaConfirmado);
        return propuesta;
    }

    public async Task<Result<ConfirmacionMaquinistaTurnoDto>> ConfirmarAsync(
        string usuarioId, string nombreUsuario, int lineaId, CancellationToken cancellationToken = default)
    {
        var (turno, fechaTurno) = await TurnoEnCursoAsync(cancellationToken);
        if (turno == null)
            return Result<ConfirmacionMaquinistaTurnoDto>.Failure("VALIDATION", MaquinistaTurnoReglas.MensajeSinTurno);

        var asignacion = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(usuarioId, lineaId, cancellationToken);
        if (asignacion == null || !MaquinistaTurnoReglas.Corresponde(asignacion, turno.TurnoId) || asignacion.Linea?.Activo == false)
            return Result<ConfirmacionMaquinistaTurnoDto>.Failure("VALIDATION",
                $"No tenés asignación como maquinista de {asignacion?.Linea?.Nombre ?? "esa línea"} para el {turno.Nombre}: no quedás como maquinista. " +
                "Pedile a tu supervisor que te cargue en \"Maquinistas del turno\".");

        var vigente = await _unitOfWork.MaquinistasTurno.GetVigenteAsync(lineaId, turno.TurnoId, fechaTurno, cancellationToken);
        var resultado = new ConfirmacionMaquinistaTurnoDto
        {
            LineaId = lineaId,
            LineaNombre = asignacion.Linea?.Nombre ?? string.Empty,
            TurnoNombre = turno.Nombre,
            FechaTurno = fechaTurno,
            YaEstaba = vigente?.UsuarioLineaId == asignacion.UsuarioLineaId,
            ReemplazadoUsuarioId = vigente != null && vigente.UsuarioLineaId != asignacion.UsuarioLineaId ? vigente.UsuarioId : null
        };

        if (!resultado.YaEstaba)
        {
            await _unitOfWork.MaquinistasTurno.AddAsync(new MaquinistaTurno
            {
                LineaId = lineaId,
                TurnoId = turno.TurnoId,
                FechaTurno = fechaTurno,
                UsuarioLineaId = asignacion.UsuarioLineaId,
                UsuarioId = asignacion.UsuarioId,
                Origen = OrigenMaquinistaTurno.LOGIN,
                FechaHora = _ahora(),
                RegistradoPorUsuarioId = usuarioId
            }, cancellationToken);
        }

        resultado.DetencionesReasignadas = await ReasignarPendientesAsync(
            lineaId, turno, fechaTurno, asignacion, usuarioId,
            MaquinistaTurnoReglas.ObservacionReasignacion(nombreUsuario, porSupervisor: false), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Maquinista del turno {Turno} {Fecha} de la línea {LineaId} confirmado en el login por {UsuarioId} (ya estaba: {YaEstaba}, reemplaza a: {Reemplazado}, detenciones reasignadas: {Reasignadas})",
            turno.Codigo, fechaTurno, lineaId, usuarioId, resultado.YaEstaba, resultado.ReemplazadoUsuarioId, resultado.DetencionesReasignadas);
        return Result<ConfirmacionMaquinistaTurnoDto>.Success(resultado);
    }

    public async Task<MaquinistasTurnoDiaDto> ObtenerDiaAsync(
        DateOnly fecha, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default)
    {
        var ahora = _ahora();
        var lineas = (await _unitOfWork.Lineas.GetAllAsync(cancellationToken))
            .Where(l => l.Activo && MaquinistaTurnoReglas.PuedeCorregir(roles, lineasAsignadas, l.LineaId))
            .OrderBy(l => l.Orden).ThenBy(l => l.Nombre)
            .ToList();
        var turnos = (await _unitOfWork.Turnos.GetAllAsync(cancellationToken))
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden).ThenBy(t => t.HoraInicio)
            .ToList();
        var lineasIds = lineas.Select(l => l.LineaId).ToList();
        var registros = await _unitOfWork.MaquinistasTurno.GetDelDiaAsync(fecha, lineasIds, cancellationToken);
        var asignaciones = (await _unitOfWork.UsuariosLineas.GetAllWithRelationsAsync(cancellationToken)).ToList();

        var dia = new MaquinistasTurnoDiaDto { Fecha = fecha };
        foreach (var linea in lineas)
        {
            var asignacionesLinea = asignaciones.Where(a => a.LineaId == linea.LineaId).ToList();
            foreach (var turno in turnos)
            {
                var (inicioPared, finPared) = MaquinistaTurnoReglas.VentanaTurno(fecha, turno);
                var inicio = HoraPlanta.DesdeHoraDePared(inicioPared, _zonaPlanta);
                var fin = HoraPlanta.DesdeHoraDePared(finPared, _zonaPlanta);
                var delTurno = registros.Where(r => r.LineaId == linea.LineaId && r.TurnoId == turno.TurnoId).ToList();
                var vigente = MaquinistaTurnoReglas.Vigente(delTurno);

                var fila = new MaquinistaTurnoFilaDto
                {
                    LineaId = linea.LineaId,
                    LineaNombre = linea.Nombre,
                    TurnoId = turno.TurnoId,
                    TurnoNombre = turno.Nombre,
                    Inicio = HoraPlanta.En(inicio, _zonaPlanta)!.Value,
                    Fin = HoraPlanta.En(fin, _zonaPlanta)!.Value,
                    EnCurso = ahora >= inicio && ahora < fin,
                    PuedeCorregir = true,
                    Vigente = vigente == null ? null : Registro(vigente),
                    Historial = delTurno.Select(Registro).ToList(),
                    Asignaciones = asignacionesLinea
                        .OrderBy(a => a.TurnoId == turno.TurnoId ? 0 : a.TurnoId == null ? 1 : 2)
                        .Select(a => new AsignacionMaquinistaDto
                        {
                            UsuarioLineaId = a.UsuarioLineaId,
                            UsuarioId = a.UsuarioId,
                            TurnoId = a.TurnoId,
                            TurnoNombre = a.Turno?.Nombre
                        })
                        .ToList()
                };
                if (vigente == null)
                    fila.SegunAsignacionUsuarioId = DetencionCamaraReglas.ElegirMaquinista(asignacionesLinea, turno.TurnoId).Elegido?.UsuarioId;
                dia.Filas.Add(fila);
            }
        }
        return dia;
    }

    public async Task<Result<CorreccionMaquinistaTurnoDto>> CorregirAsync(
        CorregirMaquinistaTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, string nombreMaquinista, CancellationToken cancellationToken = default)
    {
        if (!MaquinistaTurnoReglas.PuedeCorregir(roles, lineasAsignadas, request.LineaId))
            return Result<CorreccionMaquinistaTurnoDto>.Failure("FORBIDDEN", "No podés corregir el maquinista de esa línea.");

        var turno = await _unitOfWork.Turnos.GetByIdAsync(request.TurnoId, cancellationToken);
        if (turno == null || !turno.Activo)
            return Result<CorreccionMaquinistaTurnoDto>.Failure("NOT_FOUND", "El turno no existe o no está activo.");
        if (request.FechaTurno > HoraPlanta.Hoy(_zonaPlanta, _ahora()))
            return Result<CorreccionMaquinistaTurnoDto>.Failure("VALIDATION", "No se puede cargar el maquinista de un día que todavía no empezó.");

        var asignacion = await _unitOfWork.UsuariosLineas.GetByIdAsync(request.UsuarioLineaId, cancellationToken);
        var motivo = MaquinistaTurnoReglas.MotivoCorreccionInvalida(asignacion, request.LineaId, request.Observacion);
        if (motivo != null)
            return Result<CorreccionMaquinistaTurnoDto>.Failure("VALIDATION", motivo);

        await _unitOfWork.MaquinistasTurno.AddAsync(new MaquinistaTurno
        {
            LineaId = request.LineaId,
            TurnoId = turno.TurnoId,
            FechaTurno = request.FechaTurno,
            UsuarioLineaId = asignacion!.UsuarioLineaId,
            UsuarioId = asignacion.UsuarioId,
            Origen = OrigenMaquinistaTurno.SUPERVISOR,
            FechaHora = _ahora(),
            RegistradoPorUsuarioId = usuarioId,
            Observacion = request.Observacion!.Trim()
        }, cancellationToken);

        var reasignadas = await ReasignarPendientesAsync(
            request.LineaId, turno, request.FechaTurno, asignacion, usuarioId,
            MaquinistaTurnoReglas.ObservacionReasignacion(nombreMaquinista, porSupervisor: true), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Maquinista del turno {Turno} {Fecha} de la línea {LineaId} corregido por {UsuarioId}: asignación {UsuarioLineaId} (detenciones reasignadas: {Reasignadas})",
            turno.Codigo, request.FechaTurno, request.LineaId, usuarioId, asignacion.UsuarioLineaId, reasignadas);
        return Result<CorreccionMaquinistaTurnoDto>.Success(new CorreccionMaquinistaTurnoDto { DetencionesReasignadas = reasignadas });
    }

    /// <summary>Turno que contiene la hora actual de planta y el día en que empezó (un nocturno después de medianoche es del día anterior).</summary>
    private async Task<(Turno? Turno, DateOnly FechaTurno)> TurnoEnCursoAsync(CancellationToken cancellationToken)
    {
        var ahora = HoraPlanta.Ahora(_zonaPlanta, _ahora());
        var hora = TimeOnly.FromDateTime(ahora.DateTime);
        var turno = await _unitOfWork.Turnos.GetTurnoPorHoraAsync(new TimeOnly(hora.Hour, hora.Minute), cancellationToken);
        return turno == null ? (null, default) : (turno, MaquinistaTurnoReglas.FechaTurno(turno, ahora.DateTime));
    }

    /// <summary>
    /// Las detenciones pendientes de declaración de la línea que pararon en ese turno pasan a tomar al maquinista.
    /// Las declaradas no se tocan (son dato completo: si hay que corregirlas, es una corrección administrativa).
    /// No guarda: lo hace quien llama, junto con el registro del maquinista.
    /// </summary>
    private async Task<int> ReasignarPendientesAsync(
        int lineaId, Turno turno, DateOnly fechaTurno, UsuarioLinea asignacion, string usuarioId, string observacion,
        CancellationToken cancellationToken)
    {
        var (inicioPared, finPared) = MaquinistaTurnoReglas.VentanaTurno(fechaTurno, turno);
        var inicio = HoraPlanta.DesdeHoraDePared(inicioPared, _zonaPlanta);
        var fin = HoraPlanta.DesdeHoraDePared(finPared, _zonaPlanta);
        var turnoId = turno.TurnoId;
        var usuarioLineaId = asignacion.UsuarioLineaId;

        var pendientes = await _unitOfWork.Detenciones.FindAsync(d =>
            d.EstadoActual == EstadoDetencion.PENDIENTE_JUSTIFICACION
            && d.LineaId == lineaId
            && d.TurnoId == turnoId
            && d.FechaHoraParada >= inicio && d.FechaHoraParada < fin
            && d.UsuarioLineaId != usuarioLineaId, cancellationToken);

        var cantidad = 0;
        foreach (var d in pendientes)
        {
            d.UsuarioLineaId = usuarioLineaId;
            d.ModificadoPorUsuarioId = usuarioId;
            d.HistorialEstados.Add(new DetencionEstadoHistorial
            {
                EstadoAnterior = d.EstadoActual,
                EstadoNuevo = d.EstadoActual,
                FechaHora = _ahora(),
                UsuarioId = usuarioId,
                Observacion = observacion,
                OrigenAccion = OrigenAccionHistorial
            });
            cantidad++;
        }
        return cantidad;
    }

    private static MaquinistaTurnoRegistroDto Registro(MaquinistaTurno r) => new()
    {
        UsuarioLineaId = r.UsuarioLineaId,
        UsuarioId = r.UsuarioId,
        Origen = r.Origen.ToString(),
        FechaHora = r.FechaHora,
        RegistradoPorUsuarioId = r.RegistradoPorUsuarioId,
        Observacion = r.Observacion
    };
}
