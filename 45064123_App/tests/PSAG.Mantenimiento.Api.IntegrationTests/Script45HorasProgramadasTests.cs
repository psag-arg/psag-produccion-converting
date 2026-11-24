using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Pedido 24/11: el script 45 pasa "Horas programadas" de DASHBOARD a MAESTROS como el catálogo (mismo código, ruta
/// maestros/horas-programadas, después de Parámetros línea + producto), no toca permisos, es idempotente, se puede volver a
/// correr después del 43, y el rollback la devuelve a DASHBOARD. Sólo SQL Server.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script45HorasProgramadasTests
{
    private static readonly string[] Cambian = [Opciones.DashboardHorasProgramadas, Opciones.MaestrosCamaras, Opciones.MaestrosCalidad];

    private static List<string> Menu(BaseDePrueba b)
    {
        b.Context.ChangeTracker.Clear();
        return b.Context.OpcionesSistema.AsNoTracking().Include(o => o.Padre).Where(o => Cambian.Contains(o.Codigo)).ToList()
            .Select(o => $"{o.Codigo}|{o.Padre?.Codigo}|{o.Ruta}|{o.Orden}").OrderBy(x => x).ToList();
    }

    private static List<string> MenuCatalogo() =>
        Cambian.Select(c => CatalogoPermisos.Buscar(c)!).Select(d => $"{d.Codigo}|{d.PadreCodigo}|{d.Ruta}|{d.Orden}").OrderBy(x => x).ToList();

    private static int Permisos(BaseDePrueba b) => b.Context.PermisosRol.AsNoTracking().Count(p => p.Opcion!.Codigo == Opciones.DashboardHorasProgramadas);

    [Fact]
    public void Script45_PasaHorasProgramadasAMaestros_EsIdempotente_SinTocarPermisos_YTieneRollback()
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
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            var antes = Menu(b);
            Assert.Contains($"{Opciones.DashboardHorasProgramadas}|DASHBOARD|configuracion/horas-programadas|5", antes);
            var permisos = Permisos(b);
            Assert.Equal(2, permisos);   // ADMINISTRADOR y SUPERVISOR

            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros.sql");
            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros.sql");
            Assert.Equal(MenuCatalogo(), Menu(b));
            Assert.Equal(permisos, Permisos(b));

            // Si se vuelve a correr el 43 (deja Horas programadas en DASHBOARD), el 45 la vuelve a mover
            EsquemaSqlServer.CorrerScript("43_t803_menu_dashboards.sql");
            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros.sql");
            Assert.Equal(MenuCatalogo(), Menu(b));

            // Rollback (dos veces): como la dejó el 43; los permisos, intactos
            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros_rollback.sql");
            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros_rollback.sql");
            Assert.Equal(antes, Menu(b));
            Assert.Equal(permisos, Permisos(b));
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("45_horas_programadas_maestros.sql");
        }
        Assert.Equal(MenuCatalogo(), Menu(b));
    }
}
