namespace PSAG.Mantenimiento.Domain.Permisos;

/// <summary>
/// Opción del sistema (<c>mnt.CFG_Opciones</c>, T-802): una entrada del menú o una función sin menú. El árbol (PadreId) son
/// las secciones del menú. Catálogo fijo: lo cargan el script 39 y la semilla de la API desde <see cref="CatalogoPermisos"/>;
/// no se edita desde la pantalla.
/// </summary>
public class OpcionSistema
{
    public int OpcionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int? PadreId { get; set; }
    /// <summary>Ruta de la Web (sin barra inicial; "" = Inicio). Null en secciones y funciones sin pantalla propia.</summary>
    public string? Ruta { get; set; }
    public int Orden { get; set; }
    /// <summary>"VER,ALTA,MODIFICACION,BAJA": qué acciones tienen sentido (ver <see cref="AccionPermisoTexto"/>). Vacío en las secciones.</summary>
    public string AccionesAplicables { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public OpcionSistema? Padre { get; set; }
}

/// <summary>Permisos de un rol de Identity sobre una opción (<c>mnt.CFG_PermisosRol</c>). Único (RoleId, OpcionId).</summary>
public class PermisoRol
{
    public int PermisoRolId { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public int OpcionId { get; set; }
    public bool PuedeVer { get; set; }
    public bool PuedeAlta { get; set; }
    public bool PuedeModificar { get; set; }
    public bool PuedeBaja { get; set; }
    public DateTimeOffset FechaHoraModificacion { get; set; }
    public string ModificadoPorUsuarioId { get; set; } = string.Empty;

    public OpcionSistema? Opcion { get; set; }

    public AccionPermiso Acciones =>
        (PuedeVer ? AccionPermiso.Ver : 0) | (PuedeAlta ? AccionPermiso.Alta : 0)
        | (PuedeModificar ? AccionPermiso.Modificar : 0) | (PuedeBaja ? AccionPermiso.Baja : 0);

    public void Asignar(AccionPermiso acciones)
    {
        PuedeVer = acciones.HasFlag(AccionPermiso.Ver);
        PuedeAlta = acciones.HasFlag(AccionPermiso.Alta);
        PuedeModificar = acciones.HasFlag(AccionPermiso.Modificar);
        PuedeBaja = acciones.HasFlag(AccionPermiso.Baja);
    }
}

/// <summary>Configuración propia de cada rol (<c>mnt.CFG_RolesConfiguracion</c>, PK = FK a AspNetRoles).</summary>
public class RolConfiguracion
{
    public string RoleId { get; set; } = string.Empty;
    /// <summary>Operador de máquina: tiene que declarar el turno al entrar (lo aplica la parte 2 de T-802).</summary>
    public bool ObligadoATurno { get; set; }
    public string? Descripcion { get; set; }
    public DateTimeOffset FechaHoraModificacion { get; set; }
    public string ModificadoPorUsuarioId { get; set; } = string.Empty;
}
