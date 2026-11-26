using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Sólo en modo SQL Server (en SQLite no comprueba nada): la base armada por los scripts de database/ (02–38) tiene
/// todas las tablas y columnas del modelo de EF, con la misma nulabilidad, largo de textos y precisión de decimales.
/// Si alguien cambia una entidad sin script (o al revés), falla acá y no en producción.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class EsquemaScriptsVsModeloTests
{
    private sealed record Columna(string Tipo, bool Nula, int Largo, byte Precision, byte Escala);

    [Fact]
    public void BaseArmadaPorScripts_TieneLasTablasYColumnasDelModelo()
    {
        if (!BaseDePrueba.EsSqlServer)
            return;

        using var prueba = BaseDePrueba.Crear();
        var modelo = prueba.Context.GetService<IDesignTimeModel>().Model;
        var columnas = LeerColumnas();
        var errores = new List<string>();
        var revisadas = 0;

        foreach (var entidad in modelo.GetEntityTypes().Where(e => e.GetTableName() != null && e.GetViewName() == null))
        {
            var tabla = StoreObjectIdentifier.Table(entidad.GetTableName()!, entidad.GetSchema() ?? "dbo");
            var clave = $"{tabla.Schema}.{tabla.Name}";
            if (!columnas.ContainsKey(clave))
            {
                errores.Add($"Falta la tabla {clave} ({entidad.ClrType.Name}).");
                continue;
            }

            foreach (var propiedad in entidad.GetProperties())
            {
                var nombre = propiedad.GetColumnName(tabla);
                if (nombre == null)
                    continue;
                revisadas++;
                if (!columnas[clave].TryGetValue(nombre, out var real))
                {
                    errores.Add($"Falta la columna {clave}.{nombre}.");
                    continue;
                }

                if (propiedad.IsColumnNullable(tabla) != real.Nula)
                    errores.Add($"{clave}.{nombre}: el modelo dice {(propiedad.IsColumnNullable(tabla) ? "NULL" : "NOT NULL")}, la base {(real.Nula ? "NULL" : "NOT NULL")}.");

                var largo = propiedad.GetMaxLength(tabla);
                if (largo != null && real.Tipo is "nvarchar" or "varchar" && real.Largo != -1 && largo != real.Largo)
                    errores.Add($"{clave}.{nombre}: largo {largo} en el modelo, {real.Largo} en la base.");

                var precision = propiedad.GetPrecision(tabla);
                if (precision != null && real.Tipo == "decimal"
                    && (precision != real.Precision || (propiedad.GetScale(tabla) ?? 0) != real.Escala))
                    errores.Add($"{clave}.{nombre}: decimal({precision},{propiedad.GetScale(tabla)}) en el modelo, ({real.Precision},{real.Escala}) en la base.");
            }
        }

        Assert.True(revisadas > 300, $"Sólo se revisaron {revisadas} columnas.");
        Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
    }

    private static Dictionary<string, Dictionary<string, Columna>> LeerColumnas()
    {
        var resultado = new Dictionary<string, Dictionary<string, Columna>>(StringComparer.OrdinalIgnoreCase);
        using var conexion = new SqlConnection(BaseDePrueba.ConexionSqlServer);
        conexion.Open();
        using var comando = new SqlCommand(
            """
            SELECT SCHEMA_NAME(t.schema_id) + N'.' + t.name, c.name, ty.name, c.is_nullable,
                   CASE WHEN c.max_length = -1 THEN -1 WHEN ty.name IN (N'nvarchar', N'nchar') THEN c.max_length / 2 ELSE c.max_length END,
                   c.precision, c.scale
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE t.is_ms_shipped = 0
            """, conexion);
        using var r = comando.ExecuteReader();
        while (r.Read())
        {
            var tabla = r.GetString(0);
            if (!resultado.TryGetValue(tabla, out var cols))
                resultado[tabla] = cols = new Dictionary<string, Columna>(StringComparer.OrdinalIgnoreCase);
            cols[r.GetString(1)] = new Columna(r.GetString(2), r.GetBoolean(3), Convert.ToInt32(r.GetValue(4)), r.GetByte(5), r.GetByte(6));
        }
        return resultado;
    }
}
