using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Base de datos de cada test de integración. Dos motores:
/// <list type="bullet">
/// <item><b>SQLite en memoria</b> (por defecto): una base nueva por test con <c>EnsureCreated</c>. Rápido, pero no es el motor de producción.</item>
/// <item><b>SQL Server</b> (<c>PSAG_TEST_MOTOR=SQLSERVER</c> + <c>PSAG_TEST_CONNECTION</c>): la base de TEST con el esquema armado
/// por los scripts de <c>database/</c> (ver <see cref="EsquemaSqlServer"/>), una vez por corrida; antes de cada test se vacían
/// todas las tablas y se reinician las identidades, así cada test arranca con la base vacía, como en SQLite.
/// Los tests que usan la base van en serie (colección <see cref="ColeccionBaseDeDatos"/>).</item>
/// </list>
/// Este es el criterio de verde para entregar: la suite pasa en SQL Server.
/// </summary>
public sealed class BaseDePrueba : IDisposable
{
    public const string VariableMotor = "PSAG_TEST_MOTOR";
    public const string VariableConexion = "PSAG_TEST_CONNECTION";

    private readonly SqliteConnection? _sqlite;

    public MantenimientoDbContext Context { get; }

    public static bool EsSqlServer =>
        string.Equals(Environment.GetEnvironmentVariable(VariableMotor)?.Trim(), "SQLSERVER", StringComparison.OrdinalIgnoreCase);

    /// <summary>Cadena de conexión de la base de TEST (sólo con <c>PSAG_TEST_MOTOR=SQLSERVER</c>).</summary>
    public static string ConexionSqlServer
    {
        get
        {
            var conexion = Environment.GetEnvironmentVariable(VariableConexion);
            if (string.IsNullOrWhiteSpace(conexion))
                throw new InvalidOperationException($"{VariableMotor}=SQLSERVER pero falta {VariableConexion}.");
            var baseDatos = new SqlConnectionStringBuilder(conexion).InitialCatalog;
            // Los tests vacían la base en cada test: nunca contra una base que no sea de prueba.
            if (!baseDatos.EndsWith("_TEST", StringComparison.OrdinalIgnoreCase) && !baseDatos.EndsWith("_PRUEBA", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{VariableConexion} debe apuntar a una base _TEST o _PRUEBA (es '{baseDatos}').");
            return conexion;
        }
    }

    private BaseDePrueba(SqliteConnection? sqlite, MantenimientoDbContext context)
    {
        _sqlite = sqlite;
        Context = context;
    }

    /// <summary>Base vacía con el esquema del motor elegido y su contexto.</summary>
    public static BaseDePrueba Crear()
    {
        if (EsSqlServer)
        {
            EsquemaSqlServer.Asegurar();
            Vaciar();
            return new BaseDePrueba(null, new MantenimientoDbContext(OpcionesSqlServer()));
        }

        var conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        var context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(conexion).Options);
        context.Database.EnsureCreated();
        return new BaseDePrueba(conexion, context);
    }

    private static DbContextOptions<MantenimientoDbContext> OpcionesSqlServer() =>
        new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlServer(ConexionSqlServer).Options;

    public void Dispose()
    {
        Context.Dispose();
        _sqlite?.Dispose();
    }

    // ---------------------------------------------------------------- SQL Server: vaciado entre tests

    private static readonly object Candado = new();
    private static string? _sqlVaciar;

    /// <summary>
    /// Vacía todas las tablas de usuario (mnt y dbo, incluidas AspNet* y los respaldos de T-798) en orden de FK y reinicia
    /// las identidades usadas (el próximo id vuelve a ser 1). No usa transacciones ni desactiva constraints.
    /// </summary>
    private static void Vaciar()
    {
        lock (Candado)
        {
            using var conexion = new SqlConnection(ConexionSqlServer);
            conexion.Open();
            _sqlVaciar ??= ArmarSqlVaciar(conexion);
            using var comando = new SqlCommand(_sqlVaciar, conexion) { CommandTimeout = 120 };
            comando.ExecuteNonQuery();
        }
    }

    internal static void OlvidarOrden()
    {
        lock (Candado)
            _sqlVaciar = null;
    }

    private static string ArmarSqlVaciar(SqlConnection conexion)
    {
        var tablas = new List<string>();
        using (var c = new SqlCommand(
                   "SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) FROM sys.tables WHERE is_ms_shipped = 0 ORDER BY 1", conexion))
        using (var r = c.ExecuteReader())
            while (r.Read())
                tablas.Add(r.GetString(0));

        // hija -> padres (sin autorreferencias: un DELETE de toda la tabla las resuelve en la misma sentencia)
        var padres = tablas.ToDictionary(t => t, _ => new HashSet<string>());
        using (var c = new SqlCommand(
                   """
                   SELECT QUOTENAME(SCHEMA_NAME(h.schema_id)) + N'.' + QUOTENAME(h.name),
                          QUOTENAME(SCHEMA_NAME(p.schema_id)) + N'.' + QUOTENAME(p.name)
                   FROM sys.foreign_keys fk
                   JOIN sys.tables h ON h.object_id = fk.parent_object_id
                   JOIN sys.tables p ON p.object_id = fk.referenced_object_id
                   WHERE fk.parent_object_id <> fk.referenced_object_id
                   """, conexion))
        using (var r = c.ExecuteReader())
            while (r.Read())
                padres[r.GetString(0)].Add(r.GetString(1));

        // Orden de borrado: primero las tablas que nadie referencia (hijas), después sus padres.
        var orden = new List<string>();
        var pendientes = new HashSet<string>(tablas);
        while (pendientes.Count > 0)
        {
            var hojas = pendientes.Where(t => !pendientes.Any(o => o != t && padres[o].Contains(t))).OrderBy(t => t).ToList();
            if (hojas.Count == 0)
                throw new InvalidOperationException("Ciclo de FKs entre: " + string.Join(", ", pendientes));
            orden.AddRange(hojas);
            pendientes.ExceptWith(hojas);
        }

        var sql = new StringBuilder("SET NOCOUNT ON;\n");
        foreach (var t in orden)
            sql.Append("IF EXISTS (SELECT 1 FROM ").Append(t).Append(") DELETE FROM ").Append(t).Append(";\n");
        // Identidades: las que se usaron desde el último reinicio (last_value > 0) vuelven a 0 -> el próximo id es 1.
        sql.Append("""
                   DECLARE @r NVARCHAR(MAX) = N'';
                   SELECT @r += N'DBCC CHECKIDENT (''' + REPLACE(QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name), N'''', N'''''')
                                + N''', RESEED, 0) WITH NO_INFOMSGS;' + CHAR(10)
                   FROM sys.identity_columns ic JOIN sys.tables t ON t.object_id = ic.object_id
                   WHERE t.is_ms_shipped = 0 AND ic.last_value IS NOT NULL AND CONVERT(BIGINT, ic.last_value) <> 0;
                   EXEC sys.sp_executesql @r;
                   """);
        return sql.ToString();
    }
}

/// <summary>
/// Los tests que usan la base van en esta colección: en SQL Server comparten la base de TEST y corren en serie;
/// el fixture arma el esquema con los scripts una sola vez por corrida.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionBaseDeDatos : ICollectionFixture<EsquemaSqlServer>
{
    public const string Nombre = "BaseDeDatos";
}
