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
/// Conteo de producción y paradas por cámara (T-610). La cámara sólo informa: no crea detenciones.
/// El cruce con detenciones se calcula al consultar, porque la detención se carga después de la parada.
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

        if (respuesta.MinutosNuevos + respuesta.ParadasNuevas > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Conteo por cámara {Punto}: {MinNuevos} minutos nuevos ({MinDup} repetidos), {ParNuevas} paradas nuevas ({ParDup} repetidas)",
            codigo, respuesta.MinutosNuevos, respuesta.MinutosDuplicados, respuesta.ParadasNuevas, respuesta.ParadasDuplicadas);
        return Result<IngestaConteoCamaraRespuesta>.Success(respuesta);
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
        DateOnly fecha, int? lineaId, CancellationToken cancellationToken = default)
    {
        var minutos = await _unitOfWork.ConteosCamaraMinuto.GetRangoAsync(
            InicioDelDia(fecha), InicioDelDia(fecha.AddDays(1)), lineaId, cancellationToken);

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
            MinutosMarcha = porMinuto.Count(g => g.All(m => m.Estado == EstadoCarrilCamara.MARCHA))
        });
    }

    private DateTimeOffset InicioDelDia(DateOnly fecha) =>
        HoraPlanta.DesdeHoraDePared(fecha.ToDateTime(TimeOnly.MinValue), _zonaPlanta);
}
