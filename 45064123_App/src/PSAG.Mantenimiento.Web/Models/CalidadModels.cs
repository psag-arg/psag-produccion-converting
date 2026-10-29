namespace PSAG.Mantenimiento.Web.Models;

public class PuntoControlCalidadDto
{
    public int PuntoControlCalidadId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

public class TipoDefectoCalidadDto
{
    public int TipoDefectoCalidadId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? UnidadMedida { get; set; }
    public bool Activo { get; set; } = true;
}

public class RegistroCalidadDto
{
    public long RegistroCalidadId { get; set; }
    public DateTimeOffset FechaHoraDeteccion { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int PuntoControlCalidadId { get; set; }
    public string PuntoControlCodigo { get; set; } = string.Empty;
    public string PuntoControlNombre { get; set; } = string.Empty;
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
    public int TipoDefectoCalidadId { get; set; }
    public string TipoDefectoCodigo { get; set; } = string.Empty;
    public string TipoDefectoNombre { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string OrigenDescripcion { get; set; } = string.Empty;
    public decimal? Confianza { get; set; }
    public decimal? ValorMedido { get; set; }
    public string? UnidadMedida { get; set; }
    public int? ProductoId { get; set; }
    public string? ProductoCodigo { get; set; }
    public string? ProductoNombre { get; set; }
    public long? DetencionId { get; set; }
    public string? DetencionCodigo { get; set; }
    public string? Observaciones { get; set; }
    public string? IdentificadorExterno { get; set; }
    public bool TieneImagen { get; set; }
    public string EstadoRevision { get; set; } = string.Empty;
    public string EstadoRevisionDescripcion { get; set; } = string.Empty;
    public string? RevisadoPorUsuarioId { get; set; }
    public string? RevisadoPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraRevision { get; set; }
    public string CreadoPorUsuarioId { get; set; } = string.Empty;
    public string? CreadoPorUsuarioNombre { get; set; }
    public DateTimeOffset FechaHoraCreacion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<RegistroCalidadRevisionDto> Revisiones { get; set; } = new();
}

public class RegistroCalidadRevisionDto
{
    public string EstadoAnterior { get; set; } = string.Empty;
    public string EstadoAnteriorDescripcion { get; set; } = string.Empty;
    public string EstadoNuevo { get; set; } = string.Empty;
    public string EstadoNuevoDescripcion { get; set; } = string.Empty;
    public string? Comentario { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }
    public DateTimeOffset FechaHora { get; set; }
}

public class RegistrosCalidadFiltro
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int? TurnoId { get; set; }
    public int? LineaId { get; set; }
    public int? PuntoControlCalidadId { get; set; }
    public int? TipoDefectoCalidadId { get; set; }
    public string? EstadoRevision { get; set; }
    public string? Origen { get; set; }
}

/// <summary>
/// Datos de la carga manual. La fecha y hora se manda como hora de pared de la planta (sin offset).
/// </summary>
public class NuevoRegistroCalidadManual
{
    public int PuntoControlCalidadId { get; set; }
    public int TipoDefectoCalidadId { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.Now;
    public int? TurnoId { get; set; }
    public decimal? ValorMedido { get; set; }
    public string? UnidadMedida { get; set; }
    public int? ProductoId { get; set; }
    public string? CodigoDetencion { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Estados, badges y permisos de Calidad. Los roles son los mismos que CalidadReglas en la API, que vuelve a validar.
/// </summary>
public static class CalidadOpciones
{
    public static readonly (string Valor, string Texto)[] Estados =
    [
        ("PENDIENTE", "Pendiente"),
        ("CONFIRMADO", "Confirmado"),
        ("FALSO_POSITIVO", "Falso positivo")
    ];

    public static readonly (string Valor, string Texto)[] Origenes =
    [
        ("AUTOMATICO", "Automático"),
        ("MANUAL", "Manual")
    ];

    public static string BadgeEstado(string estado) => estado switch
    {
        "PENDIENTE" => "badge bg-warning text-dark",
        "CONFIRMADO" => "badge bg-danger",
        "FALSO_POSITIVO" => "badge bg-secondary",
        _ => "badge bg-light text-dark"
    };

    /// <summary>Confirmado / falso positivo: supervisión, mantenimiento y administrador. El operario sólo ve.</summary>
    public static bool PuedeRevisar(Func<string, bool> tieneRol) =>
        new[] { "SUPERVISOR", "MANTENIMIENTO", "ADMINISTRADOR" }.Any(tieneRol);

    /// <summary>Carga manual: supervisión y administrador.</summary>
    public static bool PuedeCargarManual(Func<string, bool> tieneRol) =>
        new[] { "SUPERVISOR", "ADMINISTRADOR" }.Any(tieneRol);
}
