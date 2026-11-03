using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Punto de control de calidad de una línea: en general una cámara (ej.: cámara 1 a la salida de la
/// cortadora de la Tango 2). Lo administra el administrador desde Maestros.
/// El detector se identifica con el Codigo, que es único en todo el sistema.
/// </summary>
public class PuntoControlCalidad : CatalogoBase
{
    public int PuntoControlCalidadId { get; set; }
    public int LineaId { get; set; }

    /// <summary>
    /// Código que manda el detector (ej.: T2-CAM1). Único; se guarda en mayúsculas.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // ===== Cámara asociada (T-610) =====

    /// <summary>
    /// Cuerpo de la línea que mira la cámara (ej.: Rebobinadora para la cámara 10). Es de referencia: la
    /// detención automática NO lo copia al campo Equipo, porque la causa puede estar en otro equipo y el equipo
    /// lo declara el operador.
    /// </summary>
    public int? EquipoId { get; set; }

    /// <summary>Número de cámara (canal) en el grabador, ej.: 10 para /Streaming/Channels/1001.</summary>
    public int? NumeroCamara { get; set; }

    /// <summary>
    /// Si la cámara da de alta detenciones automáticas (pendientes de declaración) cuando una parada supera el
    /// umbral. Apagado (por defecto), la cámara sólo informa paradas, como la 09.
    /// </summary>
    public bool CreaDetenciones { get; set; }

    /// <summary>
    /// Minutos de parada a partir de los cuales se crea la detención automática (por defecto 5). Las paradas más
    /// cortas quedan sólo en el log de paradas (microcortes). Lo cambia el administrador en Maestros, sin redeploy.
    /// </summary>
    public int MinutosUmbralDetencion { get; set; } = 5;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Equipo? Equipo { get; set; }
}
