using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Representa una línea de producción en el área de Converting.
/// En planta la llaman "la máquina" (ej: "la Futura 2", "la Tango 1").
/// </summary>
public class Linea : CatalogoBase
{
    public int LineaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    // Navegación
    public virtual ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
    public virtual ICollection<Detencion> Detenciones { get; set; } = new List<Detencion>();
    public virtual ICollection<UsuarioLinea> UsuariosLineas { get; set; } = new List<UsuarioLinea>();

    // Navegación - Eje 2: Variables de proceso
    public virtual ICollection<LineaProducto> LineasProductos { get; set; } = new List<LineaProducto>();
    public virtual ICollection<GrupoParametro> GruposParametros { get; set; } = new List<GrupoParametro>();
    public virtual ICollection<EstandarParametro> Estandares { get; set; } = new List<EstandarParametro>();
    public virtual ICollection<RegistroProceso> RegistrosProceso { get; set; } = new List<RegistroProceso>();
}
