-- ==============================================================================
-- Script: 34_t798_turnos_produccion_rollback.sql
-- Descripción: Deshace 34_t798_turnos_produccion.sql (T-798 etapa 2).
--   Elimina PRD_ProduccionesTurno, PRD_TurnosProduccionProductos, PRD_TurnosProduccionOperadores y
--   PRD_TurnosProduccion (en ese orden, por las FK).
--   SEGURIDAD: si alguna tiene filas, se detiene y no borra nada (son datos de planta: turnos tomados, relevos,
--   productos). Para forzar, primero exportar y vaciar las tablas a mano, con el OK de Emiliano.
--   Antes de correrlo, volver a una API que no use estas tablas (anterior a T-798 etapa 2).
-- Requiere: 37, 36 y 35 _rollback corridos antes (orden inverso; se verifica).
-- Idempotente: se puede ejecutar más de una vez.
-- Correr con la API detenida.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Orden inverso: antes van los rollback de 37, 36 y 35 (detenciones, conteos, paradas y registros apuntan a los turnos).
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoProduccionId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoProduccionId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CAM_Paradas', N'TurnoProduccionId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CPR_Registros', N'TurnoProduccionId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CAL_Registros', N'TurnoProduccionId') IS NOT NULL
   OR COL_LENGTH(N'mnt.PRD_ProduccionesTurno', N'Motivo') IS NOT NULL
BEGIN
    RAISERROR(N'Correr antes 37, 36 y 35 _rollback (orden inverso): todavía hay columnas que apuntan a los turnos de producción. No se cambió nada.', 16, 1);
    SET NOEXEC ON;
END
GO

DECLARE @Hay BIT = 0;
IF OBJECT_ID(N'mnt.PRD_ProduccionesTurno', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM [mnt].[PRD_ProduccionesTurno]) SET @Hay = 1;', N'@Hay BIT OUTPUT', @Hay = @Hay OUTPUT;
IF OBJECT_ID(N'mnt.PRD_TurnosProduccionProductos', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionProductos]) SET @Hay = 1;', N'@Hay BIT OUTPUT', @Hay = @Hay OUTPUT;
IF OBJECT_ID(N'mnt.PRD_TurnosProduccionOperadores', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionOperadores]) SET @Hay = 1;', N'@Hay BIT OUTPUT', @Hay = @Hay OUTPUT;
IF OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccion]) SET @Hay = 1;', N'@Hay BIT OUTPUT', @Hay = @Hay OUTPUT;
IF @Hay = 1
BEGIN
    RAISERROR(N'Las tablas de turnos de producción tienen datos: no se deshace. Revisar a mano (exportar antes de vaciar).', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'mnt.PRD_ProduccionesTurno', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[PRD_ProduccionesTurno];
    PRINT 'Tabla mnt.PRD_ProduccionesTurno eliminada.'
END
GO

IF OBJECT_ID(N'mnt.PRD_TurnosProduccionProductos', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[PRD_TurnosProduccionProductos];
    PRINT 'Tabla mnt.PRD_TurnosProduccionProductos eliminada.'
END
GO

IF OBJECT_ID(N'mnt.PRD_TurnosProduccionOperadores', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[PRD_TurnosProduccionOperadores];
    PRINT 'Tabla mnt.PRD_TurnosProduccionOperadores eliminada.'
END
GO

IF OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[PRD_TurnosProduccion];
    PRINT 'Tabla mnt.PRD_TurnosProduccion eliminada.'
END
GO

SET NOEXEC OFF;
GO
PRINT 'Script 34_t798_turnos_produccion_rollback.sql terminado.'
GO
