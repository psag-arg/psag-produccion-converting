-- ==============================================================================
-- Script: 99_drop_objects_development_only.sql
-- Descripcion: Elimina todos los objetos de la base de datos
-- ADVERTENCIA: !!!SOLO PARA DESARROLLO!!! NUNCA EJECUTAR EN PRODUCCION
-- Version: 2.0 - Fase 1 (Detenciones con modelo de tiempo)
-- ==============================================================================

/*
+==============================================================================+
|                                                                              |
|    DANGER!!!  DANGER!!!  DANGER!!!                                           |
|                                                                              |
|           ESTE SCRIPT ELIMINA TODOS LOS DATOS Y OBJETOS                      |
|                                                                              |
|                    !!!SOLO PARA DESARROLLO!!!                                |
|                                                                              |
+==============================================================================+
*/

USE [PSAG_Mantenimiento]
GO

-- Verificacion de seguridad: Comentar las siguientes lineas para ejecutar
PRINT '============================================================'
PRINT 'ADVERTENCIA: Este script eliminara TODOS los datos y objetos.'
PRINT 'Para ejecutar, comente el bloque RAISERROR y GO siguiente.'
PRINT '============================================================'
RAISERROR('Script de eliminacion deshabilitado por seguridad. Revise el codigo antes de ejecutar.', 16, 1)
GO

-- ============================================================================
-- ELIMINAR FOREIGN KEYS
-- ============================================================================
PRINT 'Eliminando foreign keys...'
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_UsuariosLineas_Lineas')
    ALTER TABLE [mnt].[UsuariosLineas] DROP CONSTRAINT [FK_UsuariosLineas_Lineas]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetencionesNotas_Detenciones')
    ALTER TABLE [mnt].[DetencionesNotas] DROP CONSTRAINT [FK_DetencionesNotas_Detenciones]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetencionesEstadoHistorial_Detenciones')
    ALTER TABLE [mnt].[DetencionesEstadoHistorial] DROP CONSTRAINT [FK_DetencionesEstadoHistorial_Detenciones]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_DetallesFalla')
    ALTER TABLE [mnt].[Detenciones] DROP CONSTRAINT [FK_Detenciones_DetallesFalla]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_TiposFalla')
    ALTER TABLE [mnt].[Detenciones] DROP CONSTRAINT [FK_Detenciones_TiposFalla]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Equipos')
    ALTER TABLE [mnt].[Detenciones] DROP CONSTRAINT [FK_Detenciones_Equipos]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Lineas')
    ALTER TABLE [mnt].[Detenciones] DROP CONSTRAINT [FK_Detenciones_Lineas]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Turnos')
    ALTER TABLE [mnt].[Detenciones] DROP CONSTRAINT [FK_Detenciones_Turnos]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Equipos_Lineas')
    ALTER TABLE [mnt].[Equipos] DROP CONSTRAINT [FK_Equipos_Lineas]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetallesFalla_Equipos')
    ALTER TABLE [mnt].[DetallesFalla] DROP CONSTRAINT [FK_DetallesFalla_Equipos]
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetallesFalla_TiposFalla')
    ALTER TABLE [mnt].[DetallesFalla] DROP CONSTRAINT [FK_DetallesFalla_TiposFalla]
GO

PRINT 'Foreign keys eliminadas.'
GO

-- ============================================================================
-- ELIMINAR VISTAS
-- ============================================================================
PRINT 'Eliminando vistas...'
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_UsuariosConLineas' AND schema_id = SCHEMA_ID('mnt'))
    DROP VIEW [mnt].[VW_UsuariosConLineas]
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_LineasConEquipos' AND schema_id = SCHEMA_ID('mnt'))
    DROP VIEW [mnt].[VW_LineasConEquipos]
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_DetencionesIndicadores' AND schema_id = SCHEMA_ID('mnt'))
    DROP VIEW [mnt].[VW_DetencionesIndicadores]
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_DetencionesAbiertas' AND schema_id = SCHEMA_ID('mnt'))
    DROP VIEW [mnt].[VW_DetencionesAbiertas]
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_DetencionesDetalle' AND schema_id = SCHEMA_ID('mnt'))
    DROP VIEW [mnt].[VW_DetencionesDetalle]
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_ResumenTurnoActual' AND schema_id = SCHEMA_ID('mnt'))
    DROP VIEW [mnt].[VW_ResumenTurnoActual]
GO

PRINT 'Vistas eliminadas.'
GO

-- ============================================================================
-- ELIMINAR TABLAS DE NEGOCIO (Esquema mnt)
-- ============================================================================
PRINT 'Eliminando tablas de negocio...'
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Auditoria]') AND type in (N'U'))
    DROP TABLE [mnt].[Auditoria]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[UsuariosLineas]') AND type in (N'U'))
    DROP TABLE [mnt].[UsuariosLineas]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[DetencionesNotas]') AND type in (N'U'))
    DROP TABLE [mnt].[DetencionesNotas]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[DetencionesEstadoHistorial]') AND type in (N'U'))
    DROP TABLE [mnt].[DetencionesEstadoHistorial]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Detenciones]') AND type in (N'U'))
    DROP TABLE [mnt].[Detenciones]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[DetallesFalla]') AND type in (N'U'))
    DROP TABLE [mnt].[DetallesFalla]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[TiposFalla]') AND type in (N'U'))
    DROP TABLE [mnt].[TiposFalla]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Turnos]') AND type in (N'U'))
    DROP TABLE [mnt].[Turnos]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Equipos]') AND type in (N'U'))
    DROP TABLE [mnt].[Equipos]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Lineas]') AND type in (N'U'))
    DROP TABLE [mnt].[Lineas]
GO

PRINT 'Tablas de negocio eliminadas.'
GO

-- ============================================================================
-- ELIMINAR TABLAS DE IDENTITY
-- ============================================================================
PRINT 'Eliminando tablas de Identity...'
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetRoleClaims]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetRoleClaims]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserRoles]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetUserRoles]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserTokens]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetUserTokens]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserLogins]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetUserLogins]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUserClaims]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetUserClaims]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetRoles]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetRoles]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND type in (N'U'))
    DROP TABLE [dbo].[AspNetUsers]
GO

PRINT 'Tablas de Identity eliminadas.'
GO

-- ============================================================================
-- ELIMINAR ESQUEMA
-- ============================================================================
PRINT 'Eliminando esquema mnt...'
GO

IF EXISTS (SELECT * FROM sys.schemas WHERE name = N'mnt')
BEGIN
    DROP SCHEMA [mnt]
    PRINT 'Esquema [mnt] eliminado.'
END
GO

-- ============================================================================
-- OPCION: ELIMINAR BASE DE DATOS COMPLETA
-- ============================================================================
/*
-- Descomente para eliminar la base de datos completa

USE [master]
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'PSAG_Mantenimiento')
BEGIN
    ALTER DATABASE [PSAG_Mantenimiento] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
    DROP DATABASE [PSAG_Mantenimiento]
    PRINT 'Base de datos PSAG_Mantenimiento eliminada.'
END
GO
*/

PRINT '============================================================'
PRINT 'Limpieza completada. Todos los objetos han sido eliminados.'
PRINT '============================================================'
GO
