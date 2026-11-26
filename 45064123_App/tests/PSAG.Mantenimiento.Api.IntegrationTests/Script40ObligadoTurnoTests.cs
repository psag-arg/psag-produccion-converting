using Microsoft.EntityFrameworkCore;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-802 parte 2: el script 40 saca ABM_OperadoresLinea.TurnoHabitualId (respaldándolo), crea el índice único de un solo
/// operador vigente por turno y deja Maquinistas sólo como consulta; es idempotente y el rollback lo deshace (devolviendo el
/// turno habitual respaldado). Sólo en SQL Server (en SQLite no hay scripts).
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script40ObligadoTurnoTests
{
    private static int Escalar(BaseDePrueba b, string sql) => b.Context.Database.SqlQueryRaw<int>(sql).AsEnumerable().First();

    private static bool HayColumna(BaseDePrueba b) =>
        Escalar(b, "SELECT CASE WHEN COL_LENGTH(N'mnt.ABM_OperadoresLinea', N'TurnoHabitualId') IS NULL THEN 0 ELSE 1 END AS Value") == 1;

    private static bool HayIndice(BaseDePrueba b) =>
        Escalar(b, "SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = N'UX_PRD_TurnosProduccionOperadores_Vigente'") == 1;

    [Fact]
    public void Script40_EsIdempotente_YElRollbackDevuelveElTurnoHabitual()
    {
        if (!BaseDePrueba.EsSqlServer)
            return;

        using var b = BaseDePrueba.Crear();
        try
        {
            // Ya corrió en el armado del esquema: correrlo otra vez no cambia nada
            EsquemaSqlServer.CorrerScript("40_t802_obligado_turno.sql");
            Assert.False(HayColumna(b));
            Assert.True(HayIndice(b));

            // Rollback (dos veces): vuelve la columna con su FK, se va el índice
            EsquemaSqlServer.CorrerScript("40_t802_obligado_turno_rollback.sql");
            EsquemaSqlServer.CorrerScript("40_t802_obligado_turno_rollback.sql");
            Assert.True(HayColumna(b));
            Assert.False(HayIndice(b));

            // Una habilitación con turno habitual (como en producción antes de T-802)
            b.Context.Database.ExecuteSqlRaw("""
                INSERT INTO mnt.ABM_Lineas (Codigo, Nombre, Orden, Activo, FechaHoraCreacion, CreadoPorUsuarioId, FechaHoraModificacion, ModificadoPorUsuarioId)
                VALUES ('L40', 'Linea 40', 1, 1, SYSDATETIMEOFFSET(), 'test', SYSDATETIMEOFFSET(), 'test');
                INSERT INTO mnt.ABM_Turnos (Codigo, Nombre, HoraInicio, HoraFin, Orden, Activo, FechaHoraCreacion, CreadoPorUsuarioId, FechaHoraModificacion, ModificadoPorUsuarioId)
                VALUES ('T40', 'Turno 40', '05:00', '13:00', 1, 1, SYSDATETIMEOFFSET(), 'test', SYSDATETIMEOFFSET(), 'test');
                EXEC sys.sp_executesql N'INSERT INTO mnt.ABM_OperadoresLinea (UsuarioId, LineaId, TurnoHabitualId, FechaAsignacion, AsignadoPorUsuarioId)
                    SELECT ''oper-40'', (SELECT LineaId FROM mnt.ABM_Lineas WHERE Codigo = ''L40''), (SELECT TurnoId FROM mnt.ABM_Turnos WHERE Codigo = ''T40''), SYSDATETIMEOFFSET(), ''test''';
                """);

            // 40 otra vez: respalda y saca la columna
            EsquemaSqlServer.CorrerScript("40_t802_obligado_turno.sql");
            Assert.False(HayColumna(b));
            Assert.True(HayIndice(b));
            Assert.Equal(1, Escalar(b, "SELECT COUNT(*) AS Value FROM mnt.T802_RespaldoTurnoHabitual WHERE UsuarioId = 'oper-40' AND TurnoHabitualId IS NOT NULL"));

            // Rollback: el turno habitual vuelve
            EsquemaSqlServer.CorrerScript("40_t802_obligado_turno_rollback.sql");
            Assert.Equal(1, Escalar(b, "EXEC sys.sp_executesql N'SELECT COUNT(*) AS Value FROM mnt.ABM_OperadoresLinea WHERE UsuarioId = ''oper-40'' AND TurnoHabitualId IS NOT NULL'"));
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("40_t802_obligado_turno.sql");
            b.Context.Database.ExecuteSqlRaw("IF OBJECT_ID(N'mnt.T802_RespaldoTurnoHabitual', N'U') IS NOT NULL DELETE FROM mnt.T802_RespaldoTurnoHabitual");
            BaseDePrueba.OlvidarOrden();   // cambiaron tablas e índices: rearmar el orden de borrado
        }
        Assert.False(HayColumna(b));
    }
}
