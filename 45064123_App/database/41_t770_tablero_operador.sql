-- ==============================================================================
-- Script: 41_t770_tablero_operador.sql
-- Descripción: T-770 · Tablero del operador (tablet).
--   Agrega al catálogo de permisos la opción PRODUCCION.TABLERO ("Tablero del turno (operador)", ruta
--   produccion/tablero, sección PRODUCCION, orden 5, aplica sólo VER) y le da VER a OPERARIO y SUPERVISOR.
--   Mismos datos que CatalogoPermisos.cs y la semilla de la API (lo verifica un test contra SQL Server).
--   No crea tablas ni toca datos de negocio.
-- Versión: 1.0
-- Requiere: 39 (CFG_Opciones, CFG_PermisosRol) y 08 (AspNetRoles).
-- Idempotente: la opción se inserta o actualiza por código; los permisos sólo se insertan para los pares (rol, opción)
--   que no existen, así que no pisa lo editado desde Configuración → Roles y permisos.
-- Correr con: sqlcmd -C -I -b -f 65001. Se puede correr con la API andando (la API vieja ignora la opción nueva).
-- Deshacer: 41_t770_tablero_operador_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NULL OR OBJECT_ID(N'mnt.CFG_PermisosRol', N'U') IS NULL
BEGIN
    RAISERROR(N'Faltan las tablas de roles y permisos (script 39).', 16, 1);
    SET NOEXEC ON;
END
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Ahora DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-41-T770';
DECLARE @PadreId INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'PRODUCCION');

IF @PadreId IS NULL
BEGIN
    RAISERROR(N'No existe la sección PRODUCCION en mnt.CFG_Opciones (correr el 39).', 16, 1);
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'PRODUCCION.TABLERO')
    BEGIN
        INSERT INTO [mnt].[CFG_Opciones] ([Codigo], [Nombre], [PadreId], [Ruta], [Orden], [AccionesAplicables], [Activo])
        VALUES (N'PRODUCCION.TABLERO', N'Tablero del turno (operador)', @PadreId, N'produccion/tablero', 5, N'VER', 1);
        PRINT 'Opcion PRODUCCION.TABLERO creada.'
    END
    ELSE
    BEGIN
        UPDATE [mnt].[CFG_Opciones]
        SET [Nombre] = N'Tablero del turno (operador)', [PadreId] = @PadreId, [Ruta] = N'produccion/tablero',
            [Orden] = 5, [AccionesAplicables] = N'VER', [Activo] = 1
        WHERE [Codigo] = N'PRODUCCION.TABLERO';
        PRINT 'Opcion PRODUCCION.TABLERO ya existe (actualizada).'
    END

    DECLARE @OpcionId INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'PRODUCCION.TABLERO');

    INSERT INTO [mnt].[CFG_PermisosRol] ([RoleId], [OpcionId], [PuedeVer], [PuedeAlta], [PuedeModificar], [PuedeBaja], [FechaHoraModificacion], [ModificadoPorUsuarioId])
    SELECT r.[Id], @OpcionId, 1, 0, 0, 0, @Ahora, @Usuario
    FROM [dbo].[AspNetRoles] r
    WHERE r.[NormalizedName] IN (N'OPERARIO', N'SUPERVISOR')
      AND NOT EXISTS (SELECT 1 FROM [mnt].[CFG_PermisosRol] x WHERE x.[RoleId] = r.[Id] AND x.[OpcionId] = @OpcionId);
    PRINT CONCAT('Permisos nuevos de PRODUCCION.TABLERO: ', @@ROWCOUNT, '.');
END

COMMIT TRANSACTION;
GO

SELECT o.[Codigo], r.[Name] AS Rol, p.[PuedeVer]
FROM [mnt].[CFG_PermisosRol] p
JOIN [mnt].[CFG_Opciones] o ON o.[OpcionId] = p.[OpcionId]
JOIN [dbo].[AspNetRoles] r ON r.[Id] = p.[RoleId]
WHERE o.[Codigo] = N'PRODUCCION.TABLERO'
ORDER BY r.[Name];
GO
