using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-802: el script 39 (más el 41 y el 42 de T-770, que suman las opciones del tablero, y el 43 de T-803, que ordena el menú) carga exactamente el catálogo y la matriz de <see cref="CatalogoPermisos"/> (la misma que la semilla
/// de la API), es idempotente y no pisa lo editado. Sólo en SQL Server (en SQLite no hay scripts).
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script39PermisosTests
{
    private static void CrearRoles(BaseDePrueba b)
    {
        foreach (var rol in MatrizMigrada.Roles)
            b.Context.Database.ExecuteSqlRaw(
                "INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (NEWID(), {0}, {0}, NEWID())", rol);
    }

    private static List<string> Matriz(BaseDePrueba b) =>
        b.Context.PermisosRol.AsNoTracking().Include(p => p.Opcion).ToList()
            .Join(b.Context.Roles.AsNoTracking().ToList(), p => p.RoleId, r => r.Id, (p, r) => $"{r.Name}|{p.Opcion!.Codigo}|{(int)p.Acciones}")
            .OrderBy(x => x).ToList();

    private static List<string> MatrizEsperada() =>
        CatalogoPermisos.Todas.Where(d => !d.EsSeccion)
            .SelectMany(d => MatrizMigrada.Roles.Select(r => (d.Codigo, Rol: r, Acciones: d.De(r))))
            .Where(x => x.Acciones != AccionPermiso.Ninguna)
            .Select(x => $"{x.Rol}|{x.Codigo}|{(int)x.Acciones}").OrderBy(x => x).ToList();

    [Fact]
    public void Script39_CargaElCatalogoYLaMatrizMigrada_EsIdempotente_YNoPisaLoEditado()
    {
        if (!BaseDePrueba.EsSqlServer)
            return;

        using var b = BaseDePrueba.Crear();
        CrearRoles(b);
        EsquemaSqlServer.CorrerScript("39_t802_roles_permisos.sql");
        EsquemaSqlServer.CorrerScript("41_t770_tablero_operador.sql");   // T-770: la opción del tablero (39 + 41 + 42 = catálogo)
        EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial.sql");
        EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");   // T-803: menú de dashboards (39 + 41 + 42 + 43 = catálogo)

        var opciones = b.Context.OpcionesSistema.AsNoTracking().Include(o => o.Padre).ToList();
        Assert.Equal(
            CatalogoPermisos.Todas.Select(d => $"{d.Codigo}|{d.Nombre}|{d.PadreCodigo}|{d.Ruta}|{d.Orden}|{AccionPermisoTexto.AColumna(d.Aplicables)}").OrderBy(x => x),
            opciones.Select(o => $"{o.Codigo}|{o.Nombre}|{o.Padre?.Codigo}|{o.Ruta}|{o.Orden}|{o.AccionesAplicables}").OrderBy(x => x));
        // El 43 deja en 0 (no borra) las filas que T-803 le sacó a la matriz (CatalogoPermisos.Quitados)
        Assert.Equal(MatrizEsperada(), Matriz(b).Where(x => !x.EndsWith("|0")).ToList());
        Assert.Equal(CatalogoPermisos.Quitados.Select(q => $"{q.Rol}|{q.Codigo}|0").OrderBy(x => x), Matriz(b).Where(x => x.EndsWith("|0")));
        var obligados = b.Context.RolesConfiguracion.AsNoTracking().Where(r => r.ObligadoATurno).Select(r => r.RoleId).ToList();
        Assert.Equal(["OPERARIO"], b.Context.Roles.AsNoTracking().Where(r => obligados.Contains(r.Id)).Select(r => r.Name!).ToList());

        // Se edita un permiso (como el configurador: la fila queda en 0) y se vuelve a correr el script.
        b.Context.Database.ExecuteSqlRaw("""
            UPDATE p SET PuedeVer = 0 FROM mnt.CFG_PermisosRol p
            JOIN dbo.AspNetRoles r ON r.Id = p.RoleId JOIN mnt.CFG_Opciones o ON o.OpcionId = p.OpcionId
            WHERE r.Name = 'GERENCIA' AND o.Codigo = 'DASHBOARD.REPORTES'
            """);
        EsquemaSqlServer.CorrerScript("39_t802_roles_permisos.sql");
        Assert.Equal(opciones.Count, b.Context.OpcionesSistema.Count());
        Assert.Equal(MatrizEsperada().Count + CatalogoPermisos.Quitados.Count, b.Context.PermisosRol.Count());
        Assert.Contains("GERENCIA|DASHBOARD.REPORTES|0", Matriz(b));

        // La semilla de la API sobre lo que cargó el script no agrega ni cambia nada
        SemillaSobre(b);
        Assert.Contains("GERENCIA|DASHBOARD.REPORTES|0", Matriz(b));
        Assert.Equal(MatrizEsperada().Count + CatalogoPermisos.Quitados.Count, b.Context.PermisosRol.Count());
    }

    private static void SemillaSobre(BaseDePrueba b)
    {
        b.Context.ChangeTracker.Clear();
        Infrastructure.Data.SemillaPermisos.SembrarAsync(b.Context).GetAwaiter().GetResult();
        b.Context.ChangeTracker.Clear();
    }

    [Fact]
    public void Script39_Rollback_EliminaLasTablas_YSePuedeVolverAAplicar()
    {
        if (!BaseDePrueba.EsSqlServer)
            return;

        using var b = BaseDePrueba.Crear();
        try
        {
            EsquemaSqlServer.CorrerScript("39_t802_roles_permisos_rollback.sql");
            EsquemaSqlServer.CorrerScript("39_t802_roles_permisos_rollback.sql");
            Assert.Equal(0, b.Context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name LIKE 'CFG[_]%'").AsEnumerable().First());
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("39_t802_roles_permisos.sql");
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador.sql");
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial.sql");
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            BaseDePrueba.OlvidarOrden();   // cambiaron las tablas: rearmar el orden de borrado
        }
        Assert.Equal(3, b.Context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name LIKE 'CFG[_]%'").AsEnumerable().First());
    }
}
