-- ==============================================================================
-- Script: 39_t802_roles_permisos_rollback.sql
-- Descripción: Deshace 39_t802_roles_permisos.sql (T-802 parte 1): elimina CFG_PermisosRol, CFG_RolesConfiguracion y
--   CFG_Opciones. Lo configurado desde "Roles y permisos" se pierde (queda en mnt.Auditoria, Accion = CambioRol).
--   Antes de correrlo, volver a publicar una API anterior a T-802 (la de T-802 no autoriza nada sin estas tablas).
--   No toca las tablas de Identity.
-- Idempotente: se puede ejecutar más de una vez.
-- Correr con la API detenida.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_PermisosRol', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[CFG_PermisosRol];
    PRINT 'Tabla mnt.CFG_PermisosRol eliminada.'
END
ELSE
    PRINT 'mnt.CFG_PermisosRol no existe.'
GO

IF OBJECT_ID(N'mnt.CFG_RolesConfiguracion', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[CFG_RolesConfiguracion];
    PRINT 'Tabla mnt.CFG_RolesConfiguracion eliminada.'
END
ELSE
    PRINT 'mnt.CFG_RolesConfiguracion no existe.'
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[CFG_Opciones];
    PRINT 'Tabla mnt.CFG_Opciones eliminada.'
END
ELSE
    PRINT 'mnt.CFG_Opciones no existe.'
GO
