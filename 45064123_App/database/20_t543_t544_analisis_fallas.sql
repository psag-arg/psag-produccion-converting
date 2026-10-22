-- ==============================================================================
-- Script: 20_t543_t544_analisis_fallas.sql
-- Descripción: T-543 historial de ediciones de análisis cerrados (Base de soluciones)
--              y T-544 fotos adjuntas al análisis de falla.
--              Las fotos se guardan en disco (Adjuntos:Carpeta del appsettings de la API);
--              acá solo queda el registro con el nombre generado del archivo.
-- Versión: 1.0 - T-543 / T-544
-- Requiere: 17_t76_ajustes.sql
-- Idempotente: se puede ejecutar más de una vez.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Tabla: mnt.AnalisisCambios
-- Descripción: Historial de ediciones de un análisis cerrado (solo inserción).
--              Un registro por campo cambiado o por foto agregada.
-- ============================================================================
IF OBJECT_ID(N'[mnt].[AnalisisCambios]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[AnalisisCambios] (
        [AnalisisCambioId] BIGINT IDENTITY(1,1) NOT NULL,
        [DetencionId] BIGINT NOT NULL,
        [Campo] NVARCHAR(20) NOT NULL,
        [ValorAnterior] NVARCHAR(4000) NULL,
        [ValorNuevo] NVARCHAR(4000) NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_AnalisisCambios_FechaHora] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_AnalisisCambios] PRIMARY KEY ([AnalisisCambioId])
    )
    PRINT 'Tabla mnt.AnalisisCambios creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.AnalisisAdjuntos
-- Descripción: Fotos (JPG/PNG) del análisis de falla (solo inserción).
--              NombreArchivo es el nombre generado en disco (GUID + extensión).
-- ============================================================================
IF OBJECT_ID(N'[mnt].[AnalisisAdjuntos]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[AnalisisAdjuntos] (
        [AnalisisAdjuntoId] BIGINT IDENTITY(1,1) NOT NULL,
        [DetencionId] BIGINT NOT NULL,
        [NombreArchivo] NVARCHAR(100) NOT NULL,
        [NombreOriginal] NVARCHAR(255) NOT NULL,
        [ContentType] NVARCHAR(50) NOT NULL,
        [TamanoBytes] BIGINT NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_AnalisisAdjuntos_FechaHora] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_AnalisisAdjuntos] PRIMARY KEY ([AnalisisAdjuntoId])
    )
    PRINT 'Tabla mnt.AnalisisAdjuntos creada exitosamente.'
END
GO

-- ============================================================================
-- CHECK CONSTRAINTS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_AnalisisCambios_Campo')
BEGIN
    ALTER TABLE [mnt].[AnalisisCambios]
    ADD CONSTRAINT [CK_AnalisisCambios_Campo] CHECK ([Campo] IN ('SOLUCION', 'CAUSA', 'PLAN_ACCION', 'FOTO'))
    PRINT 'Check Constraint CK_AnalisisCambios_Campo creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_AnalisisAdjuntos_ContentType')
BEGIN
    ALTER TABLE [mnt].[AnalisisAdjuntos]
    ADD CONSTRAINT [CK_AnalisisAdjuntos_ContentType] CHECK ([ContentType] IN ('image/jpeg', 'image/png'))
    PRINT 'Check Constraint CK_AnalisisAdjuntos_ContentType creado.'
END
GO

-- Máximo 10 MB por foto (10 * 1024 * 1024)
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_AnalisisAdjuntos_TamanoBytes')
BEGIN
    ALTER TABLE [mnt].[AnalisisAdjuntos]
    ADD CONSTRAINT [CK_AnalisisAdjuntos_TamanoBytes] CHECK ([TamanoBytes] > 0 AND [TamanoBytes] <= 10485760)
    PRINT 'Check Constraint CK_AnalisisAdjuntos_TamanoBytes creado.'
END
GO

-- ============================================================================
-- FOREIGN KEYS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_AnalisisCambios_Detenciones_DetencionId')
BEGIN
    ALTER TABLE [mnt].[AnalisisCambios]
    ADD CONSTRAINT [FK_AnalisisCambios_Detenciones_DetencionId] FOREIGN KEY ([DetencionId])
    REFERENCES [mnt].[Detenciones] ([DetencionId])
    ON DELETE CASCADE
    PRINT 'Foreign Key FK_AnalisisCambios_Detenciones_DetencionId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_AnalisisAdjuntos_Detenciones_DetencionId')
BEGIN
    ALTER TABLE [mnt].[AnalisisAdjuntos]
    ADD CONSTRAINT [FK_AnalisisAdjuntos_Detenciones_DetencionId] FOREIGN KEY ([DetencionId])
    REFERENCES [mnt].[Detenciones] ([DetencionId])
    ON DELETE CASCADE
    PRINT 'Foreign Key FK_AnalisisAdjuntos_Detenciones_DetencionId creada.'
END
GO

-- ============================================================================
-- ÍNDICES
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AnalisisCambios_DetencionId' AND object_id = OBJECT_ID(N'[mnt].[AnalisisCambios]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AnalisisCambios_DetencionId]
    ON [mnt].[AnalisisCambios] ([DetencionId])
    PRINT 'Índice IX_AnalisisCambios_DetencionId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AnalisisAdjuntos_DetencionId' AND object_id = OBJECT_ID(N'[mnt].[AnalisisAdjuntos]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AnalisisAdjuntos_DetencionId]
    ON [mnt].[AnalisisAdjuntos] ([DetencionId])
    PRINT 'Índice IX_AnalisisAdjuntos_DetencionId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AnalisisAdjuntos_NombreArchivo' AND object_id = OBJECT_ID(N'[mnt].[AnalisisAdjuntos]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AnalisisAdjuntos_NombreArchivo]
    ON [mnt].[AnalisisAdjuntos] ([NombreArchivo])
    PRINT 'Índice IX_AnalisisAdjuntos_NombreArchivo creado.'
END
GO

PRINT 'Script 20_t543_t544_analisis_fallas.sql completado.'
GO
