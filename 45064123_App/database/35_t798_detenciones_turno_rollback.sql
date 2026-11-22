-- ==============================================================================
-- Script: 35_t798_detenciones_turno_rollback.sql
-- Descripción: Deshace 35_t798_detenciones_turno.sql (T-798 etapa 3).
--   1. DET_Detenciones: vuelve a agregar TurnoId, Fecha y UsuarioLineaId y los completa:
--      - las detenciones que había al correr el 35, con sus valores originales (mnt.DET_DetencionesRespaldoT798);
--      - las cargadas después, desde su turno de producción: TurnoId del turno; Fecha = día de la hora de parada
--        (hora de planta) o, sin hora, la fecha de producción; UsuarioLineaId = la habilitación del operador en la
--        línea (ABM_OperadoresLinea), si la tiene.
--      TurnoId y Fecha quedan NOT NULL, con FK_Detenciones_Turnos, FK_Detenciones_UsuariosLineas,
--      IX_Detenciones_Fecha, IX_Detenciones_TurnoId e IX_Detenciones_UsuarioLineaId. Los índices que el 35 re-creó
--      sin la columna Fecha incluida (INCLUDE) quedan así (no cambia ningún resultado).
--   2. Vistas VW_* de detenciones: vuelven a la versión del 31 (TurnoId y Fecha de la detención).
--   3. Elimina DET_Detenciones.TurnoProduccionId y OperadorUsuarioId (con su FK e índices) y
--      CPR_Registros.TurnoProduccionId / CAL_Registros.TurnoProduccionId.
--   4. Elimina los turnos de producción que crearon las conversiones del 35 y del 36 (ObservacionCierre
--      "Convertido (T-798 etapa 3)..." / "Convertido (T-798 etapa 4)...") si no tienen operador ni producto, y el
--      respaldo. (Los que creó el 38 los elimina su propio rollback.)
-- Requiere: 38_t798_conversion_turnos_rollback.sql y 36_t798_camaras_rollback.sql corridos antes (orden inverso).
--   Antes de correrlo, volver a una API que no use estas columnas (anterior a T-798 etapa 3).
-- Idempotente: se puede ejecutar más de una vez.
-- Correr con la API detenida.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Orden inverso: antes van 38_t798_conversion_turnos_rollback.sql y 36_t798_camaras_rollback.sql (los conteos y paradas
-- de cámara y los registros convertidos por el 38 apuntan a los turnos de producción que este script elimina).
IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NOT NULL
   OR OBJECT_ID(N'mnt.CAM_Camaras', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'Correr antes 38_t798_conversion_turnos_rollback.sql y 36_t798_camaras_rollback.sql (orden inverso). No se cambió nada.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 1. Columnas viejas: agregar y completar. Todo o nada.
-- ============================================================================
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoProduccionId') IS NOT NULL
   AND (COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoId') IS NULL
        OR COL_LENGTH(N'mnt.DET_Detenciones', N'Fecha') IS NULL
        OR COL_LENGTH(N'mnt.DET_Detenciones', N'UsuarioLineaId') IS NULL)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoId') IS NULL
        ALTER TABLE [mnt].[DET_Detenciones] ADD [TurnoId] INT NULL;
    IF COL_LENGTH(N'mnt.DET_Detenciones', N'Fecha') IS NULL
        ALTER TABLE [mnt].[DET_Detenciones] ADD [Fecha] DATE NULL;
    IF COL_LENGTH(N'mnt.DET_Detenciones', N'UsuarioLineaId') IS NULL
        ALTER TABLE [mnt].[DET_Detenciones] ADD [UsuarioLineaId] INT NULL;

    -- SQL dinámico: las columnas recién agregadas no existen cuando se compila el lote.
    DECLARE @Respaldo NVARCHAR(MAX) = CASE WHEN OBJECT_ID(N'mnt.DET_DetencionesRespaldoT798', N'U') IS NULL THEN N'' ELSE N'
        UPDATE d SET d.[TurnoId] = r.[TurnoId], d.[Fecha] = r.[Fecha], d.[UsuarioLineaId] = r.[UsuarioLineaId]
        FROM [mnt].[DET_Detenciones] d
        INNER JOIN [mnt].[DET_DetencionesRespaldoT798] r ON r.[DetencionId] = d.[DetencionId]
        WHERE d.[TurnoId] IS NULL;
        PRINT CONCAT(''Detenciones restauradas desde el respaldo: '', @@ROWCOUNT);' END;
    DECLARE @Sql NVARCHAR(MAX) = @Respaldo + N'
        UPDATE d SET
            d.[TurnoId] = tp.[TurnoId],
            d.[Fecha] = CASE WHEN d.[FechaHoraParada] IS NULL THEN tp.[FechaProduccion]
                             ELSE CAST(SWITCHOFFSET(d.[FechaHoraParada], ''-03:00'') AS DATE) END,
            d.[UsuarioLineaId] = (SELECT TOP (1) ol.[UsuarioLineaId] FROM [mnt].[ABM_OperadoresLinea] ol
                                  WHERE ol.[UsuarioId] = d.[OperadorUsuarioId] AND ol.[LineaId] = d.[LineaId]
                                  ORDER BY ol.[UsuarioLineaId])
        FROM [mnt].[DET_Detenciones] d
        INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
        WHERE d.[TurnoId] IS NULL;
        PRINT CONCAT(''Detenciones completadas desde su turno de producción: '', @@ROWCOUNT);';
    EXEC sys.sp_executesql @Sql;

    COMMIT TRANSACTION;
END
ELSE
    PRINT 'Columnas viejas: sin cambios.'
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'TurnoId' AND is_nullable = 1)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'TurnoId')
       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'Fecha')
    BEGIN
        DECLARE @Nulos INT;
        EXEC sys.sp_executesql N'SELECT @n = COUNT(*) FROM [mnt].[DET_Detenciones] WHERE [TurnoId] IS NULL OR [Fecha] IS NULL;',
            N'@n INT OUTPUT', @n = @Nulos OUTPUT;
        IF @Nulos > 0
        BEGIN
            RAISERROR(N'Hay detenciones sin TurnoId o Fecha: revisar antes de seguir.', 16, 1);
            SET NOEXEC ON;
        END
    END
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'TurnoId' AND is_nullable = 1)
    ALTER TABLE [mnt].[DET_Detenciones] ALTER COLUMN [TurnoId] INT NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'Fecha' AND is_nullable = 1)
    ALTER TABLE [mnt].[DET_Detenciones] ALTER COLUMN [Fecha] DATE NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Turnos')
    ALTER TABLE [mnt].[DET_Detenciones] ADD CONSTRAINT [FK_Detenciones_Turnos] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[ABM_Turnos] ([TurnoId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Detenciones_UsuariosLineas')
    ALTER TABLE [mnt].[DET_Detenciones] ADD CONSTRAINT [FK_Detenciones_UsuariosLineas] FOREIGN KEY ([UsuarioLineaId]) REFERENCES [mnt].[ABM_OperadoresLinea] ([UsuarioLineaId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_Detenciones_Fecha')
    CREATE INDEX [IX_Detenciones_Fecha] ON [mnt].[DET_Detenciones] ([Fecha] DESC) INCLUDE ([Codigo], [EstadoActual], [LineaId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_Detenciones_TurnoId')
    CREATE INDEX [IX_Detenciones_TurnoId] ON [mnt].[DET_Detenciones] ([TurnoId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_Detenciones_UsuarioLineaId')
    CREATE INDEX [IX_Detenciones_UsuarioLineaId] ON [mnt].[DET_Detenciones] ([UsuarioLineaId]);
GO

-- ============================================================================
-- 2. Vistas: versión del 31 (TurnoId y Fecha de la detención).
-- ============================================================================
IF OBJECT_ID(N'mnt.VW_DetencionesDetalle', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesDetalle]
AS
SELECT
    d.[DetencionId],
    d.[Codigo],
    d.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[EquipoId],
    e.[Nombre] AS [EquipoNombre],
    d.[TipoFallaId],
    tf.[Nombre] AS [TipoFallaNombre],
    d.[DetalleFallaId],
    df.[Nombre] AS [DetalleFallaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    d.[MinutosDeclarados],
    d.[FechaHoraParada],
    d.[FechaHoraArranque],
    d.[OrigenTiempo],
    d.[MinutosEfectivos],
    d.[Descripcion],
    d.[EstadoActual],
    d.[AccionesRealizadas],
    d.[ResolucionDescripcion],
    d.[FechaHoraCierre],
    d.[CerradoPorUsuarioId],
    uc.[NombreCompleto] AS [CerradoPorNombre],
    d.[FechaHoraCreacion],
    d.[CreadoPorUsuarioId],
    ucr.[NombreCompleto] AS [CreadoPorNombre],
    d.[FechaHoraModificacion],
    d.[ModificadoPorUsuarioId],
    umo.[NombreCompleto] AS [ModificadoPorNombre],
    CASE WHEN d.[EstadoActual] != ''CERRADA'' THEN 1 ELSE 0 END AS [EstaAbierta],
    (SELECT COUNT(*) FROM [mnt].[DET_Notas] dn WHERE dn.[DetencionId] = d.[DetencionId]) AS [CantidadNotas]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
LEFT JOIN [mnt].[ABM_DetallesFalla] df ON d.[DetalleFallaId] = df.[DetalleFallaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] uc ON d.[CerradoPorUsuarioId] = uc.[Id]
LEFT JOIN [dbo].[AspNetUsers] ucr ON d.[CreadoPorUsuarioId] = ucr.[Id]
LEFT JOIN [dbo].[AspNetUsers] umo ON d.[ModificadoPorUsuarioId] = umo.[Id]
WHERE d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesDetalle restaurada.'
END
GO

IF OBJECT_ID(N'mnt.VW_DetencionesAbiertas', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesAbiertas]
AS
SELECT
    d.[DetencionId],
    d.[Codigo],
    d.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[EquipoId],
    e.[Nombre] AS [EquipoNombre],
    d.[TipoFallaId],
    tf.[Nombre] AS [TipoFallaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    d.[MinutosEfectivos],
    d.[Descripcion],
    d.[EstadoActual],
    d.[CreadoPorUsuarioId],
    u.[NombreCompleto] AS [CreadoPorNombre],
    d.[FechaHoraCreacion]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] u ON d.[CreadoPorUsuarioId] = u.[Id]
WHERE d.[EstadoActual] != ''CERRADA''
  AND d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesAbiertas restaurada.'
END
GO

IF OBJECT_ID(N'mnt.VW_DetencionesIndicadores', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesIndicadores]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[Fecha],
    t.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    COUNT(*) AS [TotalDetenciones],
    SUM(CASE WHEN d.[EstadoActual] = ''REGISTRADA'' THEN 1 ELSE 0 END) AS [DetencionesRegistradas],
    SUM(CASE WHEN d.[EstadoActual] = ''EN_SEGUIMIENTO'' THEN 1 ELSE 0 END) AS [DetencionesEnSeguimiento],
    SUM(CASE WHEN d.[EstadoActual] = ''CERRADA'' THEN 1 ELSE 0 END) AS [DetencionesCerradas],
    AVG(CAST(d.[MinutosEfectivos] AS FLOAT)) AS [DuracionPromedioMinutos],
    SUM(d.[MinutosEfectivos]) AS [TiempoTotalParadaMinutos]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[Fecha], t.[TurnoId], t.[Nombre]';
    PRINT 'Vista VW_DetencionesIndicadores restaurada.'
END
GO

IF OBJECT_ID(N'mnt.VW_ResumenTurnoActual', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_ResumenTurnoActual]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    COUNT(*) AS [TotalDetenciones],
    SUM(d.[MinutosEfectivos]) AS [MinutosTotales],
    SUM(CASE WHEN d.[EstadoActual] != ''CERRADA'' THEN 1 ELSE 0 END) AS [DetencionesAbiertas]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Fecha] = CAST(GETDATE() AS DATE)
  AND d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[TurnoId], t.[Nombre], d.[Fecha]';
    PRINT 'Vista VW_ResumenTurnoActual restaurada.'
END
GO

-- ============================================================================
-- 3. Columnas nuevas: eliminar (con FK e índices).
-- ============================================================================
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DET_Detenciones_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[DET_Detenciones] DROP CONSTRAINT [FK_DET_Detenciones_PRD_TurnosProduccion_TurnoProduccionId];
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_TurnoProduccionId')
    DROP INDEX [IX_DET_Detenciones_TurnoProduccionId] ON [mnt].[DET_Detenciones];
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_TurnoProduccionId_EstadoActual')
    DROP INDEX [IX_DET_Detenciones_TurnoProduccionId_EstadoActual] ON [mnt].[DET_Detenciones];
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_OperadorUsuarioId')
    DROP INDEX [IX_DET_Detenciones_OperadorUsuarioId] ON [mnt].[DET_Detenciones];
GO
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoProduccionId') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] DROP COLUMN [TurnoProduccionId];
    PRINT 'Columna DET_Detenciones.TurnoProduccionId eliminada.'
END
GO
IF COL_LENGTH(N'mnt.DET_Detenciones', N'OperadorUsuarioId') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] DROP COLUMN [OperadorUsuarioId];
    PRINT 'Columna DET_Detenciones.OperadorUsuarioId eliminada.'
END
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CPR_Registros_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[CPR_Registros] DROP CONSTRAINT [FK_CPR_Registros_PRD_TurnosProduccion_TurnoProduccionId];
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CPR_Registros') AND name = N'IX_CPR_Registros_TurnoProduccionId')
    DROP INDEX [IX_CPR_Registros_TurnoProduccionId] ON [mnt].[CPR_Registros];
GO
IF COL_LENGTH(N'mnt.CPR_Registros', N'TurnoProduccionId') IS NOT NULL
    ALTER TABLE [mnt].[CPR_Registros] DROP COLUMN [TurnoProduccionId];
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAL_Registros_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[CAL_Registros] DROP CONSTRAINT [FK_CAL_Registros_PRD_TurnosProduccion_TurnoProduccionId];
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAL_Registros') AND name = N'IX_CAL_Registros_TurnoProduccionId')
    DROP INDEX [IX_CAL_Registros_TurnoProduccionId] ON [mnt].[CAL_Registros];
GO
IF COL_LENGTH(N'mnt.CAL_Registros', N'TurnoProduccionId') IS NOT NULL
    ALTER TABLE [mnt].[CAL_Registros] DROP COLUMN [TurnoProduccionId];
GO

-- ============================================================================
-- 4. Turnos que crearon las conversiones del 35 y del 36 (sin operador ni producto) y respaldo.
-- ============================================================================
IF OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NOT NULL
BEGIN
    DELETE tp
    FROM [mnt].[PRD_TurnosProduccion] tp
    WHERE tp.[OrigenApertura] = N'SISTEMA'
      AND tp.[ObservacionCierre] LIKE N'Convertido (T-798 etapa %'
      AND NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionOperadores] o WHERE o.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionProductos] p WHERE p.[TurnoProduccionId] = tp.[TurnoProduccionId]);
    PRINT CONCAT('Turnos de producción de la conversión eliminados: ', @@ROWCOUNT);
END
GO

IF OBJECT_ID(N'mnt.DET_DetencionesRespaldoT798', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[DET_DetencionesRespaldoT798];
    PRINT 'Respaldo mnt.DET_DetencionesRespaldoT798 eliminado.'
END
GO

SET NOEXEC OFF;
GO
PRINT 'Script 35_t798_detenciones_turno_rollback.sql terminado.'
GO
