-- ==============================================================================
-- Script: 33_metodo_conteo_bolsones.sql
-- Descripción: Dashboard con varias cámaras por línea (reunión 17/11/2026).
--   mnt.PuntosControlCalidad (cámaras):
--   1. MetodoConteo admite BOLSONES (además de CARRILES y MANGAS, script 28) para la
--      cámara 08 de la Tango 2 (T2-CAM08), que cuenta bolsones. El Dashboard muestra
--      cada cámara con la unidad de su método (rollos, mangas, bolsones), sin código
--      especial por número de cámara: alcanza con dar de alta el punto de control.
--   No hace falta una columna de unidad: la unidad sale del método
--   (MetodoConteoCamara.UnidadDe en el Domain).
-- Versión: 1.0
-- Requiere: 23 (PuntosControlCalidad) y 28 (MetodoConteo).
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga datos: el alta de T2-CAM08 está COMENTADA al final (la detección de
-- bolsones todavía no está calibrada con video; no activarla hasta entonces).
-- Correr antes de publicar la API (si no, guardar BOLSONES en Maestros falla por el CHECK).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- El CHECK del script 28 sólo admite CARRILES y MANGAS: se reemplaza si todavía no admite BOLSONES.
IF EXISTS (SELECT * FROM sys.check_constraints
           WHERE name = N'CK_PuntosControlCalidad_MetodoConteo'
             AND parent_object_id = OBJECT_ID(N'mnt.PuntosControlCalidad')
             AND definition NOT LIKE N'%BOLSONES%')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] DROP CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo];
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo anterior (sin BOLSONES) eliminado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MetodoConteo')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo]
        CHECK ([MetodoConteo] IS NULL OR [MetodoConteo] IN (N'CARRILES', N'MANGAS', N'BOLSONES'));
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo creado (CARRILES, MANGAS, BOLSONES).'
END
ELSE
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo ya admitía BOLSONES.'
GO

/*
-- ============================================================================
-- NO ACTIVAR TODAVÍA: alta de la cámara 08 (bolsones) de la Tango 2.
-- La detección de bolsones no está calibrada con video. Cuando lo esté:
--   1. revisar la línea (se busca por nombre) y el número de cámara del NVR,
--   2. correr este bloque (o darla de alta desde Maestros → Calidad → Cámaras),
--   3. activar la cámara en el servicio de conteo (appsettings.local.json).
-- Con el punto activo y MetodoConteo = BOLSONES el Dashboard ya la muestra
-- ("Sin datos todavía" hasta que llegue el primer minuto). CreaDetenciones = 0:
-- sólo informa. El objetivo por hora lo pasa Emiliano (Maestros → Calidad).
-- ============================================================================
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-33';
INSERT INTO [mnt].[PuntosControlCalidad] ([LineaId], [Codigo], [Nombre], [Descripcion], [Activo],
    [NumeroCamara], [CreaDetenciones], [MinutosUmbralDetencion], [MetodoConteo], [ObjetivoPorHora],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT l.[LineaId], N'T2-CAM08', N'Cámara 08 - bolsones',
       N'NVR 172.16.9.67 canal 08. Conteo de bolsones (sin calibrar con video).', 1,
       8, 0, 5, N'BOLSONES', NULL,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
FROM [mnt].[Lineas] l
WHERE l.[Nombre] = N'Tango 2'   -- <-- nombre de la línea en Maestros
  AND NOT EXISTS (SELECT 1 FROM [mnt].[PuntosControlCalidad] x WHERE x.[Codigo] = N'T2-CAM08');
PRINT CONCAT(N'Punto de control T2-CAM08 cargado: ', @@ROWCOUNT, N'.');
GO
*/

PRINT 'Script 33_metodo_conteo_bolsones.sql completado.'
GO
