-- ==============================================================================
-- Script: 30_umbral_detencion_segundos.sql
-- Descripción: Umbral de detención automática de la cámara en SEGUNDOS (reunión 10/11/2026).
--   Para probar en planta con un umbral corto y ver rápido que se crean las detenciones;
--   después se vuelve a 5 minutos.
--   mnt.PuntosControlCalidad (cámaras):
--   1. SegundosUmbralDetencion INT NULL: umbral en segundos, 30 a 14400. El servicio de
--      cámara confirma una parada recién a los 20 s (2 ventanas de 10 s): el mínimo útil
--      es 30 s. Si está cargado manda sobre MinutosUmbralDetencion; vacío (lo normal) =
--      se usa MinutosUmbralDetencion (script 26) como hasta ahora.
--   Se carga desde la Web: Maestros → Calidad → editar la cámara (número + min/segundos).
-- Versión: 1.0
-- Requiere: 23 (PuntosControlCalidad) y 26 (MinutosUmbralDetencion).
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga valores: las cámaras existentes quedan en NULL (siguen en minutos).
-- Correr antes de publicar la API (la API lee la columna).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'SegundosUmbralDetencion') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] ADD [SegundosUmbralDetencion] INT NULL;
    PRINT 'Columna PuntosControlCalidad.SegundosUmbralDetencion agregada (vacía: se siguen usando los minutos).'
END
ELSE
    PRINT 'Columna PuntosControlCalidad.SegundosUmbralDetencion ya existía.'
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_SegundosUmbral')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [CK_PuntosControlCalidad_SegundosUmbral]
        CHECK ([SegundosUmbralDetencion] IS NULL OR [SegundosUmbralDetencion] BETWEEN 30 AND 14400);
    PRINT 'Check CK_PuntosControlCalidad_SegundosUmbral creado.'
END
GO

-- Volver a minutos a mano (lo mismo que guardar en minutos desde Maestros → Calidad):
-- UPDATE [mnt].[PuntosControlCalidad] SET [SegundosUmbralDetencion] = NULL WHERE [Codigo] = N'T2-CAM10';
