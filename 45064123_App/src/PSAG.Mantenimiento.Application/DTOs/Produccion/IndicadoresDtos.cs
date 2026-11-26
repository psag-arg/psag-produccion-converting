using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.DTOs.Produccion;

// T-798 etapa 5 · Indicadores: presupuesto (PRD_ParametrosLineaProducto, snapshot en el tramo) vs. real
// (PRD_ProduccionesTurno + detenciones que cuentan). Horas en hora de planta. "Sin datos" nunca es 0.

/// <summary>Acción del supervisor sobre la producción oficial de un tramo (POST api/turnos-produccion/{id}/produccion).</summary>
public static class AccionProduccionTurno
{
    /// <summary>Confirma lo de la cámara: CantidadOficial = CantidadCamara, VALIDADA.</summary>
    public const string Validar = "VALIDAR";
    /// <summary>Corrige lo de la cámara (CAMARA_CORREGIDA, con motivo), VALIDADA.</summary>
    public const string Corregir = "CORREGIR";
    /// <summary>Carga a mano (MANUAL; p. ej. bolsones sin cámara), VALIDADA.</summary>
    public const string Manual = "MANUAL";

    public static readonly IReadOnlyList<string> Todas = [Validar, Corregir, Manual];
}

public class RegistrarProduccionTurnoRequest
{
    /// <summary>Tramo de producto (cerrado) del turno.</summary>
    public long TurnoProduccionProductoId { get; set; }

    /// <summary>MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS.</summary>
    public string Magnitud { get; set; } = string.Empty;

    /// <summary>VALIDAR, CORREGIR o MANUAL.</summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>Obligatoria en CORREGIR y MANUAL (&gt;= 0). Se ignora en VALIDAR.</summary>
    public decimal? Cantidad { get; set; }

    /// <summary>Obligatorio en CORREGIR; opcional en MANUAL.</summary>
    public string? Motivo { get; set; }
}

/// <summary>Fila de producción oficial (vigente o histórica) de un tramo.</summary>
public class ProduccionTurnoDto
{
    public long ProduccionTurnoId { get; set; }
    public long TurnoProduccionProductoId { get; set; }
    public string Magnitud { get; set; } = string.Empty;
    public decimal? CantidadCamara { get; set; }
    public int? MinutosConDatos { get; set; }
    public decimal? CantidadOficial { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string EstadoValidacion { get; set; } = string.Empty;
    public string? ValidadoPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraValidacion { get; set; }
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public string? Motivo { get; set; }
    public long? AnuladoPorId { get; set; }

    public static ProduccionTurnoDto De(ProduccionTurno p, TimeZoneInfo zonaPlanta) => new()
    {
        ProduccionTurnoId = p.ProduccionTurnoId,
        TurnoProduccionProductoId = p.TurnoProduccionProductoId,
        Magnitud = p.Magnitud,
        CantidadCamara = p.CantidadCamara,
        MinutosConDatos = p.MinutosConDatos,
        CantidadOficial = p.CantidadOficial,
        Origen = p.Origen.ToString(),
        EstadoValidacion = p.EstadoValidacion.ToString(),
        ValidadoPorUsuarioId = p.ValidadoPorUsuarioId,
        FechaHoraValidacion = HoraPlanta.En(p.FechaHoraValidacion, zonaPlanta),
        RegistradoPorUsuarioId = p.RegistradoPorUsuarioId,
        FechaHoraRegistro = HoraPlanta.En(p.FechaHoraRegistro, zonaPlanta)!.Value,
        Motivo = p.Motivo,
        AnuladoPorId = p.AnuladoPorId
    };
}

/// <summary>Real vs. objetivo de una magnitud (suma de tramos; el % sólo con los tramos que tienen los dos datos).</summary>
public class ComparacionProduccionDto
{
    public string Magnitud { get; set; } = string.Empty;

    /// <summary>Objetivo contra el que se mide el %: a la hora si el turno está abierto, el del turno si cerró.</summary>
    public decimal? Objetivo { get; set; }

    /// <summary>T-805 · Objetivo del TURNO COMPLETO (misma regla, <c>ObjetivoTurnoReglas</c>); igual a Objetivo si cerró.</summary>
    public decimal? ObjetivoTurno { get; set; }

    public decimal? Real { get; set; }

    /// <summary>Σ real ÷ Σ objetivo × 100 (un decimal). Null si el estado no es OK.</summary>
    public decimal? Porcentaje { get; set; }

    /// <summary>OK, SIN_OBJETIVO o SIN_DATOS.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Incluye real PENDIENTE (sin validar) o PARCIAL (tramo abierto).</summary>
    public bool Provisorio { get; set; }

    /// <summary>Tramos con objetivo y sin real (no entran en el %).</summary>
    public int TramosSinDatos { get; set; }

    /// <summary>Tramos (o turnos sin producto) sin objetivo.</summary>
    public int TramosSinObjetivo { get; set; }
}

/// <summary>Una magnitud de un tramo: objetivo prorrateado, real y de dónde sale.</summary>
public class MagnitudTramoDto
{
    public string Magnitud { get; set; } = string.Empty;
    public decimal? Objetivo { get; set; }

    /// <summary>T-805 · Objetivo del tramo para el turno completo (con los huecos sin producto que le tocan).</summary>
    public decimal? ObjetivoTurno { get; set; }
    public decimal? Real { get; set; }

    /// <summary>VALIDADA, PENDIENTE, PARCIAL o SIN_DATOS.</summary>
    public string EstadoReal { get; set; } = string.Empty;
    public bool Provisorio { get; set; }

    /// <summary>La línea tiene cámara oficial de esta magnitud.</summary>
    public bool TieneCamaraOficial { get; set; }

    /// <summary>Fila vigente de PRD_ProduccionesTurno (null si el tramo está abierto o no hay fila).</summary>
    public ProduccionTurnoDto? Vigente { get; set; }
}

public class TramoIndicadorDto
{
    public long TurnoProduccionProductoId { get; set; }
    public int ProductoMaestroId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public bool Abierto { get; set; }

    /// <summary>Duración del tramo (hasta ahora si está abierto).</summary>
    public decimal MinutosTramo { get; set; }

    /// <summary>Minutos que cuentan para el objetivo del %: a la hora (turno abierto) o del turno (cerrado), según ObjetivoTurnoReglas (T-805).</summary>
    public decimal MinutosObjetivo { get; set; }

    /// <summary>T-805 · Minutos del turno completo que le tocan al tramo (su duración + huecos sin producto, escalados).</summary>
    public decimal MinutosObjetivoTurno { get; set; }

    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; }
    public decimal? MetrosPorManga { get; set; }

    public MagnitudTramoDto Mangas { get; set; } = new();
    public MagnitudTramoDto Bolsones { get; set; } = new();

    /// <summary>Eficiencia del tramo (mangas).</summary>
    public ComparacionProduccionDto Eficiencia { get; set; } = new();

    /// <summary>m/min en los minutos en marcha de la cámara oficial de mangas.</summary>
    public decimal? VelocidadMetrosMinuto { get; set; }
    public int MinutosMarcha { get; set; }

    /// <summary>Todas las filas de producción oficial del tramo (vigentes y anuladas), en orden de registro.</summary>
    public List<ProduccionTurnoDto> Producciones { get; set; } = [];
}

/// <summary>GET api/indicadores/turno/{id}: detalle del turno.</summary>
public class IndicadoresTurnoDto
{
    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int TurnoId { get; set; }
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly FechaProduccion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Abierto { get; set; }

    /// <summary>T-805 · El "ahora" (hora de planta) con el que se calcularon todos los números de la respuesta.</summary>
    public DateTimeOffset CalculadoEn { get; set; }

    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }

    /// <summary>Snapshot del turno; si era 0, recalculado desde la programación.</summary>
    public int MinutosProgramados { get; set; }

    /// <summary>Σ MinutosEfectivos de las detenciones del turno que cuentan para indicadores.</summary>
    public int MinutosParadaQueCuentan { get; set; }

    /// <summary>% (un decimal); null sin minutos programados.</summary>
    public decimal? Indisponibilidad { get; set; }

    /// <summary>Turno sin tramos de producto: "sin objetivo".</summary>
    public bool ACompletar { get; set; }

    public ComparacionProduccionDto Mangas { get; set; } = new();
    public ComparacionProduccionDto Bolsones { get; set; } = new();

    /// <summary>= Mangas (Σ real ÷ Σ objetivo prorrateado).</summary>
    public ComparacionProduccionDto Eficiencia { get; set; } = new();

    public decimal? VelocidadMetrosMinuto { get; set; }
    public List<TramoIndicadorDto> Tramos { get; set; } = [];
}

/// <summary>Cómo agrupar el presupuesto vs. real.</summary>
public static class AgrupacionIndicadores
{
    public const string Turno = "turno";
    public const string Dia = "dia";
    public const string Producto = "producto";
    public const string Linea = "linea";

    public static readonly IReadOnlyList<string> Todas = [Turno, Dia, Producto, Linea];
}

public class IndicadoresProduccionFiltro
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int? LineaId { get; set; }
    public int? ProductoMaestroId { get; set; }

    /// <summary>turno, dia, producto o linea (por defecto turno).</summary>
    public string? Agrupar { get; set; }
}

/// <summary>Una fila del presupuesto vs. real (o el total).</summary>
public class FilaPresupuestoRealDto
{
    /// <summary>Clave del grupo (id del turno, fecha yyyy-MM-dd, id de producto o de línea; "total").</summary>
    public string Clave { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;

    public long? TurnoProduccionId { get; set; }
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public DateOnly? FechaProduccion { get; set; }
    public string? TurnoCodigo { get; set; }
    public string? TurnoNombre { get; set; }
    public int? ProductoMaestroId { get; set; }
    public string? ProductoNombre { get; set; }

    /// <summary>Turnos de producción que entran en la fila.</summary>
    public int Turnos { get; set; }

    /// <summary>Hay algún turno abierto (el real es parcial).</summary>
    public bool IncluyeAbiertos { get; set; }

    /// <summary>Null al agrupar o filtrar por producto (las paradas son del turno, no del tramo).</summary>
    public int? MinutosProgramados { get; set; }
    public int? MinutosParadaQueCuentan { get; set; }
    public decimal? Indisponibilidad { get; set; }

    public ComparacionProduccionDto Mangas { get; set; } = new();
    public ComparacionProduccionDto Bolsones { get; set; } = new();
    public ComparacionProduccionDto Eficiencia { get; set; } = new();
    public decimal? VelocidadMetrosMinuto { get; set; }

    /// <summary>T-803 · Microcortes (paradas de cámara más cortas que el umbral). Null si la línea no tiene cámara de conteo.</summary>
    public int? Microcortes { get; set; }
    public int? SegundosMicrocortes { get; set; }
    public int? MinutosMicrocortes { get; set; }
}

/// <summary>GET api/indicadores/produccion: presupuesto vs. real por línea, turno, producto o día en un período.</summary>
public class PresupuestoRealDto
{
    /// <summary>T-805 · El "ahora" (hora de planta) con el que se calcularon los turnos abiertos.</summary>
    public DateTimeOffset CalculadoEn { get; set; }

    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int? LineaId { get; set; }
    public int? ProductoMaestroId { get; set; }
    public string Agrupar { get; set; } = AgrupacionIndicadores.Turno;
    public List<FilaPresupuestoRealDto> Filas { get; set; } = [];
    public FilaPresupuestoRealDto Total { get; set; } = new();
}
