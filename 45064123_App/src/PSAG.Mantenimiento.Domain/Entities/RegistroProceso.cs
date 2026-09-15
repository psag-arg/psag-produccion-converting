using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Registro de variables de proceso para una línea, producto, turno y fecha.
/// El operador recorre la máquina y verifica que los parámetros coincidan
/// con el estándar del producto que está corriendo.
///
/// Un registro por línea + producto + turno + fecha. Si ya existe, se retoma.
/// </summary>
public class RegistroProceso : EntityBase
{
    public long RegistroProcesoId { get; set; }
    public int LineaId { get; set; }
    public int ProductoId { get; set; }
    public int TurnoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string UsuarioId { get; set; } = string.Empty;

    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }

    public EstadoRegistroProceso Estado { get; set; } = EstadoRegistroProceso.PARCIAL;

    /// <summary>
    /// Control de concurrencia optimista.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Producto Producto { get; set; } = null!;
    public virtual Turno Turno { get; set; } = null!;
    public virtual ICollection<RegistroProcesoDetalle> Detalles { get; set; } = new List<RegistroProcesoDetalle>();

    /// <summary>
    /// Verifica si el registro puede ser editado.
    /// </summary>
    public bool PuedeSerEditado => Estado.PuedeSerEditado();

    /// <summary>
    /// Cantidad de desvíos (valores fuera de estándar con observación).
    /// </summary>
    public int CantidadDesvios => Detalles.Count(d => d.FueraDeEstandar);

    /// <summary>
    /// Cantidad de parámetros completados.
    /// </summary>
    public int CantidadCompletados => Detalles.Count(d => d.ValorReal != null || !string.IsNullOrEmpty(d.ValorRealTexto));
}
