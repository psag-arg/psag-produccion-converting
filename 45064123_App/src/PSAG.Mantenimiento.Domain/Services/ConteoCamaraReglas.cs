using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del conteo de producción y paradas por cámara (T-610). Funciones puras: las usa la API para validar
/// la ingesta y para cruzar las paradas de la cámara con las detenciones cargadas.
/// </summary>
public static class ConteoCamaraReglas
{
    public const int CarrilMaximo = 16;
    public const int MaximoMinutosPorLote = 5000;
    public const int MaximoParadasPorLote = 500;
    public const int LargoMaximoIdentificador = 100;

    /// <summary>Una parada de más de 7 días es un error del servicio (reloj, estado colgado).</summary>
    public static readonly TimeSpan DuracionMaximaParada = TimeSpan.FromDays(7);

    /// <summary>
    /// Margen para el cruce con detenciones: el operario anota la hora a mano, así que una detención que empieza
    /// o termina hasta 5 minutos fuera de la parada de cámara se toma como la misma.
    /// </summary>
    public static readonly TimeSpan ToleranciaCruce = TimeSpan.FromMinutes(5);

    /// <summary>Motivo por el que el minuto no se puede guardar, o null si está bien.</summary>
    public static string? MotivoMinutoInvalido(DateTimeOffset minuto, int carril, decimal rollos,
        int marcha, int parada, int acumulacion, int sinSenal)
    {
        if (minuto.Second != 0 || minuto.Millisecond != 0)
            return "El minuto tiene que venir con los segundos en cero.";
        if (carril < 1 || carril > CarrilMaximo)
            return $"El carril tiene que estar entre 1 y {CarrilMaximo}.";
        if (rollos < 0 || rollos > 10000)
            return "Los rollos del minuto tienen que estar entre 0 y 10000.";
        if (marcha < 0 || parada < 0 || acumulacion < 0 || sinSenal < 0)
            return "Los segundos no pueden ser negativos.";
        if (marcha + parada + acumulacion + sinSenal != 60)
            return "Los segundos de marcha, parada, acumulación y sin señal tienen que sumar 60.";
        return null;
    }

    /// <summary>Motivo por el que la parada no se puede guardar, o null si está bien.</summary>
    public static string? MotivoParadaInvalida(DateTimeOffset inicio, DateTimeOffset fin, IReadOnlyCollection<int> carriles, string? identificador)
    {
        if (string.IsNullOrWhiteSpace(identificador))
            return "Falta el identificadorExterno de la parada.";
        if (identificador.Trim().Length > LargoMaximoIdentificador)
            return $"identificadorExterno admite hasta {LargoMaximoIdentificador} caracteres.";
        if (fin <= inicio)
            return "El fin de la parada tiene que ser posterior al inicio.";
        if (fin - inicio > DuracionMaximaParada)
            return "La parada dura más de 7 días: revisar el reloj del servicio.";
        if (carriles.Count == 0)
            return "La parada tiene que indicar al menos un carril.";
        if (carriles.Any(c => c < 1 || c > CarrilMaximo))
            return $"Los carriles tienen que estar entre 1 y {CarrilMaximo}.";
        return null;
    }

    /// <summary>"1,2,4" ordenado y sin repetidos.</summary>
    public static string CarrilesTexto(IEnumerable<int> carriles) => string.Join(",", carriles.Distinct().OrderBy(c => c));

    /// <summary>
    /// Intervalo de una detención, si tiene horario: desde la hora de parada hasta la de arranque, o hasta
    /// parada + MinutosEfectivos si no tiene arranque. Sin hora de parada no hay intervalo (Fase 1: sólo minutos).
    /// </summary>
    public static (DateTimeOffset Desde, DateTimeOffset Hasta)? IntervaloDetencion(Detencion d)
    {
        if (d.FechaHoraParada is not { } parada)
            return null;
        var hasta = d.FechaHoraArranque ?? parada.AddMinutes(Math.Max(0, d.MinutosEfectivos));
        return (parada, hasta < parada ? parada : hasta);
    }

    /// <summary>
    /// Cruza una parada de cámara con las detenciones (no eliminadas) de su línea.
    /// CON_DETENCION si alguna con horario se superpone (con la tolerancia); si no, POSIBLE si hay alguna
    /// sin horario del mismo día y turno; si no, SIN_DETENCION. Devuelve también los códigos que la explican.
    /// </summary>
    public static (CruceDetencionCamara Cruce, IReadOnlyList<string> Codigos) Cruzar(
        ParadaCamara parada, DateOnly diaPlanta, IEnumerable<Detencion> detenciones)
    {
        var desde = parada.FechaHoraInicio - ToleranciaCruce;
        var hasta = parada.FechaHoraFin + ToleranciaCruce;
        var candidatas = detenciones.Where(d => !d.Eliminada && d.LineaId == parada.LineaId).ToList();

        var conHorario = candidatas
            .Where(d => IntervaloDetencion(d) is { } i && i.Desde <= hasta && i.Hasta >= desde)
            .Select(d => d.Codigo)
            .ToList();
        if (conHorario.Count > 0)
            return (CruceDetencionCamara.CON_DETENCION, conHorario);

        var posibles = candidatas
            .Where(d => d.FechaHoraParada == null && d.Fecha == diaPlanta && (parada.TurnoId == null || d.TurnoId == parada.TurnoId))
            .Select(d => d.Codigo)
            .ToList();
        if (posibles.Count > 0)
            return (CruceDetencionCamara.POSIBLE, posibles);

        return (CruceDetencionCamara.SIN_DETENCION, Array.Empty<string>());
    }
}
