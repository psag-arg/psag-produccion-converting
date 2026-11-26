-- ==============================================================================
-- Script: 37_t798_indicadores_rollback.sql
-- Descripción: Deshace 37_t798_indicadores.sql (T-798 etapa 5): elimina PRD_ProduccionesTurno.Motivo.
--   Los motivos cargados se pierden (se imprime cuántas filas tenían motivo). Las filas de producción oficial que creó
--   la API de la etapa 5 NO se borran (insert-only); una API anterior las ignora.
--   Antes de correrlo, volver a una API anterior a T-798 etapa 5.
-- Idempotente: se puede ejecutar más de una vez.
-- Correr con la API detenida.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.PRD_ProduccionesTurno', N'Motivo') IS NOT NULL
BEGIN
    DECLARE @ConMotivo INT;
    EXEC sys.sp_executesql N'SELECT @n = COUNT(*) FROM [mnt].[PRD_ProduccionesTurno] WHERE [Motivo] IS NOT NULL;',
        N'@n INT OUTPUT', @n = @ConMotivo OUTPUT;
    PRINT CONCAT('Filas con motivo que se pierden: ', @ConMotivo);
    ALTER TABLE [mnt].[PRD_ProduccionesTurno] DROP COLUMN [Motivo];
    PRINT 'Columna mnt.PRD_ProduccionesTurno.Motivo eliminada.'
END
ELSE
    PRINT 'mnt.PRD_ProduccionesTurno.Motivo no existe: no hay nada que deshacer.'
GO
