using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-770: el script 41 agrega la opción PRODUCCION.TABLERO con VER para OPERARIO y SUPERVISOR (lo mismo que el catálogo),
/// es idempotente, no pisa lo editado, y el rollback la saca (dos veces) y se puede volver a aplicar. Sólo SQL Server.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script41TableroTests
{
    private static List<string> PermisosTablero(BaseDePrueba b) =>
        b.Context.PermisosRol.AsNoTracking().Include(p => p.Opcion).Where(p => p.Opcion!.Codigo == Opciones.ProduccionTablero).ToList()
            .Join(b.Context.Roles.AsNoTracking().ToList(), p => p.RoleId, r => r.Id, (p, r) => $"{r.Name}|{(int)p.Acciones}")
            .OrderBy(x => x).ToList();

    private static int Opciones41(BaseDePrueba b) =>
        b.Context.OpcionesSistema.AsNoTracking().Count(o => o.Codigo == Opciones.ProduccionTablero);

    [Fact]
    public void Script41_CargaLaOpcionDelCatalogo_EsIdempotente_YElRollbackLaSaca()
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

            var def = CatalogoPermisos.Buscar(Opciones.ProduccionTablero)!;
            var opcion = b.Context.OpcionesSistema.AsNoTracking().Include(o => o.Padre).Single(o => o.Codigo == Opciones.ProduccionTablero);
            Assert.Equal($"{def.Nombre}|{def.PadreCodigo}|{def.Ruta}|{def.Orden}|{AccionPermisoTexto.AColumna(def.Aplicables)}",
                $"{opcion.Nombre}|{opcion.Padre?.Codigo}|{opcion.Ruta}|{opcion.Orden}|{opcion.AccionesAplicables}");
            var esperado = MatrizMigrada.Roles.Where(r => def.De(r) != AccionPermiso.Ninguna).Select(r => $"{r}|{(int)def.De(r)}").OrderBy(x => x).ToList();
            Assert.Equal(["OPERARIO|1", "SUPERVISOR|1"], esperado);
            Assert.Equal(esperado, PermisosTablero(b));

            // Editado desde el configurador (sin VER al supervisor) y otra vez el 41: no lo pisa ni duplica
            b.Context.Database.ExecuteSqlRaw("""
                UPDATE p SET PuedeVer = 0 FROM mnt.CFG_PermisosRol p
                JOIN dbo.AspNetRoles r ON r.Id = p.RoleId JOIN mnt.CFG_Opciones o ON o.OpcionId = p.OpcionId
                WHERE r.Name = 'SUPERVISOR' AND o.Codigo = 'PRODUCCION.TABLERO'
                """);
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador.sql");
            Assert.Equal(1, Opciones41(b));
            Assert.Equal(["OPERARIO|1", "SUPERVISOR|0"], PermisosTablero(b));

            // Rollback dos veces: sin opción ni permisos
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador_rollback.sql");
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador_rollback.sql");
            Assert.Equal(0, Opciones41(b));
            Assert.Empty(PermisosTablero(b));

            // Volver a aplicar
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador.sql");
            Assert.Equal(["OPERARIO|1", "SUPERVISOR|1"], PermisosTablero(b));
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("41_t770_tablero_operador.sql");
        }
    }
}
