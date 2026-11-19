-- ==============================================================================
-- Script: 32_agrupador_detenciones.sql
-- Descripción: Agrupador de detenciones (reunión 17/11/2026, pedido de Emiliano: guardar todo,
--   marcar lo que supera el umbral y agrupar en una tabla anexa SIN modificar los registros originales).
--   1. mnt.DetencionAgrupacion: un microcorte de cámara (hijo, mnt.ParadasCamara) agrupado bajo una
--      detención (padre, mnt.Detenciones) porque el operario respondió "mismo motivo".
--      Origen OPERADOR (hoy el único) o AUTOMATICO (reservado). Desagrupar = Activo 0 con quién y
--      cuándo (DesactivadoPorUsuarioId, FechaHoraDesactivacion): nunca se borra.
--      Una parada sólo puede estar activa en UN grupo (índice único filtrado).
--   2. mnt.vw_DetencionesAgrupadas: por detención padre con al menos un microcorte activo, inicio real
--      (el menor entre la parada de la detención y los microcortes), fin real, cantidad de microcortes,
--      segundos de microcortes y segundos totales (MinutosEfectivos x 60 + microcortes).
--   IMPORTANTE: agrupar NO cambia mnt.Detenciones.MinutosEfectivos (sigue siendo la única verdad del
--   tiempo de la detención; insert-only). El tiempo agrupado es sólo para reportes, vía la vista.
--   La API calcula lo mismo para la pantalla (AgrupacionMicrocortesReglas.Resumir).
-- Versión: 1.0
-- Requiere: 17 (Detenciones.Eliminada), 25 (ParadasCamara), 26 (FechaHoraArranqueDetectada).
--   Conviene correr 31 antes (marca de umbral), pero esta tabla no depende de él.
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga datos.
-- Correr antes de publicar la API (la API lee y escribe la tabla).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- Los índices únicos filtrados exigen estas opciones; sqlcmd arranca con QUOTED_IDENTIFIER OFF.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ============================================================================
-- 1. Tabla mnt.DetencionAgrupacion
-- ============================================================================
IF OBJECT_ID(N'[mnt].[DetencionAgrupacion]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[DetencionAgrupacion] (
        [DetencionAgrupacionId] BIGINT IDENTITY(1,1) NOT NULL,
        [DetencionId] BIGINT NOT NULL,
        [ParadaCamaraId] BIGINT NOT NULL,
        [Origen] NVARCHAR(20) NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_DetencionAgrupacion_FechaHoraCreacion] DEFAULT SYSDATETIMEOFFSET(),
        [Activo] BIT NOT NULL CONSTRAINT [DF_DetencionAgrupacion_Activo] DEFAULT 1,
        [DesactivadoPorUsuarioId] NVARCHAR(450) NULL,
        [FechaHoraDesactivacion] DATETIMEOFFSET NULL,
        CONSTRAINT [PK_DetencionAgrupacion] PRIMARY KEY ([DetencionAgrupacionId]),
        CONSTRAINT [CK_DetencionAgrupacion_Origen] CHECK ([Origen] IN ('OPERADOR', 'AUTOMATICO')),
        -- Activa: sin datos de desactivación. Desactivada: con quién y cuándo.
        CONSTRAINT [CK_DetencionAgrupacion_Desactivacion] CHECK (
            ([Activo] = 1 AND [DesactivadoPorUsuarioId] IS NULL AND [FechaHoraDesactivacion] IS NULL)
            OR ([Activo] = 0 AND [DesactivadoPorUsuarioId] IS NOT NULL AND [FechaHoraDesactivacion] IS NOT NULL)),
        CONSTRAINT [FK_DetencionAgrupacion_Detenciones_DetencionId] FOREIGN KEY ([DetencionId])
            REFERENCES [mnt].[Detenciones] ([DetencionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DetencionAgrupacion_ParadasCamara_ParadaCamaraId] FOREIGN KEY ([ParadaCamaraId])
            REFERENCES [mnt].[ParadasCamara] ([ParadaCamaraId]) ON DELETE NO ACTION
    );
    PRINT 'Tabla mnt.DetencionAgrupacion creada.'
END
ELSE
    PRINT 'Tabla mnt.DetencionAgrupacion ya existía.'
GO

-- Una parada (hijo) sólo puede estar activa en UN grupo. Las desagrupadas no cuentan: se puede volver
-- a agrupar (queda otra fila activa y la vieja desactivada como historial).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'UX_DetencionAgrupacion_ParadaActiva')
BEGIN
    CREATE UNIQUE INDEX [UX_DetencionAgrupacion_ParadaActiva]
        ON [mnt].[DetencionAgrupacion] ([ParadaCamaraId])
        WHERE [Activo] = 1;
    PRINT 'Índice UX_DetencionAgrupacion_ParadaActiva creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_DetencionAgrupacion_DetencionId_Activo')
BEGIN
    CREATE INDEX [IX_DetencionAgrupacion_DetencionId_Activo]
        ON [mnt].[DetencionAgrupacion] ([DetencionId], [Activo])
        INCLUDE ([ParadaCamaraId]);
    PRINT 'Índice IX_DetencionAgrupacion_DetencionId_Activo creado.'
END
GO

-- ============================================================================
-- 2. Vista mnt.vw_DetencionesAgrupadas (sólo reportes; excluye detenciones eliminadas)
--    Fin de la detención: la hora de arranque declarada o, si no hay, la que vio la cámara.
--    SegundosTotales no suma los ratos en marcha entre microcortes; SegundosVentana sí (de inicio
--    real a fin real). Sólo aparecen detenciones con microcortes activos: para reportes, LEFT JOIN a esta vista.
-- ============================================================================
CREATE OR ALTER VIEW [mnt].[vw_DetencionesAgrupadas]
AS
WITH [Hijos] AS (
    SELECT
        a.[DetencionId],
        MIN(p.[FechaHoraInicio]) AS [InicioMicrocortes],
        MAX(p.[FechaHoraFin]) AS [FinMicrocortes],
        COUNT(*) AS [CantidadMicrocortes],
        SUM(p.[DuracionSegundos]) AS [SegundosMicrocortes]
    FROM [mnt].[DetencionAgrupacion] a
    INNER JOIN [mnt].[ParadasCamara] p ON p.[ParadaCamaraId] = a.[ParadaCamaraId]
    WHERE a.[Activo] = 1
    GROUP BY a.[DetencionId]
)
SELECT
    d.[DetencionId],
    d.[Codigo],
    d.[LineaId],
    d.[TurnoId],
    d.[Fecha],
    d.[PuntoControlCalidadId],
    d.[FechaHoraParada],
    f.[FinDetencion] AS [FechaHoraArranque],
    d.[MinutosEfectivos],
    r.[FechaHoraInicioReal],
    r.[FechaHoraFinReal],
    h.[CantidadMicrocortes],
    h.[SegundosMicrocortes],
    d.[MinutosEfectivos] * 60 AS [SegundosDetencion],
    d.[MinutosEfectivos] * 60 + h.[SegundosMicrocortes] AS [SegundosTotales],
    DATEDIFF(SECOND, r.[FechaHoraInicioReal], r.[FechaHoraFinReal]) AS [SegundosVentana]
FROM [mnt].[Detenciones] d
INNER JOIN [Hijos] h ON h.[DetencionId] = d.[DetencionId]
CROSS APPLY (SELECT COALESCE(d.[FechaHoraArranque], d.[FechaHoraArranqueDetectada]) AS [FinDetencion]) f
CROSS APPLY (SELECT
    CASE WHEN d.[FechaHoraParada] IS NULL OR h.[InicioMicrocortes] < d.[FechaHoraParada]
         THEN h.[InicioMicrocortes] ELSE d.[FechaHoraParada] END AS [FechaHoraInicioReal],
    CASE WHEN f.[FinDetencion] IS NULL OR h.[FinMicrocortes] > f.[FinDetencion]
         THEN h.[FinMicrocortes] ELSE f.[FinDetencion] END AS [FechaHoraFinReal]) r
WHERE d.[Eliminada] = 0;
GO
PRINT 'Vista mnt.vw_DetencionesAgrupadas creada o actualizada.'
GO

PRINT 'Script 32_agrupador_detenciones.sql completado.'
GO
