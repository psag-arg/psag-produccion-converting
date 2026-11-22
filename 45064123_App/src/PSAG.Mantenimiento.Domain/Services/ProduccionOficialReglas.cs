using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Un minuto de una cámara, liviano (sin navegación), para los cálculos de producción y velocidad.</summary>
public readonly record struct MinutoConteo(int CamaraId, DateTimeOffset Minuto, decimal Cantidad, short SegundosMarcha);

/// <summary>Estado del real de un tramo para los indicadores.</summary>
public static class EstadoRealProduccion
{
    /// <summary>Validada por el supervisor (cámara, cámara corregida o carga manual).</summary>
    public const string Validada = "VALIDADA";
    /// <summary>De la cámara, con cobertura suficiente, sin validar: cuenta como provisorio.</summary>
    public const string Pendiente = "PENDIENTE";
    /// <summary>Tramo abierto: suma al vuelo de la cámara (no se guarda), provisorio.</summary>
    public const string Parcial = "PARCIAL";
    /// <summary>Sin dato (sin cámara, cobertura insuficiente o tramo cerrado sin fila): nunca 0.</summary>
    public const string SinDatos = "SIN_DATOS";
}

/// <summary>Real de un tramo y una magnitud para los indicadores. <see cref="Cantidad"/> null = "sin datos".</summary>
public sealed record RealProduccion(decimal? Cantidad, string Estado, bool Provisorio)
{
    public static readonly RealProduccion SinDatos = new(null, EstadoRealProduccion.SinDatos, false);

    public bool TieneDato => Cantidad != null;
}

/// <summary>
/// Producción oficial (PRD_ProduccionesTurno, T-798 2.6 y 2.10, etapa 5). Funciones puras. ÚNICO lugar de:
/// <list type="bullet">
/// <item>qué cuenta como real para los indicadores (<see cref="RealParaIndicadores"/>);</item>
/// <item>cómo se arma la fila de la cámara al cerrar un tramo (<see cref="FilaDeCamara"/>: suma, minutos con datos y
/// cobertura contra el umbral);</item>
/// <item>el parcial de un tramo abierto (<see cref="Parcial"/>).</item>
/// </list>
/// </summary>
public static class ProduccionOficialReglas
{
    /// <summary>Cobertura mínima por defecto (minutos con datos ÷ minutos del tramo). Configurable: TurnosProduccion:CoberturaMinimaProduccion.</summary>
    public const decimal CoberturaMinimaPorDefecto = 0.90m;

    /// <summary>Fila vigente (no anulada) de la magnitud; si por error hubiera más de una, la última registrada.</summary>
    public static ProduccionTurno? Vigente(IEnumerable<ProduccionTurno> filas, string magnitud) =>
        filas.Where(f => f.AnuladoPorId == null && f.Magnitud == magnitud)
            .OrderByDescending(f => f.FechaHoraRegistro)
            .ThenByDescending(f => f.ProduccionTurnoId)
            .FirstOrDefault();

    /// <summary>
    /// Qué cuenta como real: <c>CantidadOficial</c> de la fila vigente si está VALIDADA o PENDIENTE (pendiente =
    /// provisorio, marcado). SIN_DATOS, sin fila o sin cantidad → "sin datos" (nunca 0).
    /// </summary>
    public static RealProduccion RealParaIndicadores(ProduccionTurno? vigente)
    {
        if (vigente?.CantidadOficial is not { } cantidad)
            return RealProduccion.SinDatos;
        return vigente.EstadoValidacion switch
        {
            EstadoValidacionProduccion.VALIDADA => new RealProduccion(cantidad, EstadoRealProduccion.Validada, false),
            EstadoValidacionProduccion.PENDIENTE => new RealProduccion(cantidad, EstadoRealProduccion.Pendiente, true),
            _ => RealProduccion.SinDatos
        };
    }

    /// <summary>Parcial de un tramo abierto (al vuelo, no se guarda): provisorio; sin ningún minuto, "sin datos".</summary>
    public static RealProduccion Parcial(decimal cantidad, int minutosConDatos) =>
        minutosConDatos <= 0 ? RealProduccion.SinDatos : new RealProduccion(cantidad, EstadoRealProduccion.Parcial, true);

    /// <summary>Σ cantidad y minutos con fila de la cámara con Minuto en [desde, hasta).</summary>
    public static (decimal Cantidad, int MinutosConDatos) ConteoDelTramo(
        IEnumerable<MinutoConteo> minutos, int camaraId, DateTimeOffset desde, DateTimeOffset hasta)
    {
        var delTramo = minutos.Where(m => m.CamaraId == camaraId && m.Minuto >= desde && m.Minuto < hasta).ToList();
        return (delTramo.Sum(m => m.Cantidad), delTramo.Select(m => m.Minuto).Distinct().Count());
    }

    /// <summary>Minutos del tramo [desde, hasta) (con fracción).</summary>
    public static decimal MinutosTramo(DateTimeOffset desde, DateTimeOffset hasta) =>
        hasta > desde ? (decimal)(hasta - desde).TotalMinutes : 0m;

    /// <summary>¿Los minutos con datos cubren al menos el umbral de los minutos del tramo?</summary>
    public static bool CoberturaSuficiente(int minutosConDatos, decimal minutosTramo, decimal coberturaMinima) =>
        minutosTramo > 0 && minutosConDatos / minutosTramo >= coberturaMinima;

    /// <summary>
    /// Valores de la fila CAMARA al cerrar el tramo: con cobertura suficiente, PENDIENTE con CantidadOficial =
    /// CantidadCamara; si no, SIN_DATOS con CantidadOficial null (la cantidad contada queda en CantidadCamara).
    /// </summary>
    public static (decimal CantidadCamara, int MinutosConDatos, decimal? CantidadOficial, EstadoValidacionProduccion Estado) FilaDeCamara(
        decimal cantidad, int minutosConDatos, decimal minutosTramo, decimal coberturaMinima)
    {
        var redondeada = Math.Round(cantidad, 1, MidpointRounding.AwayFromZero);
        return CoberturaSuficiente(minutosConDatos, minutosTramo, coberturaMinima)
            ? (redondeada, minutosConDatos, redondeada, EstadoValidacionProduccion.PENDIENTE)
            : (redondeada, minutosConDatos, null, EstadoValidacionProduccion.SIN_DATOS);
    }
}
