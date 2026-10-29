using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Exceptions;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Una detección de calidad (T-611): qué defecto se vio, dónde, cuándo y con qué imagen. Ante un defecto
/// el sistema SOLO registra: no rechaza producto ni dispara alertas.
/// Nada se borra: la revisión humana cambia el estado y deja un registro en el historial (Revisiones).
/// </summary>
public class RegistroCalidad : EntityBase
{
    /// <summary>
    /// Usuario con el que quedan los registros que manda el detector (no hay persona detrás).
    /// </summary>
    public const string UsuarioDetector = "DETECTOR-CALIDAD";

    public long RegistroCalidadId { get; set; }

    /// <summary>
    /// Instante de la detección (lo manda el detector o lo carga la persona).
    /// </summary>
    public DateTimeOffset FechaHoraDeteccion { get; set; }

    /// <summary>
    /// Línea del punto de control, guardada para filtrar sin JOIN y para que no cambie si se reasigna la cámara.
    /// </summary>
    public int LineaId { get; set; }

    public int PuntoControlCalidadId { get; set; }

    /// <summary>
    /// Turno materializado al guardar (no se recalcula al consultar). Null solo si a esa hora no había
    /// turno activo: una detección del detector no se pierde por eso.
    /// </summary>
    public int? TurnoId { get; set; }

    public int TipoDefectoCalidadId { get; set; }

    public OrigenRegistroCalidad Origen { get; set; }

    /// <summary>
    /// Confianza del modelo entre 0 y 1. Solo en los automáticos (en los manuales queda vacía).
    /// </summary>
    public decimal? Confianza { get; set; }

    /// <summary>
    /// Dato medido (ej.: 14 mm de rabo, 3 paquetes). Opcional.
    /// </summary>
    public decimal? ValorMedido { get; set; }

    /// <summary>
    /// Unidad del dato medido al momento del registro (snapshot: si cambia el maestro, el histórico no cambia).
    /// </summary>
    public string? UnidadMedida { get; set; }

    public int? ProductoId { get; set; }

    /// <summary>
    /// Detención (parada) relacionada, para el cruce calidad - paradas. Opcional.
    /// </summary>
    public long? DetencionId { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>
    /// Id del evento en el detector. Si reintenta el envío con el mismo id no se duplica el registro.
    /// </summary>
    public string? IdentificadorExterno { get; set; }

    // Imagen: el archivo vive en disco (Calidad:CarpetaImagenes) con nombre generado; acá solo los datos.
    public string? ImagenNombreArchivo { get; set; }
    public string? ImagenNombreOriginal { get; set; }
    public string? ImagenContentType { get; set; }
    public long? ImagenTamanoBytes { get; set; }

    public EstadoRevisionCalidad EstadoRevision { get; set; } = EstadoRevisionCalidad.PENDIENTE;
    public string? RevisadoPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraRevision { get; set; }

    /// <summary>
    /// Control de concurrencia optimista (dos personas revisando el mismo registro).
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual PuntoControlCalidad PuntoControl { get; set; } = null!;
    public virtual Turno? Turno { get; set; }
    public virtual TipoDefectoCalidad TipoDefecto { get; set; } = null!;
    public virtual Producto? Producto { get; set; }
    public virtual Detencion? Detencion { get; set; }
    public virtual ICollection<RegistroCalidadRevision> Revisiones { get; set; } = new List<RegistroCalidadRevision>();

    public bool TieneImagen => !string.IsNullOrEmpty(ImagenNombreArchivo);

    /// <summary>
    /// Marca el registro como confirmado o falso positivo. Guarda quién y cuándo, y deja el cambio en el historial.
    /// </summary>
    public RegistroCalidadRevision Revisar(EstadoRevisionCalidad nuevoEstado, string usuarioId, DateTimeOffset ahora, string? comentario)
    {
        if (!EstadoRevision.PuedeTransicionarA(nuevoEstado))
            throw new TransicionEstadoInvalidaException(EstadoRevision.ObtenerDescripcion(), nuevoEstado.ObtenerDescripcion());

        var revision = new RegistroCalidadRevision
        {
            RegistroCalidadId = RegistroCalidadId,
            EstadoAnterior = EstadoRevision,
            EstadoNuevo = nuevoEstado,
            Comentario = comentario,
            UsuarioId = usuarioId,
            FechaHora = ahora
        };

        EstadoRevision = nuevoEstado;
        RevisadoPorUsuarioId = usuarioId;
        FechaHoraRevision = ahora;
        ModificadoPorUsuarioId = usuarioId;
        Revisiones.Add(revision);
        return revision;
    }
}

/// <summary>
/// Historial de la revisión humana de un registro de calidad. Solo inserción.
/// </summary>
public class RegistroCalidadRevision
{
    public long RegistroCalidadRevisionId { get; set; }
    public long RegistroCalidadId { get; set; }

    public EstadoRevisionCalidad EstadoAnterior { get; set; }

    public EstadoRevisionCalidad EstadoNuevo { get; set; }
    public string? Comentario { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }

    // Navegación
    public virtual RegistroCalidad RegistroCalidad { get; set; } = null!;
}
