namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Hallazgo NO OK para seguimiento por supervisores/mantenimiento.
/// R17: Los hallazgos se marcan como resueltos, no se borran.
/// R18: INSERT-ONLY para mantener trazabilidad.
/// </summary>
public class HallazgoNoOk
{
    public long HallazgoNoOkId { get; set; }

    /// <summary>
    /// Detalle de ejecución que generó este hallazgo.
    /// </summary>
    public long EjecucionInspeccionDetalleId { get; set; }
    public EjecucionInspeccionDetalle EjecucionInspeccionDetalle { get; set; } = null!;

    /// <summary>
    /// Línea donde se detectó el hallazgo (denormalizado para consultas).
    /// </summary>
    public int LineaId { get; set; }
    public Linea Linea { get; set; } = null!;

    /// <summary>
    /// Equipo donde se detectó el hallazgo (opcional).
    /// </summary>
    public int? EquipoId { get; set; }
    public Equipo? Equipo { get; set; }

    /// <summary>
    /// Usuario que reportó el hallazgo.
    /// </summary>
    public string UsuarioReporteId { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora del hallazgo.
    /// </summary>
    public DateTimeOffset FechaHoraHallazgo { get; set; }

    /// <summary>
    /// Descripción del problema encontrado.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Nivel de criticidad estimado (1-5, donde 5 es más crítico).
    /// </summary>
    public int? NivelCriticidad { get; set; }

    /// <summary>
    /// Si el hallazgo ha sido resuelto.
    /// </summary>
    public bool Resuelto { get; set; }

    /// <summary>
    /// Usuario que resolvió el hallazgo.
    /// </summary>
    public string? UsuarioResolucionId { get; set; }

    /// <summary>
    /// Fecha y hora de resolución.
    /// </summary>
    public DateTimeOffset? FechaHoraResolucion { get; set; }

    /// <summary>
    /// Observación de la resolución.
    /// </summary>
    public string? ObservacionResolucion { get; set; }

    /// <summary>
    /// Indica si se requiere acción correctiva posterior.
    /// </summary>
    public bool RequiereAccionCorrectiva { get; set; }

    /// <summary>
    /// Descripción de la acción correctiva requerida.
    /// </summary>
    public string? AccionCorrectivaDescripcion { get; set; }
}
