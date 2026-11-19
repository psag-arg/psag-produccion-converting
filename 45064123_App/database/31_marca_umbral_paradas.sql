-- ==============================================================================
-- Script: 31_marca_umbral_paradas.sql
-- Descripción: Marca de umbral en las paradas de cámara (reunión 17/11/2026, pedido de Emiliano:
--   "guardar todo, marcar lo que supera el umbral, agrupar en tabla anexa").
--   mnt.ParadasCamara:
--   1. SuperaUmbral BIT NULL: 1 = la parada llegó al umbral efectivo de la cámara (la que crea la
--      detención automática si la cámara tiene CreaDetenciones); 0 = microcorte.
--   2. UmbralSegundos INT NULL: umbral efectivo en segundos vigente AL INSERTAR la parada (snapshot):
--      SegundosUmbralDetencion si está cargado (script 30), si no MinutosUmbralDetencion x 60 (script 26).
--   La API completa las dos UNA sola vez al insertar la parada (ingesta del servicio de cámara,
--   DetencionCamaraReglas.MarcaUmbral). No hay UPDATE posteriores: si después cambia el umbral de la
--   cámara, la parada conserva contra qué se comparó ese día (como ValorEstandarSnapshot).
--   CHECK: las dos vacías (paradas anteriores a este script) o las dos cargadas con umbral 30..14400.
-- Versión: 1.0
-- Requiere: 25 (ParadasCamara), 26 y 30 (umbral de la cámara).
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga valores: las paradas existentes quedan en NULL (la app las evalúa con el umbral actual
-- cuando las necesita, sin guardar). Bloque comentado al final para completarlas si se decide.
-- Correr antes de publicar la API (la API escribe las columnas).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.ParadasCamara', N'SuperaUmbral') IS NULL
BEGIN
    ALTER TABLE [mnt].[ParadasCamara] ADD [SuperaUmbral] BIT NULL;
    PRINT 'Columna ParadasCamara.SuperaUmbral agregada (las paradas existentes quedan en NULL).'
END
ELSE
    PRINT 'Columna ParadasCamara.SuperaUmbral ya existía.'
GO

IF COL_LENGTH(N'mnt.ParadasCamara', N'UmbralSegundos') IS NULL
BEGIN
    ALTER TABLE [mnt].[ParadasCamara] ADD [UmbralSegundos] INT NULL;
    PRINT 'Columna ParadasCamara.UmbralSegundos agregada (las paradas existentes quedan en NULL).'
END
ELSE
    PRINT 'Columna ParadasCamara.UmbralSegundos ya existía.'
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE [name] = N'CK_ParadasCamara_MarcaUmbral')
BEGIN
    ALTER TABLE [mnt].[ParadasCamara]
        ADD CONSTRAINT [CK_ParadasCamara_MarcaUmbral]
        CHECK (([SuperaUmbral] IS NULL AND [UmbralSegundos] IS NULL)
            OR ([SuperaUmbral] IS NOT NULL AND [UmbralSegundos] BETWEEN 30 AND 14400));
    PRINT 'Check CK_ParadasCamara_MarcaUmbral creado.'
END
GO

-- Para buscar rápido los microcortes de una cámara antes de una detención (agrupador, script 32).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_ParadasCamara_Punto_Inicio')
BEGIN
    CREATE INDEX [IX_ParadasCamara_Punto_Inicio]
        ON [mnt].[ParadasCamara] ([PuntoControlCalidadId], [FechaHoraInicio])
        INCLUDE ([FechaHoraFin], [DuracionSegundos], [SuperaUmbral]);
    PRINT 'Índice IX_ParadasCamara_Punto_Inicio creado.'
END
GO

/*
-- ============================================================================
-- OPCIONAL (backfill): completar la marca de las paradas viejas con el umbral ACTUAL de cada cámara.
-- OJO: no es el umbral vigente el día de la parada (ese no se guardó); por eso no se corre solo.
-- Misma regla que la app: segundos (30..14400) si están cargados, si no minutos (1..240) x 60, si no 300;
-- supera = duración (inicio a fin, exacta) >= umbral.
-- ============================================================================
UPDATE p
SET p.[UmbralSegundos] = u.[Umbral],
    p.[SuperaUmbral] = CASE WHEN DATEDIFF_BIG(MILLISECOND, p.[FechaHoraInicio], p.[FechaHoraFin]) >= CAST(u.[Umbral] AS BIGINT) * 1000
                            THEN 1 ELSE 0 END
FROM [mnt].[ParadasCamara] p
INNER JOIN [mnt].[PuntosControlCalidad] pc ON pc.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]
CROSS APPLY (SELECT CASE
                 WHEN pc.[SegundosUmbralDetencion] BETWEEN 30 AND 14400 THEN pc.[SegundosUmbralDetencion]
                 WHEN pc.[MinutosUmbralDetencion] BETWEEN 1 AND 240 THEN pc.[MinutosUmbralDetencion] * 60
                 ELSE 300 END) u([Umbral])
WHERE p.[SuperaUmbral] IS NULL;
PRINT CONCAT(N'Paradas completadas con el umbral actual: ', @@ROWCOUNT, N'.');
GO
*/

PRINT 'Script 31_marca_umbral_paradas.sql completado.'
GO
