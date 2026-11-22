-- ==============================================================================
-- Script: 36_t798_camaras.sql
-- Descripción: T-798 etapa 4 · Cámaras unificadas, conteos y T-785 alineada
--              (docs/diseno/T-798_modelo_produccion.md, 2.4 y 2.8).
--   1. CAM_Camaras: maestro único de cámaras (el dispositivo), con CHECK por función (CONTEO / CALIDAD) y una sola
--      cámara de producción oficial por línea y magnitud (índice único filtrado).
--   2. Columnas nuevas, NULL hasta migrar: CamaraId en CAL_PuntosControl, CAM_ConteosMinuto, CAM_Paradas,
--      CAM_Alertas y DET_Detenciones; TurnoProduccionId en CAM_ConteosMinuto y CAM_Paradas.
--      CAM_ConteosMinuto.Rollos pasa a llamarse Cantidad (la magnitud la da la cámara).
--   3. Migración (todo o nada) de las filas de CAL_PuntosControl que son cámaras: tienen NumeroCamara o MetodoConteo,
--      o las usa algún conteo, parada, alerta o detención de cámara. Se crea una fila en CAM_Camaras por cada una,
--      CONSERVANDO EL CÓDIGO (el appsettings del servicio sigue matcheando):
--        Funcion = CONTEO; Magnitud desde MetodoConteo (CARRILES → ROLLOS, MANGAS → MANGAS, BOLSONES → BOLSONES; sin
--        método: ROLLOS si sus conteos tienen más de un carril, si no MANGAS); MetodoDeteccion CARRILES (rollos) o
--        PASOS; CantidadCarriles CARRILES → 4, otros → 1; DetectaParadas = 1 salvo BOLSONES (la 8 no da paradas)
--        si no crea detenciones; CreaDetenciones como estaba; umbral = segundos si hay, si no minutos × 60;
--        Habilitada = Activo (del punto); CuentaProduccionOficial = 1 sólo para la cámara de MANGAS (activa) de cada
--        línea; NumeroCanalGrabador = NumeroCamara; EquipoId como estaba.
--      El punto queda usando su cámara (CAL_PuntosControl.CamaraId). Los puntos que eran SOLO cámara (sin registros
--      de calidad) se desactivan (borrado lógico), no se borran.
--      Todo queda anotado en mnt.CAM_CamarasMigracionT798 (lo usa el rollback).
--   4. Repunta CAM_ConteosMinuto, CAM_Paradas, CAM_Alertas y DET_Detenciones a la cámara nueva.
--      Conteos y paradas con TurnoId: se cuelgan del turno de producción (como el script 35 con las detenciones:
--      si falta, se crea CERRADO por SISTEMA sin operador ni producto; fecha de producción = día de inicio del turno
--      materializado, hora de planta UTC-3).
--   5. CamaraId NOT NULL en los CAM_*, FK e índices nuevos; CHECK de alertas con SIN_DATOS y SIN_CONFIGURACION.
--   6. Recién ahí elimina las columnas viejas (con sus FK, CHECK, DEFAULT, estadísticas e índices):
--      CAM_ConteosMinuto y CAM_Paradas: LineaId, PuntoControlCalidadId, TurnoId; CAM_Alertas: LineaId,
--      PuntoControlCalidadId; DET_Detenciones: PuntoControlCalidadId; CAL_PuntosControl: NumeroCamara, MetodoConteo,
--      CreaDetenciones, MinutosUmbralDetencion, SegundosUmbralDetencion, ObjetivoPorHora, EquipoId (sólo servía a la
--      cámara: pasa a CAM_Camaras.EquipoId) e IncluirEnEficiencia si existiera.
-- Después de correrlo:
--   - Revisar en Web → Maestros → Cámaras que estén HABILITADAS sólo las que el servicio tiene que correr (antes lo
--     decía "Activa" en el appsettings del servidor; ahora lo decide Maestros). Ver docs/CONTEO_CAMARA.md.
--   - El ObjetivoPorHora de las cámaras no se migra: el objetivo sale del parámetro línea + producto (etapa 5); queda
--     guardado en mnt.CAM_CamarasMigracionT798.
-- Versión: 1.0
-- Requiere: 31, 34 y 35.
-- Idempotente: se puede ejecutar más de una vez (hace solo lo que falta). Cada paso que toca datos va en transacción.
-- Correr con la API y el servicio PSAG_ConteoCamara detenidos, antes de publicar la API de T-798 etapa 4.
-- Deshacer: 36_t798_camaras_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CAL_PuntosControl', N'U') IS NULL
   OR OBJECT_ID(N'mnt.CAM_ConteosMinuto', N'U') IS NULL
   OR OBJECT_ID(N'mnt.CAM_Paradas', N'U') IS NULL
   OR OBJECT_ID(N'mnt.CAM_Alertas', N'U') IS NULL
   OR OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NULL
   OR COL_LENGTH(N'mnt.DET_Detenciones', N'TurnoProduccionId') IS NULL
BEGIN
    RAISERROR(N'Falta correr 31_t798_renombre_por_modulo.sql, 34_t798_turnos_produccion.sql y 35_t798_detenciones_turno.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

-- Procedimiento temporal (vive mientras dure la conexión): elimina columnas de una tabla con todo lo que dependa de
-- ellas (FK que salen de ellas, CHECK, DEFAULT, estadísticas e índices). Los índices nuevos se crean aparte.
IF OBJECT_ID(N'tempdb..#QuitarColumnasT798') IS NOT NULL
    DROP PROCEDURE #QuitarColumnasT798;
GO
CREATE PROCEDURE #QuitarColumnasT798 @Tabla NVARCHAR(256), @Columnas NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT = OBJECT_ID(@Tabla);
    IF @Id IS NULL RETURN;
    DECLARE @Viejas TABLE ([column_id] INT PRIMARY KEY, [name] SYSNAME);
    INSERT INTO @Viejas
    SELECT c.[column_id], c.[name] FROM sys.columns c
    WHERE c.[object_id] = @Id AND c.[name] IN (SELECT LTRIM(RTRIM([value])) FROM STRING_SPLIT(@Columnas, N','));
    IF NOT EXISTS (SELECT 1 FROM @Viejas) RETURN;

    DECLARE @Sql NVARCHAR(MAX), @Nombre SYSNAME;

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
               OR EXISTS (SELECT 1 FROM @Viejas v WHERE cc.[definition] LIKE N'%[[]' + v.[name] + N']%'))
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
        EXEC sys.sp_executesql @Sql; PRINT N'Estadística eliminada: ' + @Nombre;
        FETCH NEXT FROM c INTO @Nombre;
    END
    CLOSE c; DEALLOCATE c;

    DECLARE @EsRestriccion BIT;
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
-- 1. CAM_Camaras (diseño 2.4).
-- ============================================================================
IF OBJECT_ID(N'mnt.CAM_Camaras', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CAM_Camaras] (
        [CamaraId] INT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(30) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [LineaId] INT NOT NULL,
        [EquipoId] INT NULL,
        [NumeroCanalGrabador] INT NULL,
        [Funcion] NVARCHAR(10) NOT NULL,
        [Magnitud] NVARCHAR(10) NULL,
        [MetodoDeteccion] NVARCHAR(10) NULL,
        [CantidadCarriles] TINYINT NULL,
        [DetectaParadas] BIT NOT NULL CONSTRAINT [DF_CAM_Camaras_DetectaParadas] DEFAULT 0,
        [CreaDetenciones] BIT NOT NULL CONSTRAINT [DF_CAM_Camaras_CreaDetenciones] DEFAULT 0,
        [UmbralDetencionSegundos] INT NULL,
        [CuentaProduccionOficial] BIT NOT NULL CONSTRAINT [DF_CAM_Camaras_CuentaProduccionOficial] DEFAULT 0,
        [Habilitada] BIT NOT NULL CONSTRAINT [DF_CAM_Camaras_Habilitada] DEFAULT 0,
        [UltimoDatoRecibido] DATETIMEOFFSET NULL,
        [Activo] BIT NOT NULL CONSTRAINT [DF_CAM_Camaras_Activo] DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_CAM_Camaras_FechaHoraCreacion] DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_CAM_Camaras_FechaHoraModificacion] DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_CAM_Camaras] PRIMARY KEY ([CamaraId]),
        CONSTRAINT [FK_CAM_Camaras_ABM_Lineas_LineaId] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[ABM_Lineas] ([LineaId]),
        CONSTRAINT [FK_CAM_Camaras_ABM_Equipos_EquipoId] FOREIGN KEY ([EquipoId]) REFERENCES [mnt].[ABM_Equipos] ([EquipoId]),
        CONSTRAINT [CK_CAM_Camaras_Funcion] CHECK ([Funcion] IN ('CONTEO', 'CALIDAD')),
        CONSTRAINT [CK_CAM_Camaras_Magnitud] CHECK ([Magnitud] IS NULL OR [Magnitud] IN ('MANGAS', 'ROLLOS', 'PAQUETES', 'BOLSONES', 'PALLETS')),
        CONSTRAINT [CK_CAM_Camaras_MetodoDeteccion] CHECK ([MetodoDeteccion] IS NULL OR [MetodoDeteccion] IN ('CARRILES', 'PASOS')),
        CONSTRAINT [CK_CAM_Camaras_CantidadCarriles] CHECK ([CantidadCarriles] IS NULL OR [CantidadCarriles] BETWEEN 1 AND 16),
        CONSTRAINT [CK_CAM_Camaras_Umbral] CHECK ([UmbralDetencionSegundos] IS NULL OR [UmbralDetencionSegundos] BETWEEN 30 AND 14400),
        CONSTRAINT [CK_CAM_Camaras_Canal] CHECK ([NumeroCanalGrabador] IS NULL OR [NumeroCanalGrabador] BETWEEN 1 AND 999),
        -- Por función: CONTEO necesita magnitud y método (y para crear detenciones tiene que detectar paradas);
        -- CALIDAD no tiene nada del conteo.
        CONSTRAINT [CK_CAM_Camaras_PorFuncion] CHECK (
            ([Funcion] = 'CONTEO' AND [Magnitud] IS NOT NULL AND [MetodoDeteccion] IS NOT NULL AND ([CreaDetenciones] = 0 OR [DetectaParadas] = 1))
            OR ([Funcion] = 'CALIDAD' AND [Magnitud] IS NULL AND [MetodoDeteccion] IS NULL AND [CantidadCarriles] IS NULL
                AND [DetectaParadas] = 0 AND [CreaDetenciones] = 0 AND [UmbralDetencionSegundos] IS NULL AND [CuentaProduccionOficial] = 0))
    );
    PRINT 'Tabla mnt.CAM_Camaras creada.'
END
ELSE
    PRINT 'Tabla mnt.CAM_Camaras ya existía.'
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Camaras') AND name = N'UX_CAM_Camaras_Codigo')
    CREATE UNIQUE INDEX [UX_CAM_Camaras_Codigo] ON [mnt].[CAM_Camaras] ([Codigo]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Camaras') AND name = N'IX_CAM_Camaras_LineaId')
    CREATE INDEX [IX_CAM_Camaras_LineaId] ON [mnt].[CAM_Camaras] ([LineaId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Camaras') AND name = N'IX_CAM_Camaras_EquipoId')
    CREATE INDEX [IX_CAM_Camaras_EquipoId] ON [mnt].[CAM_Camaras] ([EquipoId]);
GO
-- Una sola cámara de producción oficial por línea y magnitud.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Camaras') AND name = N'UX_CAM_Camaras_Oficial_Linea_Magnitud')
    CREATE UNIQUE INDEX [UX_CAM_Camaras_Oficial_Linea_Magnitud] ON [mnt].[CAM_Camaras] ([LineaId], [Magnitud])
        WHERE [CuentaProduccionOficial] = 1;
GO

-- ============================================================================
-- 2. Columnas nuevas (NULL hasta migrar) y Rollos → Cantidad.
-- ============================================================================
IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'CamaraId') IS NULL
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD [CamaraId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'CamaraId') IS NULL
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD [CamaraId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoProduccionId') IS NULL
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD [TurnoProduccionId] BIGINT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Paradas', N'CamaraId') IS NULL
    ALTER TABLE [mnt].[CAM_Paradas] ADD [CamaraId] INT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Paradas', N'TurnoProduccionId') IS NULL
    ALTER TABLE [mnt].[CAM_Paradas] ADD [TurnoProduccionId] BIGINT NULL;
GO
IF COL_LENGTH(N'mnt.CAM_Alertas', N'CamaraId') IS NULL
    ALTER TABLE [mnt].[CAM_Alertas] ADD [CamaraId] INT NULL;
GO
IF COL_LENGTH(N'mnt.DET_Detenciones', N'CamaraId') IS NULL
    ALTER TABLE [mnt].[DET_Detenciones] ADD [CamaraId] INT NULL;
GO

IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'Rollos') IS NOT NULL AND COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'Cantidad') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConteosCamaraMinuto_Rollos' AND parent_object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto'))
        ALTER TABLE [mnt].[CAM_ConteosMinuto] DROP CONSTRAINT [CK_ConteosCamaraMinuto_Rollos];
    EXEC sys.sp_rename N'mnt.CAM_ConteosMinuto.Rollos', N'Cantidad', N'COLUMN';
    PRINT 'CAM_ConteosMinuto.Rollos renombrada a Cantidad.'
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConteosCamaraMinuto_Cantidad')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [CK_ConteosCamaraMinuto_Cantidad] CHECK ([Cantidad] >= 0);
GO

-- ============================================================================
-- 3. Migración de los puntos de control que son cámaras. Todo o nada.
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
    PRINT 'Tabla de respaldo mnt.CAM_CamarasMigracionT798 creada.'
END
GO

IF COL_LENGTH(N'mnt.CAL_PuntosControl', N'NumeroCamara') IS NOT NULL
   AND COL_LENGTH(N'mnt.CAL_PuntosControl', N'MetodoConteo') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    -- Las columnas viejas se leen con SQL dinámico: en una re-ejecución ya no existen y el lote no compilaría.
    DECLARE @Sql NVARCHAR(MAX) = N'
        -- Qué puntos son cámaras.
        SELECT p.[PuntoControlCalidadId], p.[Codigo], p.[Nombre], p.[LineaId], p.[EquipoId], p.[NumeroCamara],
               p.[MetodoConteo], p.[CreaDetenciones], p.[MinutosUmbralDetencion], p.[SegundosUmbralDetencion],
               p.[ObjetivoPorHora], p.[Activo],
               (SELECT COUNT(DISTINCT m.[Carril]) FROM [mnt].[CAM_ConteosMinuto] m WHERE m.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]) AS [Carriles]
        INTO #Camaras
        FROM [mnt].[CAL_PuntosControl] p
        WHERE p.[NumeroCamara] IS NOT NULL OR p.[MetodoConteo] IS NOT NULL
           OR EXISTS (SELECT 1 FROM [mnt].[CAM_ConteosMinuto] x WHERE x.[PuntoControlCalidadId] = p.[PuntoControlCalidadId])
           OR EXISTS (SELECT 1 FROM [mnt].[CAM_Paradas] x WHERE x.[PuntoControlCalidadId] = p.[PuntoControlCalidadId])
           OR EXISTS (SELECT 1 FROM [mnt].[CAM_Alertas] x WHERE x.[PuntoControlCalidadId] = p.[PuntoControlCalidadId])
           OR EXISTS (SELECT 1 FROM [mnt].[DET_Detenciones] x WHERE x.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]);

        IF EXISTS (SELECT 1 FROM #Camaras WHERE LEN([Codigo]) > 30)
        BEGIN
            SELECT [Codigo] AS [CodigoDeMasDe30Caracteres] FROM #Camaras WHERE LEN([Codigo]) > 30;
            ;THROW 50036, N''Hay cámaras con código de más de 30 caracteres (CAM_Camaras.Codigo admite 30): acortarlos en Maestros antes de seguir. No se cambió nada.'', 1;
        END

        ;WITH Datos AS (
            SELECT c.*,
                   CASE c.[MetodoConteo]
                       WHEN N''CARRILES'' THEN N''ROLLOS''
                       WHEN N''MANGAS'' THEN N''MANGAS''
                       WHEN N''BOLSONES'' THEN N''BOLSONES''
                       ELSE CASE WHEN c.[Carriles] > 1 THEN N''ROLLOS'' ELSE N''MANGAS'' END
                   END AS [Magnitud],
                   CASE WHEN c.[MetodoConteo] = N''CARRILES'' OR (c.[MetodoConteo] IS NULL AND c.[Carriles] > 1)
                        THEN 1 ELSE 0 END AS [EsCarriles],
                   CASE WHEN c.[SegundosUmbralDetencion] IS NOT NULL THEN c.[SegundosUmbralDetencion]
                        ELSE c.[MinutosUmbralDetencion] * 60 END AS [Umbral]
            FROM #Camaras c
        ), ConOficial AS (
            SELECT d.*,
                   CASE WHEN d.[Magnitud] = N''MANGAS'' AND d.[Activo] = 1
                             AND ROW_NUMBER() OVER (PARTITION BY d.[LineaId], CASE WHEN d.[Magnitud] = N''MANGAS'' AND d.[Activo] = 1 THEN 1 ELSE 0 END
                                                    ORDER BY d.[PuntoControlCalidadId]) = 1
                        THEN 1 ELSE 0 END AS [Oficial]
            FROM Datos d
        )
        INSERT INTO [mnt].[CAM_Camaras]
            ([Codigo], [Nombre], [LineaId], [EquipoId], [NumeroCanalGrabador], [Funcion], [Magnitud], [MetodoDeteccion],
             [CantidadCarriles], [DetectaParadas], [CreaDetenciones], [UmbralDetencionSegundos], [CuentaProduccionOficial],
             [Habilitada], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT o.[Codigo], o.[Nombre], o.[LineaId], o.[EquipoId],
               CASE WHEN o.[NumeroCamara] BETWEEN 1 AND 999 THEN o.[NumeroCamara] END,
               N''CONTEO'', o.[Magnitud], CASE WHEN o.[EsCarriles] = 1 THEN N''CARRILES'' ELSE N''PASOS'' END,
               CASE WHEN o.[EsCarriles] = 1 THEN 4 ELSE 1 END,
               CASE WHEN o.[Magnitud] <> N''BOLSONES'' OR o.[CreaDetenciones] = 1 THEN 1 ELSE 0 END,
               o.[CreaDetenciones],
               CASE WHEN o.[Umbral] BETWEEN 30 AND 14400 THEN o.[Umbral] END,
               o.[Oficial]
                 * CASE WHEN EXISTS (SELECT 1 FROM [mnt].[CAM_Camaras] x WHERE x.[LineaId] = o.[LineaId] AND x.[Magnitud] = N''MANGAS'' AND x.[CuentaProduccionOficial] = 1) THEN 0 ELSE 1 END,
               o.[Activo], o.[Activo], N''SISTEMA'', N''SISTEMA''
        FROM ConOficial o
        WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CAM_Camaras] x WHERE x.[Codigo] = o.[Codigo]);
        PRINT CONCAT(''Cámaras creadas en CAM_Camaras desde CAL_PuntosControl: '', @@ROWCOUNT);

        INSERT INTO [mnt].[CAM_CamarasMigracionT798]
            ([PuntoControlCalidadId], [CamaraId], [Codigo], [EquipoId], [NumeroCamara], [MetodoConteo], [CreaDetenciones],
             [MinutosUmbralDetencion], [SegundosUmbralDetencion], [ObjetivoPorHora], [ActivoAntes])
        SELECT c.[PuntoControlCalidadId], k.[CamaraId], c.[Codigo], c.[EquipoId], c.[NumeroCamara], c.[MetodoConteo], c.[CreaDetenciones],
               c.[MinutosUmbralDetencion], c.[SegundosUmbralDetencion], c.[ObjetivoPorHora], c.[Activo]
        FROM #Camaras c
        INNER JOIN [mnt].[CAM_Camaras] k ON k.[Codigo] = c.[Codigo]
        WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CAM_CamarasMigracionT798] r WHERE r.[PuntoControlCalidadId] = c.[PuntoControlCalidadId]);

        -- El punto usa su cámara.
        UPDATE p SET p.[CamaraId] = r.[CamaraId]
        FROM [mnt].[CAL_PuntosControl] p
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]
        WHERE p.[CamaraId] IS NULL;

        -- Los que eran SOLO cámara (sin registros de calidad): borrado lógico.
        UPDATE p SET p.[Activo] = 0
        FROM [mnt].[CAL_PuntosControl] p
        INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]
        WHERE p.[Activo] = 1
          AND NOT EXISTS (SELECT 1 FROM [mnt].[CAL_Registros] g WHERE g.[PuntoControlCalidadId] = p.[PuntoControlCalidadId]);
        PRINT CONCAT(''Puntos de control que eran sólo cámara, desactivados: '', @@ROWCOUNT);

        UPDATE r SET r.[Desactivado] = 1
        FROM [mnt].[CAM_CamarasMigracionT798] r
        INNER JOIN [mnt].[CAL_PuntosControl] p ON p.[PuntoControlCalidadId] = r.[PuntoControlCalidadId]
        WHERE r.[ActivoAntes] = 1 AND p.[Activo] = 0;

        DROP TABLE #Camaras;';
    EXEC sys.sp_executesql @Sql;

    COMMIT TRANSACTION;
END
ELSE
    PRINT 'Migración de cámaras: sin cambios (CAL_PuntosControl ya no tiene las columnas de cámara).'
GO

-- ============================================================================
-- 4. Repuntar a la cámara nueva y colgar conteos y paradas del turno de producción. Todo o nada.
-- ============================================================================
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'PuntoControlCalidadId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CAM_Paradas', N'PuntoControlCalidadId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CAM_Alertas', N'PuntoControlCalidadId') IS NOT NULL
   OR COL_LENGTH(N'mnt.DET_Detenciones', N'PuntoControlCalidadId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'PuntoControlCalidadId') IS NOT NULL
        EXEC sys.sp_executesql N'
            UPDATE x SET x.[CamaraId] = r.[CamaraId]
            FROM [mnt].[CAM_ConteosMinuto] x
            INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = x.[PuntoControlCalidadId]
            WHERE x.[CamaraId] IS NULL;
            PRINT CONCAT(''CAM_ConteosMinuto repuntados a su cámara: '', @@ROWCOUNT);';

    IF COL_LENGTH(N'mnt.CAM_Paradas', N'PuntoControlCalidadId') IS NOT NULL
        EXEC sys.sp_executesql N'
            UPDATE x SET x.[CamaraId] = r.[CamaraId]
            FROM [mnt].[CAM_Paradas] x
            INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = x.[PuntoControlCalidadId]
            WHERE x.[CamaraId] IS NULL;
            PRINT CONCAT(''CAM_Paradas repuntadas a su cámara: '', @@ROWCOUNT);';

    IF COL_LENGTH(N'mnt.CAM_Alertas', N'PuntoControlCalidadId') IS NOT NULL
        EXEC sys.sp_executesql N'
            UPDATE x SET x.[CamaraId] = r.[CamaraId]
            FROM [mnt].[CAM_Alertas] x
            INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = x.[PuntoControlCalidadId]
            WHERE x.[CamaraId] IS NULL;
            PRINT CONCAT(''CAM_Alertas repuntadas a su cámara: '', @@ROWCOUNT);';

    IF COL_LENGTH(N'mnt.DET_Detenciones', N'PuntoControlCalidadId') IS NOT NULL
        EXEC sys.sp_executesql N'
            UPDATE x SET x.[CamaraId] = r.[CamaraId]
            FROM [mnt].[DET_Detenciones] x
            INNER JOIN [mnt].[CAM_CamarasMigracionT798] r ON r.[PuntoControlCalidadId] = x.[PuntoControlCalidadId]
            WHERE x.[CamaraId] IS NULL AND x.[PuntoControlCalidadId] IS NOT NULL;
            PRINT CONCAT(''Detenciones automáticas repuntadas a su cámara: '', @@ROWCOUNT);';

    COMMIT TRANSACTION;
END
GO

-- Conteos y paradas con turno materializado: al turno de producción (como el script 35). Todo o nada.
IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoId') IS NOT NULL
   OR COL_LENGTH(N'mnt.CAM_Paradas', N'TurnoId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    CREATE TABLE #Conversion ([Tabla] CHAR(1) NOT NULL, [Id] BIGINT NOT NULL, [LineaId] INT NOT NULL, [TurnoId] INT NOT NULL, [FechaProduccion] DATE NOT NULL);

    -- Fecha de producción = día de inicio del turno materializado (hora de planta UTC-3; el T3 después de medianoche
    -- es del día anterior). La línea es la de la cámara.
    IF COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoId') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO #Conversion ([Tabla], [Id], [LineaId], [TurnoId], [FechaProduccion])
            SELECT ''M'', m.[ConteoCamaraMinutoId], c.[LineaId], m.[TurnoId],
                   CASE WHEN t.[HoraFin] <= t.[HoraInicio] AND CAST(SWITCHOFFSET(m.[Minuto], ''-03:00'') AS TIME) < t.[HoraInicio]
                        THEN DATEADD(DAY, -1, CAST(SWITCHOFFSET(m.[Minuto], ''-03:00'') AS DATE))
                        ELSE CAST(SWITCHOFFSET(m.[Minuto], ''-03:00'') AS DATE) END
            FROM [mnt].[CAM_ConteosMinuto] m
            INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = m.[CamaraId]
            INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = m.[TurnoId]
            WHERE m.[TurnoProduccionId] IS NULL AND m.[TurnoId] IS NOT NULL;';

    IF COL_LENGTH(N'mnt.CAM_Paradas', N'TurnoId') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO #Conversion ([Tabla], [Id], [LineaId], [TurnoId], [FechaProduccion])
            SELECT ''P'', p.[ParadaCamaraId], c.[LineaId], p.[TurnoId],
                   CASE WHEN t.[HoraFin] <= t.[HoraInicio] AND CAST(SWITCHOFFSET(p.[FechaHoraInicio], ''-03:00'') AS TIME) < t.[HoraInicio]
                        THEN DATEADD(DAY, -1, CAST(SWITCHOFFSET(p.[FechaHoraInicio], ''-03:00'') AS DATE))
                        ELSE CAST(SWITCHOFFSET(p.[FechaHoraInicio], ''-03:00'') AS DATE) END
            FROM [mnt].[CAM_Paradas] p
            INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = p.[CamaraId]
            INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = p.[TurnoId]
            WHERE p.[TurnoProduccionId] IS NULL AND p.[TurnoId] IS NOT NULL;';

    -- Turnos de producción que faltan (CERRADO por SISTEMA, sin operador ni producto: "a completar").
    INSERT INTO [mnt].[PRD_TurnosProduccion]
        ([LineaId], [TurnoId], [FechaProduccion], [InicioProgramado], [FinProgramado], [MinutosProgramados],
         [InicioReal], [FinReal], [Estado], [OrigenApertura], [ObservacionCierre], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    SELECT k.[LineaId], k.[TurnoId], k.[FechaProduccion], v.[Inicio], v.[Fin], 0, v.[Inicio], v.[Fin],
           N'CERRADO', N'SISTEMA',
           N'Convertido (T-798 etapa 4) desde conteos y paradas de cámara: minutos programados a recalcular en la etapa 7.',
           N'SISTEMA', N'SISTEMA'
    FROM (SELECT DISTINCT [LineaId], [TurnoId], [FechaProduccion] FROM #Conversion) k
    INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = k.[TurnoId]
    CROSS APPLY (SELECT
        TODATETIMEOFFSET(DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00' AS TIME), t.[HoraInicio]), CAST(k.[FechaProduccion] AS DATETIME2(0))), '-03:00') AS [Inicio],
        TODATETIMEOFFSET(DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00' AS TIME), t.[HoraFin]),
            CAST(CASE WHEN t.[HoraFin] <= t.[HoraInicio] THEN DATEADD(DAY, 1, k.[FechaProduccion]) ELSE k.[FechaProduccion] END AS DATETIME2(0))), '-03:00') AS [Fin]) v
    WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccion] tp
                      WHERE tp.[LineaId] = k.[LineaId] AND tp.[TurnoId] = k.[TurnoId] AND tp.[FechaProduccion] = k.[FechaProduccion]);
    PRINT CONCAT('Turnos de producción creados por la conversión de cámara: ', @@ROWCOUNT);

    UPDATE m SET m.[TurnoProduccionId] = tp.[TurnoProduccionId]
    FROM [mnt].[CAM_ConteosMinuto] m
    INNER JOIN #Conversion c ON c.[Tabla] = 'M' AND c.[Id] = m.[ConteoCamaraMinutoId]
    INNER JOIN [mnt].[PRD_TurnosProduccion] tp
        ON tp.[LineaId] = c.[LineaId] AND tp.[TurnoId] = c.[TurnoId] AND tp.[FechaProduccion] = c.[FechaProduccion];
    PRINT CONCAT('Conteos colgados de su turno de producción: ', @@ROWCOUNT);

    UPDATE p SET p.[TurnoProduccionId] = tp.[TurnoProduccionId]
    FROM [mnt].[CAM_Paradas] p
    INNER JOIN #Conversion c ON c.[Tabla] = 'P' AND c.[Id] = p.[ParadaCamaraId]
    INNER JOIN [mnt].[PRD_TurnosProduccion] tp
        ON tp.[LineaId] = c.[LineaId] AND tp.[TurnoId] = c.[TurnoId] AND tp.[FechaProduccion] = c.[FechaProduccion];
    PRINT CONCAT('Paradas colgadas de su turno de producción: ', @@ROWCOUNT);

    DROP TABLE #Conversion;
    COMMIT TRANSACTION;
END
GO

-- ============================================================================
-- 5. CamaraId NOT NULL + FK + índices; CHECK de alertas.
-- ============================================================================
IF EXISTS (SELECT 1 FROM [mnt].[CAM_ConteosMinuto] WHERE [CamaraId] IS NULL)
   OR EXISTS (SELECT 1 FROM [mnt].[CAM_Paradas] WHERE [CamaraId] IS NULL)
   OR EXISTS (SELECT 1 FROM [mnt].[CAM_Alertas] WHERE [CamaraId] IS NULL)
BEGIN
    RAISERROR(N'Hay conteos, paradas o alertas de cámara sin cámara nueva (¿punto de control inexistente?). Revisar antes de seguir: no se eliminó nada.', 16, 1);
    SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'CamaraId' AND is_nullable = 1)
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ALTER COLUMN [CamaraId] INT NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'CamaraId' AND is_nullable = 1)
    ALTER TABLE [mnt].[CAM_Paradas] ALTER COLUMN [CamaraId] INT NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'mnt.CAM_Alertas') AND name = N'CamaraId' AND is_nullable = 1)
    ALTER TABLE [mnt].[CAM_Alertas] ALTER COLUMN [CamaraId] INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAM_ConteosMinuto_CAM_Camaras_CamaraId')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [FK_CAM_ConteosMinuto_CAM_Camaras_CamaraId]
        FOREIGN KEY ([CamaraId]) REFERENCES [mnt].[CAM_Camaras] ([CamaraId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAM_ConteosMinuto_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[CAM_ConteosMinuto] ADD CONSTRAINT [FK_CAM_ConteosMinuto_PRD_TurnosProduccion_TurnoProduccionId]
        FOREIGN KEY ([TurnoProduccionId]) REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAM_Paradas_CAM_Camaras_CamaraId')
    ALTER TABLE [mnt].[CAM_Paradas] ADD CONSTRAINT [FK_CAM_Paradas_CAM_Camaras_CamaraId]
        FOREIGN KEY ([CamaraId]) REFERENCES [mnt].[CAM_Camaras] ([CamaraId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAM_Paradas_PRD_TurnosProduccion_TurnoProduccionId')
    ALTER TABLE [mnt].[CAM_Paradas] ADD CONSTRAINT [FK_CAM_Paradas_PRD_TurnosProduccion_TurnoProduccionId]
        FOREIGN KEY ([TurnoProduccionId]) REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAM_Alertas_CAM_Camaras_CamaraId')
    ALTER TABLE [mnt].[CAM_Alertas] ADD CONSTRAINT [FK_CAM_Alertas_CAM_Camaras_CamaraId]
        FOREIGN KEY ([CamaraId]) REFERENCES [mnt].[CAM_Camaras] ([CamaraId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DET_Detenciones_CAM_Camaras_CamaraId')
    ALTER TABLE [mnt].[DET_Detenciones] ADD CONSTRAINT [FK_DET_Detenciones_CAM_Camaras_CamaraId]
        FOREIGN KEY ([CamaraId]) REFERENCES [mnt].[CAM_Camaras] ([CamaraId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CAL_PuntosControl_CAM_Camaras_CamaraId')
    ALTER TABLE [mnt].[CAL_PuntosControl] ADD CONSTRAINT [FK_CAL_PuntosControl_CAM_Camaras_CamaraId]
        FOREIGN KEY ([CamaraId]) REFERENCES [mnt].[CAM_Camaras] ([CamaraId]);
GO

-- ============================================================================
-- 6. Eliminar las columnas viejas (con lo que dependa de ellas). Todo o nada.
-- ============================================================================
BEGIN TRANSACTION;
SET XACT_ABORT ON;
EXEC #QuitarColumnasT798 N'[mnt].[CAM_ConteosMinuto]', N'LineaId,PuntoControlCalidadId,TurnoId';
EXEC #QuitarColumnasT798 N'[mnt].[CAM_Paradas]', N'LineaId,PuntoControlCalidadId,TurnoId';
EXEC #QuitarColumnasT798 N'[mnt].[CAM_Alertas]', N'LineaId,PuntoControlCalidadId';
EXEC #QuitarColumnasT798 N'[mnt].[DET_Detenciones]', N'PuntoControlCalidadId';
EXEC #QuitarColumnasT798 N'[mnt].[CAL_PuntosControl]',
    N'NumeroCamara,MetodoConteo,CreaDetenciones,MinutosUmbralDetencion,SegundosUmbralDetencion,ObjetivoPorHora,EquipoId,IncluirEnEficiencia';
COMMIT TRANSACTION;
PRINT 'Columnas viejas de cámara eliminadas (las que quedaban).'
GO

-- Índices nuevos (después de quitar los viejos: el único de minuto+carril pasa a ser por cámara).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'UX_CAM_ConteosMinuto_Camara_Minuto_Carril')
    CREATE UNIQUE INDEX [UX_CAM_ConteosMinuto_Camara_Minuto_Carril] ON [mnt].[CAM_ConteosMinuto] ([CamaraId], [Minuto], [Carril]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND name = N'IX_CAM_ConteosMinuto_TurnoProduccionId')
    CREATE INDEX [IX_CAM_ConteosMinuto_TurnoProduccionId] ON [mnt].[CAM_ConteosMinuto] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'IX_CAM_Paradas_Camara_Inicio')
    CREATE INDEX [IX_CAM_Paradas_Camara_Inicio] ON [mnt].[CAM_Paradas] ([CamaraId], [FechaHoraInicio]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Paradas') AND name = N'IX_CAM_Paradas_TurnoProduccionId')
    CREATE INDEX [IX_CAM_Paradas_TurnoProduccionId] ON [mnt].[CAM_Paradas] ([TurnoProduccionId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAM_Alertas') AND name = N'IX_CAM_Alertas_Camara_Inicio')
    CREATE INDEX [IX_CAM_Alertas_Camara_Inicio] ON [mnt].[CAM_Alertas] ([CamaraId], [FechaHoraInicio]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'IX_DET_Detenciones_CamaraId')
    CREATE INDEX [IX_DET_Detenciones_CamaraId] ON [mnt].[DET_Detenciones] ([CamaraId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.CAL_PuntosControl') AND name = N'IX_CAL_PuntosControl_CamaraId')
    CREATE INDEX [IX_CAL_PuntosControl_CamaraId] ON [mnt].[CAL_PuntosControl] ([CamaraId]);
GO

-- Alertas: SIN_DATOS (la abre la API con el turno abierto) y SIN_CONFIGURACION (la manda el servicio).
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AlertasCamara_Tipo'
           AND parent_object_id = OBJECT_ID(N'mnt.CAM_Alertas') AND definition NOT LIKE N'%SIN_DATOS%')
    ALTER TABLE [mnt].[CAM_Alertas] DROP CONSTRAINT [CK_AlertasCamara_Tipo];
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AlertasCamara_Tipo')
    ALTER TABLE [mnt].[CAM_Alertas] ADD CONSTRAINT [CK_AlertasCamara_Tipo]
        CHECK ([Tipo] IN ('CONGELADA', 'SIN_VIDEO', 'SIN_DATOS', 'SIN_CONFIGURACION'));
GO

IF OBJECT_ID(N'tempdb..#QuitarColumnasT798') IS NOT NULL
    DROP PROCEDURE #QuitarColumnasT798;
GO

-- Resumen para revisar la habilitación (antes la decidía "Activa" en el appsettings del servidor).
SELECT c.[Codigo], c.[Nombre], l.[Nombre] AS [Linea], c.[NumeroCanalGrabador] AS [Canal], c.[Magnitud], c.[MetodoDeteccion],
       c.[CreaDetenciones], c.[UmbralDetencionSegundos], c.[CuentaProduccionOficial], c.[Habilitada], c.[Activo]
FROM [mnt].[CAM_Camaras] c
INNER JOIN [mnt].[ABM_Lineas] l ON l.[LineaId] = c.[LineaId]
ORDER BY l.[Nombre], c.[NumeroCanalGrabador], c.[Codigo];
GO

SET NOEXEC OFF;
GO
PRINT 'Script 36_t798_camaras.sql terminado. Revisar en Maestros → Cámaras qué cámaras quedan habilitadas.'
GO
