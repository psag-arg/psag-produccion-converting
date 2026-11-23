-- ==============================================================================
-- Script: 36_t798_camaras_rollback.sql
-- Descripción: Deshace 36_t798_camaras.sql (T-798 etapa 4).
--   1. Las cámaras dadas de alta después de la migración (no están en mnt.CAM_CamarasMigracionT798) vuelven a ser un
--      punto de control (mismo código; si ya hay un punto con ese código, se usa ése).
--   2. CAL_PuntosControl recupera NumeroCamara, MetodoConteo, CreaDetenciones, MinutosUmbralDetencion,
--      SegundosUmbralDetencion, ObjetivoPorHora y EquipoId, con los valores ACTUALES de la cámara (el objetivo, del
--      respaldo; el umbral, del respaldo si no cambió). Los puntos que el script desactivó por ser sólo cámara se reactivan.
--   3. CAM_ConteosMinuto, CAM_Paradas, CAM_Alertas y DET_Detenciones recuperan PuntoControlCalidadId (y los CAM_,
--      LineaId; conteos y paradas, TurnoId desde su turno de producción, salvo los de la excepción 05:00-06:00, que
--      recuperan el turno original de mnt.CAM_ExcepcionTurnoT798), con los nombres de FK e índices de los
--      scripts 25 y 26. CAM_ConteosMinuto.Cantidad vuelve a llamarse Rollos.
--   4. Elimina CamaraId (y TurnoProduccionId de conteos y paradas), CAM_Camaras y el respaldo.
--   Los turnos de producción que creó la conversión quedan: los elimina 35_t798_detenciones_turno_rollback.sql.
--   Las alertas SIN_DATOS / SIN_CONFIGURACION no se borran: si hay, el CHECK de tipos queda con los cuatro valores.
--   Antes de correrlo, volver a una API y a un servicio PSAG_ConteoCamara anteriores a T-798 etapa 4 (y al
--   appsettings del servidor con "PuntoControl" y "Activa").
-- Requiere: 38_t798_conversion_turnos_rollback.sql corrido antes (se verifica).
-- Idempotente: se puede ejecutar más de una vez. Cada paso que toca datos va en transacción.
-- Correr con la API y el servicio PSAG_ConteoCamara detenidos.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Orden inverso: antes va 38_t798_conversion_turnos_rollback.sql (cuelga conteos y paradas de turnos que crea él).
IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'Correr antes 38_t798_conversion_turnos_rollback.sql (orden inverso). No se cambió nada.', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'mnt.CAM_Camaras', N'U') IS NULL
BEGIN
    PRINT 'No existe mnt.CAM_Camaras: no hay nada que deshacer.'
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'tempdb..#QuitarColumnasT798') IS NOT NULL
    DROP PROCEDURE #QuitarColumnasT798;
GO
-- Mismo procedimiento temporal que el script 36: quita columnas con sus FK, CHECK, DEFAULT, estadísticas e índices.
CREATE PROCEDURE #QuitarColumnasT798 @Tabla NVARCHAR(256), @Columnas NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT = OBJECT_ID(@Tabla);
    IF @Id IS NULL RETURN;
    DECLARE @Viejas TABLE ([column_id] INT PRIMARY KEY, [name] SYSNAME COLLATE Latin1_General_CI_AS);
    INSERT INTO @Viejas
    SELECT c.[column_id], c.[name] FROM sys.columns c
    WHERE c.[object_id] = @Id
      -- Sin STRING_SPLIT: un procedimiento temporal compila con la intercalación de tempdb y choca con la de la base.
      AND (N',' + REPLACE(@Columnas COLLATE Latin1_General_CI_AS, N' ', N'') + N',')
          LIKE (N'%,' + c.[name] COLLATE Latin1_General_CI_AS + N',%');
    IF NOT EXISTS (SELECT 1 FROM @Viejas) RETURN;

    DECLARE @Sql NVARCHAR(MAX), @Nombre SYSNAME, @EsRestriccion BIT;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT fk.[name] FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fkc.[constraint_object_id] = fk.[object_id]
        WHERE fk.[parent_object_id] = @Id AND fkc.[parent_column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @Tabla + N' DROP CONSTRAINT ' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql; PRINT N'FK eliminada: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT cc.[name] FROM sys.check_constraints cc
        WHERE cc.[parent_object_id] = @Id
          AND (cc.[parent_column_id] IN (SELECT [column_id] FROM @Viejas)
               OR EXISTS (SELECT 1 FROM @Viejas v WHERE cc.[definition] COLLATE Latin1_General_CI_AS LIKE N'%[[]' + v.[name] + N']%'))
        UNION
        SELECT dc.[name] FROM sys.default_constraints dc
        WHERE dc.[parent_object_id] = @Id AND dc.[parent_column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @Tabla + N' DROP CONSTRAINT ' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql; PRINT N'Restricción eliminada: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT s.[name] FROM sys.stats s
        INNER JOIN sys.stats_columns sc ON sc.[object_id] = s.[object_id] AND sc.[stats_id] = s.[stats_id]
        WHERE s.[object_id] = @Id AND s.[user_created] = 1 AND sc.[column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'DROP STATISTICS ' + @Tabla + N'.' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT i.[name], i.[is_unique_constraint] FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id]
        WHERE i.[object_id] = @Id AND i.[is_primary_key] = 0 AND ic.[column_id] IN (SELECT [column_id] FROM @Viejas);
    OPEN c; FETCH NEXT FROM c INTO @Nombre, @EsRestriccion;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = CASE WHEN @EsRestriccion = 1
                        THEN N'ALTER TABLE ' + @Tabla + N' DROP CONSTRAINT ' + QUOTENAME(@Nombre) + N';'
                        ELSE N'DROP INDEX ' + QUOTENAME(@Nombre) + N' ON ' + @Tabla + N';' END;
        EXEC sys.sp_executesql @Sql; PRINT N'Índice eliminado: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre, @EsRestriccion;
    END
    CLOSE c; DEALLOCATE c;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT [name] FROM @Viejas;
    OPEN c; FETCH NEXT FROM c INTO @Nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @Tabla + N' DROP COLUMN ' + QUOTENAME(@Nombre) + N';';
        EXEC sys.sp_executesql @Sql; PRINT N'Columna eliminada: ' + @Tabla + N'.' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;
END
GO

-- ============================================================================
-- 1. Cámaras sin punto de control (dadas de alta después de la migración): vuelven a ser un punto.
-- ============================================================================
IF OBJECT_ID(N'mnt.CAM_CamarasMigracionT798', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CAM_CamarasMigracionT798] (
        [PuntoControlCalidadId] INT NOT NULL,
        [CamaraId] INT NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [EquipoId] INT NULL,
        [NumeroCamara] INT NULL,
        [MetodoConteo] NVARCHAR(20) NULL,
        [CreaDetenciones] BIT NULL,
        [MinutosUmbralDetencion] INT NULL,
        [SegundosUmbralDetencion] INT NULL,
        [ObjetivoPorHora] DECIMAL(10, 2) NULL,
        [ActivoAntes] BIT NOT NULL,
        [Desactivado] BIT NOT NULL CONSTRAINT [DF_CAM_CamarasMigracionT798_Desactivado] DEFAULT 0,
        [FechaHoraMigracion] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_CAM_CamarasMigracionT798_Fecha] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_CAM_CamarasMigracionT798] PRIMARY KEY ([PuntoControlCalidadId])
    );
END
GO

BEGIN TRANSACTION;
SET XACT_ABORT ON;

INSERT INTO [mnt].[CAL_PuntosControl] ([LineaId], [Codigo], [Nombre], [Descripcion], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT c.[LineaId], c.[Codigo], c.[Nombre], N'Cámara (vuelta de CAM_Camaras por el rollback del script 36).', c.[Activo],
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'SISTEMA', N'SISTEMA'
FROM [mnt].[CAM_Camaras] c
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CAM_CamarasMigracionT798] r WHERE r.[CamaraId] = c.[CamaraId])
  AND NOT EXISTS (SELECT 1 FROM [mnt].[CAL_PuntosControl] p WHERE p.[Codigo] = c.[Codigo]);
PRINT CONCAT('Puntos de control creados para cámaras nuevas: ', @@ROWCOUNT);

INSERT INTO [mnt].[CAM_CamarasMigracionT798] ([PuntoControlCalidadId], [CamaraId], [Codigo], [ActivoAntes])
SELECT p.[PuntoControlCalidadId], c.[CamaraId], c.[Codigo], p.[Activo]
FROM [mnt].[CAM_Camaras] c
INNER JOIN [mnt].[CAL_PuntosControl] p ON p.[Codigo] = c.[Codigo]
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CAM_CamarasMigracionT798] r WHERE r.[CamaraId] = c.[CamaraId])
  AND NOT EXISTS (SELECT 1 FROM [mnt].[CAM_CamarasMigracionT798] r WHERE r.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]);

COMMIT TRANSACTION;
GO

-- ============================================================================
-- 2. CAL_PuntosControl: columnas de cámara (scripts 26, 28 y 30) con los valores actuales de la cámara.
-- ============================================================================
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'EquipoId') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [EquipoId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'NumeroCamara') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [NumeroCamara] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'CreaDetenciones') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [CreaDetenciones] BIT NOT NULL CONSTRAINT [DF_PuntosControlCalidad_CreaDetenciones] DEFAULT 0;
GO
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'MinutosUmbralDetencion') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [MinutosUmbralDetencion] INT NOT NULL CONSTRAINT [DF_PuntosControlCalidad_MinutosUmbral] DEFAULT 5;
GO
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'ObjetivoPorHora') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [ObjetivoPorHora] DECIMAL(10, 2) NULL;
GO
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'MetodoConteo') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [MetodoConteo] NVARCHAR(20) NULL;
GO
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'SegundosUmbralDetencion') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [SegundosUmbralDetencion] INT NULL;
GO

IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'CamaraId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    EXEC sys.sp_executesql N'
        UPDATE p SET
            p.[EquipoId] = c.[EquipoId],
            p.[NumeroCamara] = c.[NumeroCanalGrabador],
            p.[MetodoConteo] = CASE c.[Magnitud] WHEN N''ROLLOS'' THEN N''CARRILES'' WHEN N''MANGAS'' THEN N''MANGAS''
                                                 WHEN N''BOLSONES'' THEN N''BOLSONES'' END,
            p.[CreaDetenciones] = c.[CreaDetenciones],
            -- Si el umbral de la cámara no cambió desde la migración, vuelven los valores exactos del respaldo.
            p.[MinutosUmbralDetencion] = CASE WHEN r.[MinutosUmbralDetencion] IS NOT NULL
                                                   AND c.[UmbralDetencionSegundos] = COALESCE(r.[SegundosUmbralDetencion], r.[MinutosUmbralDetencion] * 60)
                                              THEN r.[MinutosUmbralDetencion]
                                              WHEN c.[UmbralDetencionSegundos] % 60 = 0 AND c.[UmbralDetencionSegundos] / 60 BETWEEN 1 AND 240
                                              THEN c.[UmbralDetencionSegundos] / 60
                                              ELSE ISNULL(r.[MinutosUmbralDetencion], 5) END,
            p.[SegundosUmbralDetencion] = CASE WHEN r.[MinutosUmbralDetencion] IS NOT NULL
                                                    AND c.[UmbralDetencionSegundos] = COALESCE(r.[SegundosUmbralDetencion], r.[MinutosUmbralDetencion] * 60)
                                               THEN r.[SegundosUmbralDetencion]
                                               WHEN c.[UmbralDetencionSegundos] % 60 = 0 THEN NULL ELSE c.[UmbralDetencionSegundos] END,
            p.[ObjetivoPorHora] = r.[ObjetivoPorHora],
            p.[Activo] = CASE WHEN r.[Desactivado] = 1 THEN r.[ActivoAntes] ELSE p.[Activo] END
        FROM [mnt].[CAL_PuntosControl] p
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]
        INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = r.[CamaraId];
        PRINT CONCAT(''Puntos de control con los datos de su cámara: '', @@ROWCOUNT);';
    COMMIT TRANSACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MinutosUmbral')
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD CONSTRAINT [CK_PuntosControlCalidad_MinutosUmbral] CHECK ([MinutosUmbralDetencion] BETWEEN 1 AND 240);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_SegundosUmbral')
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD CONSTRAINT [CK_PuntosControlCalidad_SegundosUmbral]
        CHECK ([SegundosUmbralDetencion] IS NULL OR [SegundosUmbralDetencion] BETWEEN 30 AND 14400);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_ObjetivoPorHora')
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD CONSTRAINT [CK_PuntosControlCalidad_ObjetivoPorHora]
        CHECK ([ObjetivoPorHora] IS NULL OR ([ObjetivoPorHora] > 0 AND [ObjetivoPorHora] <= 100000));
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MetodoConteo')
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo]
        CHECK ([MetodoConteo] IS NULL OR [MetodoConteo] IN (N'CARRILES', N'MANGAS', N'BOLSONES'));
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PuntosControlCalidad_Equipos')
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD CONSTRAINT [FK_PuntosControlCalidad_Equipos]
        FOREIGN KEY ([EquipoId]) REFERENCES [mnt].[ABM_Equipos] ([EquipoId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAL_PuntosControl') AND name = N'IX_PuntosControlCalidad_EquipoId')
    CREATE INDEX [IX_PuntosControlCalidad_EquipoId] ON [mnt].[CAL_PuntosControl] ([EquipoId]);
GO

-- ============================================================================
-- 3. CAM_* y DET_Detenciones: punto de control, línea y turno como antes.
-- ============================================================================
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'LineaId') IS NULL ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD [LineaId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'PuntoControlCalidadId') IS NULL ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD [PuntoControlCalidadId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoId') IS NULL ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD [TurnoId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Paradas', N'LineaId') IS NULL ALTER TABLE [mnt].[CAM_Paradas] ADD [LineaId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Paradas', N'PuntoControlCalidadId') IS NULL ALTER TABLE [mnt].[CAM_Paradas] ADD [PuntoControlCalidadId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Paradas', N'TurnoId') IS NULL ALTER TABLE [mnt].[CAM_Paradas] ADD [TurnoId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Alertas', N'LineaId') IS NULL ALTER TABLE [mnt].[CAM_Alertas] ADD [LineaId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Alertas', N'PuntoControlCalidadId') IS NULL ALTER TABLE [mnt].[CAM_Alertas] ADD [PuntoControlCalidadId] INT NULL;
GO
IF COL_LENGTH(N'mnt.DET_Detenciones', N'PuntoControlCalidadId') IS NULL ALTER TABLE [mnt].[DET_Detenciones] ADD [PuntoControlCalidadId] INT NULL;
GO

IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'CamaraId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    EXEC sys.sp_executesql N'
        UPDATE x SET x.[LineaId] = c.[LineaId], x.[PuntoControlCalidadId] = r.[PuntoControlCalidadId], x.[TurnoId] = tp.[TurnoId]
        FROM [mnt].[CAM_ConteosMinuto] x
        INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = x.[CamaraId]
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[CamaraId] = x.[CamaraId]
        LEFT JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = x.[TurnoProduccionId];
        UPDATE x SET x.[LineaId] = c.[LineaId], x.[PuntoControlCalidadId] = r.[PuntoControlCalidadId], x.[TurnoId] = tp.[TurnoId]
        FROM [mnt].[CAM_Paradas] x
        INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = x.[CamaraId]
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[CamaraId] = x.[CamaraId]
        LEFT JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = x.[TurnoProduccionId];
        UPDATE x SET x.[LineaId] = c.[LineaId], x.[PuntoControlCalidadId] = r.[PuntoControlCalidadId]
        FROM [mnt].[CAM_Alertas] x
        INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = x.[CamaraId]
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[CamaraId] = x.[CamaraId];
        UPDATE x SET x.[PuntoControlCalidadId] = r.[PuntoControlCalidadId]
        FROM [mnt].[DET_Detenciones] x
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[CamaraId] = x.[CamaraId];
        PRINT ''Conteos, paradas, alertas y detenciones vuelven a su punto de control.'';';
    COMMIT TRANSACTION;
END
GO

-- Excepción 05:00-06:00: los conteos y paradas que el 36 pasó del T3 al T1 vuelven a su turno original.
IF OBJECT_ID(N'mnt.CAM_ExcepcionTurnoT798', N'U') IS NOT NULL
   AND COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoId') IS NOT NULL
   AND COL_LENGTH(N'mnt.CAM_Paradas', N'TurnoId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    EXEC sys.sp_executesql N'
        UPDATE m SET m.[TurnoId] = r.[TurnoIdOriginal]
        FROM [mnt].[CAM_ConteosMinuto] m
        INNER JOIN [mnt].[CAM_ExcepcionTurnoT798] r ON r.[Tabla] = ''M'' AND r.[Id] = m.[ConteoCamaraMinutoId];
        PRINT CONCAT(''Conteos de 05:00 a 06:00 que vuelven a su turno original: '', @@ROWCOUNT);
        UPDATE p SET p.[TurnoId] = r.[TurnoIdOriginal]
        FROM [mnt].[CAM_Paradas] p
        INNER JOIN [mnt].[CAM_ExcepcionTurnoT798] r ON r.[Tabla] = ''P'' AND r.[Id] = p.[ParadaCamaraId];
        PRINT CONCAT(''Paradas de 05:00 a 06:00 que vuelven a su turno original: '', @@ROWCOUNT);';
    DROP TABLE [mnt].[CAM_ExcepcionTurnoT798];
    COMMIT TRANSACTION;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'LineaId' AND is_nullable = 1)
BEGIN
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ALTER COLUMN [LineaId] INT NOT NULL;
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ALTER COLUMN [PuntoControlCalidadId] INT NOT NULL;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'LineaId' AND is_nullable = 1)
BEGIN
    ALTER TABLE [mnt].[CAM_Paradas] ALTER COLUMN [LineaId] INT NOT NULL;
    ALTER TABLE [mnt].[CAM_Paradas] ALTER COLUMN [PuntoControlCalidadId] INT NOT NULL;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.CAM_Alertas') AND name = N'LineaId' AND is_nullable = 1)
BEGIN
    ALTER TABLE [mnt].[CAM_Alertas] ALTER COLUMN [LineaId] INT NOT NULL;
    ALTER TABLE [mnt].[CAM_Alertas] ALTER COLUMN [PuntoControlCalidadId] INT NOT NULL;
END
GO

-- 4. Quitar CamaraId / TurnoProduccionId (antes de re-crear los índices únicos por punto).
BEGIN TRANSACTION;
SET XACT_ABORT ON;
EXEC #QuitarColumnasT798 N'[mnt].[CAM_ConteosMinuto]', N'CamaraId,TurnoProduccionId';
EXEC #QuitarColumnasT798 N'[mnt].[CAM_Paradas]', N'CamaraId,TurnoProduccionId';
EXEC #QuitarColumnasT798 N'[mnt].[CAM_Alertas]', N'CamaraId';
EXEC #QuitarColumnasT798 N'[mnt].[DET_Detenciones]', N'CamaraId';
EXEC #QuitarColumnasT798 N'[mnt].[CAL_PuntosControl]', N'CamaraId';
COMMIT TRANSACTION;
GO

-- FK e índices con los nombres de los scripts 25 y 26.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ConteosCamaraMinuto_Lineas_LineaId')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [FK_ConteosCamaraMinuto_Lineas_LineaId] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[ABM_Lineas] ([LineaId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ConteosCamaraMinuto_PuntosControlCalidad_PuntoControlCalidadId')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [FK_ConteosCamaraMinuto_PuntosControlCalidad_PuntoControlCalidadId] FOREIGN KEY ([PuntoControlCalidadId]) REFERENCES [mnt].[CAL_PuntosControl] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ConteosCamaraMinuto_Turnos_TurnoId')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [FK_ConteosCamaraMinuto_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[ABM_Turnos] ([TurnoId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'UX_ConteosCamaraMinuto_Punto_Minuto_Carril')
    CREATE UNIQUE INDEX [UX_ConteosCamaraMinuto_Punto_Minuto_Carril] ON [mnt].[CAM_ConteosMinuto] ([PuntoControlCalidadId], [Minuto], [Carril]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'IX_ConteosCamaraMinuto_Linea_Minuto')
BEGIN
    -- Igual que el 25 (con INCLUDE). Cantidad vuelve a llamarse Rollos más abajo: se incluye la que exista
    -- (es la misma columna).
    DECLARE @IxSql NVARCHAR(MAX) = N'CREATE INDEX [IX_ConteosCamaraMinuto_Linea_Minuto] ON [mnt].[CAM_ConteosMinuto] ([LineaId], [Minuto]) INCLUDE ([Carril], '
        + QUOTENAME(CASE WHEN COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'Rollos') IS NOT NULL THEN N'Rollos' ELSE N'Cantidad' END) + N', [Estado]);';
    EXEC sys.sp_executesql @IxSql;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'IX_ConteosCamaraMinuto_PuntoControlCalidadId')
    CREATE INDEX [IX_ConteosCamaraMinuto_PuntoControlCalidadId] ON [mnt].[CAM_ConteosMinuto] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'IX_ConteosCamaraMinuto_TurnoId')
    CREATE INDEX [IX_ConteosCamaraMinuto_TurnoId] ON [mnt].[CAM_ConteosMinuto] ([TurnoId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ParadasCamara_Lineas_LineaId')
    ALTER TABLE [mnt].[CAM_Paradas] ADD CONSTRAINT [FK_ParadasCamara_Lineas_LineaId] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[ABM_Lineas] ([LineaId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ParadasCamara_PuntosControlCalidad_PuntoControlCalidadId')
    ALTER TABLE [mnt].[CAM_Paradas] ADD CONSTRAINT [FK_ParadasCamara_PuntosControlCalidad_PuntoControlCalidadId] FOREIGN KEY ([PuntoControlCalidadId]) REFERENCES [mnt].[CAL_PuntosControl] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ParadasCamara_Turnos_TurnoId')
    ALTER TABLE [mnt].[CAM_Paradas] ADD CONSTRAINT [FK_ParadasCamara_Turnos_TurnoId] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[ABM_Turnos] ([TurnoId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'IX_ParadasCamara_Linea_Inicio')
    CREATE INDEX [IX_ParadasCamara_Linea_Inicio] ON [mnt].[CAM_Paradas] ([LineaId], [FechaHoraInicio]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'IX_ParadasCamara_PuntoControlCalidadId')
    CREATE INDEX [IX_ParadasCamara_PuntoControlCalidadId] ON [mnt].[CAM_Paradas] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'IX_ParadasCamara_TurnoId')
    CREATE INDEX [IX_ParadasCamara_TurnoId] ON [mnt].[CAM_Paradas] ([TurnoId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AlertasCamara_PuntosControlCalidad')
    ALTER TABLE [mnt].[CAM_Alertas] ADD CONSTRAINT [FK_AlertasCamara_PuntosControlCalidad] FOREIGN KEY ([PuntoControlCalidadId]) REFERENCES [mnt].[CAL_PuntosControl] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AlertasCamara_Lineas')
    ALTER TABLE [mnt].[CAM_Alertas] ADD CONSTRAINT [FK_AlertasCamara_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[ABM_Lineas] ([LineaId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Alertas') AND name = N'IX_AlertasCamara_Punto_Inicio')
    CREATE INDEX [IX_AlertasCamara_Punto_Inicio] ON [mnt].[CAM_Alertas] ([PuntoControlCalidadId], [FechaHoraInicio]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Alertas') AND name = N'IX_AlertasCamara_LineaId')
    CREATE INDEX [IX_AlertasCamara_LineaId] ON [mnt].[CAM_Alertas] ([LineaId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Detenciones_PuntosControlCalidad')
    ALTER TABLE [mnt].[DET_Detenciones] ADD CONSTRAINT [FK_Detenciones_PuntosControlCalidad] FOREIGN KEY ([PuntoControlCalidadId]) REFERENCES [mnt].[CAL_PuntosControl] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_Detenciones_PuntoControlCalidadId')
    CREATE INDEX [IX_Detenciones_PuntoControlCalidadId] ON [mnt].[DET_Detenciones] ([PuntoControlCalidadId]);
GO

-- Cantidad → Rollos.
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'Cantidad') IS NOT NULL AND COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'Rollos') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConteosCamaraMinuto_Cantidad')
        ALTER TABLE [mnt].[CAM_ConteosMinuto] DROP CONSTRAINT [CK_ConteosCamaraMinuto_Cantidad];
    EXEC sys.sp_rename N'mnt.CAM_ConteosMinuto.Cantidad', N'Rollos', N'COLUMN';
    PRINT 'CAM_ConteosMinuto.Cantidad vuelve a llamarse Rollos.'
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConteosCamaraMinuto_Rollos')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [CK_ConteosCamaraMinuto_Rollos] CHECK ([Rollos] >= 0);
GO

-- Alertas: si no hay de los tipos nuevos, el CHECK vuelve a CONGELADA / SIN_VIDEO.
IF NOT EXISTS (SELECT 1 FROM [mnt].[CAM_Alertas] WHERE [Tipo] IN ('SIN_DATOS', 'SIN_CONFIGURACION'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AlertasCamara_Tipo')
        ALTER TABLE [mnt].[CAM_Alertas] DROP CONSTRAINT [CK_AlertasCamara_Tipo];
    ALTER TABLE [mnt].[CAM_Alertas] ADD CONSTRAINT [CK_AlertasCamara_Tipo] CHECK ([Tipo] IN ('CONGELADA', 'SIN_VIDEO'));
END
ELSE
    PRINT 'Hay alertas SIN_DATOS / SIN_CONFIGURACION: el CHECK de tipos queda con los cuatro valores (no se borran datos).'
GO

-- ============================================================================
-- 5. Eliminar CAM_Camaras y el respaldo (si quedara, una nueva corrida del 36 usaría ids viejos).
-- ============================================================================
IF OBJECT_ID(N'mnt.CAM_Camaras', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[CAM_Camaras];
    PRINT 'Tabla mnt.CAM_Camaras eliminada.'
END
GO
IF OBJECT_ID(N'mnt.CAM_CamarasMigracionT798', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[CAM_CamarasMigracionT798];
    PRINT 'Tabla mnt.CAM_CamarasMigracionT798 eliminada.'
END
GO

IF OBJECT_ID(N'tempdb..#QuitarColumnasT798') IS NOT NULL
    DROP PROCEDURE #QuitarColumnasT798;
GO

SET NOEXEC OFF;
GO
PRINT 'Rollback 36_t798_camaras_rollback.sql terminado.'
GO
