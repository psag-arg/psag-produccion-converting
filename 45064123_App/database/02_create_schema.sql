-- ==============================================================================
-- Script: 02_create_schema.sql
-- Descripción: Crea el esquema [mnt] para las tablas de mantenimiento
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- Crear esquema si no existe
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'mnt')
BEGIN
    EXEC('CREATE SCHEMA [mnt]')
    PRINT 'Esquema [mnt] creado exitosamente.'
END
ELSE
BEGIN
    PRINT 'El esquema [mnt] ya existe.'
END
GO
