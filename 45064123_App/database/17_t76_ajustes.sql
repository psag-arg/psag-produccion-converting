-- ==============================================================================
-- Script: 17_t76_ajustes.sql
-- Descripción: Ajustes T-76 Converting. Eliminación lógica de detenciones,
--              análisis de falla (reglas y responsables), criticidad de equipos,
--              color de tipos de falla y tiempo programado por día,
--              estados con PENDIENTE_JUSTIFICACION y vistas sin eliminadas.
-- Versión: 1.0 - T-76 (fundación U0)
-- Idempotente: se puede ejecutar más de una vez.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Equipos: Critico
-- ============================================================================
IF COL_LENGTH(N'mnt.Equipos', N'Critico') IS NULL
BEGIN
    ALTER TABLE [mnt].[Equipos]
    ADD [Critico] BIT NOT NULL CONSTRAINT [DF_Equipos_Critico] DEFAULT 0
    PRINT 'Columna mnt.Equipos.Critico creada.'
END
GO

-- ============================================================================
-- TiposFalla: Color (#RRGGBB, nulo = color por defecto)
-- ============================================================================
IF COL_LENGTH(N'mnt.TiposFalla', N'Color') IS NULL
BEGIN
    ALTER TABLE [mnt].[TiposFalla]
    ADD [Color] NVARCHAR(7) NULL
    PRINT 'Columna mnt.TiposFalla.Color creada.'
END
GO

-- ============================================================================
-- Tabla: mnt.ReglasAnalisis
-- Descripción: Reglas que deciden si una detención requiere análisis y quién lo responde
-- ============================================================================
IF OBJECT_ID(N'[mnt].[ReglasAnalisis]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[ReglasAnalisis] (
        [ReglaAnalisisId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NULL,
        [TipoFallaId] INT NOT NULL,
        [EquipoId] INT NULL,
        [MinutosMinimos] INT NOT NULL,
        [ResponsableRol] NVARCHAR(50) NULL,
        [ResponsableUsuarioId] NVARCHAR(450) NULL,
        [Activo] BIT NOT NULL CONSTRAINT [DF_ReglasAnalisis_Activo] DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_ReglasAnalisis] PRIMARY KEY ([ReglaAnalisisId])
    )
    PRINT 'Tabla mnt.ReglasAnalisis creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.TiemposProgramadosDia
-- Descripción: Excepciones de tiempo programado por línea y fecha
-- ============================================================================
IF OBJECT_ID(N'[mnt].[TiemposProgramadosDia]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[TiemposProgramadosDia] (
        [TiempoProgramadoDiaId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [Fecha] DATE NOT NULL,
        [Horas] DECIMAL(5,2) NOT NULL,
        [Motivo] NVARCHAR(200) NULL,
        [Origen] NVARCHAR(20) NOT NULL CONSTRAINT [DF_TiemposProgramadosDia_Origen] DEFAULT 'EXCEPCION',
        [Activo] BIT NOT NULL CONSTRAINT [DF_TiemposProgramadosDia_Activo] DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_TiemposProgramadosDia] PRIMARY KEY ([TiempoProgramadoDiaId])
    )
    PRINT 'Tabla mnt.TiemposProgramadosDia creada exitosamente.'
END
GO

-- ============================================================================
-- Detenciones: eliminación lógica y análisis de falla
-- ============================================================================
IF COL_LENGTH(N'mnt.Detenciones', N'Eliminada') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD [Eliminada] BIT NOT NULL CONSTRAINT [DF_Detenciones_Eliminada] DEFAULT 0
    PRINT 'Columna mnt.Detenciones.Eliminada creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'FechaHoraEliminacion') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [FechaHoraEliminacion] DATETIMEOFFSET NULL
    PRINT 'Columna mnt.Detenciones.FechaHoraEliminacion creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'EliminadaPorUsuarioId') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [EliminadaPorUsuarioId] NVARCHAR(450) NULL
    PRINT 'Columna mnt.Detenciones.EliminadaPorUsuarioId creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'MotivoEliminacion') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [MotivoEliminacion] NVARCHAR(500) NULL
    PRINT 'Columna mnt.Detenciones.MotivoEliminacion creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'RequiereAnalisis') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD [RequiereAnalisis] BIT NOT NULL CONSTRAINT [DF_Detenciones_RequiereAnalisis] DEFAULT 0
    PRINT 'Columna mnt.Detenciones.RequiereAnalisis creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'ReglaAnalisisId') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [ReglaAnalisisId] INT NULL
    PRINT 'Columna mnt.Detenciones.ReglaAnalisisId creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'ResponsableRol') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [ResponsableRol] NVARCHAR(50) NULL
    PRINT 'Columna mnt.Detenciones.ResponsableRol creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'ResponsableUsuarioId') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [ResponsableUsuarioId] NVARCHAR(450) NULL
    PRINT 'Columna mnt.Detenciones.ResponsableUsuarioId creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'CausaFalla') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [CausaFalla] NVARCHAR(2000) NULL
    PRINT 'Columna mnt.Detenciones.CausaFalla creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'PlanAccion') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [PlanAccion] NVARCHAR(2000) NULL
    PRINT 'Columna mnt.Detenciones.PlanAccion creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'AnalisisPorUsuarioId') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [AnalisisPorUsuarioId] NVARCHAR(450) NULL
    PRINT 'Columna mnt.Detenciones.AnalisisPorUsuarioId creada.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'FechaHoraAnalisis') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [FechaHoraAnalisis] DATETIMEOFFSET NULL
    PRINT 'Columna mnt.Detenciones.FechaHoraAnalisis creada.'
END
GO

-- ============================================================================
-- CHECK CONSTRAINTS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_ReglasAnalisis_MinutosMinimos')
BEGIN
    ALTER TABLE [mnt].[ReglasAnalisis]
    ADD CONSTRAINT [CK_ReglasAnalisis_MinutosMinimos] CHECK ([MinutosMinimos] >= 0)
    PRINT 'Check Constraint CK_ReglasAnalisis_MinutosMinimos creado.'
END
GO

-- Exactamente uno de los dos responsables
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_ReglasAnalisis_Responsable')
BEGIN
    ALTER TABLE [mnt].[ReglasAnalisis]
    ADD CONSTRAINT [CK_ReglasAnalisis_Responsable] CHECK (
        ([ResponsableRol] IS NULL AND [ResponsableUsuarioId] IS NOT NULL)
        OR ([ResponsableRol] IS NOT NULL AND [ResponsableUsuarioId] IS NULL))
    PRINT 'Check Constraint CK_ReglasAnalisis_Responsable creado.'
END
GO

-- CAST: en SQLite el decimal se guarda como texto; el CAST sirve en ambos motores.
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_TiemposProgramadosDia_Horas')
BEGIN
    ALTER TABLE [mnt].[TiemposProgramadosDia]
    ADD CONSTRAINT [CK_TiemposProgramadosDia_Horas] CHECK (CAST([Horas] AS FLOAT) >= 0 AND CAST([Horas] AS FLOAT) <= 24)
    PRINT 'Check Constraint CK_TiemposProgramadosDia_Horas creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_TiemposProgramadosDia_Origen')
BEGIN
    ALTER TABLE [mnt].[TiemposProgramadosDia]
    ADD CONSTRAINT [CK_TiemposProgramadosDia_Origen] CHECK ([Origen] IN ('EXCEPCION', 'HISTORICO'))
    PRINT 'Check Constraint CK_TiemposProgramadosDia_Origen creado.'
END
GO

-- ============================================================================
-- FOREIGN KEYS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ReglasAnalisis_Lineas_LineaId')
BEGIN
    ALTER TABLE [mnt].[ReglasAnalisis]
    ADD CONSTRAINT [FK_ReglasAnalisis_Lineas_LineaId] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_ReglasAnalisis_Lineas_LineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ReglasAnalisis_Equipos_EquipoId')
BEGIN
    ALTER TABLE [mnt].[ReglasAnalisis]
    ADD CONSTRAINT [FK_ReglasAnalisis_Equipos_EquipoId] FOREIGN KEY ([EquipoId])
    REFERENCES [mnt].[Equipos] ([EquipoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_ReglasAnalisis_Equipos_EquipoId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ReglasAnalisis_TiposFalla_TipoFallaId')
BEGIN
    ALTER TABLE [mnt].[ReglasAnalisis]
    ADD CONSTRAINT [FK_ReglasAnalisis_TiposFalla_TipoFallaId] FOREIGN KEY ([TipoFallaId])
    REFERENCES [mnt].[TiposFalla] ([TipoFallaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_ReglasAnalisis_TiposFalla_TipoFallaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TiemposProgramadosDia_Lineas_LineaId')
BEGIN
    ALTER TABLE [mnt].[TiemposProgramadosDia]
    ADD CONSTRAINT [FK_TiemposProgramadosDia_Lineas_LineaId] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_TiemposProgramadosDia_Lineas_LineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_ReglasAnalisis_ReglaAnalisisId')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [FK_Detenciones_ReglasAnalisis_ReglaAnalisisId] FOREIGN KEY ([ReglaAnalisisId])
    REFERENCES [mnt].[ReglasAnalisis] ([ReglaAnalisisId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Detenciones_ReglasAnalisis_ReglaAnalisisId creada.'
END
GO

-- ============================================================================
-- ÍNDICES
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_ReglaAnalisisId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_ReglaAnalisisId] ON [mnt].[Detenciones] ([ReglaAnalisisId])
    PRINT 'Índice IX_Detenciones_ReglaAnalisisId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ReglasAnalisis_LineaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReglasAnalisis_LineaId] ON [mnt].[ReglasAnalisis] ([LineaId])
    PRINT 'Índice IX_ReglasAnalisis_LineaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ReglasAnalisis_TipoFallaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReglasAnalisis_TipoFallaId] ON [mnt].[ReglasAnalisis] ([TipoFallaId])
    PRINT 'Índice IX_ReglasAnalisis_TipoFallaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ReglasAnalisis_EquipoId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ReglasAnalisis_EquipoId] ON [mnt].[ReglasAnalisis] ([EquipoId])
    PRINT 'Índice IX_ReglasAnalisis_EquipoId creado.'
END
GO

-- Una excepción por línea y fecha
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_TiemposProgramadosDia_LineaId_Fecha')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_TiemposProgramadosDia_LineaId_Fecha] ON [mnt].[TiemposProgramadosDia] ([LineaId], [Fecha])
    PRINT 'Índice UX_TiemposProgramadosDia_LineaId_Fecha creado.'
END
GO

-- ============================================================================
-- ESTADOS: PENDIENTE_JUSTIFICACION (PLC, Fase 2) es un estado válido del enum.
-- Los checks de script 05 no lo incluían y las columnas eran VARCHAR(20),
-- que no alcanza para sus 23 caracteres. Se amplían a NVARCHAR(30), igual que EF.
-- ============================================================================

-- SQL Server no permite ALTER COLUMN sobre una columna que tiene DEFAULT, CHECK o
-- índices que la usan (msg 5074). Por cada columna, en una sola transacción: se
-- capturan y eliminan esos objetos desde sys.*, se ALTERA la columna y se recrean con
-- la misma definición (mismo nombre de DEFAULT, mismas claves, INCLUDE, filtro y
-- unicidad). Los CHECK eliminados se vuelven a crear más abajo. max_length en bytes:
-- VARCHAR(20) = 20, NVARCHAR(30) = 60.
SET XACT_ABORT ON;

DECLARE @Tabla SYSNAME, @Columna SYSNAME, @ObjectId INT, @ColumnId INT, @Nulo BIT;
DECLARE @Antes NVARCHAR(MAX), @Alter NVARCHAR(MAX), @Despues NVARCHAR(MAX);
DECLARE @Indices TABLE ([Nombre] SYSNAME NOT NULL, [Definicion] NVARCHAR(MAX) NOT NULL);

DECLARE columnas_estado CURSOR LOCAL FAST_FORWARD FOR
    SELECT v.[Tabla], v.[Columna]
    FROM (VALUES (N'Detenciones', N'EstadoActual'),
                 (N'DetencionesEstadoHistorial', N'EstadoNuevo'),
                 (N'DetencionesEstadoHistorial', N'EstadoAnterior')) AS v ([Tabla], [Columna]);

OPEN columnas_estado;
FETCH NEXT FROM columnas_estado INTO @Tabla, @Columna;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ObjectId = OBJECT_ID(N'[mnt].' + QUOTENAME(@Tabla));
    SET @ColumnId = NULL;
    SELECT @ColumnId = [column_id], @Nulo = [is_nullable]
    FROM sys.columns
    WHERE [object_id] = @ObjectId AND [name] = @Columna AND [max_length] < 60;

    IF @ColumnId IS NOT NULL
    BEGIN
        DELETE FROM @Indices;
        INSERT INTO @Indices ([Nombre], [Definicion])
        SELECT i.[name],
               N'CREATE ' + CASE WHEN i.[is_unique] = 1 THEN N'UNIQUE ' ELSE N'' END
               + N'NONCLUSTERED INDEX ' + QUOTENAME(i.[name]) + N' ON [mnt].' + QUOTENAME(@Tabla)
               + N' (' + (SELECT STRING_AGG(QUOTENAME(c.[name]) + CASE WHEN ic.[is_descending_key] = 1 THEN N' DESC' ELSE N' ASC' END, N', ')
                                              WITHIN GROUP (ORDER BY ic.[key_ordinal])
                          FROM sys.index_columns ic
                          JOIN sys.columns c ON c.[object_id] = ic.[object_id] AND c.[column_id] = ic.[column_id]
                          WHERE ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id] AND ic.[is_included_column] = 0)
               + N')'
               + ISNULL(N' INCLUDE (' + (SELECT STRING_AGG(QUOTENAME(c.[name]), N', ') WITHIN GROUP (ORDER BY ic.[index_column_id])
                                          FROM sys.index_columns ic
                                          JOIN sys.columns c ON c.[object_id] = ic.[object_id] AND c.[column_id] = ic.[column_id]
                                          WHERE ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id] AND ic.[is_included_column] = 1)
                        + N')', N'')
               + ISNULL(N' WHERE ' + i.[filter_definition], N'')
        FROM sys.indexes i
        WHERE i.[object_id] = @ObjectId AND i.[type] = 2 AND i.[is_primary_key] = 0 AND i.[is_unique_constraint] = 0
          AND EXISTS (SELECT 1 FROM sys.index_columns ic
                      WHERE ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id] AND ic.[column_id] = @ColumnId);

        SET @Antes = N'';
        SET @Despues = N'';

        SELECT @Antes += N'DROP INDEX ' + QUOTENAME([Nombre]) + N' ON [mnt].' + QUOTENAME(@Tabla) + N';'
        FROM @Indices;

        SELECT @Antes += N'ALTER TABLE [mnt].' + QUOTENAME(@Tabla) + N' DROP CONSTRAINT ' + QUOTENAME(dc.[name]) + N';',
               @Despues += N'ALTER TABLE [mnt].' + QUOTENAME(@Tabla) + N' ADD CONSTRAINT ' + QUOTENAME(dc.[name])
                         + N' DEFAULT ' + dc.[definition] + N' FOR ' + QUOTENAME(@Columna) + N';'
        FROM sys.default_constraints dc
        WHERE dc.[parent_object_id] = @ObjectId AND dc.[parent_column_id] = @ColumnId;

        SELECT @Antes += N'ALTER TABLE [mnt].' + QUOTENAME(@Tabla) + N' DROP CONSTRAINT ' + QUOTENAME(cc.[name]) + N';'
        FROM sys.check_constraints cc
        WHERE cc.[parent_object_id] = @ObjectId AND CHARINDEX(QUOTENAME(@Columna), cc.[definition]) > 0;

        SELECT @Despues += [Definicion] + N';'
        FROM @Indices;

        SET @Alter = N'ALTER TABLE [mnt].' + QUOTENAME(@Tabla) + N' ALTER COLUMN ' + QUOTENAME(@Columna)
                   + N' NVARCHAR(30) ' + CASE WHEN @Nulo = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';';

        BEGIN TRANSACTION;
        EXEC (@Antes);
        EXEC (@Alter);
        EXEC (@Despues);
        COMMIT TRANSACTION;

        PRINT CONCAT(N'Columna mnt.', @Tabla, N'.', @Columna, N' ampliada a NVARCHAR(30).');
    END

    FETCH NEXT FROM columnas_estado INTO @Tabla, @Columna;
END

CLOSE columnas_estado;
DEALLOCATE columnas_estado;
SET XACT_ABORT OFF;
GO

IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_EstadoActual'
           AND definition NOT LIKE N'%PENDIENTE_JUSTIFICACION%')
BEGIN
    ALTER TABLE [mnt].[Detenciones] DROP CONSTRAINT [CK_Detenciones_EstadoActual]
    PRINT 'Check Constraint CK_Detenciones_EstadoActual eliminado (versión sin PENDIENTE_JUSTIFICACION).'
END
GO

IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_DetencionesEstadoHistorial_EstadoNuevo'
           AND definition NOT LIKE N'%PENDIENTE_JUSTIFICACION%')
BEGIN
    ALTER TABLE [mnt].[DetencionesEstadoHistorial] DROP CONSTRAINT [CK_DetencionesEstadoHistorial_EstadoNuevo]
    PRINT 'Check Constraint CK_DetencionesEstadoHistorial_EstadoNuevo eliminado (versión sin PENDIENTE_JUSTIFICACION).'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_EstadoActual')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [CK_Detenciones_EstadoActual] CHECK ([EstadoActual] IN ('REGISTRADA', 'EN_SEGUIMIENTO', 'CERRADA', 'PENDIENTE_JUSTIFICACION'))
    PRINT 'Check Constraint CK_Detenciones_EstadoActual creado (con PENDIENTE_JUSTIFICACION).'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_DetencionesEstadoHistorial_EstadoNuevo')
BEGIN
    ALTER TABLE [mnt].[DetencionesEstadoHistorial]
    ADD CONSTRAINT [CK_DetencionesEstadoHistorial_EstadoNuevo] CHECK ([EstadoNuevo] IN ('REGISTRADA', 'EN_SEGUIMIENTO', 'CERRADA', 'PENDIENTE_JUSTIFICACION'))
    PRINT 'Check Constraint CK_DetencionesEstadoHistorial_EstadoNuevo creado (con PENDIENTE_JUSTIFICACION).'
END
GO

-- ============================================================================
-- VISTAS: excluyen detenciones eliminadas (Eliminada = 1).
-- Copia exacta de 06_create_views.sql con sólo el filtro agregado.
-- Ningún código de la aplicación lee estas vistas; se mantienen para reportes SQL.
-- ============================================================================

-- Vista: VW_DetencionesDetalle (excluye eliminadas)
CREATE OR ALTER VIEW [mnt].[VW_DetencionesDetalle]
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
    -- Indicador si esta abierto
    CASE WHEN d.[EstadoActual] != 'CERRADA' THEN 1 ELSE 0 END AS [EstaAbierta],
    -- Cantidad de notas
    (SELECT COUNT(*) FROM [mnt].[DetencionesNotas] dn WHERE dn.[DetencionId] = d.[DetencionId]) AS [CantidadNotas]
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
LEFT JOIN [mnt].[DetallesFalla] df ON d.[DetalleFallaId] = df.[DetalleFallaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] uc ON d.[CerradoPorUsuarioId] = uc.[Id]
LEFT JOIN [dbo].[AspNetUsers] ucr ON d.[CreadoPorUsuarioId] = ucr.[Id]
LEFT JOIN [dbo].[AspNetUsers] umo ON d.[ModificadoPorUsuarioId] = umo.[Id]
WHERE d.[Eliminada] = 0
GO

PRINT 'Vista VW_DetencionesDetalle actualizada (T-76).'
GO

-- Vista: VW_DetencionesAbiertas (excluye eliminadas)
CREATE OR ALTER VIEW [mnt].[VW_DetencionesAbiertas]
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
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] u ON d.[CreadoPorUsuarioId] = u.[Id]
WHERE d.[EstadoActual] != 'CERRADA'
  AND d.[Eliminada] = 0
GO

PRINT 'Vista VW_DetencionesAbiertas actualizada (T-76).'
GO

-- Vista: VW_DetencionesIndicadores (excluye eliminadas)
CREATE OR ALTER VIEW [mnt].[VW_DetencionesIndicadores]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[Fecha],
    t.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    COUNT(*) AS [TotalDetenciones],
    SUM(CASE WHEN d.[EstadoActual] = 'REGISTRADA' THEN 1 ELSE 0 END) AS [DetencionesRegistradas],
    SUM(CASE WHEN d.[EstadoActual] = 'EN_SEGUIMIENTO' THEN 1 ELSE 0 END) AS [DetencionesEnSeguimiento],
    SUM(CASE WHEN d.[EstadoActual] = 'CERRADA' THEN 1 ELSE 0 END) AS [DetencionesCerradas],
    AVG(CAST(d.[MinutosEfectivos] AS FLOAT)) AS [DuracionPromedioMinutos],
    SUM(d.[MinutosEfectivos]) AS [TiempoTotalParadaMinutos]
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[Fecha], t.[TurnoId], t.[Nombre]
GO

PRINT 'Vista VW_DetencionesIndicadores actualizada (T-76).'
GO

-- Vista: VW_ResumenTurnoActual (excluye eliminadas)
CREATE OR ALTER VIEW [mnt].[VW_ResumenTurnoActual]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    COUNT(*) AS [TotalDetenciones],
    SUM(d.[MinutosEfectivos]) AS [MinutosTotales],
    SUM(CASE WHEN d.[EstadoActual] != 'CERRADA' THEN 1 ELSE 0 END) AS [DetencionesAbiertas]
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Fecha] = CAST(GETDATE() AS DATE)
  AND d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[TurnoId], t.[Nombre], d.[Fecha]
GO

PRINT 'Vista VW_ResumenTurnoActual actualizada (T-76).'
GO

PRINT 'Script 17_t76_ajustes.sql completado.'
GO
