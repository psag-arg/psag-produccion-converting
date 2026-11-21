namespace PSAG.Mantenimiento.Web.Models;

// T-798 etapa 2 · Turno de producción (api/turnos-produccion). Copia de los DTOs de Application
// (PSAG.Mantenimiento.Application.DTOs.Produccion): mantener los mismos nombres de propiedad.

public class TurnoProduccionDto
{
    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int TurnoId { get; set; }
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly FechaProduccion { get; set; }
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }
    public int MinutosProgramados { get; set; }
    public DateTimeOffset? InicioReal { get; set; }
    public DateTimeOffset? FinReal { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string OrigenApertura { get; set; } = string.Empty;
    public string? AbiertoPorUsuarioId { get; set; }
    public string? AbiertoPorNombre { get; set; }
    public string? CerradoPorUsuarioId { get; set; }
    public string? CerradoPorNombre { get; set; }
    public string? ObservacionCierre { get; set; }

    /// <summary>Sin tramo de producto (abierto por SISTEMA o sin elegir): hay que completarlo.</summary>
    public bool ACompletar { get; set; }

    public TramoOperadorDto? OperadorActual { get; set; }
    public TramoProductoDto? ProductoActual { get; set; }

    /// <summary>Todos los tramos, también los anulados (historial de correcciones).</summary>
    public List<TramoOperadorDto> Operadores { get; set; } = [];
    public List<TramoProductoDto> Productos { get; set; } = [];
}

public class TramoOperadorDto
{
    public long TurnoProduccionOperadorId { get; set; }
    public string OperadorUsuarioId { get; set; } = string.Empty;
    public string? OperadorNombre { get; set; }
    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;
    public string? RegistradoPorNombre { get; set; }
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public string? Motivo { get; set; }
    public long? AnuladoPorId { get; set; }
    public bool Anulado => AnuladoPorId != null;
}

public class TramoProductoDto
{
    public long TurnoProduccionProductoId { get; set; }
    public int ProductoMaestroId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public string Origen { get; set; } = string.Empty;
    public int ParametroLineaProductoId { get; set; }
    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; }
    public decimal? MetrosPorManga { get; set; }
    public string DeclaradoPorUsuarioId { get; set; } = string.Empty;
    public string? DeclaradoPorNombre { get; set; }
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public string? Motivo { get; set; }
    public long? AnuladoPorId { get; set; }
    public bool Anulado => AnuladoPorId != null;
}

/// <summary>Producto que se puede elegir en la línea (parámetro vigente y habilitado) con su objetivo vigente.</summary>
public class ProductoHabilitadoDto
{
    public int ProductoMaestroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; }
}

public class OperadorHabilitadoDto
{
    public string UsuarioId { get; set; } = string.Empty;
    public string? Nombre { get; set; }
}

// ===================== Login del operario ("Inicio de turno") =====================

public class PropuestaTurnoProduccionDto
{
    /// <summary>Hay algo que preguntar (una línea para tomar o un aviso). Si es false, el login sigue de largo.</summary>
    public bool Preguntar { get; set; }

    /// <summary>Aviso (p. ej., no está habilitado en ninguna línea).</summary>
    public string? Aviso { get; set; }

    public int? TurnoId { get; set; }
    public string? TurnoCodigo { get; set; }
    public string? TurnoNombre { get; set; }
    public DateOnly? FechaProduccion { get; set; }
    public List<OpcionTurnoProduccionDto> Lineas { get; set; } = [];
}

public class OpcionTurnoProduccionDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;

    /// <summary>Null si el turno todavía no tiene registro (nadie lo abrió).</summary>
    public long? TurnoProduccionId { get; set; }

    /// <summary>ABIERTO, CERRADO, CERRADO_AUTOMATICO, SIN_APERTURA o null (sin registro).</summary>
    public string? Estado { get; set; }

    /// <summary>Se puede tomar: sin registro, SIN_APERTURA o ABIERTO (los cerrados, no).</summary>
    public bool PuedeTomar { get; set; }

    /// <summary>El usuario ya es el operador vigente.</summary>
    public bool SoyOperadorActual { get; set; }

    public string? OperadorActualUsuarioId { get; set; }
    public string? OperadorActualNombre { get; set; }
    public DateTimeOffset? OperadorActualDesde { get; set; }
    public int? ProductoActualId { get; set; }
    public string? ProductoActualNombre { get; set; }
    public List<ProductoHabilitadoDto> Productos { get; set; } = [];
}

// ===================== Requests y respuestas =====================

public class AbrirTurnoProduccionRequest
{
    public int LineaId { get; set; }
    public int? ProductoMaestroId { get; set; }

    /// <summary>El usuario confirmó que releva al operador que está (si hay otro).</summary>
    public bool Relevar { get; set; }
}

public class AperturaTurnoProduccionDto
{
    public TurnoProduccionDto? Turno { get; set; }

    /// <summary>Hay otro operador y no se pidió relevar: no se cambió nada; hay que preguntarle al usuario.</summary>
    public bool RequiereConfirmarRelevo { get; set; }

    public bool YaEraOperador { get; set; }
    public string? ReemplazadoUsuarioId { get; set; }
    public string? ReemplazadoNombre { get; set; }

    /// <summary>Turno anterior de la línea que se cerró (CERRADO_AUTOMATICO) al abrir este.</summary>
    public long? TurnoAnteriorCerradoId { get; set; }
}

public class CambiarProductoTurnoRequest
{
    public int ProductoMaestroId { get; set; }

    /// <summary>Hora del cambio (por defecto, ahora). Tiene que ser posterior al inicio del tramo vigente.</summary>
    public DateTimeOffset? Desde { get; set; }
}

public class CerrarTurnoProduccionRequest
{
    public string? Observacion { get; set; }
}

public class CorregirOperadorTurnoRequest
{
    public string OperadorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public string? Motivo { get; set; }

    /// <summary>Tramo que se reemplaza (queda anulado por el nuevo). Null = se agrega un tramo.</summary>
    public long? AnulaTramoId { get; set; }
}

public class CorregirProductoTurnoRequest
{
    public int ProductoMaestroId { get; set; }
    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public string? Motivo { get; set; }
    public long? AnulaTramoId { get; set; }
}

// ===================== Listado del supervisor =====================

public class TurnosProduccionDiaDto
{
    public DateOnly Fecha { get; set; }
    public List<FilaTurnoProduccionDto> Filas { get; set; } = [];
}

/// <summary>Una línea × turno de planta del día: con el registro si existe, o sólo lo programado si no.</summary>
public class FilaTurnoProduccionDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int TurnoId { get; set; }
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }

    /// <summary>Minutos programados: el snapshot del registro, o el plan de hoy si no hay registro.</summary>
    public int MinutosProgramados { get; set; }

    public bool EnCurso { get; set; }
    public TurnoProduccionDto? Turno { get; set; }
    public List<OperadorHabilitadoDto> OperadoresHabilitados { get; set; } = [];
    public List<ProductoHabilitadoDto> ProductosHabilitados { get; set; } = [];
}
