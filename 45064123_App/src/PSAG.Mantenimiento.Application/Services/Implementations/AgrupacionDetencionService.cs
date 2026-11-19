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
/// Agrupador de detenciones (reunión 17/11/2026, pedido de Emiliano): se guarda todo (mnt.ParadasCamara), se marca
/// lo que supera el umbral (SuperaUmbral, script 31) y los microcortes previos se agrupan bajo la detención en
/// mnt.DetencionAgrupacion (script 32) sin modificar los registros originales.
/// <para>
/// IMPORTANTE: agrupar NO cambia MinutosEfectivos de la detención padre (insert-only, única verdad del tiempo).
/// El tiempo agrupado (inicio real, segundos totales) es sólo para reportes vía mnt.vw_DetencionesAgrupadas; acá se
/// calcula igual con <see cref="AgrupacionMicrocortesReglas.Resumir"/> para mostrarlo en pantalla.
/// </para>
/// Cada agrupación o desagrupación deja una entrada en el historial de la detención (quién, cuándo, qué paradas).
/// </summary>
public class AgrupacionDetencionService : IAgrupacionDetencionService
{
    /// <summary>OrigenAccion de las entradas del historial de la detención que deja el agrupador.</summary>
    public const string OrigenHistorial = "AGRUPADOR";

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<AgrupacionDetencionService> _logger;

    public AgrupacionDetencionService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<AgrupacionDetencionService> logger)
    {
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
    }

    public async Task<Result<MicrocortesPreviosDto>> BuscarMicrocortesAsync(long detencionId, CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<MicrocortesPreviosDto>.Failure("NOT_FOUND", "Detención no encontrada.");
        if (detencion.FechaHoraParada is not { } inicio)
            return Result<MicrocortesPreviosDto>.Success(new MicrocortesPreviosDto
            {
                Aviso = "La detención no tiene hora de parada: no se pueden buscar microcortes previos."
            });
        return Result<MicrocortesPreviosDto>.Success(
            await BuscarAsync(detencion.LineaId, detencion.PuntoControlCalidadId, inicio, cancellationToken));
    }

    public async Task<Result<MicrocortesPreviosDto>> BuscarMicrocortesAsync(
        int lineaId, int? puntoControlCalidadId, DateTimeOffset inicio, CancellationToken cancellationToken = default)
    {
        if (await _unitOfWork.Lineas.GetByIdAsync(lineaId, cancellationToken) == null)
            return Result<MicrocortesPreviosDto>.Failure("VALIDATION", "La línea no existe.");
        if (puntoControlCalidadId is { } pid)
        {
            var punto = await _unitOfWork.PuntosControlCalidad.GetByIdAsync(pid, cancellationToken);
            if (punto == null || punto.LineaId != lineaId)
                return Result<MicrocortesPreviosDto>.Failure("VALIDATION", "La cámara no es de esa línea.");
        }
        return Result<MicrocortesPreviosDto>.Success(await BuscarAsync(lineaId, puntoControlCalidadId, inicio, cancellationToken));
    }

    public async Task<Result<AgrupacionDetencionDto>> ObtenerAsync(long detencionId, CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<AgrupacionDetencionDto>.Failure("NOT_FOUND", "Detención no encontrada.");
        var activas = await _unitOfWork.AgrupacionesDetencion.GetActivasPorDetencionAsync(detencionId, cancellationToken);
        return Result<AgrupacionDetencionDto>.Success(Armar(detencion, activas.Select(a => a.ParadaCamara)));
    }

    public async Task<Result<AgrupacionDetencionDto>> AgruparAsync(
        long detencionId, IReadOnlyCollection<long> paradaCamaraIds, string usuarioId, CancellationToken cancellationToken = default)
    {
        var ids = paradaCamaraIds.Distinct().ToList();
        var (detencion, error) = await DetencionEditableAsync(detencionId, ids.Count, cancellationToken);
        if (error != null)
            return error;

        var paradas = (await _unitOfWork.ParadasCamara.GetPorIdsAsync(ids, cancellationToken)).ToDictionary(p => p.ParadaCamaraId);
        var yaAgrupadas = (await _unitOfWork.AgrupacionesDetencion.GetActivasPorParadasAsync(ids, cancellationToken))
            .Select(a => a.ParadaCamaraId).ToHashSet();
        var umbrales = await UmbralesAsync(paradas.Values.Select(p => p.PuntoControlCalidadId), cancellationToken);

        // Todo o nada: se valida cada parada antes de guardar.
        foreach (var id in ids)
        {
            paradas.TryGetValue(id, out var parada);
            var motivo = AgrupacionMicrocortesReglas.MotivoNoAgrupable(
                parada, detencion!.LineaId, detencion.PuntoControlCalidadId, detencion.FechaHoraParada,
                parada == null ? 0 : umbrales[parada.PuntoControlCalidadId], yaAgrupadas.Contains(id));
            if (motivo != null)
                return Result<AgrupacionDetencionDto>.Failure("VALIDATION", motivo);
        }

        foreach (var id in ids)
        {
            await _unitOfWork.AgrupacionesDetencion.AddAsync(new DetencionAgrupacion
            {
                DetencionId = detencionId,
                ParadaCamaraId = id,
                Origen = OrigenAgrupacion.OPERADOR,
                CreadoPorUsuarioId = usuarioId,
                FechaHoraCreacion = DateTimeOffset.UtcNow,
                Activo = true
            }, cancellationToken);
        }
        AgregarHistorial(detencion!, usuarioId, true, ids.Select(id => paradas[id]));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención {DetencionId}: {Cantidad} microcortes agrupados por {UsuarioId}", detencionId, ids.Count, usuarioId);
        return await ObtenerAsync(detencionId, cancellationToken);
    }

    public async Task<Result<AgrupacionDetencionDto>> DesagruparAsync(
        long detencionId, IReadOnlyCollection<long> paradaCamaraIds, string usuarioId, CancellationToken cancellationToken = default)
    {
        var ids = paradaCamaraIds.Distinct().ToList();
        var (detencion, error) = await DetencionEditableAsync(detencionId, ids.Count, cancellationToken);
        if (error != null)
            return error;

        var activas = (await _unitOfWork.AgrupacionesDetencion.GetActivasPorDetencionAsync(detencionId, cancellationToken))
            .ToDictionary(a => a.ParadaCamaraId);
        var faltantes = ids.Where(id => !activas.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
            return Result<AgrupacionDetencionDto>.Failure("VALIDATION", $"La parada {faltantes[0]} no está agrupada en esta detención.");

        var ahora = DateTimeOffset.UtcNow;
        foreach (var id in ids)
            activas[id].Desactivar(usuarioId, ahora);
        AgregarHistorial(detencion!, usuarioId, false, ids.Select(id => activas[id].ParadaCamara));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención {DetencionId}: {Cantidad} microcortes desagrupados por {UsuarioId}", detencionId, ids.Count, usuarioId);
        return await ObtenerAsync(detencionId, cancellationToken);
    }

    // ===================== Privados =====================

    private async Task<MicrocortesPreviosDto> BuscarAsync(
        int lineaId, int? puntoControlCalidadId, DateTimeOffset inicio, CancellationToken cancellationToken)
    {
        var desde = AgrupacionMicrocortesReglas.InicioVentana(inicio);
        var enVentana = await _unitOfWork.ParadasCamara.GetVentanaAsync(lineaId, puntoControlCalidadId, desde, inicio, cancellationToken);
        var umbrales = await UmbralesAsync(enVentana.Select(p => p.PuntoControlCalidadId), cancellationToken);
        var agrupadas = (await _unitOfWork.AgrupacionesDetencion.GetActivasPorParadasAsync(
            enVentana.Select(p => p.ParadaCamaraId).ToList(), cancellationToken)).Select(a => a.ParadaCamaraId).ToHashSet();

        var microcortes = enVentana
            .Where(p => AgrupacionMicrocortesReglas.EnVentana(p, inicio)
                        && AgrupacionMicrocortesReglas.EsMicrocorte(p, umbrales[p.PuntoControlCalidadId])
                        && !agrupadas.Contains(p.ParadaCamaraId))
            .Select(Mapear)
            .ToList();

        return new MicrocortesPreviosDto
        {
            Microcortes = microcortes,
            Pregunta = microcortes.Count == 0 ? null : AgrupacionMicrocortesReglas.Pregunta(microcortes.Select(m => m.FechaHoraInicio)),
            Desde = HoraPlanta.En(desde, _zonaPlanta),
            Hasta = HoraPlanta.En(inicio, _zonaPlanta)
        };
    }

    /// <summary>Umbral actual de cada cámara (sólo para las paradas viejas, sin marca guardada).</summary>
    private async Task<Dictionary<int, int>> UmbralesAsync(IEnumerable<int> puntoIds, CancellationToken cancellationToken)
    {
        var umbrales = new Dictionary<int, int>();
        foreach (var id in puntoIds.Distinct())
        {
            var punto = await _unitOfWork.PuntosControlCalidad.GetByIdAsync(id, cancellationToken);
            umbrales[id] = punto == null
                ? DetencionCamaraReglas.MinutosUmbralPorDefecto * 60
                : DetencionCamaraReglas.UmbralSegundos(punto);
        }
        return umbrales;
    }

    private async Task<(Detencion? Detencion, Result<AgrupacionDetencionDto>? Error)> DetencionEditableAsync(
        long detencionId, int cantidad, CancellationToken cancellationToken)
    {
        if (cantidad == 0)
            return (null, Result<AgrupacionDetencionDto>.Failure("VALIDATION", "Elegí al menos un microcorte."));
        if (cantidad > AgrupacionMicrocortesReglas.MaximoPorPedido)
            return (null, Result<AgrupacionDetencionDto>.Failure("VALIDATION",
                $"Se pueden agrupar hasta {AgrupacionMicrocortesReglas.MaximoPorPedido} microcortes por vez."));

        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return (null, Result<AgrupacionDetencionDto>.Failure("NOT_FOUND", "Detención no encontrada."));
        // Mismo criterio que la edición: una detención cerrada no se toca.
        if (detencion.EstaCerrada)
            return (null, Result<AgrupacionDetencionDto>.Failure("VALIDATION", "La detención está cerrada: no se pueden agrupar ni desagrupar microcortes."));
        return (detencion, null);
    }

    private void AgregarHistorial(Detencion detencion, string usuarioId, bool agrupar, IEnumerable<ParadaCamara> paradas)
    {
        detencion.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            DetencionId = detencion.DetencionId,
            EstadoAnterior = detencion.EstadoActual,
            EstadoNuevo = detencion.EstadoActual,
            FechaHora = DateTimeOffset.UtcNow,
            UsuarioId = usuarioId,
            Observacion = AgrupacionMicrocortesReglas.ObservacionHistorial(
                agrupar, paradas.Select(p => HoraPlanta.En(p.FechaHoraInicio, _zonaPlanta)!.Value)),
            OrigenAccion = OrigenHistorial
        });
    }

    private AgrupacionDetencionDto Armar(Detencion detencion, IEnumerable<ParadaCamara> activas)
    {
        var hijos = activas.ToList();
        var resumen = AgrupacionMicrocortesReglas.Resumir(detencion, hijos);
        return new AgrupacionDetencionDto
        {
            DetencionId = detencion.DetencionId,
            Agrupados = hijos.Select(Mapear).ToList(),
            InicioReal = HoraPlanta.En(resumen.InicioReal, _zonaPlanta),
            FinReal = HoraPlanta.En(resumen.FinReal, _zonaPlanta),
            CantidadMicrocortes = resumen.CantidadMicrocortes,
            SegundosMicrocortes = resumen.SegundosMicrocortes,
            SegundosDetencion = resumen.SegundosDetencion,
            SegundosTotales = resumen.SegundosTotales,
            MinutosEfectivos = detencion.MinutosEfectivos
        };
    }

    private MicrocorteDto Mapear(ParadaCamara p) => new()
    {
        ParadaCamaraId = p.ParadaCamaraId,
        PuntoControlCalidadId = p.PuntoControlCalidadId,
        FechaHoraInicio = HoraPlanta.En(p.FechaHoraInicio, _zonaPlanta)!.Value,
        FechaHoraFin = HoraPlanta.En(p.FechaHoraFin, _zonaPlanta)!.Value,
        DuracionSegundos = p.DuracionSegundos,
        Tipo = p.Tipo.ToString()
    };
}
