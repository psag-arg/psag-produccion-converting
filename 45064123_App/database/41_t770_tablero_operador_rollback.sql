-- ==============================================================================
-- Script: 41_t770_tablero_operador_rollback.sql
-- Descripción: Deshace 41_t770_tablero_operador.sql (T-770): borra los permisos de la opción PRODUCCION.TABLERO
--   (CFG_PermisosRol) y la opción (CFG_Opciones). Son filas de configuración, no datos de negocio; lo que se haya
--   editado de esa opción desde Roles y permisos queda en mnt.Auditoria (CambioRol).
--   Antes de correrlo, volver a publicar la API anterior (la de T-770 la vuelve a sembrar al arrancar).
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
    WHERE o.[Codigo] = N'PRODUCCION.TABLERO';
    PRINT CONCAT('Permisos de PRODUCCION.TABLERO eliminados: ', @@ROWCOUNT, '.');
    DELETE FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'PRODUCCION.TABLERO';
    PRINT CONCAT('Opcion PRODUCCION.TABLERO eliminada: ', @@ROWCOUNT, '.');
    COMMIT TRANSACTION;
END
ELSE
    PRINT 'No hay tablas de roles y permisos (script 39): nada que deshacer.'
GO
