using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-770 iteración 2: el script 42 agrega la opción PRODUCCION.TABLERO_GERENCIAL (sin ruta de menú) con VER para
/// ADMINISTRADOR, SUPERVISOR y GERENCIA (lo mismo que el catálogo), es idempotente, no pisa lo editado, y el rollback la
/// saca (dos veces) sin tocar la del 41 y se puede volver a aplicar. Sólo SQL Server.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script42TableroGerencialTests
{
    private static List<string> Permisos(BaseDePrueba b, string codigo) =>
        b.Context.PermisosRol.AsNoTracking().Include(p => p.Opcion).Where(p => p.Opcion!.Codigo == codigo).ToList()
            .Join(b.Context.Roles.AsNoTracking().ToList(), p => p.RoleId, r => r.Id, (p, r) => $"{r.Name}|{(int)p.Acciones}")
            .OrderBy(x => x).ToList();

    private static int Cantidad(BaseDePrueba b) =>
        b.Context.OpcionesSistema.AsNoTracking().Count(o => o.Codigo == Opciones.ProduccionTableroGerencial);

    [Fact]
    public void Script42_CargaLaOpcionDelCatalogo_EsIdempotente_YElRollbackLaSaca()
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

            var def = CatalogoPermisos.Buscar(Opciones.ProduccionTableroGerencial)!;
            Assert.Null(def.Ruta);   // sin entrada de menú (T-803)
            var opcion = b.Context.OpcionesSistema.AsNoTracking().Include(o => o.Padre).Single(o => o.Codigo == Opciones.ProduccionTableroGerencial);
            Assert.Equal($"{def.Nombre}|{def.PadreCodigo}|{def.Ruta}|{def.Orden}|{AccionPermisoTexto.AColumna(def.Aplicables)}",
                $"{opcion.Nombre}|{opcion.Padre?.Codigo}|{opcion.Ruta}|{opcion.Orden}|{opcion.AccionesAplicables}");
            var esperado = MatrizMigrada.Roles.Where(r => def.De(r) != AccionPermiso.Ninguna).Select(r => $"{r}|{(int)def.De(r)}").OrderBy(x => x).ToList();
            Assert.Equal(["ADMINISTRADOR|1", "GERENCIA|1", "SUPERVISOR|1"], esperado);
            Assert.Equal(esperado, Permisos(b, Opciones.ProduccionTableroGerencial));

            // Editado desde el configurador (sin VER a gerencia) y otra vez el 42: no lo pisa ni duplica
            b.Context.Database.ExecuteSqlRaw("""
                UPDATE p SET PuedeVer = 0 FROM mnt.CFG_PermisosRol p
                JOIN dbo.AspNetRoles r ON r.Id = p.RoleId JOIN mnt.CFG_Opciones o ON o.OpcionId = p.OpcionId
                WHERE r.Name = 'GERENCIA' AND o.Codigo = 'PRODUCCION.TABLERO_GERENCIAL'
                """);
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial.sql");
            Assert.Equal(1, Cantidad(b));
            Assert.Equal(["ADMINISTRADOR|1", "GERENCIA|0", "SUPERVISOR|1"], Permisos(b, Opciones.ProduccionTableroGerencial));

            // Rollback dos veces: sin opción ni permisos; la del 41 sigue
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial_rollback.sql");
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial_rollback.sql");
            Assert.Equal(0, Cantidad(b));
            Assert.Empty(Permisos(b, Opciones.ProduccionTableroGerencial));
            Assert.Equal(["OPERARIO|1", "SUPERVISOR|1"], Permisos(b, Opciones.ProduccionTablero));

            // Volver a aplicar
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial.sql");
            Assert.Equal(["ADMINISTRADOR|1", "GERENCIA|1", "SUPERVISOR|1"], Permisos(b, Opciones.ProduccionTableroGerencial));
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("42_t770_tablero_gerencial.sql");
        }
    }
}
