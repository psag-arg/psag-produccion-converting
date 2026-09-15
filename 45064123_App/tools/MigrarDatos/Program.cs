using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

// Modo verificación
if (args.Length > 0 && args[0].ToLower() == "verificar")
{
    string verifyPath = args.Length > 1 ? args[1] : @"C:\Temp\PSAG_Migration\mantenimiento.db";
    VerificarDatos(verifyPath);
    return;
}

Console.WriteLine("==========================================");
Console.WriteLine("  PSAG.Mantenimiento - Migrador de Datos");
Console.WriteLine("  SQL Server -> SQLite");
Console.WriteLine("==========================================");
Console.WriteLine();

// Connection strings
string sqlServerConn = "Server=PILLOWSRV001;Database=PSAG_Mantenimiento;User Id=sa;Password=P177Ow_SQLsALog1n;TrustServerCertificate=True;";
string sqlitePath = @"C:\Temp\PSAG_Migration\mantenimiento.db";

// Verificar argumentos para override de rutas
if (args.Length >= 1)
{
    sqlitePath = args[0];
    Console.WriteLine($"SQLite path override: {sqlitePath}");
}

string sqliteConn = $"Data Source={sqlitePath}";

Console.WriteLine($"SQL Server: PILLOWSRV001\\PSAG_Mantenimiento");
Console.WriteLine($"SQLite: {sqlitePath}");
Console.WriteLine();

try
{
    // Crear directorio si no existe
    var dir = Path.GetDirectoryName(sqlitePath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);

    // Eliminar archivo existente
    if (File.Exists(sqlitePath))
    {
        File.Delete(sqlitePath);
        Console.WriteLine("Archivo SQLite anterior eliminado.");
    }

    using var sqlConn = new SqlConnection(sqlServerConn);
    using var liteConn = new SqliteConnection(sqliteConn);

    Console.WriteLine("[1/3] Conectando a SQL Server...");
    sqlConn.Open();
    Console.WriteLine("  SQL Server: OK");

    Console.WriteLine("[2/3] Creando esquema SQLite...");
    liteConn.Open();
    CreateSchema(liteConn);
    Console.WriteLine("  Esquema creado: OK");
    Console.WriteLine();

    Console.WriteLine("[3/3] Migrando datos...");
    Console.WriteLine();

    // Migrar tablas en orden (respetando foreign keys)
    MigrateTable(sqlConn, liteConn, "AspNetRoles", "[dbo].[AspNetRoles]",
        "Id, Name, NormalizedName, ConcurrencyStamp",
        "@Id, @Name, @NormalizedName, @ConcurrencyStamp");

    MigrateTable(sqlConn, liteConn, "AspNetUsers", "[dbo].[AspNetUsers]",
        "Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, NombreCompleto, Activo, RequiereCambioPassword, FechaCreacion, UltimoAcceso, RefreshToken, RefreshTokenExpiration",
        "@Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @EmailConfirmed, @PasswordHash, @SecurityStamp, @ConcurrencyStamp, @PhoneNumber, @PhoneNumberConfirmed, @TwoFactorEnabled, @LockoutEnd, @LockoutEnabled, @AccessFailedCount, @NombreCompleto, @Activo, @RequiereCambioPassword, @FechaCreacion, @UltimoAcceso, @RefreshToken, @RefreshTokenExpiration");

    MigrateTable(sqlConn, liteConn, "AspNetRoleClaims", "[dbo].[AspNetRoleClaims]",
        "Id, RoleId, ClaimType, ClaimValue",
        "@Id, @RoleId, @ClaimType, @ClaimValue");

    MigrateTable(sqlConn, liteConn, "AspNetUserClaims", "[dbo].[AspNetUserClaims]",
        "Id, UserId, ClaimType, ClaimValue",
        "@Id, @UserId, @ClaimType, @ClaimValue");

    MigrateTable(sqlConn, liteConn, "AspNetUserRoles", "[dbo].[AspNetUserRoles]",
        "UserId, RoleId",
        "@UserId, @RoleId");

    MigrateTable(sqlConn, liteConn, "Lineas", "[mnt].[Lineas]",
        "LineaId, Codigo, Nombre, Descripcion, Orden, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@LineaId, @Codigo, @Nombre, @Descripcion, @Orden, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "Turnos", "[mnt].[Turnos]",
        "TurnoId, Codigo, Nombre, HoraInicio, HoraFin, Orden, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@TurnoId, @Codigo, @Nombre, @HoraInicio, @HoraFin, @Orden, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "TiposFalla", "[mnt].[TiposFalla]",
        "TipoFallaId, Codigo, Nombre, Descripcion, Orden, RequiereDescripcion, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@TipoFallaId, @Codigo, @Nombre, @Descripcion, @Orden, @RequiereDescripcion, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "Equipos", "[mnt].[Equipos]",
        "EquipoId, LineaId, Codigo, Nombre, Descripcion, Orden, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@EquipoId, @LineaId, @Codigo, @Nombre, @Descripcion, @Orden, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "DetallesFalla", "[mnt].[DetallesFalla]",
        "DetalleFallaId, EquipoId, TipoFallaId, Codigo, Nombre, Descripcion, Orden, RequiereDescripcion, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@DetalleFallaId, @EquipoId, @TipoFallaId, @Codigo, @Nombre, @Descripcion, @Orden, @RequiereDescripcion, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "UsuariosLineas", "[mnt].[UsuariosLineas]",
        "UsuarioLineaId, UsuarioId, LineaId, FechaAsignacion, AsignadoPorUsuarioId, TurnoId",
        "@UsuarioLineaId, @UsuarioId, @LineaId, @FechaAsignacion, @AsignadoPorUsuarioId, @TurnoId");

    MigrateTable(sqlConn, liteConn, "Detenciones", "[mnt].[Detenciones]",
        "DetencionId, Codigo, LineaId, EquipoId, TipoFallaId, DetalleFallaId, TurnoId, Fecha, MinutosDeclarados, FechaHoraParada, FechaHoraArranque, OrigenTiempo, MinutosEfectivos, Descripcion, EstadoActual, AccionesRealizadas, ResolucionDescripcion, FechaHoraCierre, CerradoPorUsuarioId, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId, UsuarioLineaId",
        "@DetencionId, @Codigo, @LineaId, @EquipoId, @TipoFallaId, @DetalleFallaId, @TurnoId, @Fecha, @MinutosDeclarados, @FechaHoraParada, @FechaHoraArranque, @OrigenTiempo, @MinutosEfectivos, @Descripcion, @EstadoActual, @AccionesRealizadas, @ResolucionDescripcion, @FechaHoraCierre, @CerradoPorUsuarioId, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId, @UsuarioLineaId");

    MigrateTable(sqlConn, liteConn, "DetencionesEstadoHistorial", "[mnt].[DetencionesEstadoHistorial]",
        "DetencionEstadoHistorialId, DetencionId, EstadoAnterior, EstadoNuevo, FechaHora, UsuarioId, Observacion, OrigenAccion, CorrelationId",
        "@DetencionEstadoHistorialId, @DetencionId, @EstadoAnterior, @EstadoNuevo, @FechaHora, @UsuarioId, @Observacion, @OrigenAccion, @CorrelationId");

    MigrateTable(sqlConn, liteConn, "DetencionesNotas", "[mnt].[DetencionesNotas]",
        "DetencionNotaId, DetencionId, Texto, UsuarioId, FechaHora, TipoNota, EsNotaDeCierre",
        "@DetencionNotaId, @DetencionId, @Texto, @UsuarioId, @FechaHora, @TipoNota, @EsNotaDeCierre");

    // Productos y parámetros
    MigrateTable(sqlConn, liteConn, "Productos", "[mnt].[Productos]",
        "ProductoId, Codigo, Nombre, Descripcion, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@ProductoId, @Codigo, @Nombre, @Descripcion, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "LineasProductos", "[mnt].[LineasProductos]",
        "LineaProductoId, LineaId, ProductoId, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@LineaProductoId, @LineaId, @ProductoId, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "GruposParametro", "[mnt].[GruposParametro]",
        "GrupoParametroId, LineaId, Codigo, Nombre, Descripcion, Orden, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@GrupoParametroId, @LineaId, @Codigo, @Nombre, @Descripcion, @Orden, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "Parametros", "[mnt].[Parametros]",
        "ParametroId, GrupoParametroId, Codigo, Nombre, Descripcion, UnidadMedida, TipoDato, ValorMinimo, ValorMaximo, Orden, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@ParametroId, @GrupoParametroId, @Codigo, @Nombre, @Descripcion, @UnidadMedida, @TipoDato, @ValorMinimo, @ValorMaximo, @Orden, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "EstandaresParametro", "[mnt].[EstandaresParametro]",
        "EstandarParametroId, ParametroId, LineaId, ProductoId, ValorEstandar, ToleranciaInferior, ToleranciaSuperior, VigenciaDesde, VigenciaHasta, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@EstandarParametroId, @ParametroId, @LineaId, @ProductoId, @ValorEstandar, @ToleranciaInferior, @ToleranciaSuperior, @VigenciaDesde, @VigenciaHasta, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "RegistrosProceso", "[mnt].[RegistrosProceso]",
        "RegistroProcesoId, LineaId, ProductoId, TurnoId, Fecha, FechaHora, Estado, Observaciones, UsuarioRegistroId, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@RegistroProcesoId, @LineaId, @ProductoId, @TurnoId, @Fecha, @FechaHora, @Estado, @Observaciones, @UsuarioRegistroId, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "RegistrosProcesoDetalle", "[mnt].[RegistrosProcesoDetalle]",
        "RegistroProcesoDetalleId, RegistroProcesoId, ParametroId, ValorRegistrado, ValorEstandarSnapshot, ToleranciaInferiorSnapshot, ToleranciaSuperiorSnapshot, EstaEnRango, Observacion",
        "@RegistroProcesoDetalleId, @RegistroProcesoId, @ParametroId, @ValorRegistrado, @ValorEstandarSnapshot, @ToleranciaInferiorSnapshot, @ToleranciaSuperiorSnapshot, @EstaEnRango, @Observacion");

    // Inspecciones
    MigrateTable(sqlConn, liteConn, "PlanesInspeccion", "[mnt].[PlanesInspeccion]",
        "PlanInspeccionId, LineaId, Codigo, Nombre, Descripcion, TipoPuesto, FrecuenciaHoras, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@PlanInspeccionId, @LineaId, @Codigo, @Nombre, @Descripcion, @TipoPuesto, @FrecuenciaHoras, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "ItemsPlanInspeccion", "[mnt].[ItemsPlanInspeccion]",
        "ItemPlanInspeccionId, PlanInspeccionId, Codigo, Descripcion, MetodoInspeccion, CriterioAceptacion, Orden, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@ItemPlanInspeccionId, @PlanInspeccionId, @Codigo, @Descripcion, @MetodoInspeccion, @CriterioAceptacion, @Orden, @Activo, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "EjecucionesInspeccion", "[mnt].[EjecucionesInspeccion]",
        "EjecucionInspeccionId, PlanInspeccionId, TurnoId, FechaHora, UsuarioEjecutorId, Observaciones, TieneNoOk, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@EjecucionInspeccionId, @PlanInspeccionId, @TurnoId, @FechaHora, @UsuarioEjecutorId, @Observaciones, @TieneNoOk, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    MigrateTable(sqlConn, liteConn, "EjecucionesInspeccionDetalle", "[mnt].[EjecucionesInspeccionDetalle]",
        "EjecucionInspeccionDetalleId, EjecucionInspeccionId, ItemPlanInspeccionId, Resultado, ValorMedido, Observacion",
        "@EjecucionInspeccionDetalleId, @EjecucionInspeccionId, @ItemPlanInspeccionId, @Resultado, @ValorMedido, @Observacion");

    MigrateTable(sqlConn, liteConn, "HallazgosNoOk", "[mnt].[HallazgosNoOk]",
        "HallazgoNoOkId, EjecucionInspeccionDetalleId, Descripcion, AccionTomada, Responsable, FechaCompromiso, Estado, FechaCierre, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId",
        "@HallazgoNoOkId, @EjecucionInspeccionDetalleId, @Descripcion, @AccionTomada, @Responsable, @FechaCompromiso, @Estado, @FechaCierre, @FechaHoraCreacion, @FechaHoraModificacion, @CreadoPorUsuarioId, @ModificadoPorUsuarioId");

    // Auditoría
    MigrateTable(sqlConn, liteConn, "Auditoria", "[mnt].[Auditoria]",
        "AuditoriaId, UsuarioId, FechaHora, Accion, Entidad, EntidadId, ValoresAnteriores, ValoresNuevos, DireccionIp, CorrelationId, Descripcion",
        "@AuditoriaId, @UsuarioId, @FechaHora, @Accion, @Entidad, @EntidadId, @ValoresAnteriores, @ValoresNuevos, @DireccionIp, @CorrelationId, @Descripcion");

    Console.WriteLine();
    Console.WriteLine("==========================================");
    Console.WriteLine("  MIGRACION COMPLETADA EXITOSAMENTE");
    Console.WriteLine("==========================================");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

static void MigrateTable(SqlConnection sqlConn, SqliteConnection liteConn, string tableName, string sourceTable, string columns, string parameters)
{
    Console.Write($"  {tableName}... ");

    try
    {
        // Leer de SQL Server
        var selectSql = $"SELECT {columns.Replace("@", "")} FROM {sourceTable}";
        using var sqlCmd = new SqlCommand(selectSql, sqlConn);
        using var reader = sqlCmd.ExecuteReader();

        var columnNames = columns.Split(',').Select(c => c.Trim().Replace("@", "")).ToArray();
        var paramNames = parameters.Split(',').Select(p => p.Trim()).ToArray();

        int count = 0;

        using var transaction = liteConn.BeginTransaction();

        while (reader.Read())
        {
            var insertSql = $"INSERT INTO {tableName} ({columns.Replace("@", "")}) VALUES ({parameters})";
            using var liteCmd = liteConn.CreateCommand();
            liteCmd.Transaction = transaction;
            liteCmd.CommandText = insertSql;

            for (int i = 0; i < columnNames.Length; i++)
            {
                var value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);

                // Convertir TimeOnly a string para SQLite
                if (value is TimeSpan ts)
                {
                    value = ts.ToString(@"hh\:mm\:ss");
                }
                // Convertir DateOnly a string
                else if (value is DateTime dt && columnNames[i] == "Fecha")
                {
                    value = dt.ToString("yyyy-MM-dd");
                }

                liteCmd.Parameters.AddWithValue(paramNames[i], value);
            }

            liteCmd.ExecuteNonQuery();
            count++;
        }

        transaction.Commit();
        reader.Close();

        Console.WriteLine($"{count} registros");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}

static void CreateSchema(SqliteConnection conn)
{
    var schemaScript = @"
-- Desactivar FK durante la migración
PRAGMA foreign_keys = OFF;

-- Identity tables
CREATE TABLE IF NOT EXISTS AspNetRoles (
    Id TEXT PRIMARY KEY,
    Name TEXT NULL,
    NormalizedName TEXT NULL,
    ConcurrencyStamp TEXT NULL
);

CREATE TABLE IF NOT EXISTS AspNetUsers (
    Id TEXT PRIMARY KEY,
    UserName TEXT NULL,
    NormalizedUserName TEXT NULL,
    Email TEXT NULL,
    NormalizedEmail TEXT NULL,
    EmailConfirmed INTEGER NOT NULL,
    PasswordHash TEXT NULL,
    SecurityStamp TEXT NULL,
    ConcurrencyStamp TEXT NULL,
    PhoneNumber TEXT NULL,
    PhoneNumberConfirmed INTEGER NOT NULL,
    TwoFactorEnabled INTEGER NOT NULL,
    LockoutEnd TEXT NULL,
    LockoutEnabled INTEGER NOT NULL,
    AccessFailedCount INTEGER NOT NULL,
    NombreCompleto TEXT NULL,
    Activo INTEGER NOT NULL DEFAULT 1,
    RequiereCambioPassword INTEGER NOT NULL DEFAULT 0,
    FechaCreacion TEXT NULL,
    UltimoAcceso TEXT NULL,
    RefreshToken TEXT NULL,
    RefreshTokenExpiration TEXT NULL
);

CREATE TABLE IF NOT EXISTS AspNetRoleClaims (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RoleId TEXT NOT NULL,
    ClaimType TEXT NULL,
    ClaimValue TEXT NULL,
    FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id)
);

CREATE TABLE IF NOT EXISTS AspNetUserClaims (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    ClaimType TEXT NULL,
    ClaimValue TEXT NULL,
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
);

CREATE TABLE IF NOT EXISTS AspNetUserRoles (
    UserId TEXT NOT NULL,
    RoleId TEXT NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id),
    FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id)
);

-- Catalogos
CREATE TABLE IF NOT EXISTS Lineas (
    LineaId INTEGER PRIMARY KEY AUTOINCREMENT,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL
);

CREATE TABLE IF NOT EXISTS Turnos (
    TurnoId INTEGER PRIMARY KEY AUTOINCREMENT,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    HoraInicio TEXT NULL,
    HoraFin TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL
);

CREATE TABLE IF NOT EXISTS TiposFalla (
    TipoFallaId INTEGER PRIMARY KEY AUTOINCREMENT,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    RequiereDescripcion INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL
);

CREATE TABLE IF NOT EXISTS Equipos (
    EquipoId INTEGER PRIMARY KEY AUTOINCREMENT,
    LineaId INTEGER NOT NULL,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId)
);

CREATE TABLE IF NOT EXISTS DetallesFalla (
    DetalleFallaId INTEGER PRIMARY KEY AUTOINCREMENT,
    EquipoId INTEGER NULL,
    TipoFallaId INTEGER NULL,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    RequiereDescripcion INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (EquipoId) REFERENCES Equipos(EquipoId),
    FOREIGN KEY (TipoFallaId) REFERENCES TiposFalla(TipoFallaId)
);

CREATE TABLE IF NOT EXISTS UsuariosLineas (
    UsuarioLineaId INTEGER PRIMARY KEY AUTOINCREMENT,
    UsuarioId TEXT NOT NULL,
    LineaId INTEGER NOT NULL,
    FechaAsignacion TEXT NULL,
    AsignadoPorUsuarioId TEXT NULL,
    TurnoId INTEGER NULL,
    FOREIGN KEY (UsuarioId) REFERENCES AspNetUsers(Id),
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId),
    FOREIGN KEY (TurnoId) REFERENCES Turnos(TurnoId)
);

-- Detenciones
CREATE TABLE IF NOT EXISTS Detenciones (
    DetencionId INTEGER PRIMARY KEY AUTOINCREMENT,
    Codigo TEXT NOT NULL,
    LineaId INTEGER NOT NULL,
    EquipoId INTEGER NULL,
    TipoFallaId INTEGER NULL,
    DetalleFallaId INTEGER NULL,
    TurnoId INTEGER NULL,
    Fecha TEXT NOT NULL,
    MinutosDeclarados INTEGER NULL,
    FechaHoraParada TEXT NULL,
    FechaHoraArranque TEXT NULL,
    OrigenTiempo TEXT NOT NULL DEFAULT 'DECLARADO',
    MinutosEfectivos INTEGER NOT NULL,
    Descripcion TEXT NULL,
    EstadoActual TEXT NOT NULL DEFAULT 'REGISTRADA',
    AccionesRealizadas TEXT NULL,
    ResolucionDescripcion TEXT NULL,
    FechaHoraCierre TEXT NULL,
    CerradoPorUsuarioId TEXT NULL,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    UsuarioLineaId INTEGER NULL,
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId),
    FOREIGN KEY (EquipoId) REFERENCES Equipos(EquipoId),
    FOREIGN KEY (TipoFallaId) REFERENCES TiposFalla(TipoFallaId),
    FOREIGN KEY (DetalleFallaId) REFERENCES DetallesFalla(DetalleFallaId),
    FOREIGN KEY (TurnoId) REFERENCES Turnos(TurnoId),
    FOREIGN KEY (UsuarioLineaId) REFERENCES UsuariosLineas(UsuarioLineaId)
);

CREATE TABLE IF NOT EXISTS DetencionesEstadoHistorial (
    DetencionEstadoHistorialId INTEGER PRIMARY KEY AUTOINCREMENT,
    DetencionId INTEGER NOT NULL,
    EstadoAnterior TEXT NULL,
    EstadoNuevo TEXT NOT NULL,
    FechaHora TEXT NOT NULL,
    UsuarioId TEXT NULL,
    Observacion TEXT NULL,
    OrigenAccion TEXT NULL,
    CorrelationId TEXT NULL,
    FOREIGN KEY (DetencionId) REFERENCES Detenciones(DetencionId)
);

CREATE TABLE IF NOT EXISTS DetencionesNotas (
    DetencionNotaId INTEGER PRIMARY KEY AUTOINCREMENT,
    DetencionId INTEGER NOT NULL,
    Texto TEXT NOT NULL,
    UsuarioId TEXT NULL,
    FechaHora TEXT NOT NULL,
    TipoNota TEXT NOT NULL,
    EsNotaDeCierre INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (DetencionId) REFERENCES Detenciones(DetencionId)
);

-- Productos y parametros
CREATE TABLE IF NOT EXISTS Productos (
    ProductoId INTEGER PRIMARY KEY AUTOINCREMENT,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL
);

CREATE TABLE IF NOT EXISTS LineasProductos (
    LineaProductoId INTEGER PRIMARY KEY AUTOINCREMENT,
    LineaId INTEGER NOT NULL,
    ProductoId INTEGER NOT NULL,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId),
    FOREIGN KEY (ProductoId) REFERENCES Productos(ProductoId)
);

CREATE TABLE IF NOT EXISTS GruposParametro (
    GrupoParametroId INTEGER PRIMARY KEY AUTOINCREMENT,
    LineaId INTEGER NOT NULL,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId)
);

CREATE TABLE IF NOT EXISTS Parametros (
    ParametroId INTEGER PRIMARY KEY AUTOINCREMENT,
    GrupoParametroId INTEGER NOT NULL,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    UnidadMedida TEXT NULL,
    TipoDato TEXT NOT NULL DEFAULT 'NUMERICO',
    ValorMinimo REAL NULL,
    ValorMaximo REAL NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (GrupoParametroId) REFERENCES GruposParametro(GrupoParametroId)
);

CREATE TABLE IF NOT EXISTS EstandaresParametro (
    EstandarParametroId INTEGER PRIMARY KEY AUTOINCREMENT,
    ParametroId INTEGER NOT NULL,
    LineaId INTEGER NOT NULL,
    ProductoId INTEGER NULL,
    ValorEstandar REAL NOT NULL,
    ToleranciaInferior REAL NULL,
    ToleranciaSuperior REAL NULL,
    VigenciaDesde TEXT NULL,
    VigenciaHasta TEXT NULL,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (ParametroId) REFERENCES Parametros(ParametroId),
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId),
    FOREIGN KEY (ProductoId) REFERENCES Productos(ProductoId)
);

CREATE TABLE IF NOT EXISTS RegistrosProceso (
    RegistroProcesoId INTEGER PRIMARY KEY AUTOINCREMENT,
    LineaId INTEGER NOT NULL,
    ProductoId INTEGER NULL,
    TurnoId INTEGER NULL,
    Fecha TEXT NOT NULL,
    FechaHora TEXT NOT NULL,
    Estado TEXT NOT NULL DEFAULT 'REGISTRADO',
    Observaciones TEXT NULL,
    UsuarioRegistroId TEXT NULL,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId),
    FOREIGN KEY (ProductoId) REFERENCES Productos(ProductoId),
    FOREIGN KEY (TurnoId) REFERENCES Turnos(TurnoId)
);

CREATE TABLE IF NOT EXISTS RegistrosProcesoDetalle (
    RegistroProcesoDetalleId INTEGER PRIMARY KEY AUTOINCREMENT,
    RegistroProcesoId INTEGER NOT NULL,
    ParametroId INTEGER NOT NULL,
    ValorRegistrado REAL NULL,
    ValorEstandarSnapshot REAL NULL,
    ToleranciaInferiorSnapshot REAL NULL,
    ToleranciaSuperiorSnapshot REAL NULL,
    EstaEnRango INTEGER NULL,
    Observacion TEXT NULL,
    FOREIGN KEY (RegistroProcesoId) REFERENCES RegistrosProceso(RegistroProcesoId),
    FOREIGN KEY (ParametroId) REFERENCES Parametros(ParametroId)
);

-- Inspecciones
CREATE TABLE IF NOT EXISTS PlanesInspeccion (
    PlanInspeccionId INTEGER PRIMARY KEY AUTOINCREMENT,
    LineaId INTEGER NOT NULL,
    Codigo TEXT NOT NULL,
    Nombre TEXT NOT NULL,
    Descripcion TEXT NULL,
    TipoPuesto TEXT NOT NULL,
    FrecuenciaHoras INTEGER NOT NULL DEFAULT 8,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (LineaId) REFERENCES Lineas(LineaId)
);

CREATE TABLE IF NOT EXISTS ItemsPlanInspeccion (
    ItemPlanInspeccionId INTEGER PRIMARY KEY AUTOINCREMENT,
    PlanInspeccionId INTEGER NOT NULL,
    Codigo TEXT NOT NULL,
    Descripcion TEXT NOT NULL,
    MetodoInspeccion TEXT NULL,
    CriterioAceptacion TEXT NULL,
    Orden INTEGER NOT NULL DEFAULT 0,
    Activo INTEGER NOT NULL DEFAULT 1,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (PlanInspeccionId) REFERENCES PlanesInspeccion(PlanInspeccionId)
);

CREATE TABLE IF NOT EXISTS EjecucionesInspeccion (
    EjecucionInspeccionId INTEGER PRIMARY KEY AUTOINCREMENT,
    PlanInspeccionId INTEGER NOT NULL,
    TurnoId INTEGER NULL,
    FechaHora TEXT NOT NULL,
    UsuarioEjecutorId TEXT NULL,
    Observaciones TEXT NULL,
    TieneNoOk INTEGER NOT NULL DEFAULT 0,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (PlanInspeccionId) REFERENCES PlanesInspeccion(PlanInspeccionId),
    FOREIGN KEY (TurnoId) REFERENCES Turnos(TurnoId)
);

CREATE TABLE IF NOT EXISTS EjecucionesInspeccionDetalle (
    EjecucionInspeccionDetalleId INTEGER PRIMARY KEY AUTOINCREMENT,
    EjecucionInspeccionId INTEGER NOT NULL,
    ItemPlanInspeccionId INTEGER NOT NULL,
    Resultado TEXT NOT NULL,
    ValorMedido TEXT NULL,
    Observacion TEXT NULL,
    FOREIGN KEY (EjecucionInspeccionId) REFERENCES EjecucionesInspeccion(EjecucionInspeccionId),
    FOREIGN KEY (ItemPlanInspeccionId) REFERENCES ItemsPlanInspeccion(ItemPlanInspeccionId)
);

CREATE TABLE IF NOT EXISTS HallazgosNoOk (
    HallazgoNoOkId INTEGER PRIMARY KEY AUTOINCREMENT,
    EjecucionInspeccionDetalleId INTEGER NOT NULL,
    Descripcion TEXT NOT NULL,
    AccionTomada TEXT NULL,
    Responsable TEXT NULL,
    FechaCompromiso TEXT NULL,
    Estado TEXT NOT NULL DEFAULT 'ABIERTO',
    FechaCierre TEXT NULL,
    FechaHoraCreacion TEXT NULL,
    FechaHoraModificacion TEXT NULL,
    CreadoPorUsuarioId TEXT NULL,
    ModificadoPorUsuarioId TEXT NULL,
    FOREIGN KEY (EjecucionInspeccionDetalleId) REFERENCES EjecucionesInspeccionDetalle(EjecucionInspeccionDetalleId)
);

-- Auditoria
CREATE TABLE IF NOT EXISTS Auditoria (
    AuditoriaId INTEGER PRIMARY KEY AUTOINCREMENT,
    UsuarioId TEXT NULL,
    FechaHora TEXT NOT NULL,
    Accion TEXT NOT NULL,
    Entidad TEXT NOT NULL,
    EntidadId TEXT NULL,
    ValoresAnteriores TEXT NULL,
    ValoresNuevos TEXT NULL,
    DireccionIp TEXT NULL,
    CorrelationId TEXT NULL,
    Descripcion TEXT NULL
);

-- Indices
CREATE INDEX IF NOT EXISTS IX_Detenciones_Fecha ON Detenciones(Fecha);
CREATE INDEX IF NOT EXISTS IX_Detenciones_LineaId ON Detenciones(LineaId);
CREATE INDEX IF NOT EXISTS IX_Detenciones_Estado ON Detenciones(EstadoActual);
CREATE INDEX IF NOT EXISTS IX_Equipos_LineaId ON Equipos(LineaId);
CREATE INDEX IF NOT EXISTS IX_DetallesFalla_EquipoId ON DetallesFalla(EquipoId);
CREATE INDEX IF NOT EXISTS IX_UsuariosLineas_UsuarioId ON UsuariosLineas(UsuarioId);
CREATE INDEX IF NOT EXISTS IX_RegistrosProceso_Fecha ON RegistrosProceso(Fecha);
CREATE INDEX IF NOT EXISTS IX_EjecucionesInspeccion_FechaHora ON EjecucionesInspeccion(FechaHora);
";

    using var cmd = conn.CreateCommand();
    cmd.CommandText = schemaScript;
    cmd.ExecuteNonQuery();
}

static void VerificarDatos(string sqlitePath)
{
    Console.WriteLine("==========================================");
    Console.WriteLine("  Verificación de datos SQLite");
    Console.WriteLine("==========================================");
    Console.WriteLine();
    Console.WriteLine($"Archivo: {sqlitePath}");
    Console.WriteLine();

    if (!File.Exists(sqlitePath))
    {
        Console.WriteLine("ERROR: El archivo no existe!");
        return;
    }

    using var conn = new SqliteConnection($"Data Source={sqlitePath}");
    conn.Open();

    // Verificar conteo de tablas
    Console.WriteLine("=== Conteo de registros ===");
    string[] tablas = { "AspNetUsers", "AspNetRoles", "Lineas", "Turnos", "Equipos", "TiposFalla", "DetallesFalla", "Detenciones", "UsuariosLineas" };

    foreach (var tabla in tablas)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {tabla}";
            var count = cmd.ExecuteScalar();
            Console.WriteLine($"  {tabla}: {count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {tabla}: ERROR - {ex.Message}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("=== Lineas ===");
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT LineaId, Codigo, Nombre FROM Lineas";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            Console.WriteLine($"  LineaId={reader["LineaId"]}, Nombre={reader["Nombre"]}");
    }

    Console.WriteLine();
    Console.WriteLine("=== Ultimas 5 Detenciones ===");
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT DetencionId, Codigo, LineaId, TurnoId, Fecha, EstadoActual, MinutosEfectivos FROM Detenciones ORDER BY DetencionId DESC LIMIT 5";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            Console.WriteLine($"  ID={reader["DetencionId"]}, LineaId={reader["LineaId"]}, Fecha={reader["Fecha"]}, Estado={reader["EstadoActual"]}, Min={reader["MinutosEfectivos"]}");
    }

    Console.WriteLine();
    Console.WriteLine("=== Turnos ===");
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT TurnoId, Codigo, Nombre FROM Turnos";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            Console.WriteLine($"  TurnoId={reader["TurnoId"]}, Nombre={reader["Nombre"]}");
    }
}
