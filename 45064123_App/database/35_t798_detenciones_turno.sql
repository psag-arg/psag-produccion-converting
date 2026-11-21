-- ==============================================================================
-- Script: 35_t798_detenciones_turno.sql
-- Descripción: T-798 etapa 3 · Detenciones colgadas del turno de producción
--              (docs/diseno/T-798_modelo_produccion.md, 2.7 y 2.11).
--   1. DET_Detenciones: agrega TurnoProduccionId (BIGINT) y OperadorUsuarioId (NVARCHAR(450) NULL).
--   2. Respaldo: guarda TurnoId, Fecha y UsuarioLineaId de las detenciones existentes en
--      mnt.DET_DetencionesRespaldoT798 (lo usa el rollback para dejarlas como estaban).
--   3. Conversión (opción B, 2.11) de las detenciones existentes (en producción hay detenciones reales de prueba):
--      por cada (LineaId, TurnoId, fecha de producción) que falte crea un PRD_TurnosProduccion CERRADO, con
--      OrigenApertura SISTEMA, sin operador ni producto ("a completar") y MinutosProgramados = 0 (se recalculan
--      con el plan en la etapa 7; quedan marcados en ObservacionCierre). Fecha de producción = día de inicio del
--      turno según FechaHoraParada (hora de planta, UTC-3: el T3 después de medianoche es del día anterior) si la
--      hay; si no, la Fecha de la detención. El TurnoId es el materializado en la detención (no se recalcula).
--      Cuelga cada detención de su turno y completa OperadorUsuarioId con el usuario de UsuarioLineaId
--      (ABM_OperadoresLinea.UsuarioId).
--   4. TurnoProduccionId pasa a NOT NULL, con FK e índices.
--   5. Vistas VW_* de detenciones: se re-crean con la fecha de producción y el turno del turno de producción.
--   6. Recién ahí elimina TurnoId, Fecha y UsuarioLineaId de DET_Detenciones con sus FK, CHECK, DEFAULT,
--      estadísticas e índices. Los índices que sólo INCLUÍAN alguna de esas columnas (por ejemplo
--      IX_Detenciones_EstadoActual INCLUDE ... [Fecha]) se re-crean con el mismo nombre sin esa columna.
--   7. CPR_Registros y CAL_Registros: TurnoProduccionId BIGINT NULL con FK e índice (NULL en los registros
--      anteriores; la API lo completa al crear). PENDIENTE: sus TurnoId y Fecha se mantienen porque las pantallas
--      de Control de Proceso y Calidad los usan; salen en una etapa siguiente.
-- Versión: 1.0
-- Requiere: 31, 32 y 34.
-- Idempotente: se puede ejecutar más de una vez (hace solo lo que falta). Cada paso que toca datos va en
--   transacción: si falla, no queda a medias.
-- Correr con la API detenida, antes de publicar la API de T-798 etapa 3.
-- Deshacer: 35_t798_detenciones_turno_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.DET_Detenciones', N'U') IS NULL
   OR OBJECT_ID(N'mnt.ABM_Turnos', N'U') IS NULL
   OR OBJECT_ID(N'mnt.ABM_OperadoresLinea', N'U') IS NULL
   OR OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NULL
BEGIN
    RAISERROR(N'Falta correr 31_t798_renombre_por_modulo.sql y 34_t798_turnos_produccion.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 1. Columnas nuevas (NULL hasta convertir).
-- ============================================================================
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoProduccionId') IS NULL
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] ADD [TurnoProduccionId] BIGINT NULL;
    PRINT 'Columna DET_Detenciones.TurnoProduccionId agregada (NULL hasta convertir).'
END
GO

IF COL_LENGTH(N'mnt.DET_Detenciones', N'OperadorUsuarioId') IS NULL
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] ADD [OperadorUsuarioId] NVARCHAR(450) NULL;
    PRINT 'Columna DET_Detenciones.OperadorUsuarioId agregada.'
END
GO

-- ============================================================================
-- 2. Respaldo de las columnas que se eliminan (para el rollback).
-- ============================================================================
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoId') IS NOT NULL
   AND OBJECT_ID(N'mnt.DET_DetencionesRespaldoT798', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[DET_DetencionesRespaldoT798] (
        [DetencionId] BIGINT NOT NULL,
        [TurnoId] INT NULL,
        [Fecha] DATE NULL,
        [UsuarioLineaId] INT NULL,
        [FechaHoraRespaldo] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_DET_DetencionesRespaldoT798_Fecha] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_DET_DetencionesRespaldoT798] PRIMARY KEY ([DetencionId])
    );
    -- Las columnas viejas se leen con SQL dinámico: en una re-ejecución ya no existen y el lote no compilaría.
    DECLARE @Ul NVARCHAR(100) = CASE WHEN COL_LENGTH(N'mnt.DET_Detenciones', N'UsuarioLineaId') IS NULL
                                     THEN N'CAST(NULL AS INT)' ELSE N'd.[UsuarioLineaId]' END;
    DECLARE @Sql NVARCHAR(MAX) = N'
        INSERT INTO [mnt].[DET_DetencionesRespaldoT798] ([DetencionId], [TurnoId], [Fecha], [UsuarioLineaId])
        SELECT d.[DetencionId], d.[TurnoId], d.[Fecha], ' + @Ul + N'
        FROM [mnt].[DET_Detenciones] d;';
    EXEC sys.sp_executesql @Sql;
    PRINT CONCAT('Respaldo mnt.DET_DetencionesRespaldoT798 creado: ', @@ROWCOUNT, ' detenciones.');
END
ELSE
    PRINT 'Respaldo: sin cambios (ya existía o las columnas viejas ya no están).'
GO

-- ============================================================================
-- 3. Conversión: turnos de producción que faltan + TurnoProduccionId + OperadorUsuarioId. Todo o nada.
-- ============================================================================
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoId') IS NOT NULL
   AND COL_LENGTH(N'mnt.DET_Detenciones', N'Fecha') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Sql NVARCHAR(MAX) = N'
        -- Fecha de producción de cada detención sin turno de producción.
        SELECT d.[DetencionId], d.[LineaId], d.[TurnoId],
               CASE
                   WHEN d.[FechaHoraParada] IS NULL THEN d.[Fecha]
                   WHEN t.[HoraFin] <= t.[HoraInicio]
                        AND CAST(SWITCHOFFSET(d.[FechaHoraParada], ''-03:00'') AS TIME) < t.[HoraInicio]
                       THEN DATEADD(DAY, -1, CAST(SWITCHOFFSET(d.[FechaHoraParada], ''-03:00'') AS DATE))
                   ELSE CAST(SWITCHOFFSET(d.[FechaHoraParada], ''-03:00'') AS DATE)
               END AS [FechaProduccion]
        INTO #Conversion
        FROM [mnt].[DET_Detenciones] d
        INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = d.[TurnoId]
        WHERE d.[TurnoProduccionId] IS NULL;

        -- Turnos de producción que faltan (CERRADO por SISTEMA, sin operador ni producto).
        INSERT INTO [mnt].[PRD_TurnosProduccion]
            ([LineaId], [TurnoId], [FechaProduccion], [InicioProgramado], [FinProgramado], [MinutosProgramados],
             [InicioReal], [FinReal], [Estado], [OrigenApertura], [ObservacionCierre], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT k.[LineaId], k.[TurnoId], k.[FechaProduccion], v.[Inicio], v.[Fin], 0, v.[Inicio], v.[Fin],
               N''CERRADO'', N''SISTEMA'',
               N''Convertido (T-798 etapa 3) desde detenciones existentes: minutos programados a recalcular en la etapa 7.'',
               N''SISTEMA'', N''SISTEMA''
        FROM (SELECT DISTINCT [LineaId], [TurnoId], [FechaProduccion] FROM #Conversion) k
        INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = k.[TurnoId]
        CROSS APPLY (SELECT
            TODATETIMEOFFSET(DATEADD(MINUTE, DATEDIFF(MINUTE, CAST(''00:00'' AS TIME), t.[HoraInicio]), CAST(k.[FechaProduccion] AS DATETIME2(0))), ''-03:00'') AS [Inicio],
            TODATETIMEOFFSET(DATEADD(MINUTE, DATEDIFF(MINUTE, CAST(''00:00'' AS TIME), t.[HoraFin]),
                CAST(CASE WHEN t.[HoraFin] <= t.[HoraInicio] THEN DATEADD(DAY, 1, k.[FechaProduccion]) ELSE k.[FechaProduccion] END AS DATETIME2(0))), ''-03:00'') AS [Fin]) v
        WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccion] tp
                          WHERE tp.[LineaId] = k.[LineaId] AND tp.[TurnoId] = k.[TurnoId] AND tp.[FechaProduccion] = k.[FechaProduccion]);
        PRINT CONCAT(''Turnos de producción creados por la conversión: '', @@ROWCOUNT);

        UPDATE d SET d.[TurnoProduccionId] = tp.[TurnoProduccionId]
        FROM [mnt].[DET_Detenciones] d
        INNER JOIN #Conversion c ON c.[DetencionId] = d.[DetencionId]
        INNER JOIN [mnt].[PRD_TurnosProduccion] tp
            ON tp.[LineaId] = c.[LineaId] AND tp.[TurnoId] = c.[TurnoId] AND tp.[FechaProduccion] = c.[FechaProduccion];
        PRINT CONCAT(''Detenciones colgadas de su turno de producción: '', @@ROWCOUNT);

        DROP TABLE #Conversion;';
    EXEC sys.sp_executesql @Sql;

    IF COL_LENGTH(N'mnt.DET_Detenciones', N'UsuarioLineaId') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE d SET d.[OperadorUsuarioId] = ol.[UsuarioId]
            FROM [mnt].[DET_Detenciones] d
            INNER JOIN [mnt].[ABM_OperadoresLinea] ol ON ol.[UsuarioLineaId] = d.[UsuarioLineaId]
            WHERE d.[OperadorUsuarioId] IS NULL;
            PRINT CONCAT(''Operador completado desde la habilitación (UsuarioLineaId): '', @@ROWCOUNT);';
    END

    COMMIT TRANSACTION;
END
ELSE
    PRINT 'Conversión: sin cambios (las columnas viejas ya no están).'
GO

-- ============================================================================
-- 4. TurnoProduccionId NOT NULL + FK + índices.
-- ============================================================================
IF EXISTS (SELECT 1 FROM [mnt].[DET_Detenciones] WHERE [TurnoProduccionId] IS NULL)
BEGIN
    RAISERROR(N'Hay detenciones sin turno de producción (¿TurnoId que no existe en ABM_Turnos?). Revisar antes de seguir: no se eliminó nada.', 16, 1);
    SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'TurnoProduccionId' AND is_nullable = 1)
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] ALTER COLUMN [TurnoProduccionId] BIGINT NOT NULL;
    PRINT 'DET_Detenciones.TurnoProduccionId pasa a NOT NULL.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DET_Detenciones_PRD_TurnosProduccion_TurnoProduccionId')
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] ADD CONSTRAINT [FK_DET_Detenciones_PRD_TurnosProduccion_TurnoProduccionId]
        FOREIGN KEY ([TurnoProduccionId]) REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]);
    PRINT 'FK FK_DET_Detenciones_PRD_TurnosProduccion_TurnoProduccionId creada.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_TurnoProduccionId')
    CREATE INDEX [IX_DET_Detenciones_TurnoProduccionId] ON [mnt].[DET_Detenciones] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_TurnoProduccionId_EstadoActual')
    CREATE INDEX [IX_DET_Detenciones_TurnoProduccionId_EstadoActual] ON [mnt].[DET_Detenciones] ([TurnoProduccionId], [EstadoActual]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_OperadorUsuarioId')
    CREATE INDEX [IX_DET_Detenciones_OperadorUsuarioId] ON [mnt].[DET_Detenciones] ([OperadorUsuarioId]);
GO

-- ============================================================================
-- 5. Vistas de reportes (06, 17, 31): la fecha y el turno salen del turno de producción. Solo las que existen.
--    La aplicación no las usa. Mismas columnas que antes (Fecha = fecha de producción) + TurnoProduccionId.
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
    tp.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    tp.[FechaProduccion] AS [Fecha],
    d.[TurnoProduccionId],
    d.[OperadorUsuarioId],
    uop.[NombreCompleto] AS [OperadorNombre],
    d.[MinutosDeclarados],
    d.[FechaHoraParada],
    d.[FechaHoraArranque],
    d.[OrigenTiempo],
    d.[MinutosEfectivos],
    CAST(CASE WHEN tf.[TipoFallaId] IS NULL OR tf.[CuentaParaIndicadores] = 1 THEN 1 ELSE 0 END AS BIT) AS [CuentaParaIndicadores],
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
INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = tp.[TurnoId]
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
LEFT JOIN [mnt].[ABM_DetallesFalla] df ON d.[DetalleFallaId] = df.[DetalleFallaId]
LEFT JOIN [dbo].[AspNetUsers] uop ON d.[OperadorUsuarioId] = uop.[Id]
LEFT JOIN [dbo].[AspNetUsers] uc ON d.[CerradoPorUsuarioId] = uc.[Id]
LEFT JOIN [dbo].[AspNetUsers] ucr ON d.[CreadoPorUsuarioId] = ucr.[Id]
LEFT JOIN [dbo].[AspNetUsers] umo ON d.[ModificadoPorUsuarioId] = umo.[Id]
WHERE d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesDetalle re-creada con el turno de producción.'
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
    tp.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    tp.[FechaProduccion] AS [Fecha],
    d.[TurnoProduccionId],
    d.[MinutosEfectivos],
    d.[Descripcion],
    d.[EstadoActual],
    d.[CreadoPorUsuarioId],
    u.[NombreCompleto] AS [CreadoPorNombre],
    d.[FechaHoraCreacion]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = tp.[TurnoId]
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
LEFT JOIN [dbo].[AspNetUsers] u ON d.[CreadoPorUsuarioId] = u.[Id]
WHERE d.[EstadoActual] != ''CERRADA''
  AND d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesAbiertas re-creada con el turno de producción.'
END
GO

-- Indicadores: el tiempo de parada que cuenta (CuentaParaIndicadores) va aparte del total.
IF OBJECT_ID(N'mnt.VW_DetencionesIndicadores', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesIndicadores]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    tp.[FechaProduccion] AS [Fecha],
    t.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    COUNT(*) AS [TotalDetenciones],
    SUM(CASE WHEN d.[EstadoActual] = ''REGISTRADA'' THEN 1 ELSE 0 END) AS [DetencionesRegistradas],
    SUM(CASE WHEN d.[EstadoActual] = ''EN_SEGUIMIENTO'' THEN 1 ELSE 0 END) AS [DetencionesEnSeguimiento],
    SUM(CASE WHEN d.[EstadoActual] = ''CERRADA'' THEN 1 ELSE 0 END) AS [DetencionesCerradas],
    AVG(CAST(d.[MinutosEfectivos] AS FLOAT)) AS [DuracionPromedioMinutos],
    SUM(d.[MinutosEfectivos]) AS [TiempoTotalParadaMinutos],
    SUM(CASE WHEN tf.[TipoFallaId] IS NULL OR tf.[CuentaParaIndicadores] = 1 THEN d.[MinutosEfectivos] ELSE 0 END) AS [TiempoParadaQueCuentaMinutos]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = tp.[TurnoId]
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
WHERE d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], tp.[FechaProduccion], t.[TurnoId], t.[Nombre]';
    PRINT 'Vista VW_DetencionesIndicadores re-creada con el turno de producción.'
END
GO

-- Resumen del turno: el turno de producción ABIERTO de cada línea (el T3 después de medianoche incluye lo de antes).
IF OBJECT_ID(N'mnt.VW_ResumenTurnoActual', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_ResumenTurnoActual]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    tp.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    tp.[FechaProduccion] AS [Fecha],
    tp.[TurnoProduccionId],
    COUNT(*) AS [TotalDetenciones],
    SUM(CASE WHEN tf.[TipoFallaId] IS NULL OR tf.[CuentaParaIndicadores] = 1 THEN d.[MinutosEfectivos] ELSE 0 END) AS [MinutosTotales],
    SUM(CASE WHEN d.[EstadoActual] != ''CERRADA'' THEN 1 ELSE 0 END) AS [DetencionesAbiertas]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = tp.[TurnoId]
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
WHERE tp.[Estado] = N''ABIERTO''
  AND d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], tp.[TurnoId], t.[Nombre], tp.[FechaProduccion], tp.[TurnoProduccionId]';
    PRINT 'Vista VW_ResumenTurnoActual re-creada: turno de producción abierto.'
END
GO

-- Otras vistas / procedimientos que todavía nombren las columnas viejas: se avisa (no se tocan).
SELECT OBJECT_SCHEMA_NAME(m.[object_id]) + N'.' + OBJECT_NAME(m.[object_id]) AS [ObjetoARevisar]
FROM sys.sql_modules m
WHERE m.[definition] LIKE N'%DET[_]Detenciones%'
  AND (m.[definition] LIKE N'%d.[[]TurnoId]%' OR m.[definition] LIKE N'%d.[[]Fecha]%' OR m.[definition] LIKE N'%UsuarioLineaId%')
  AND OBJECT_NAME(m.[object_id]) NOT IN (N'VW_DetencionesDetalle', N'VW_DetencionesAbiertas', N'VW_DetencionesIndicadores', N'VW_ResumenTurnoActual');
GO

-- ============================================================================
-- 6. Eliminar TurnoId, Fecha y UsuarioLineaId de DET_Detenciones (con lo que dependa de ellas). Todo o nada.
-- ============================================================================
IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoId') IS NOT NULL
   OR COL_LENGTH(N'mnt.DET_Detenciones', N'Fecha') IS NOT NULL
   OR COL_LENGTH(N'mnt.DET_Detenciones', N'UsuarioLineaId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Tabla INT = OBJECT_ID(N'mnt.DET_Detenciones');
    DECLARE @Viejas TABLE ([column_id] INT PRIMARY KEY, [name] SYSNAME);
    INSERT INTO @Viejas SELECT c.[column_id], c.[name] FROM sys.columns c
    WHERE c.[object_id] = @Tabla AND c.[name] IN (N'TurnoId', N'Fecha', N'UsuarioLineaId');

    DECLARE @Sql NVARCHAR(MAX), @Nombre SYSNAME;

    -- FK que salen de esas columnas.
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT fk.[name] FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fkc.[constraint_object_id] = fk.[object_id]
        WHERE fk.[parent_object_id] = @Tabla AND fkc.[parent_column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE [mnt].[DET_Detenciones] DROP CONSTRAINT ' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql; PRINT N'FK eliminada: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    -- CHECK y DEFAULT sobre esas columnas.
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT cc.[name] FROM sys.check_constraints cc
        WHERE cc.[parent_object_id] = @Tabla
          AND (cc.[parent_column_id] IN (SELECT [column_id] FROM @Viejas)
               OR cc.[definition] LIKE N'%[[]TurnoId]%' OR cc.[definition] LIKE N'%[[]Fecha]%' OR cc.[definition] LIKE N'%[[]UsuarioLineaId]%')
        UNION
        SELECT dc.[name] FROM sys.default_constraints dc
        WHERE dc.[parent_object_id] = @Tabla AND dc.[parent_column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE [mnt].[DET_Detenciones] DROP CONSTRAINT ' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql; PRINT N'Restricción eliminada: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    -- Estadísticas creadas a mano sobre esas columnas.
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT s.[name] FROM sys.stats s
        INNER JOIN sys.stats_columns sc ON sc.[object_id] = s.[object_id] AND sc.[stats_id] = s.[stats_id]
        WHERE s.[object_id] = @Tabla AND s.[user_created] = 1 AND sc.[column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'DROP STATISTICS [mnt].[DET_Detenciones].' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql; PRINT N'Estadística eliminada: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    -- Índices: los que tienen una columna vieja como clave o incluida. Se re-crean con el mismo nombre sin las
    -- columnas viejas si les queda alguna clave (p. ej. IX_Detenciones_EstadoActual INCLUDE ... Fecha); si no, se eliminan.
    DECLARE @IndexId INT, @Unico BIT, @Filtro NVARCHAR(MAX), @Claves NVARCHAR(MAX), @Incluidas NVARCHAR(MAX), @EsRestriccion BIT;
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT i.[index_id], i.[name], i.[is_unique], i.[filter_definition], i.[is_unique_constraint]
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id]
        WHERE i.[object_id] = @Tabla AND i.[is_primary_key] = 0 AND ic.[column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @IndexId, @Nombre, @Unico, @Filtro, @EsRestriccion;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Claves = STUFF((
            SELECT N', ' + QUOTENAME(col.[name]) + CASE WHEN ic.[is_descending_key] = 1 THEN N' DESC' ELSE N'' END
            FROM sys.index_columns ic
            INNER JOIN sys.columns col ON col.[object_id] = ic.[object_id] AND col.[column_id] = ic.[column_id]
            WHERE ic.[object_id] = @Tabla AND ic.[index_id] = @IndexId AND ic.[is_included_column] = 0
              AND ic.[column_id] NOT IN (SELECT [column_id] FROM @Viejas)
            ORDER BY ic.[key_ordinal]
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
        SET @Incluidas = STUFF((
            SELECT N', ' + QUOTENAME(col.[name])
            FROM sys.index_columns ic
            INNER JOIN sys.columns col ON col.[object_id] = ic.[object_id] AND col.[column_id] = ic.[column_id]
            WHERE ic.[object_id] = @Tabla AND ic.[index_id] = @IndexId AND ic.[is_included_column] = 1
              AND ic.[column_id] NOT IN (SELECT [column_id] FROM @Viejas)
            ORDER BY ic.[index_column_id]
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');

        SET @Sql = CASE WHEN @EsRestriccion = 1
                        THEN N'ALTER TABLE [mnt].[DET_Detenciones] DROP CONSTRAINT ' + QUOTENAME(@Nombre) + N';'
                        ELSE N'DROP INDEX ' + QUOTENAME(@Nombre) + N' ON [mnt].[DET_Detenciones];' END;
        EXEC sys.sp_executesql @Sql;

        -- Un índice único sin una de sus claves cambiaría de significado: no se re-crea (se avisa).
        IF @Claves IS NOT NULL AND @Unico = 0
           AND (@Filtro IS NULL OR (@Filtro NOT LIKE N'%[[]TurnoId]%' AND @Filtro NOT LIKE N'%[[]Fecha]%' AND @Filtro NOT LIKE N'%[[]UsuarioLineaId]%'))
        BEGIN
            SET @Sql = N'CREATE INDEX ' + QUOTENAME(@Nombre) + N' ON [mnt].[DET_Detenciones] (' + @Claves + N')'
                     + ISNULL(N' INCLUDE (' + @Incluidas + N')', N'')
                     + ISNULL(N' WHERE ' + @Filtro, N'') + N';';
            EXEC sys.sp_executesql @Sql;
            PRINT N'Índice re-creado sin las columnas viejas: ' + @Nombre;
        END
        ELSE
            PRINT N'Índice eliminado: ' + @Nombre;
        FETCH NEXT FROM c INTO @IndexId, @Nombre, @Unico, @Filtro, @EsRestriccion;
    END
    CLOSE c; DEALLOCATE c;

    IF COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoId') IS NOT NULL
        ALTER TABLE [mnt].[DET_Detenciones] DROP COLUMN [TurnoId];
    IF COL_LENGTH(N'mnt.DET_Detenciones', N'Fecha') IS NOT NULL
        ALTER TABLE [mnt].[DET_Detenciones] DROP COLUMN [Fecha];
    IF COL_LENGTH(N'mnt.DET_Detenciones', N'UsuarioLineaId') IS NOT NULL
        ALTER TABLE [mnt].[DET_Detenciones] DROP COLUMN [UsuarioLineaId];
    PRINT 'Columnas TurnoId, Fecha y UsuarioLineaId eliminadas de DET_Detenciones.'

    COMMIT TRANSACTION;
END
ELSE
    PRINT 'DET_Detenciones ya no tiene TurnoId, Fecha ni UsuarioLineaId: sin cambios.'
GO

-- ============================================================================
-- 7. CPR_Registros y CAL_Registros: TurnoProduccionId NULL (se completa al crear).
-- ============================================================================
IF COL_LENGTH(N'mnt.CPR_Registros', N'TurnoProduccionId') IS NULL
BEGIN
    ALTER TABLE [mnt].[CPR_Registros] ADD [TurnoProduccionId] BIGINT NULL;
    PRINT 'Columna CPR_Registros.TurnoProduccionId agregada.'
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CPR_Registros_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[CPR_Registros] ADD CONSTRAINT [FK_CPR_Registros_PRD_TurnosProduccion_TurnoProduccionId]
        FOREIGN KEY ([TurnoProduccionId]) REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CPR_Registros') AND name = N'IX_CPR_Registros_TurnoProduccionId')
    CREATE INDEX [IX_CPR_Registros_TurnoProduccionId] ON [mnt].[CPR_Registros] ([TurnoProduccionId]);
GO

IF COL_LENGTH(N'mnt.CAL_Registros', N'TurnoProduccionId') IS NULL
BEGIN
    ALTER TABLE [mnt].[CAL_Registros] ADD [TurnoProduccionId] BIGINT NULL;
    PRINT 'Columna CAL_Registros.TurnoProduccionId agregada.'
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAL_Registros_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[CAL_Registros] ADD CONSTRAINT [FK_CAL_Registros_PRD_TurnosProduccion_TurnoProduccionId]
        FOREIGN KEY ([TurnoProduccionId]) REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAL_Registros') AND name = N'IX_CAL_Registros_TurnoProduccionId')
    CREATE INDEX [IX_CAL_Registros_TurnoProduccionId] ON [mnt].[CAL_Registros] ([TurnoProduccionId]);
GO

SET NOEXEC OFF;
GO
PRINT 'Script 35_t798_detenciones_turno.sql terminado.'
GO
