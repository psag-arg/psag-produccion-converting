-- ==============================================================================
-- Script: 42_t770_tablero_gerencial_rollback.sql
-- Descripción: Deshace 42_t770_tablero_gerencial.sql (T-770 iteración 2): borra los permisos de la opción PRODUCCION.TABLERO_GERENCIAL
--   (CFG_PermisosRol) y la opción (CFG_Opciones). Son filas de configuración, no datos de negocio; lo que se haya
--   editado de esa opción desde Roles y permisos queda en mnt.Auditoria (CambioRol).
--   Antes de correrlo, volver a publicar la API anterior (la de esta iteración la vuelve a sembrar al arrancar). No toca PRODUCCION.TABLERO (script 41).
-- Idempotente: se puede ejecutar más de una vez.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NOT NULL AND OBJECT_ID(N'mnt.CFG_PermisosRol', N'U') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DELETE p FROM [mnt].[CFG_PermisosRol] p
    JOIN [mnt].[CFG_Opciones] o ON o.[OpcionId] = p.[OpcionId]
    WHERE o.[Codigo] = N'PRODUCCION.TABLERO_GERENCIAL';
    PRINT CONCAT('Permisos de PRODUCCION.TABLERO_GERENCIAL eliminados: ', @@ROWCOUNT, '.');
    DELETE FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'PRODUCCION.TABLERO_GERENCIAL';
    PRINT CONCAT('Opcion PRODUCCION.TABLERO_GERENCIAL eliminada: ', @@ROWCOUNT, '.');
    COMMIT TRANSACTION;
END
ELSE
    PRINT 'No hay tablas de roles y permisos (script 39): nada que deshacer.'
GO
