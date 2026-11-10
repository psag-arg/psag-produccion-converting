using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Declaración de las detenciones automáticas (T-610): el operador completa equipo, tipo y detalle de falla,
/// descripción y la hora de arranque (o los minutos). Pasa a REGISTRADA y sigue el flujo normal (reglas de análisis).
/// </summary>
public class DeclaracionDetencionService : IDeclaracionDetencionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDetencionService _detenciones;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<DeclaracionDetencionService> _logger;
    private readonly Func<DateTimeOffset> _ahora;

    public DeclaracionDetencionService(IUnitOfWork unitOfWork, IDetencionService detenciones, TimeZoneInfo zonaPlanta,
        ILogger<DeclaracionDetencionService> logger)
        : this(unitOfWork, detenciones, zonaPlanta, logger, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Con reloj inyectable, para los tests del aviso de fin de turno.</summary>
    public DeclaracionDetencionService(IUnitOfWork unitOfWork, IDetencionService detenciones, TimeZoneInfo zonaPlanta,
        ILogger<DeclaracionDetencionService> logger, Func<DateTimeOffset> ahora)
    {
        _unitOfWork = unitOfWork;
        _detenciones = detenciones;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
        _ahora = ahora;
    }

    public async Task<Result<DeclaracionDetencionDto>> ObtenerAsync(long detencionId, CancellationToken cancellationToken = default)
    {
        var d = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (d == null)
            return Result<DeclaracionDetencionDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        PuntoControlCalidad? punto = d.PuntoControlCalidadId is { } pid
            ? await _unitOfWork.PuntosControlCalidad.GetByIdAsync(pid, cancellationToken)
            : null;
        Equipo? cuerpo = punto?.EquipoId is { } eid ? await _unitOfWork.Equipos.GetByIdAsync(eid, cancellationToken) : null;
        UsuarioLinea? maquinista = d.UsuarioLineaId is { } ulid ? await _unitOfWork.UsuariosLineas.GetByIdAsync(ulid, cancellationToken) : null;

        return Result<DeclaracionDetencionDto>.Success(new DeclaracionDetencionDto
        {
            DetencionId = d.DetencionId,
            Codigo = d.Codigo,
            LineaId = d.LineaId,
            LineaNombre = d.Linea?.Nombre ?? string.Empty,
            TurnoNombre = d.Turno?.Nombre ?? string.Empty,
            Fecha = d.Fecha,
            FechaHoraParada = HoraPlanta.En(d.FechaHoraParada, _zonaPlanta),
            FechaHoraArranqueDetectada = HoraPlanta.En(d.FechaHoraArranqueDetectada, _zonaPlanta),
            OrigenRegistro = d.OrigenRegistro.ToString(),
            PuntoControlCodigo = punto?.Codigo,
            PuntoControlNombre = punto?.Nombre,
            CuerpoCamaraNombre = cuerpo?.Nombre,
            CuerpoCamaraEquipoId = cuerpo?.EquipoId,
            MaquinistaUsuarioId = maquinista?.UsuarioId,
            EstadoActual = d.EstadoActual.ToString(),
            EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion(),
            PendienteDeclaracion = d.EsPendienteDeclaracion,
            RowVersion = d.RowVersion
        });
    }

    public async Task<Result<DeclararDetencionResponse>> DeclararAsync(
        long detencionId, DeclararDetencionRequest request, string usuarioId, UsuarioLinea? asignacionPropia,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<DeclararDetencionResponse>.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!detencion.RowVersion.SequenceEqual(request.RowVersion ?? []))
            return Result<DeclararDetencionResponse>.Failure("CONCURRENCY", "Alguien modificó esta detención mientras la declarabas. Recargá la página.");

        var arranque = ArranqueDesdeHora(detencion.FechaHoraParada, request.HoraArranque);
        var motivo = DetencionCamaraReglas.MotivoDeclaracionInvalida(
            detencion, request.Descripcion, arranque, request.MinutosDeclarados, _ahora());
        if (motivo != null)
            return Result<DeclararDetencionResponse>.Failure("VALIDATION", motivo);

        if (request.EquipoId is { } equipoId)
        {
            var equipo = await _unitOfWork.Equipos.GetByIdAsync(equipoId, cancellationToken);
            if (equipo == null || equipo.LineaId != detencion.LineaId)
                return Result<DeclararDetencionResponse>.Failure("VALIDATION", "El equipo no es de la línea de la detención.");
        }

        if (request.TipoFallaId is { } tipoFallaId && await _unitOfWork.TiposFalla.GetByIdAsync(tipoFallaId, cancellationToken) == null)
            return Result<DeclararDetencionResponse>.Failure("VALIDATION", "El tipo de falla elegido no existe.");

        // El detalle tiene que corresponder al equipo y al tipo elegidos (los mismos que ofrece la pantalla).
        if (request.DetalleFallaId is { } detalleFallaId
            && !await _unitOfWork.DetallesFalla.EsCoherenteAsync(detalleFallaId, request.EquipoId, request.TipoFallaId, cancellationToken))
            return Result<DeclararDetencionResponse>.Failure("VALIDATION",
                "El detalle de falla no corresponde al equipo o al tipo de falla elegidos. Volvé a elegirlo.");

        var (origen, minutos) = DetencionCamaraReglas.TiempoDeclarado(detencion.FechaHoraParada, arranque, request.MinutosDeclarados);
        detencion.EquipoId = request.EquipoId;
        detencion.TipoFallaId = request.TipoFallaId;
        detencion.DetalleFallaId = request.DetalleFallaId;
        detencion.Descripcion = request.Descripcion.Trim();
        detencion.FechaHoraArranque = arranque;
        detencion.MinutosDeclarados = origen == OrigenTiempo.DECLARADO ? minutos : null;
        detencion.OrigenTiempo = origen;
        detencion.MinutosEfectivos = minutos;
        detencion.ModificadoPorUsuarioId = usuarioId;
        // Maquinista: si el turno no tenía uno claro, queda quien declara (si está asignado a la línea).
        if (detencion.UsuarioLineaId == null && asignacionPropia?.LineaId == detencion.LineaId)
            detencion.UsuarioLineaId = asignacionPropia.UsuarioLineaId;

        detencion.CambiarEstado(EstadoDetencion.REGISTRADA, usuarioId, "Detención declarada por el operador");
        await _detenciones.AplicarReglaAnalisisAsync(detencion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención automática {Codigo} declarada por {UsuarioId}: {Minutos} min ({Origen})",
            detencion.Codigo, usuarioId, minutos, origen);

        return Result<DeclararDetencionResponse>.Success(new DeclararDetencionResponse
        {
            DetencionId = detencion.DetencionId,
            Codigo = detencion.Codigo,
            Estado = detencion.EstadoActual.ToString(),
            MinutosEfectivos = detencion.MinutosEfectivos,
            RequiereAnalisis = detencion.RequiereAnalisis
        });
    }

    public async Task<AvisoFinTurnoDto> ObtenerAvisoFinTurnoAsync(
        IReadOnlyCollection<int>? lineasIds, int minutosAviso = DetencionCamaraReglas.MinutosAvisoFinTurnoPorDefecto,
        CancellationToken cancellationToken = default)
    {
        var aviso = new AvisoFinTurnoDto { MinutosAviso = minutosAviso };
        if (lineasIds is { Count: 0 })
            return aviso;

        aviso.Pendientes = lineasIds == null
            ? await _unitOfWork.Detenciones.CountAsync(d => d.EstadoActual == EstadoDetencion.PENDIENTE_JUSTIFICACION, cancellationToken)
            : await _unitOfWork.Detenciones.CountAsync(
                d => d.EstadoActual == EstadoDetencion.PENDIENTE_JUSTIFICACION && lineasIds.Contains(d.LineaId), cancellationToken);

        var ahora = HoraPlanta.Ahora(_zonaPlanta, _ahora());
        var hora = TimeOnly.FromDateTime(ahora.DateTime);
        var turno = await _unitOfWork.Turnos.GetTurnoPorHoraAsync(new TimeOnly(hora.Hour, hora.Minute), cancellationToken);
        if (turno != null)
        {
            var inicioPared = DetencionCamaraReglas.InicioTurnoEnCurso(turno, ahora.DateTime);
            var inicio = HoraPlanta.DesdeHoraDePared(inicioPared, _zonaPlanta);
            var turnoId = turno.TurnoId;
            aviso.TurnoId = turnoId;
            aviso.TurnoNombre = turno.Nombre;
            aviso.FechaInicioTurno = DateOnly.FromDateTime(inicioPared);
            aviso.MinutosParaFin = DetencionCamaraReglas.MinutosParaFinDeTurno(turno, hora);
            if (aviso.Pendientes > 0)
            {
                // Las del turno en curso: pararon en este turno desde que empezó (no las de la noche anterior).
                aviso.PendientesTurnoActual = await _unitOfWork.Detenciones.CountAsync(d =>
                    d.EstadoActual == EstadoDetencion.PENDIENTE_JUSTIFICACION
                    && (lineasIds == null || lineasIds.Contains(d.LineaId))
                    && d.TurnoId == turnoId
                    && d.FechaHoraParada >= inicio, cancellationToken);
            }
        }
        aviso.Mostrar = DetencionCamaraReglas.CorrespondeAvisoFinTurno(aviso.MinutosParaFin, aviso.Pendientes, minutosAviso);
        return aviso;
    }

    /// <summary>Hora de pared de arranque → instante, el primero posterior a la parada (puede ser del día siguiente).</summary>
    internal DateTimeOffset? ArranqueDesdeHora(DateTimeOffset? parada, TimeOnly? horaArranque)
    {
        if (parada is not { } p || horaArranque is not { } h)
            return null;
        var paradaPlanta = TimeZoneInfo.ConvertTime(p, _zonaPlanta);
        var candidato = HoraPlanta.DesdeHoraDePared(paradaPlanta.Date.Add(h.ToTimeSpan()), _zonaPlanta);
        // Sólo pasa al día siguiente si es de un minuto anterior al de la parada (HH:mm no tiene segundos).
        var minutoParada = new DateTimeOffset(paradaPlanta.Year, paradaPlanta.Month, paradaPlanta.Day,
            paradaPlanta.Hour, paradaPlanta.Minute, 0, paradaPlanta.Offset);
        if (candidato < minutoParada)
            candidato = HoraPlanta.DesdeHoraDePared(paradaPlanta.Date.AddDays(1).Add(h.ToTimeSpan()), _zonaPlanta);
        return candidato;
    }
}
