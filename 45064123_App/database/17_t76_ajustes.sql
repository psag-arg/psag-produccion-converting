-- ==============================================================================
-- Script: 17_t76_ajustes.sql
-- Descripción: Ajustes T-76 Converting. Eliminación lógica de detenciones,
--              análisis de falla (reglas y responsables), criticidad de equipos,
--              color de tipos de falla y tiempo programado por día.
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

PRINT 'Script 17_t76_ajustes.sql completado.'
GO
