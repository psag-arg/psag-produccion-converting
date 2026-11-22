-- ==============================================================================
-- T-798_conteos_antes.sql · Conteos de las tablas ANTES del deploy de T-798 (paso 3 del T-798_DEPLOY.md).
-- SOLO LECTURA. Guardar la salida (conteos_antes.txt) para compararla con T-798_verificacion.sql (bloque 3).
--   sqlcmd -S localhost\SQLEXPRESS -d PSAG_Mantenimiento -I -f 65001 -W -i T-798_conteos_antes.sql -o conteos_antes.txt
-- Una tabla que no existe en esta base sale con -1.
-- ==============================================================================
SET NOCOUNT ON;
DECLARE @r TABLE ([Tabla] SYSNAME, [Filas] BIGINT);
DECLARE @t SYSNAME, @n BIGINT, @sql NVARCHAR(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT v.[t] FROM (VALUES (N'Detenciones'), (N'DetencionesEstadoHistorial'), (N'DetencionesNotas'),
        (N'ConteosCamaraMinuto'), (N'ParadasCamara'), (N'AlertasCamara'), (N'RegistrosProceso'), (N'RegistrosCalidad'),
        (N'PuntosControlCalidad'), (N'Productos'), (N'Lineas'), (N'UsuariosLineas'), (N'Turnos')) v([t]);
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @n = -1;
    IF OBJECT_ID(N'mnt.' + QUOTENAME(@t), N'U') IS NOT NULL
    BEGIN
        SET @sql = N'SELECT @n = COUNT_BIG(*) FROM [mnt].' + QUOTENAME(@t);
        EXEC sys.sp_executesql @sql, N'@n BIGINT OUTPUT', @n = @n OUTPUT;
    END
    INSERT INTO @r VALUES (@t, @n);
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;
SELECT [Tabla], [Filas] FROM @r;

PRINT '== Puntos de control que son cámaras (pasan a CAM_Camaras con el mismo código)';
IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'NumeroCamara') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT [Codigo], [Nombre], [NumeroCamara], [MetodoConteo], [Activo] FROM [mnt].[PuntosControlCalidad]
                             WHERE [NumeroCamara] IS NOT NULL OR [MetodoConteo] IS NOT NULL ORDER BY [Codigo];';

PRINT '== Horarios de turno antes (el 32 los deja en T1 05-13, T2 13-21, T3 21-05)';
IF OBJECT_ID(N'mnt.Turnos', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT [Codigo], [Nombre], [HoraInicio], [HoraFin], [Activo] FROM [mnt].[Turnos] ORDER BY [HoraInicio];';
