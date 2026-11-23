namespace PSAG.Mantenimiento.Web.Models;

/// <summary>GET api/permisos/mios (T-802).</summary>
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

public enum AccionPermiso { Ver, Alta, Modificar, Baja }

/// <summary>Permisos del usuario logueado, como los devuelve la API (unión de sus roles).</summary>
public sealed class PermisosUsuario
{
    public static readonly PermisosUsuario Ninguno = new(new PermisosMiosDto());

    private readonly Dictionary<string, OpcionPermitidaDto> _porCodigo;

    public PermisosUsuario(PermisosMiosDto dto)
    {
        Dto = dto;
        _porCodigo = dto.Opciones.GroupBy(o => o.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public PermisosMiosDto Dto { get; }

    public bool ObligadoATurno => Dto.ObligadoATurno;

    public bool Tiene(string codigo, AccionPermiso accion = AccionPermiso.Ver) =>
        _porCodigo.TryGetValue(codigo, out var o) && accion switch
        {
            AccionPermiso.Ver => o.Ver,
            AccionPermiso.Alta => o.Alta,
            AccionPermiso.Modificar => o.Modificar,
            AccionPermiso.Baja => o.Baja,
            _ => false
        };

    public bool Ve(string codigo) => Tiene(codigo, AccionPermiso.Ver);

    /// <summary>Opciones con menú (con ruta) de una sección, en orden.</summary>
    public IEnumerable<OpcionPermitidaDto> MenuDe(string seccion) =>
        Dto.Opciones.Where(o => o.PadreCodigo == seccion && o.Ruta != null && o.Ver).OrderBy(o => o.Orden);

    public IEnumerable<OpcionPermitidaDto> Secciones =>
        Dto.Opciones.Where(o => o.PadreCodigo == null && MenuDe(o.Codigo).Any()).OrderBy(o => o.Orden);
}

/// <summary>Configurador (GET api/permisos/roles).</summary>
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
}

public class GuardarPermisosRolRequest
{
    public bool ObligadoATurno { get; set; }
    public string? Descripcion { get; set; }
    public List<PermisoOpcionDto> Permisos { get; set; } = [];
}
