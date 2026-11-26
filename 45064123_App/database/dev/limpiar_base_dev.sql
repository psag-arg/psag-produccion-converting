-- ==============================================================================
-- Script: database/dev/limpiar_base_dev.sql
-- SOLO DESARROLLO / TEST. NO va en el paquete de producción.
-- Deja vacía la base ACTUAL (la del -d de sqlcmd): elimina FKs, vistas, procedimientos,
-- funciones, tablas (de todos los esquemas que no son del sistema) y el esquema [mnt].
-- NUNCA hace DROP DATABASE. Se niega a correr si el nombre de la base no termina en
-- _DEV, _TEST o _PRUEBA.
-- Uso: sqlcmd -S <servidor> -d PSAG_Mantenimiento_DEV -C -I -b -i limpiar_base_dev.sql
-- ==============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE N'%[_]DEV' AND DB_NAME() NOT LIKE N'%[_]TEST' AND DB_NAME() NOT LIKE N'%[_]PRUEBA'
BEGIN
    RAISERROR(N'limpiar_base_dev: la base actual (%s) no es de DEV/TEST/PRUEBA. No se toca nada.', 16, 1, N'?');
    SET NOEXEC ON;
END
GO

DECLARE @sql NVARCHAR(MAX);

-- 1. Foreign keys
SET @sql = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
             + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id
WHERE t.is_ms_shipped = 0;
EXEC sys.sp_executesql @sql;

-- 2. Vistas
SET @sql = N'';
SELECT @sql += N'DROP VIEW ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.views WHERE is_ms_shipped = 0;
EXEC sys.sp_executesql @sql;

-- 3. Procedimientos y funciones
SET @sql = N'';
SELECT @sql += N'DROP ' + CASE WHEN type = 'P' THEN N'PROCEDURE ' ELSE N'FUNCTION ' END
             + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.objects WHERE is_ms_shipped = 0 AND type IN ('P', 'FN', 'IF', 'TF');
EXEC sys.sp_executesql @sql;

-- 4. Tablas
SET @sql = N'';
SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.tables WHERE is_ms_shipped = 0;
EXEC sys.sp_executesql @sql;

-- 5. Esquema de negocio
IF SCHEMA_ID(N'mnt') IS NOT NULL EXEC (N'DROP SCHEMA [mnt]');

SELECT COUNT(*) AS ObjetosRestantes FROM sys.objects WHERE is_ms_shipped = 0 AND type IN ('U', 'V', 'P', 'FN', 'IF', 'TF', 'F');
PRINT N'limpiar_base_dev: base ' + DB_NAME() + N' vacía.';
GO
SET NOEXEC OFF;
GO
