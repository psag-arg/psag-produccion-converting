using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Infrastructure.Data;

/// <summary>
/// Carga del catálogo de opciones y de la matriz migrada (T-802) al arrancar la API: la misma que hace el script
/// <c>39_t802_roles_permisos.sql</c> en SQL Server. Idempotente y sin pisar lo que se editó desde el configurador:
/// <list type="bullet">
/// <item>opciones: inserta las que faltan por código y actualiza nombre, padre, ruta, orden y acciones aplicables;</item>
/// <item>permisos: sólo inserta los pares (rol, opción) que no existen (el configurador guarda todas las filas del rol,
/// también las que quedan en 0, así que una opción quitada no vuelve);</item>
/// <item>configuración de rol: inserta la de los roles que no la tienen (ObligadoATurno = 1 sólo OPERARIO).</item>
/// </list>
/// </summary>
public static class SemillaPermisos
{
    public const string UsuarioSemilla = "SYSTEM-SEED";

    public static async Task SembrarAsync(MantenimientoDbContext context, CancellationToken cancellationToken = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var existentes = await context.OpcionesSistema.ToDictionaryAsync(o => o.Codigo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Primero las secciones (padres), después las opciones.
        foreach (var def in CatalogoPermisos.Todas.OrderBy(d => d.EsSeccion ? 0 : 1))
        {
            if (!existentes.TryGetValue(def.Codigo, out var opcion))
            {
                opcion = new OpcionSistema { Codigo = def.Codigo };
                context.OpcionesSistema.Add(opcion);
                existentes[def.Codigo] = opcion;
            }
            opcion.Nombre = def.Nombre;
            opcion.Ruta = def.Ruta;
            opcion.Orden = def.Orden;
            opcion.AccionesAplicables = AccionPermisoTexto.AColumna(def.Aplicables);
            opcion.Activo = true;
            opcion.Padre = def.PadreCodigo == null ? null : existentes[def.PadreCodigo];
        }
        await context.SaveChangesAsync(cancellationToken);

        var roles = await context.Roles.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(cancellationToken);
        var pares = (await context.PermisosRol.AsNoTracking().Select(p => new { p.RoleId, p.OpcionId }).ToListAsync(cancellationToken))
            .Select(p => (p.RoleId, p.OpcionId)).ToHashSet();
        var configurados = (await context.RolesConfiguracion.AsNoTracking().Select(r => r.RoleId).ToListAsync(cancellationToken)).ToHashSet();

        foreach (var rol in roles)
        {
            var nombre = rol.Name ?? string.Empty;
            foreach (var def in CatalogoPermisos.Todas.Where(d => !d.EsSeccion))
            {
                var acciones = def.De(nombre.ToUpperInvariant());
                var opcionId = existentes[def.Codigo].OpcionId;
                if (acciones == AccionPermiso.Ninguna || pares.Contains((rol.Id, opcionId)))
                    continue;
                var permiso = new PermisoRol { RoleId = rol.Id, OpcionId = opcionId, FechaHoraModificacion = ahora, ModificadoPorUsuarioId = UsuarioSemilla };
                permiso.Asignar(acciones);
                context.PermisosRol.Add(permiso);
            }

            if (!configurados.Contains(rol.Id))
            {
                context.RolesConfiguracion.Add(new RolConfiguracion
                {
                    RoleId = rol.Id,
                    ObligadoATurno = CatalogoPermisos.RolesObligadosATurno.Contains(nombre.ToUpperInvariant()),
                    FechaHoraModificacion = ahora,
                    ModificadoPorUsuarioId = UsuarioSemilla
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
