using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Permisos;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Services;

/// <summary>
/// Matriz de permisos (T-802) con caché corta: la matriz completa (pocas filas) se lee una vez cada
/// <see cref="DuracionCache"/> y se invalida al guardar desde el configurador, así el cambio vale en la siguiente request.
/// </summary>
public class PermisosService : IPermisosService
{
    public static readonly TimeSpan DuracionCache = TimeSpan.FromSeconds(60);
    private const string ClaveCache = "T802.MatrizPermisos";

    private readonly MantenimientoDbContext _context;
    private readonly IMemoryCache _cache;

    public PermisosService(MantenimientoDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private sealed record Matriz(
        IReadOnlyList<OpcionSistema> Opciones,
        IReadOnlyDictionary<string, Dictionary<string, AccionPermiso>> PorRol,   // nombre de rol (mayúsculas) -> código -> acciones
        IReadOnlySet<string> RolesObligadosATurno);

    public void InvalidarCache() => _cache.Remove(ClaveCache);

    private async Task<Matriz> MatrizAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(ClaveCache, out Matriz? enCache) && enCache != null)
            return enCache;

        var opciones = await _context.OpcionesSistema.AsNoTracking().Where(o => o.Activo).ToListAsync(ct);
        var porId = opciones.ToDictionary(o => o.OpcionId);
        var roles = await _context.Roles.AsNoTracking().Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => (r.Name ?? string.Empty).ToUpperInvariant(), ct);
        var porRol = new Dictionary<string, Dictionary<string, AccionPermiso>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in await _context.PermisosRol.AsNoTracking().ToListAsync(ct))
        {
            if (!roles.TryGetValue(p.RoleId, out var rol) || !porId.TryGetValue(p.OpcionId, out var opcion))
                continue;
            if (!porRol.TryGetValue(rol, out var mapa))
                porRol[rol] = mapa = new Dictionary<string, AccionPermiso>(StringComparer.OrdinalIgnoreCase);
            mapa[opcion.Codigo] = p.Acciones & AccionPermisoTexto.DeColumna(opcion.AccionesAplicables);
        }
        var obligados = (await _context.RolesConfiguracion.AsNoTracking().Where(r => r.ObligadoATurno).Select(r => r.RoleId).ToListAsync(ct))
            .Where(roles.ContainsKey).Select(id => roles[id]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matriz = new Matriz(opciones, porRol, obligados);
        _cache.Set(ClaveCache, matriz, DuracionCache);
        return matriz;
    }

    public async Task<PermisosUsuario> ObtenerDeRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var matriz = await MatrizAsync(cancellationToken);
        var union = new Dictionary<string, AccionPermiso>(StringComparer.OrdinalIgnoreCase);
        var obligado = false;
        foreach (var rol in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            obligado |= matriz.RolesObligadosATurno.Contains(rol);
            if (!matriz.PorRol.TryGetValue(rol, out var mapa))
                continue;
            foreach (var (codigo, acciones) in mapa)
                union[codigo] = (union.TryGetValue(codigo, out var previo) ? previo : AccionPermiso.Ninguna) | acciones;
        }
        return new PermisosUsuario(union, obligado);
    }

    public async Task<PermisosMiosDto> ObtenerMiosAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var lista = roles.ToList();
        var matriz = await MatrizAsync(cancellationToken);
        var permisos = await ObtenerDeRolesAsync(lista, cancellationToken);
        var porId = matriz.Opciones.ToDictionary(o => o.OpcionId);

        var visibles = matriz.Opciones
            .Where(o => o.PadreId != null && permisos.De(o.Codigo) != AccionPermiso.Ninguna)
            .ToList();
        var secciones = visibles.Select(o => o.PadreId!.Value).Distinct().Where(porId.ContainsKey).Select(id => porId[id]);

        var dto = new PermisosMiosDto { ObligadoATurno = permisos.ObligadoATurno, Roles = lista };
        foreach (var o in secciones.Concat(visibles).OrderBy(o => o.PadreId == null ? o.Orden * 1000 : porId[o.PadreId.Value].Orden * 1000 + o.Orden))
        {
            var a = permisos.De(o.Codigo);
            dto.Opciones.Add(new OpcionPermitidaDto
            {
                Codigo = o.Codigo,
                Nombre = o.Nombre,
                PadreCodigo = o.PadreId is { } p && porId.TryGetValue(p, out var padre) ? padre.Codigo : null,
                Ruta = o.Ruta,
                Orden = o.Orden,
                Ver = a.HasFlag(AccionPermiso.Ver) || o.PadreId == null,
                Alta = a.HasFlag(AccionPermiso.Alta),
                Modificar = a.HasFlag(AccionPermiso.Modificar),
                Baja = a.HasFlag(AccionPermiso.Baja)
            });
        }
        return dto;
    }

    public async Task<ConfiguracionPermisosDto> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default)
    {
        var opciones = await _context.OpcionesSistema.AsNoTracking().Where(o => o.Activo).ToListAsync(cancellationToken);
        var porId = opciones.ToDictionary(o => o.OpcionId);
        var roles = await _context.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => new { r.Id, r.Name }).ToListAsync(cancellationToken);
        var permisos = await _context.PermisosRol.AsNoTracking().ToListAsync(cancellationToken);
        var config = await _context.RolesConfiguracion.AsNoTracking().ToDictionaryAsync(r => r.RoleId, cancellationToken);

        var dto = new ConfiguracionPermisosDto();
        foreach (var o in opciones.OrderBy(o => (o.PadreId is { } p && porId.TryGetValue(p, out var padre) ? padre.Orden : o.Orden) * 1000 + (o.PadreId == null ? 0 : o.Orden)))
        {
            var aplicables = AccionPermisoTexto.DeColumna(o.AccionesAplicables);
            dto.Opciones.Add(new OpcionCatalogoDto
            {
                OpcionId = o.OpcionId,
                Codigo = o.Codigo,
                Nombre = o.Nombre,
                PadreCodigo = o.PadreId is { } p && porId.TryGetValue(p, out var padre) ? padre.Codigo : null,
                Ruta = o.Ruta,
                Orden = o.Orden,
                AplicaVer = aplicables.HasFlag(AccionPermiso.Ver),
                AplicaAlta = aplicables.HasFlag(AccionPermiso.Alta),
                AplicaModificar = aplicables.HasFlag(AccionPermiso.Modificar),
                AplicaBaja = aplicables.HasFlag(AccionPermiso.Baja)
            });
        }

        foreach (var r in roles)
        {
            config.TryGetValue(r.Id, out var c);
            dto.Roles.Add(new RolPermisosDto
            {
                RoleId = r.Id,
                Nombre = r.Name ?? r.Id,
                ObligadoATurno = c?.ObligadoATurno ?? false,
                Descripcion = c?.Descripcion,
                Permisos = permisos.Where(p => p.RoleId == r.Id && porId.ContainsKey(p.OpcionId))
                    .Select(p => PermisoOpcionDto.De(porId[p.OpcionId].Codigo, p.Acciones & AccionPermisoTexto.DeColumna(porId[p.OpcionId].AccionesAplicables)))
                    .OrderBy(p => p.Codigo)
                    .ToList()
            });
        }
        return dto;
    }

    public async Task<Result<RolPermisosDto>> GuardarRolAsync(
        string roleId, GuardarPermisosRolRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var rol = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        if (rol == null)
            return Result<RolPermisosDto>.Failure("NOT_FOUND", "El rol no existe.");
        var nombreRol = rol.Name ?? roleId;

        var desconocidas = request.Permisos.Select(p => p.Codigo)
            .Where(c => CatalogoPermisos.Buscar(c) == null).ToList();
        if (desconocidas.Count > 0)
            return Result<RolPermisosDto>.Failure("VALIDATION", "Opciones desconocidas: " + string.Join(", ", desconocidas) + ".");

        var opciones = await _context.OpcionesSistema.Where(o => o.Activo && o.PadreId != null).ToListAsync(cancellationToken);
        var pedidas = request.Permisos
            .GroupBy(p => p.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Aggregate(AccionPermiso.Ninguna, (t, p) => t | p.Acciones()), StringComparer.OrdinalIgnoreCase);

        var nuevas = opciones.ToDictionary(o => o.OpcionId, o => ReglasPermisos.Normalizar(
            pedidas.TryGetValue(o.Codigo, out var a) ? a : AccionPermiso.Ninguna, AccionPermisoTexto.DeColumna(o.AccionesAplicables)));

        var configRoles = opciones.FirstOrDefault(o => o.Codigo == Opciones.ConfigRoles);
        if (configRoles != null && !ReglasPermisos.AdminConservaRoles(nombreRol, nuevas[configRoles.OpcionId]))
            return Result<RolPermisosDto>.Failure("VALIDATION", ReglasPermisos.MensajeAdminSinRoles);

        // T-802: el obligado a turno no puede hacer nada sin abrir o tomar un turno: tiene que poder operarlo.
        var operarTurno = opciones.FirstOrDefault(o => o.Codigo == Opciones.ProduccionOperacionTurno);
        const AccionPermiso necesarias = AccionPermiso.Ver | AccionPermiso.Alta | AccionPermiso.Modificar;
        if (request.ObligadoATurno && operarTurno != null && (nuevas[operarTurno.OpcionId] & necesarias) != necesarias)
            return Result<RolPermisosDto>.Failure("VALIDATION",
                "Un rol obligado a turno necesita \"Operar el turno\" con Ver, Alta y Modificación (si no, no podría abrir su turno ni trabajar).");

        var ahora = DateTimeOffset.UtcNow;
        var actuales = await _context.PermisosRol.Where(p => p.RoleId == roleId).ToDictionaryAsync(p => p.OpcionId, cancellationToken);
        var antes = new Dictionary<string, string>();
        var despues = new Dictionary<string, string>();
        foreach (var o in opciones)
        {
            var nueva = nuevas[o.OpcionId];
            actuales.TryGetValue(o.OpcionId, out var fila);
            var anterior = fila?.Acciones ?? AccionPermiso.Ninguna;
            if (fila == null)
            {
                fila = new PermisoRol { RoleId = roleId, OpcionId = o.OpcionId };
                _context.PermisosRol.Add(fila);
            }
            else if (anterior == nueva)
            {
                continue;
            }
            fila.Asignar(nueva);
            fila.FechaHoraModificacion = ahora;
            fila.ModificadoPorUsuarioId = usuarioId;
            if (anterior != nueva)
            {
                antes[o.Codigo] = AccionPermisoTexto.AColumna(anterior);
                despues[o.Codigo] = AccionPermisoTexto.AColumna(nueva);
            }
        }

        var config = await _context.RolesConfiguracion.FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (config == null)
        {
            config = new RolConfiguracion { RoleId = roleId };
            _context.RolesConfiguracion.Add(config);
        }
        var descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        if (descripcion is { Length: > 300 })
            return Result<RolPermisosDto>.Failure("VALIDATION", "La descripción admite hasta 300 caracteres.");
        if (config.ObligadoATurno != request.ObligadoATurno)
        {
            antes["ObligadoATurno"] = config.ObligadoATurno ? "SI" : "NO";
            despues["ObligadoATurno"] = request.ObligadoATurno ? "SI" : "NO";
        }
        config.ObligadoATurno = request.ObligadoATurno;
        config.Descripcion = descripcion;
        config.FechaHoraModificacion = ahora;
        config.ModificadoPorUsuarioId = usuarioId;

        if (despues.Count > 0)
        {
            _context.Auditoria.Add(new Auditoria
            {
                UsuarioId = usuarioId,
                FechaHora = ahora,
                Accion = TipoAccionAuditoria.CambioRol,
                Entidad = "CFG_PermisosRol",
                EntidadId = nombreRol,
                ValoresAnteriores = JsonSerializer.Serialize(antes),
                ValoresNuevos = JsonSerializer.Serialize(despues),
                Descripcion = $"Permisos del rol {nombreRol} modificados desde el configurador ({despues.Count} cambio(s))."
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        InvalidarCache();

        var dto = (await ObtenerConfiguracionAsync(cancellationToken)).Roles.First(r => r.RoleId == roleId);
        return Result<RolPermisosDto>.Success(dto);
    }
}
