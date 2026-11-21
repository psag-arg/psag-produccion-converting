-- ==============================================================================
-- Script: 33_bolsones_camara_8.sql
-- Descripción: Eficiencia con mangas y bolsones (T-785, pedido de Ferretti del 16/11, punto 1).
--   La cámara 8 (la que ve los bolsones) cuenta bolsones y reemplaza a la 09 en la eficiencia.
--   mnt.PuntosControlCalidad:
--   1. MetodoConteo admite BOLSONES (además de CARRILES y MANGAS): se rehace el CHECK del script 28.
--   2. IncluirEnEficiencia BIT NOT NULL DEFAULT 1: si la cámara aparece en la eficiencia del
--      Dashboard. Para reemplazar la 09 por la 8 se destilda la 09 (sigue informando paradas).
--   Se carga desde la Web: Maestros → Calidad → editar la cámara.
-- Versión: 1.0
-- Requiere: 23 (PuntosControlCalidad) y 28 (MetodoConteo).
-- Idempotente: se puede ejecutar más de una vez.
-- NO cambia datos: el alta de la T2-CAM08 y destildar la 09 están abajo, comentados (o desde la Web).
-- Correr antes de publicar la API (la API lee la columna).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MetodoConteo'
           AND definition NOT LIKE N'%BOLSONES%')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] DROP CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo];
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo anterior quitado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MetodoConteo')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo]
        CHECK ([MetodoConteo] IS NULL OR [MetodoConteo] IN (N'CARRILES', N'MANGAS', N'BOLSONES'));
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo creado (CARRILES, MANGAS, BOLSONES).'
END
GO

IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'IncluirEnEficiencia') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD [IncluirEnEficiencia] BIT NOT NULL CONSTRAINT [DF_PuntosControlCalidad_IncluirEnEficiencia] DEFAULT 1;
    PRINT 'Columna PuntosControlCalidad.IncluirEnEficiencia agregada (todas las cámaras en 1, como hasta ahora).'
END
ELSE
    PRINT 'Columna PuntosControlCalidad.IncluirEnEficiencia ya existía.'
GO

/*
-- ============================================================================
-- OPCIONAL: alta de la cámara 8 y reemplazo de la 09 en la eficiencia (también desde
-- la Web: Maestros → Calidad). Revisar el nombre de la línea. Destildar la 09 recién
-- cuando la 8 ya cuente bien (ver docs/CONTEO_CAMARA.md, sección 10).
-- El objetivo por hora de bolsones NO se carga acá: lo tiene que pasar Emiliano.
-- ============================================================================
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-33';
INSERT INTO [mnt].[PuntosControlCalidad] ([LineaId], [Codigo], [Nombre], [Descripcion], [Activo], [NumeroCamara],
    [MetodoConteo], [IncluirEnEficiencia], [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT l.[LineaId], N'T2-CAM08', N'Cámara 8 - bolsones',
       N'NVR 172.16.9.67 canal 8. Conteo de bolsones para la eficiencia (T-785).', 1, 8, N'BOLSONES', 1,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
FROM [mnt].[Lineas] l
WHERE l.[Nombre] = N'Tango 2'   -- <-- nombre de la línea en Maestros
  AND NOT EXISTS (SELECT 1 FROM [mnt].[PuntosControlCalidad] x WHERE x.[Codigo] = N'T2-CAM08');
PRINT CONCAT(N'Punto de control T2-CAM08 cargado: ', @@ROWCOUNT, N'.');

-- UPDATE [mnt].[PuntosControlCalidad] SET [IncluirEnEficiencia] = 0 WHERE [Codigo] = N'T2-CAM09';
-- UPDATE [mnt].[PuntosControlCalidad] SET [ObjetivoPorHora] = <valor> WHERE [Codigo] = N'T2-CAM08';
GO
*/
