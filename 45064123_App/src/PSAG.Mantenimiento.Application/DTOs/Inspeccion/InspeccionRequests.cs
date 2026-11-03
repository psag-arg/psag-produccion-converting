namespace PSAG.Mantenimiento.Application.DTOs.Inspeccion;

/// <summary>
/// Iniciar una nueva ejecución de un plan de inspección.
/// </summary>
public class IniciarEjecucionRequest
{
    public int PlanInspeccionId { get; set; }

    /// <summary>Quién realiza (PACKAGING, MECANICO...). Vacío = todos los puntos del plan.</summary>
    public string? Puesto { get; set; }
}

/// <summary>
/// Respuesta al iniciar una ejecución.
/// </summary>
public class IniciarEjecucionResponse
{
    public long EjecucionInspeccionId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public int CantidadItems { get; set; }
    public bool EsNuevo { get; set; }
    public bool Completada { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Cerrar (firmar) una inspección: después no se edita. Pide respondidos todos los puntos que vencen hoy.
/// </summary>
public class CerrarEjecucionRequest
{
    public long EjecucionInspeccionId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public string? ObservacionesGenerales { get; set; }
}

/// <summary>
/// Guardar resultados de inspección.
/// </summary>
public class GuardarResultadosInspeccionRequest
{
    public long EjecucionInspeccionId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ResultadoItemRequest> Resultados { get; set; } = new();
    public string? ObservacionesGenerales { get; set; }
    public bool Completar { get; set; }
}

public class ResultadoItemRequest
{
    public int ItemPlanInspeccionId { get; set; }
    /// <summary>
    /// "OK" o "NO_OK"
    /// </summary>
    public string Resultado { get; set; } = string.Empty;
    /// <summary>
    /// Obligatorio si Resultado = "NO_OK"
    /// </summary>
    public string? Observacion { get; set; }
}

/// <summary>
/// Resolver un hallazgo NO OK.
/// </summary>
public class ResolverHallazgoRequest
{
    public long HallazgoNoOkId { get; set; }
    public string ObservacionResolucion { get; set; } = string.Empty;
    public bool RequiereAccionCorrectiva { get; set; }
    public string? AccionCorrectivaDescripcion { get; set; }
}

/// <summary>
/// Filtros para listar ejecuciones.
/// </summary>
public class EjecucionInspeccionFiltroRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int? LineaId { get; set; }
    public int? PlanInspeccionId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public bool? SoloConNoOk { get; set; }
    public int? EquipoId { get; set; }

    /// <summary>Quién realiza: PACKAGING, MECANICO...</summary>
    public string? Puesto { get; set; }

    /// <summary>Frecuencia en días (1 = diaria, 10 = cada 10 días).</summary>
    public int? FrecuenciaDias { get; set; }

    /// <summary>EN_CURSO o CERRADA.</summary>
    public string? Estado { get; set; }
}

/// <summary>
/// Filtros para listar hallazgos.
/// </summary>
public class HallazgoNoOkFiltroRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public bool? SoloPendientes { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}
