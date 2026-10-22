-- ==============================================================================
-- Script: 22_t546_t547_tareas_mantenimiento.sql
-- Descripción: T-546/T-547. Tareas de mantenimiento relevadas en la máquina:
--              el operador las informa (PENDIENTE), mantenimiento/supervisión
--              las programa (PROGRAMADA) y las cierra (CERRADA). Las cerradas
--              no se borran: quedan en el historial.
-- Idempotente: se puede ejecutar más de una vez.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Tabla: mnt.TareasMantenimiento
-- ============================================================================
IF OBJECT_ID(N'[mnt].[TareasMantenimiento]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[TareasMantenimiento] (
        [TareaMantenimientoId] BIGINT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [EquipoId] INT NOT NULL,
        [ParteEquipo] NVARCHAR(200) NULL,
        [Detalle] NVARCHAR(2000) NOT NULL,
        [TipoTarea] NVARCHAR(20) NOT NULL,
        [Criticidad] NVARCHAR(10) NOT NULL,
        [Estado] NVARCHAR(20) NOT NULL,
        [FechaHoraProgramacion] DATETIMEOFFSET NULL,
        [ProgramadaPorUsuarioId] NVARCHAR(450) NULL,
        [FechaHoraCierre] DATETIMEOFFSET NULL,
        [CerradaPorUsuarioId] NVARCHAR(450) NULL,
        [RowVersion] ROWVERSION NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_TareasMantenimiento] PRIMARY KEY ([TareaMantenimientoId])
    )
    PRINT 'Tabla mnt.TareasMantenimiento creada exitosamente.'
END
GO

-- ============================================================================
-- CHECK CONSTRAINTS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_TareasMantenimiento_TipoTarea')
BEGIN
    ALTER TABLE [mnt].[TareasMantenimiento]
    ADD CONSTRAINT [CK_TareasMantenimiento_TipoTarea] CHECK ([TipoTarea] IN ('MECANICA', 'ELECTRICA', 'ELECTRONICA'))
    PRINT 'Check Constraint CK_TareasMantenimiento_TipoTarea creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_TareasMantenimiento_Criticidad')
BEGIN
    ALTER TABLE [mnt].[TareasMantenimiento]
    ADD CONSTRAINT [CK_TareasMantenimiento_Criticidad] CHECK ([Criticidad] IN ('ALTA', 'MEDIA', 'BAJA'))
    PRINT 'Check Constraint CK_TareasMantenimiento_Criticidad creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_TareasMantenimiento_Estado')
BEGIN
    ALTER TABLE [mnt].[TareasMantenimiento]
    ADD CONSTRAINT [CK_TareasMantenimiento_Estado] CHECK ([Estado] IN ('PENDIENTE', 'PROGRAMADA', 'CERRADA'))
    PRINT 'Check Constraint CK_TareasMantenimiento_Estado creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_TareasMantenimiento_FechaCierre')
BEGIN
    ALTER TABLE [mnt].[TareasMantenimiento]
    ADD CONSTRAINT [CK_TareasMantenimiento_FechaCierre] CHECK ([FechaHoraCierre] IS NULL OR [FechaHoraCierre] >= [FechaHoraCreacion])
    PRINT 'Check Constraint CK_TareasMantenimiento_FechaCierre creado.'
END
GO

-- ============================================================================
-- FOREIGN KEYS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TareasMantenimiento_Lineas_LineaId')
BEGIN
    ALTER TABLE [mnt].[TareasMantenimiento]
    ADD CONSTRAINT [FK_TareasMantenimiento_Lineas_LineaId] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_TareasMantenimiento_Lineas_LineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TareasMantenimiento_Equipos_EquipoId')
BEGIN
    ALTER TABLE [mnt].[TareasMantenimiento]
    ADD CONSTRAINT [FK_TareasMantenimiento_Equipos_EquipoId] FOREIGN KEY ([EquipoId])
    REFERENCES [mnt].[Equipos] ([EquipoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_TareasMantenimiento_Equipos_EquipoId creada.'
END
GO

-- ============================================================================
-- ÍNDICES
-- ============================================================================

-- Grilla activa: Estado <> CERRADA, filtrada por línea
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TareasMantenimiento_Estado_LineaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TareasMantenimiento_Estado_LineaId] ON [mnt].[TareasMantenimiento] ([Estado], [LineaId])
    PRINT 'Índice IX_TareasMantenimiento_Estado_LineaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TareasMantenimiento_EquipoId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TareasMantenimiento_EquipoId] ON [mnt].[TareasMantenimiento] ([EquipoId])
    PRINT 'Índice IX_TareasMantenimiento_EquipoId creado.'
END
GO

-- Historial: orden y filtro por fecha de cierre
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TareasMantenimiento_FechaHoraCierre')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TareasMantenimiento_FechaHoraCierre] ON [mnt].[TareasMantenimiento] ([FechaHoraCierre])
    PRINT 'Índice IX_TareasMantenimiento_FechaHoraCierre creado.'
END
GO

PRINT 'Script 22_t546_t547_tareas_mantenimiento.sql ejecutado.'
GO
