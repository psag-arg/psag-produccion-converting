-- ==============================================================================
-- Script: 32_marca_umbral_paradas_camara.sql
-- Descripción: Registrar todas las paradas de la cámara con marca de umbral (T-784, criterio
--   de Emiliano del 20/11/2026: toda microparada se registra; los reportes deciden si la toman).
--   mnt.ParadasCamara:
--   1. UmbralSegundos INT NULL: umbral de detención efectivo de la cámara AL INGRESAR la parada
--      (snapshot). Si después cambia el umbral (ej.: de 30 s de prueba a 5 min) la parada sigue
--      marcada contra el de ese día.
--   2. SuperaUmbral BIT NULL: la parada llegó a ese umbral. Las que no son microparadas: quedan
--      registradas pero no se le piden declarar al operador (no crean detención).
--   Las filas existentes quedan en NULL: la app las marca con el umbral actual, como hasta ahora.
-- Versión: 1.0
-- Requiere: 25 (ParadasCamara).
-- Idempotente: se puede ejecutar más de una vez.
-- Correr antes de publicar la API (la API escribe las columnas).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.ParadasCamara', N'UmbralSegundos') IS NULL
BEGIN
    ALTER TABLE [mnt].[ParadasCamara] ADD [UmbralSegundos] INT NULL;
    PRINT 'Columna ParadasCamara.UmbralSegundos agregada.'
END
ELSE
    PRINT 'Columna ParadasCamara.UmbralSegundos ya existía.'
GO

IF COL_LENGTH(N'mnt.ParadasCamara', N'SuperaUmbral') IS NULL
BEGIN
    ALTER TABLE [mnt].[ParadasCamara] ADD [SuperaUmbral] BIT NULL;
    PRINT 'Columna ParadasCamara.SuperaUmbral agregada.'
END
ELSE
    PRINT 'Columna ParadasCamara.SuperaUmbral ya existía.'
GO
