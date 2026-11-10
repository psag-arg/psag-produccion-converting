-- ==============================================================================
-- Script: 29_snapshot_rango_control_proceso.sql
-- Descripción: Control de Proceso - snapshot de si el estándar tenía rango (auditoría).
--   mnt.RegistrosProcesoDetalle:
--   1. EstandarConRango BIT NULL: al guardar un valor, si el estándar contra el que se
--      comparó ESE DÍA tenía rango (mín/máx o tolerancia). Sin rango (sin estándar o
--      sólo una referencia sin tolerancia) el valor se muestra "Falta estándar", aunque
--      después se cargue un estándar. Evita que un registro viejo cargado sin estándar
--      aparezca OK contra el estándar de hoy.
--   Las filas existentes quedan en NULL: la app decide por ValorEstandarSnapshot
--   (sin texto = ese día no había estándar).
-- Versión: 1.0
-- Requiere: 11 (RegistrosProcesoDetalle) y 21.
-- Idempotente: se puede ejecutar más de una vez. Correr ANTES de publicar la API nueva.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.RegistrosProcesoDetalle', N'EstandarConRango') IS NULL
BEGIN
    ALTER TABLE [mnt].[RegistrosProcesoDetalle] ADD [EstandarConRango] BIT NULL;
    PRINT 'Columna RegistrosProcesoDetalle.EstandarConRango agregada (NULL en las filas existentes).'
END
ELSE
    PRINT 'Columna RegistrosProcesoDetalle.EstandarConRango ya existía.'
GO
