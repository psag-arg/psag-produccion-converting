using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-803: el script 43 deja la sección DASHBOARD como el catálogo (Dashboard Admin, Presupuesto vs. real, Tablero del
/// turno, Tablero de producción (gerencial), Horas programadas; Inicio sin ruta), le saca Dashboard Admin al operario sólo
/// si la fila no se editó, es idempotente, no pisa lo editado, y el rollback vuelve todo a 39/41/42 (dos veces) y se puede
/// volver a aplicar. Sólo SQL Server.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script43MenuDashboardsTests
{
    private static readonly string[] Cambian =
    [
        Opciones.DashboardInicio, Opciones.DashboardReportes, Opciones.DashboardHorasProgramadas,
        Opciones.ProduccionPresupuestoReal, Opciones.ProduccionTablero, Opciones.ProduccionTableroGerencial
    ];

    private static List<string> Menu(BaseDePrueba b)
    {
        b.Context.ChangeTracker.Clear();
        return b.Context.OpcionesSistema.AsNoTracking().Include(o => o.Padre).Where(o => Cambian.Contains(o.Codigo)).ToList()
            .Select(o => $"{o.Codigo}|{o.Nombre}|{o.Padre?.Codigo}|{o.Ruta ?? "NULL"}|{o.Orden}|{o.AccionesAplicables}|{o.Activo}")
            .OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Lo que deja el 43: el catálogo, salvo Horas programadas, que el 45 pasó después a MAESTROS (el 43 publicado la deja en
    /// DASHBOARD, orden 5, configuracion/horas-programadas).
    /// </summary>
    private static List<string> MenuCatalogo() =>
        Cambian.Select(c => CatalogoPermisos.Buscar(c)!)
            .Select(d => d.Codigo == Opciones.DashboardHorasProgramadas
                ? $"{d.Codigo}|{d.Nombre}|{Opciones.Dashboard}|configuracion/horas-programadas|5|{AccionPermisoTexto.AColumna(d.Aplicables)}|True"
                : $"{d.Codigo}|{d.Nombre}|{d.PadreCodigo}|{d.Ruta ?? "NULL"}|{d.Orden}|{AccionPermisoTexto.AColumna(d.Aplicables)}|True")
            .OrderBy(x => x).ToList();

    private static string Fila(BaseDePrueba b, string rol, string codigo)
    {
        b.Context.ChangeTracker.Clear();
        var r = b.Context.Roles.AsNoTracking().Single(x => x.Name == rol);
        var p = b.Context.PermisosRol.AsNoTracking().Include(x => x.Opcion).SingleOrDefault(x => x.RoleId == r.Id && x.Opcion!.Codigo == codigo);
        return p == null ? "-" : $"{(int)p.Acciones}|{p.ModificadoPorUsuarioId}";
    }

    private static void Editar(BaseDePrueba b, string rol, string codigo, int ver, string usuario) =>
        b.Context.Database.ExecuteSqlRaw("""
            UPDATE p SET PuedeVer = {0}, ModificadoPorUsuarioId = {1} FROM mnt.CFG_PermisosRol p
            JOIN dbo.AspNetRoles r ON r.Id = p.RoleId JOIN mnt.CFG_Opciones o ON o.OpcionId = p.OpcionId
            WHERE r.Name = {2} AND o.Codigo = {3}
            """, ver, usuario, rol, codigo);

    [Fact]
    public void Script43_OrdenaElMenu_QuitaDashboardAdminAlOperario_EsIdempotente_NoPisaLoEditado_YTieneRollback()
    {
        if (!BaseDePrueba.EsSqlServer)
            return;

        using var b = BaseDePrueba.Crear();
        foreach (var rol in MatrizMigrada.Roles)
            b.Context.Database.ExecuteSqlRaw(
                "INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (NEWID(), {0}, {0}, NEWID())", rol);
        try
        {
            EsquemaSqlServer.CorrerScript("39_t802_roles_permisos.sql");
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador.sql");
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial.sql");
            var antes = Menu(b);
            Assert.Contains("DASHBOARD.INICIO|Inicio|DASHBOARD||1|VER|True", antes);
            Assert.Equal("1|SCRIPT-39-T802", Fila(b, "OPERARIO", Opciones.DashboardReportes));

            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            Assert.Equal(MenuCatalogo(), Menu(b));
            Assert.Equal("0|SCRIPT-43-T803", Fila(b, "OPERARIO", Opciones.DashboardReportes));
            Assert.Equal("1|SCRIPT-39-T802", Fila(b, "GERENCIA", Opciones.DashboardReportes));
            // Las demás opciones de Producción quedan en Producción
            Assert.Equal("PRODUCCION", b.Context.OpcionesSistema.AsNoTracking().Include(o => o.Padre)
                .Single(o => o.Codigo == Opciones.ProduccionTurnos).Padre!.Codigo);

            // Otra vez: igual
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            Assert.Equal(MenuCatalogo(), Menu(b));
            Assert.Equal("0|SCRIPT-43-T803", Fila(b, "OPERARIO", Opciones.DashboardReportes));

            // Un administrador se lo devuelve al operario desde el configurador: el 43 no lo pisa
            Editar(b, "OPERARIO", Opciones.DashboardReportes, 1, "admin-configurador");
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            Assert.Equal("1|admin-configurador", Fila(b, "OPERARIO", Opciones.DashboardReportes));

            // Rollback (dos veces): menú de 39/41/42; el permiso editado a mano no se toca
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards_rollback.sql");
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards_rollback.sql");
            Assert.Equal(antes, Menu(b));
            Assert.Equal("1|admin-configurador", Fila(b, "OPERARIO", Opciones.DashboardReportes));

            // Con la fila marcada por el 43, el rollback le devuelve VER
            Editar(b, "OPERARIO", Opciones.DashboardReportes, 1, "SCRIPT-39-T802");
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            Assert.Equal("0|SCRIPT-43-T803", Fila(b, "OPERARIO", Opciones.DashboardReportes));
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards_rollback.sql");
            Assert.Equal("1|SCRIPT-39-T802", Fila(b, "OPERARIO", Opciones.DashboardReportes));
            Assert.Equal(antes, Menu(b));

            // Volver a aplicar
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            Assert.Equal(MenuCatalogo(), Menu(b));
            Assert.Equal("0|SCRIPT-43-T803", Fila(b, "OPERARIO", Opciones.DashboardReportes));
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros.sql");   // el 45 va después del 43
        }
    }
}
