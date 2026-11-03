-- ==============================================================================
-- Script: 28_eficiencia_objetivo_camara.sql
-- Descripción: Indicadores de eficiencia en el Dashboard (reunión 03/11/2026, [8:31] [80:05]).
--   mnt.PuntosControlCalidad (cámaras):
--   1. ObjetivoPorHora DECIMAL(10,2) NULL: unidades por hora con la máquina en marcha
--      plena (rollos para la 09, mangas para la 10). Objetivo del turno =
--      ObjetivoPorHora x horas programadas del turno. Vacío = "Falta cargar el objetivo".
--   2. MetodoConteo NVARCHAR(20) NULL: qué cuenta la cámara, CARRILES (rollos) o
--      MANGAS. Vacío = la app lo deduce de los datos (1 carril = mangas).
--   Se cargan desde la Web: Maestros → Calidad → editar la cámara.
-- Versión: 1.0
-- Requiere: 23 (PuntosControlCalidad), 25 y 26.
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga valores (ver el bloque comentado al final).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'ObjetivoPorHora') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] ADD [ObjetivoPorHora] DECIMAL(10, 2) NULL;
    PRINT 'Columna PuntosControlCalidad.ObjetivoPorHora agregada (vacía).'
END
ELSE
    PRINT 'Columna PuntosControlCalidad.ObjetivoPorHora ya existía.'
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_ObjetivoPorHora')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [CK_PuntosControlCalidad_ObjetivoPorHora]
        CHECK ([ObjetivoPorHora] IS NULL OR ([ObjetivoPorHora] > 0 AND [ObjetivoPorHora] <= 100000));
    PRINT 'Check CK_PuntosControlCalidad_ObjetivoPorHora creado.'
END
GO

IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'MetodoConteo') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] ADD [MetodoConteo] NVARCHAR(20) NULL;
    PRINT 'Columna PuntosControlCalidad.MetodoConteo agregada (vacía).'
END
ELSE
    PRINT 'Columna PuntosControlCalidad.MetodoConteo ya existía.'
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MetodoConteo')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo]
        CHECK ([MetodoConteo] IS NULL OR [MetodoConteo] IN (N'CARRILES', N'MANGAS'));
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo creado.'
END
GO

/*
-- ============================================================================
-- OPCIONAL: cargar el método de las cámaras actuales (también desde la Web).
-- El objetivo por hora NO se carga acá: lo tiene que pasar Emiliano.
-- ============================================================================
UPDATE [mnt].[PuntosControlCalidad] SET [MetodoConteo] = N'CARRILES' WHERE [Codigo] = N'T2-CAM09' AND [MetodoConteo] IS NULL;
UPDATE [mnt].[PuntosControlCalidad] SET [MetodoConteo] = N'MANGAS'   WHERE [Codigo] = N'T2-CAM10' AND [MetodoConteo] IS NULL;
-- UPDATE [mnt].[PuntosControlCalidad] SET [ObjetivoPorHora] = <valor> WHERE [Codigo] = N'T2-CAM10';
GO
*/

PRINT 'Script 28_eficiencia_objetivo_camara.sql completado.'
GO
