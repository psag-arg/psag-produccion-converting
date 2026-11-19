using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Agrupador de detenciones (reunión 17/11/2026): un microcorte de cámara (parada que no llegó al umbral) que el
/// operario declaró con el mismo motivo que la detención. Tabla anexa: NO modifica la detención ni la parada
/// (ni MinutosEfectivos de la detención, que sigue siendo la única verdad del tiempo). El tiempo agrupado sale de
/// la vista mnt.vw_DetencionesAgrupadas, sólo para reportes.
/// Desagrupar = Activo en 0 con quién y cuándo; nunca se borra. Una parada sólo puede estar activa en un grupo
/// (índice único filtrado UX_DetencionAgrupacion_ParadaActiva).
/// </summary>
public class DetencionAgrupacion
{
    public long DetencionAgrupacionId { get; set; }

    /// <summary>Detención padre.</summary>
    public long DetencionId { get; set; }

    /// <summary>Microcorte (parada de cámara) hijo.</summary>
    public long ParadaCamaraId { get; set; }

    public OrigenAgrupacion Origen { get; set; } = OrigenAgrupacion.OPERADOR;

    public string CreadoPorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraCreacion { get; set; }

    public bool Activo { get; set; } = true;
    public string? DesactivadoPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraDesactivacion { get; set; }

    // Navegación
    public virtual Detencion Detencion { get; set; } = null!;
    public virtual ParadaCamara ParadaCamara { get; set; } = null!;

    /// <summary>Desagrupa sin borrar: queda quién y cuándo.</summary>
    public void Desactivar(string usuarioId, DateTimeOffset ahora)
    {
        Activo = false;
        DesactivadoPorUsuarioId = usuarioId;
        FechaHoraDesactivacion = ahora;
    }
}
