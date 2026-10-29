using PSAG.Mantenimiento.Application.Common;

namespace PSAG.Mantenimiento.Application.DTOs.Calidad;

// ===================== Maestros =====================

public class PuntoControlCalidadDto
{
    public int PuntoControlCalidadId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

/// <summary>
/// Alta y edición de un punto de control (cámara). El código es el que manda el detector.
/// </summary>
public class GuardarPuntoControlCalidadRequest
{
    public int LineaId { get; set; }
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
    public bool Activo { get; set; }
}

public class GuardarTipoDefectoCalidadRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? UnidadMedida { get; set; }
    public bool Activo { get; set; } = true;
}

// ===================== Registros =====================

/// <summary>
/// Registro de calidad para la grilla y el detalle. Los nombres de usuario los completa el controller.
/// La imagen se baja por GET api/calidad/registros/{id}/imagen.
/// </summary>
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

    /// <summary>
    /// Historial de revisión, del más reciente al más viejo. Solo viene en el detalle.
    /// </summary>
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

/// <summary>
/// Filtros de la grilla. Las fechas son días de planta (de la detección), ambos inclusive.
/// Estado y origen llegan como texto del enum (PENDIENTE, AUTOMATICO...).
/// </summary>
public class RegistrosCalidadFiltroRequest : PaginationParams
{
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
/// Marcar un registro como CONFIRMADO o FALSO_POSITIVO.
/// </summary>
public class RevisarRegistroCalidadRequest
{
    public string Estado { get; set; } = string.Empty;
    public string? Comentario { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Relacionar el registro con una detención por su código (DET-...). Vacío = quitar la relación.
/// </summary>
public class VincularDetencionCalidadRequest
{
    public string? CodigoDetencion { get; set; }
}

/// <summary>
/// Carga manual (multipart, con la imagen opcional en el campo "archivo").
/// Los números van como texto con punto decimal (0.87) para no depender de la cultura del servidor.
/// FechaHora: ISO 8601; sin offset se toma como hora de la planta. Vacía = ahora.
/// </summary>
public class CrearRegistroCalidadManualRequest
{
    public int PuntoControlCalidadId { get; set; }
    public int TipoDefectoCalidadId { get; set; }
    public string? FechaHora { get; set; }
    public int? TurnoId { get; set; }
    public string? ValorMedido { get; set; }
    public string? UnidadMedida { get; set; }
    public int? ProductoId { get; set; }
    public string? CodigoDetencion { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Lo que manda el detector (multipart, imagen obligatoria en el campo "imagen"). Ver docs/CALIDAD.md.
/// </summary>
public class IngestaCalidadRequest
{
    /// <summary>Código del punto de control (cámara), ej.: T2-CAM1.</summary>
    public string? PuntoControl { get; set; }

    /// <summary>Código del tipo de defecto.</summary>
    public string? TipoDefecto { get; set; }

    public string? FechaHora { get; set; }
    public string? Confianza { get; set; }
    public string? ValorMedido { get; set; }
    public string? UnidadMedida { get; set; }

    /// <summary>Código del producto/SKU (opcional; tiene que existir en Productos).</summary>
    public string? Producto { get; set; }

    public int? TurnoId { get; set; }
    public string? CodigoDetencion { get; set; }
    public string? IdentificadorExterno { get; set; }
    public string? Observaciones { get; set; }
}

public class IngestaCalidadRespuesta
{
    public long RegistroCalidadId { get; set; }

    /// <summary>
    /// true si ya existía un registro con ese IdentificadorExterno (reintento del detector): no se creó otro.
    /// </summary>
    public bool Duplicado { get; set; }

    public string EstadoRevision { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string? TurnoNombre { get; set; }
    public DateTimeOffset FechaHoraDeteccion { get; set; }
}

/// <summary>
/// Imagen recibida para guardar. El stream tiene que poder rebobinarse (el de un IFormFile puede).
/// </summary>
public sealed record ImagenEntrante(string? NombreOriginal, long TamanoBytes, Stream Contenido);
