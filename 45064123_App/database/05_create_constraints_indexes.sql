-- ==============================================================================
-- Script: 05_create_constraints_indexes.sql
-- Descripción: Crea constraints, foreign keys e índices
-- Versión: 2.0 - Fase 1 (Detenciones con modelo de tiempo)
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- UNIQUE CONSTRAINTS
-- ============================================================================

-- Lineas: Nombre único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_Lineas_Nombre')
BEGIN
    ALTER TABLE [mnt].[Lineas] ADD CONSTRAINT [UQ_Lineas_Nombre] UNIQUE ([Nombre])
    PRINT 'Constraint UQ_Lineas_Nombre creado.'
END
GO

-- Lineas: Código único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_Lineas_Codigo')
BEGIN
    ALTER TABLE [mnt].[Lineas] ADD CONSTRAINT [UQ_Lineas_Codigo] UNIQUE ([Codigo])
    PRINT 'Constraint UQ_Lineas_Codigo creado.'
END
GO

-- TiposFalla: Código único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_TiposFalla_Codigo')
BEGIN
    ALTER TABLE [mnt].[TiposFalla] ADD CONSTRAINT [UQ_TiposFalla_Codigo] UNIQUE ([Codigo])
    PRINT 'Constraint UQ_TiposFalla_Codigo creado.'
END
GO

-- DetallesFalla: Código único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_DetallesFalla_Codigo')
BEGIN
    ALTER TABLE [mnt].[DetallesFalla] ADD CONSTRAINT [UQ_DetallesFalla_Codigo] UNIQUE ([Codigo])
    PRINT 'Constraint UQ_DetallesFalla_Codigo creado.'
END
GO

-- Turnos: Código único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_Turnos_Codigo')
BEGIN
    ALTER TABLE [mnt].[Turnos] ADD CONSTRAINT [UQ_Turnos_Codigo] UNIQUE ([Codigo])
    PRINT 'Constraint UQ_Turnos_Codigo creado.'
END
GO

-- Detenciones: Código único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_Detenciones_Codigo')
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD CONSTRAINT [UQ_Detenciones_Codigo] UNIQUE ([Codigo])
    PRINT 'Constraint UQ_Detenciones_Codigo creado.'
END
GO

-- Equipos: Código único por línea
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_Equipos_Linea_Codigo')
BEGIN
    ALTER TABLE [mnt].[Equipos] ADD CONSTRAINT [UQ_Equipos_Linea_Codigo] UNIQUE ([LineaId], [Codigo])
    PRINT 'Constraint UQ_Equipos_Linea_Codigo creado.'
END
GO

-- UsuariosLineas: Usuario-Línea único
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UQ_UsuariosLineas_Usuario_Linea')
BEGIN
    ALTER TABLE [mnt].[UsuariosLineas] ADD CONSTRAINT [UQ_UsuariosLineas_Usuario_Linea] UNIQUE ([UsuarioId], [LineaId])
    PRINT 'Constraint UQ_UsuariosLineas_Usuario_Linea creado.'
END
GO

-- ============================================================================
-- FOREIGN KEYS
-- ============================================================================

-- Equipos -> Lineas
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Equipos_Lineas')
BEGIN
    ALTER TABLE [mnt].[Equipos]
    ADD CONSTRAINT [FK_Equipos_Lineas] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Equipos_Lineas creada.'
END
GO

-- DetallesFalla -> Equipos (opcional)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetallesFalla_Equipos')
BEGIN
    ALTER TABLE [mnt].[DetallesFalla]
    ADD CONSTRAINT [FK_DetallesFalla_Equipos] FOREIGN KEY ([EquipoId])
    REFERENCES [mnt].[Equipos] ([EquipoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_DetallesFalla_Equipos creada.'
END
GO

-- DetallesFalla -> TiposFalla (opcional)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetallesFalla_TiposFalla')
BEGIN
    ALTER TABLE [mnt].[DetallesFalla]
    ADD CONSTRAINT [FK_DetallesFalla_TiposFalla] FOREIGN KEY ([TipoFallaId])
    REFERENCES [mnt].[TiposFalla] ([TipoFallaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_DetallesFalla_TiposFalla creada.'
END
GO

-- Detenciones -> Lineas
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Lineas')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [FK_Detenciones_Lineas] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Detenciones_Lineas creada.'
END
GO

-- Detenciones -> Equipos
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Equipos')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [FK_Detenciones_Equipos] FOREIGN KEY ([EquipoId])
    REFERENCES [mnt].[Equipos] ([EquipoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Detenciones_Equipos creada.'
END
GO

-- Detenciones -> TiposFalla
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_TiposFalla')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [FK_Detenciones_TiposFalla] FOREIGN KEY ([TipoFallaId])
    REFERENCES [mnt].[TiposFalla] ([TipoFallaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Detenciones_TiposFalla creada.'
END
GO

-- Detenciones -> DetallesFalla
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_DetallesFalla')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [FK_Detenciones_DetallesFalla] FOREIGN KEY ([DetalleFallaId])
    REFERENCES [mnt].[DetallesFalla] ([DetalleFallaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Detenciones_DetallesFalla creada.'
END
GO

-- Detenciones -> Turnos
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_Turnos')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [FK_Detenciones_Turnos] FOREIGN KEY ([TurnoId])
    REFERENCES [mnt].[Turnos] ([TurnoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_Detenciones_Turnos creada.'
END
GO

-- DetencionesEstadoHistorial -> Detenciones
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetencionesEstadoHistorial_Detenciones')
BEGIN
    ALTER TABLE [mnt].[DetencionesEstadoHistorial]
    ADD CONSTRAINT [FK_DetencionesEstadoHistorial_Detenciones] FOREIGN KEY ([DetencionId])
    REFERENCES [mnt].[Detenciones] ([DetencionId])
    ON DELETE CASCADE
    PRINT 'Foreign Key FK_DetencionesEstadoHistorial_Detenciones creada.'
END
GO

-- DetencionesNotas -> Detenciones
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_DetencionesNotas_Detenciones')
BEGIN
    ALTER TABLE [mnt].[DetencionesNotas]
    ADD CONSTRAINT [FK_DetencionesNotas_Detenciones] FOREIGN KEY ([DetencionId])
    REFERENCES [mnt].[Detenciones] ([DetencionId])
    ON DELETE CASCADE
    PRINT 'Foreign Key FK_DetencionesNotas_Detenciones creada.'
END
GO

-- UsuariosLineas -> Lineas
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_UsuariosLineas_Lineas')
BEGIN
    ALTER TABLE [mnt].[UsuariosLineas]
    ADD CONSTRAINT [FK_UsuariosLineas_Lineas] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE CASCADE
    PRINT 'Foreign Key FK_UsuariosLineas_Lineas creada.'
END
GO

-- ============================================================================
-- CHECK CONSTRAINTS
-- ============================================================================

-- Detenciones: Estado válido
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_EstadoActual')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [CK_Detenciones_EstadoActual] CHECK ([EstadoActual] IN ('REGISTRADA', 'EN_SEGUIMIENTO', 'CERRADA'))
    PRINT 'Check Constraint CK_Detenciones_EstadoActual creado.'
END
GO

-- Detenciones: OrigenTiempo válido
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_OrigenTiempo')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [CK_Detenciones_OrigenTiempo] CHECK ([OrigenTiempo] IN ('DECLARADO', 'CALCULADO', 'PLC'))
    PRINT 'Check Constraint CK_Detenciones_OrigenTiempo creado.'
END
GO

-- Detenciones: Fecha de cierre no anterior a creación
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_FechaCierre')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [CK_Detenciones_FechaCierre] CHECK ([FechaHoraCierre] IS NULL OR [FechaHoraCierre] >= [FechaHoraCreacion])
    PRINT 'Check Constraint CK_Detenciones_FechaCierre creado.'
END
GO

-- Detenciones: MinutosEfectivos positivo
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_MinutosEfectivos')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
    ADD CONSTRAINT [CK_Detenciones_MinutosEfectivos] CHECK ([MinutosEfectivos] >= 0)
    PRINT 'Check Constraint CK_Detenciones_MinutosEfectivos creado.'
END
GO

-- DetencionesEstadoHistorial: Estados válidos
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_DetencionesEstadoHistorial_EstadoNuevo')
BEGIN
    ALTER TABLE [mnt].[DetencionesEstadoHistorial]
    ADD CONSTRAINT [CK_DetencionesEstadoHistorial_EstadoNuevo] CHECK ([EstadoNuevo] IN ('REGISTRADA', 'EN_SEGUIMIENTO', 'CERRADA'))
    PRINT 'Check Constraint CK_DetencionesEstadoHistorial_EstadoNuevo creado.'
END
GO

-- DetencionesNotas: Tipo de nota válido
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_DetencionesNotas_TipoNota')
BEGIN
    ALTER TABLE [mnt].[DetencionesNotas]
    ADD CONSTRAINT [CK_DetencionesNotas_TipoNota] CHECK ([TipoNota] IN ('Operativa', 'Tecnica', 'Cierre'))
    PRINT 'Check Constraint CK_DetencionesNotas_TipoNota creado.'
END
GO

-- ============================================================================
-- ÍNDICES
-- ============================================================================

-- Lineas
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Lineas_Activo')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Lineas_Activo] ON [mnt].[Lineas] ([Activo]) INCLUDE ([Nombre], [Orden])
    PRINT 'Índice IX_Lineas_Activo creado.'
END
GO

-- Equipos
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Equipos_LineaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Equipos_LineaId] ON [mnt].[Equipos] ([LineaId]) INCLUDE ([Nombre], [Activo], [Orden])
    PRINT 'Índice IX_Equipos_LineaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Equipos_Activo')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Equipos_Activo] ON [mnt].[Equipos] ([Activo]) INCLUDE ([Nombre], [LineaId])
    PRINT 'Índice IX_Equipos_Activo creado.'
END
GO

-- TiposFalla
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TiposFalla_Activo')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TiposFalla_Activo] ON [mnt].[TiposFalla] ([Activo]) INCLUDE ([Nombre], [Orden])
    PRINT 'Índice IX_TiposFalla_Activo creado.'
END
GO

-- DetallesFalla
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_DetallesFalla_Activo')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DetallesFalla_Activo] ON [mnt].[DetallesFalla] ([Activo]) INCLUDE ([Nombre], [Orden])
    PRINT 'Índice IX_DetallesFalla_Activo creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_DetallesFalla_EquipoId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DetallesFalla_EquipoId] ON [mnt].[DetallesFalla] ([EquipoId])
    PRINT 'Índice IX_DetallesFalla_EquipoId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_DetallesFalla_TipoFallaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DetallesFalla_TipoFallaId] ON [mnt].[DetallesFalla] ([TipoFallaId])
    PRINT 'Índice IX_DetallesFalla_TipoFallaId creado.'
END
GO

-- Turnos
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Turnos_Activo')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Turnos_Activo] ON [mnt].[Turnos] ([Activo]) INCLUDE ([Nombre], [Orden])
    PRINT 'Índice IX_Turnos_Activo creado.'
END
GO

-- Detenciones
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_EstadoActual')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_EstadoActual] ON [mnt].[Detenciones] ([EstadoActual]) INCLUDE ([Codigo], [LineaId], [Fecha])
    PRINT 'Índice IX_Detenciones_EstadoActual creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_Fecha')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_Fecha] ON [mnt].[Detenciones] ([Fecha] DESC) INCLUDE ([Codigo], [EstadoActual], [LineaId])
    PRINT 'Índice IX_Detenciones_Fecha creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_LineaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_LineaId] ON [mnt].[Detenciones] ([LineaId]) INCLUDE ([Codigo], [EstadoActual], [Fecha])
    PRINT 'Índice IX_Detenciones_LineaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_LineaId_EstadoActual')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_LineaId_EstadoActual] ON [mnt].[Detenciones] ([LineaId], [EstadoActual]) INCLUDE ([Codigo], [Fecha])
    PRINT 'Índice IX_Detenciones_LineaId_EstadoActual creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_TipoFallaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_TipoFallaId] ON [mnt].[Detenciones] ([TipoFallaId])
    PRINT 'Índice IX_Detenciones_TipoFallaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_DetalleFallaId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_DetalleFallaId] ON [mnt].[Detenciones] ([DetalleFallaId])
    PRINT 'Índice IX_Detenciones_DetalleFallaId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_EquipoId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_EquipoId] ON [mnt].[Detenciones] ([EquipoId])
    PRINT 'Índice IX_Detenciones_EquipoId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_TurnoId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_TurnoId] ON [mnt].[Detenciones] ([TurnoId])
    PRINT 'Índice IX_Detenciones_TurnoId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_Linea_Turno_Fecha')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Detenciones_Linea_Turno_Fecha] ON [mnt].[Detenciones] ([LineaId], [TurnoId], [Fecha]) INCLUDE ([Codigo], [EstadoActual], [MinutosEfectivos])
    PRINT 'Índice IX_Detenciones_Linea_Turno_Fecha creado.'
END
GO

-- DetencionesEstadoHistorial
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_DetencionesEstadoHistorial_DetencionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DetencionesEstadoHistorial_DetencionId] ON [mnt].[DetencionesEstadoHistorial] ([DetencionId]) INCLUDE ([FechaHora], [EstadoNuevo])
    PRINT 'Índice IX_DetencionesEstadoHistorial_DetencionId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_DetencionesEstadoHistorial_FechaHora')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DetencionesEstadoHistorial_FechaHora] ON [mnt].[DetencionesEstadoHistorial] ([FechaHora] DESC)
    PRINT 'Índice IX_DetencionesEstadoHistorial_FechaHora creado.'
END
GO

-- DetencionesNotas
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_DetencionesNotas_DetencionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DetencionesNotas_DetencionId] ON [mnt].[DetencionesNotas] ([DetencionId]) INCLUDE ([FechaHora], [TipoNota])
    PRINT 'Índice IX_DetencionesNotas_DetencionId creado.'
END
GO

-- UsuariosLineas
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_UsuariosLineas_UsuarioId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_UsuariosLineas_UsuarioId] ON [mnt].[UsuariosLineas] ([UsuarioId]) INCLUDE ([LineaId])
    PRINT 'Índice IX_UsuariosLineas_UsuarioId creado.'
END
GO

-- Auditoria
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Auditoria_FechaHora')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Auditoria_FechaHora] ON [mnt].[Auditoria] ([FechaHora] DESC)
    PRINT 'Índice IX_Auditoria_FechaHora creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Auditoria_UsuarioId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Auditoria_UsuarioId] ON [mnt].[Auditoria] ([UsuarioId])
    PRINT 'Índice IX_Auditoria_UsuarioId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Auditoria_Entidad')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Auditoria_Entidad] ON [mnt].[Auditoria] ([Entidad])
    PRINT 'Índice IX_Auditoria_Entidad creado.'
END
GO

PRINT 'Todos los constraints e índices creados exitosamente.'
GO
