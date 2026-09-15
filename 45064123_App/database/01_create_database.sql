-- ==============================================================================
-- Script: 01_create_database.sql
-- Descripción: Crea la base de datos PSAG_Mantenimiento
-- Nota: Ejecutar como administrador de SQL Server
-- ==============================================================================

USE [master]
GO

-- Verificar si la base de datos existe y eliminarla (SOLO DESARROLLO)
-- ADVERTENCIA: Esto eliminará todos los datos existentes
-- IF EXISTS (SELECT name FROM sys.databases WHERE name = N'PSAG_Mantenimiento')
-- BEGIN
--     ALTER DATABASE [PSAG_Mantenimiento] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
--     DROP DATABASE [PSAG_Mantenimiento]
-- END
-- GO

-- Crear la base de datos si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PSAG_Mantenimiento')
BEGIN
    CREATE DATABASE [PSAG_Mantenimiento]
    COLLATE Latin1_General_CI_AS
END
GO

-- Configuraciones de la base de datos
ALTER DATABASE [PSAG_Mantenimiento] SET RECOVERY SIMPLE
GO

ALTER DATABASE [PSAG_Mantenimiento] SET AUTO_CLOSE OFF
GO

ALTER DATABASE [PSAG_Mantenimiento] SET READ_COMMITTED_SNAPSHOT ON
GO

PRINT 'Base de datos PSAG_Mantenimiento creada exitosamente.'
GO
