-- ==============================================================================
-- Script: 37_t798_indicadores.sql
-- Descripción: T-798 etapa 5 · Indicadores (presupuesto vs. real).
--   PRD_ProduccionesTurno.Motivo NVARCHAR(500) NULL: motivo de la corrección del supervisor (CAMARA_CORREGIDA, obligatorio
--   en la API) o de la carga manual (MANUAL, opcional). Mismo nombre y tipo que TurnoProduccionConfigurations.cs.
--   No toca datos. El resto de la etapa 5 (producción oficial al cerrar el tramo, validación, indicadores) es de la API.
--   La cobertura mínima (0,90) va en el appsettings de la API: TurnosProduccion:CoberturaMinimaProduccion.
-- Versión: 1.0
-- Requiere: 34_t798_turnos_produccion.sql.
-- Idempotente: se puede ejecutar más de una vez (crea solo lo que falta).
-- Correr con la API detenida, antes de publicar la API de T-798 etapa 5.
-- Deshacer: 37_t798_indicadores_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.PRD_ProduccionesTurno', N'U') IS NULL
BEGIN
    RAISERROR(N'Falta correr 34_t798_turnos_produccion.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

IF COL_LENGTH(N'mnt.PRD_ProduccionesTurno', N'Motivo') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ProduccionesTurno] ADD [Motivo] NVARCHAR(500) NULL;
    PRINT 'Columna mnt.PRD_ProduccionesTurno.Motivo agregada.'
END
ELSE
    PRINT 'mnt.PRD_ProduccionesTurno.Motivo ya existe: no se hace nada.'
GO

SET NOEXEC OFF;
GO
