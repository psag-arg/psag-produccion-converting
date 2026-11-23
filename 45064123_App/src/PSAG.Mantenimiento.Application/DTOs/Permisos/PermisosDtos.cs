using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Application.DTOs.Permisos;

/// <summary>Permisos efectivos de un usuario: la unión de los de sus roles (T-802).</summary>
public sealed class PermisosUsuario
{
    public static readonly PermisosUsuario Ninguno = new(new Dictionary<string, AccionPermiso>(), false);

    private readonly IReadOnlyDictionary<string, AccionPermiso> _porOpcion;

    public PermisosUsuario(IReadOnlyDictionary<string, AccionPermiso> porOpcion, bool obligadoATurno)
    {
        _porOpcion = porOpcion;
        ObligadoATurno = obligadoATurno;
    }

    public bool ObligadoATurno { get; }

    public IReadOnlyDictionary<string, AccionPermiso> PorOpcion => _porOpcion;

    public AccionPermiso De(string codigo) => _porOpcion.TryGetValue(codigo, out var a) ? a : AccionPermiso.Ninguna;

    public bool Tiene(string codigo, AccionPermiso accion) => accion != AccionPermiso.Ninguna && De(codigo).HasFlag(accion);

    /// <summary>Tiene la acción en alguna de las opciones.</summary>
    public bool TieneAlguna(IEnumerable<string> codigos, AccionPermiso accion) => codigos.Any(c => Tiene(c, accion));
}

/// <summary>GET api/permisos/mios: lo que arma el menú de la Web.</summary>
public class PermisosMiosDto
{
    public bool ObligadoATurno { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<OpcionPermitidaDto> Opciones { get; set; } = [];
}

public class OpcionPermitidaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? PadreCodigo { get; set; }
    public string? Ruta { get; set; }
    public int Orden { get; set; }
    public bool Ver { get; set; }
    public bool Alta { get; set; }
    public bool Modificar { get; set; }
    public bool Baja { get; set; }
}

/// <summary>GET api/permisos/roles: todo lo que necesita el configurador.</summary>
public class ConfiguracionPermisosDto
{
    public List<OpcionCatalogoDto> Opciones { get; set; } = [];
    public List<RolPermisosDto> Roles { get; set; } = [];
}

public class OpcionCatalogoDto
{
    public int OpcionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? PadreCodigo { get; set; }
    public string? Ruta { get; set; }
    public int Orden { get; set; }
    public bool AplicaVer { get; set; }
    public bool AplicaAlta { get; set; }
    public bool AplicaModificar { get; set; }
    public bool AplicaBaja { get; set; }
}

public class RolPermisosDto
{
    public string RoleId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool ObligadoATurno { get; set; }
    public string? Descripcion { get; set; }
    public List<PermisoOpcionDto> Permisos { get; set; } = [];
}

public class PermisoOpcionDto
{
    public string Codigo { get; set; } = string.Empty;
    public bool Ver { get; set; }
    public bool Alta { get; set; }
    public bool Modificar { get; set; }
    public bool Baja { get; set; }

    public AccionPermiso Acciones() =>
        (Ver ? AccionPermiso.Ver : 0) | (Alta ? AccionPermiso.Alta : 0) | (Modificar ? AccionPermiso.Modificar : 0) | (Baja ? AccionPermiso.Baja : 0);

    public static PermisoOpcionDto De(string codigo, AccionPermiso a) => new()
    {
        Codigo = codigo,
        Ver = a.HasFlag(AccionPermiso.Ver),
        Alta = a.HasFlag(AccionPermiso.Alta),
        Modificar = a.HasFlag(AccionPermiso.Modificar),
        Baja = a.HasFlag(AccionPermiso.Baja)
    };
}

/// <summary>PUT api/permisos/roles/{roleId}: la matriz completa del rol (las opciones que no vienen quedan sin permisos).</summary>
public class GuardarPermisosRolRequest
{
    public bool ObligadoATurno { get; set; }
    public string? Descripcion { get; set; }
    public List<PermisoOpcionDto> Permisos { get; set; } = [];
}
