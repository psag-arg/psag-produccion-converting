using Microsoft.EntityFrameworkCore;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Turno automático: el script 44 (tolerancia del operario por turno, producto heredado con confirmación y vínculo de las
/// detenciones que cruzan de turno) es idempotente y el rollback lo deshace (respaldando lo que saca). Sólo en SQL Server.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class Script44TurnoAutomaticoTests
{
    private static int Escalar(BaseDePrueba b, string sql) => b.Context.Database.SqlQueryRaw<int>(sql).AsEnumerable().First();

    private static int Columnas(BaseDePrueba b) => Escalar(b, """
        SELECT (CASE WHEN COL_LENGTH(N'mnt.ABM_Turnos', N'ToleranciaCierreOperadorMinutos') IS NULL THEN 0 ELSE 1 END)
             + (CASE WHEN COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'ConfirmadoPorUsuarioId') IS NULL THEN 0 ELSE 1 END)
             + (CASE WHEN COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'FechaHoraConfirmacion') IS NULL THEN 0 ELSE 1 END)
             + (CASE WHEN COL_LENGTH(N'mnt.DET_Detenciones', N'DetencionAnteriorId') IS NULL THEN 0 ELSE 1 END) AS Value
        """);

    private static bool AdmiteHeredado(BaseDePrueba b) => Escalar(b,
        "SELECT COUNT(*) AS Value FROM sys.check_constraints WHERE name = N'CK_PRD_TurnosProduccionProductos_Origen' AND definition LIKE N'%HEREDADO%'") == 1;

    [Fact]
    public void Script44_EsIdempotente_YElRollbackLoDeshace()
    {
        if (!BaseDePrueba.EsSqlServer)
            return;

        using var b = BaseDePrueba.Crear();
        try
        {
            EsquemaSqlServer.CorrerScript("44_turno_automatico.sql");   // ya corrió en el armado: no cambia nada
            Assert.Equal(4, Columnas(b));
            Assert.True(AdmiteHeredado(b));
            Assert.Equal(1, Escalar(b, "SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = N'UX_DET_Detenciones_DetencionAnteriorId' AND has_filter = 1"));

            // Un turno existente queda con 30 de tolerancia
            b.Context.Database.ExecuteSqlRaw("""
                INSERT INTO mnt.ABM_Turnos (Codigo, Nombre, HoraInicio, HoraFin, Orden, Activo, FechaHoraCreacion, CreadoPorUsuarioId, FechaHoraModificacion, ModificadoPorUsuarioId)
                VALUES ('T44', 'Turno 44', '05:00', '13:00', 1, 1, SYSDATETIMEOFFSET(), 'test', SYSDATETIMEOFFSET(), 'test');
                """);
            Assert.Equal(30, Escalar(b, "SELECT ToleranciaCierreOperadorMinutos AS Value FROM mnt.ABM_Turnos WHERE Codigo = 'T44'"));
            Assert.ThrowsAny<Exception>(() => b.Context.Database.ExecuteSqlRaw("UPDATE mnt.ABM_Turnos SET ToleranciaCierreOperadorMinutos = 241 WHERE Codigo = 'T44'"));

            EsquemaSqlServer.CorrerScript("44_turno_automatico_rollback.sql");
            EsquemaSqlServer.CorrerScript("44_turno_automatico_rollback.sql");
            Assert.Equal(0, Columnas(b));
            Assert.False(AdmiteHeredado(b));
        }
        finally
        {
            EsquemaSqlServer.CorrerScript("44_turno_automatico.sql");
            BaseDePrueba.OlvidarOrden();
        }
        Assert.Equal(4, Columnas(b));
        Assert.True(AdmiteHeredado(b));
    }
}
