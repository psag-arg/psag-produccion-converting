-- ==============================================================================
-- Script: 23_t611_calidad_defectos.sql
-- Descripción: T-611 Control de Calidad por cámaras. Registro de defectos con
--              imagen y revisión humana (pendiente / confirmado / falso positivo).
--              Tablas: mnt.PuntosControlCalidad (cámaras por línea),
--              mnt.TiposDefectoCalidad (maestro de defectos), mnt.RegistrosCalidad
--              (cada detección) y mnt.RegistrosCalidadRevisiones (historial).
--              Ante un defecto el sistema SOLO registra: no rechaza ni alerta.
--              Las imágenes van a disco (Calidad:CarpetaImagenes del appsettings de
--              la API); acá solo queda el nombre generado del archivo.
-- Versión: 1.0 - T-611
-- Requiere: 04_create_tables.sql, 11_create_tables_proceso.sql (Productos)
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga datos: los maestros arrancan vacíos (ver el bloque comentado al final
-- y docs/CALIDAD.md). Los datos de prueba viven solo en los tests (SQLite).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- Los índices únicos filtrados (WHERE ... IS NOT NULL) exigen estas opciones; sqlcmd
-- arranca con QUOTED_IDENTIFIER OFF.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ============================================================================
-- Tabla: mnt.PuntosControlCalidad
-- Descripción: Puntos de control de calidad (cámaras) por línea. El detector
--              se identifica con Codigo (único, en mayúsculas).
-- ============================================================================
IF OBJECT_ID(N'[mnt].[PuntosControlCalidad]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PuntosControlCalidad] (
        [PuntoControlCalidadId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Activo] BIT NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_PuntosControlCalidad] PRIMARY KEY ([PuntoControlCalidadId])
    )
    PRINT 'Tabla mnt.PuntosControlCalidad creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.TiposDefectoCalidad
-- Descripción: Tipos de defecto (código, nombre, descripción, unidad opcional, activo).
-- ============================================================================
IF OBJECT_ID(N'[mnt].[TiposDefectoCalidad]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[TiposDefectoCalidad] (
        [TipoDefectoCalidadId] INT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [UnidadMedida] NVARCHAR(20) NULL,
        [Activo] BIT NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_TiposDefectoCalidad] PRIMARY KEY ([TipoDefectoCalidadId])
    )
    PRINT 'Tabla mnt.TiposDefectoCalidad creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.RegistrosCalidad
-- Descripción: Cada detección de calidad (automática del detector o manual).
--              Nada se borra: la revisión cambia el estado y deja historial.
-- ============================================================================
IF OBJECT_ID(N'[mnt].[RegistrosCalidad]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[RegistrosCalidad] (
        [RegistroCalidadId] BIGINT IDENTITY(1,1) NOT NULL,
        [FechaHoraDeteccion] DATETIMEOFFSET NOT NULL,
        [LineaId] INT NOT NULL,
        [PuntoControlCalidadId] INT NOT NULL,
        [TurnoId] INT NULL,
        [TipoDefectoCalidadId] INT NOT NULL,
        [Origen] NVARCHAR(20) NOT NULL,
        [Confianza] DECIMAL(5,4) NULL,
        [ValorMedido] DECIMAL(18,4) NULL,
        [UnidadMedida] NVARCHAR(20) NULL,
        [ProductoId] INT NULL,
        [DetencionId] BIGINT NULL,
        [Observaciones] NVARCHAR(1000) NULL,
        [IdentificadorExterno] NVARCHAR(100) NULL,
        [ImagenNombreArchivo] NVARCHAR(100) NULL,
        [ImagenNombreOriginal] NVARCHAR(255) NULL,
        [ImagenContentType] NVARCHAR(50) NULL,
        [ImagenTamanoBytes] BIGINT NULL,
        [EstadoRevision] NVARCHAR(20) NOT NULL,
        [RevisadoPorUsuarioId] NVARCHAR(450) NULL,
        [FechaHoraRevision] DATETIMEOFFSET NULL,
        [RowVersion] ROWVERSION NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_RegistrosCalidad] PRIMARY KEY ([RegistroCalidadId])
    )
    PRINT 'Tabla mnt.RegistrosCalidad creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.RegistrosCalidadRevisiones
-- Descripción: Historial de revisión humana (solo inserción): quién marcó
--              confirmado / falso positivo, cuándo y desde qué estado.
-- ============================================================================
IF OBJECT_ID(N'[mnt].[RegistrosCalidadRevisiones]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[RegistrosCalidadRevisiones] (
        [RegistroCalidadRevisionId] BIGINT IDENTITY(1,1) NOT NULL,
        [RegistroCalidadId] BIGINT NOT NULL,
        [EstadoAnterior] NVARCHAR(20) NOT NULL,
        [EstadoNuevo] NVARCHAR(20) NOT NULL,
        [Comentario] NVARCHAR(500) NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_RegistrosCalidadRevisiones_FechaHora] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_RegistrosCalidadRevisiones] PRIMARY KEY ([RegistroCalidadRevisionId])
    )
    PRINT 'Tabla mnt.RegistrosCalidadRevisiones creada exitosamente.'
END
GO

-- ============================================================================
-- CHECK CONSTRAINTS (replicados en EF con HasCheckConstraint)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidad_Origen')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [CK_RegistrosCalidad_Origen] CHECK ([Origen] IN ('AUTOMATICO', 'MANUAL'))
    PRINT 'Check Constraint CK_RegistrosCalidad_Origen creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidad_EstadoRevision')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [CK_RegistrosCalidad_EstadoRevision] CHECK ([EstadoRevision] IN ('PENDIENTE', 'CONFIRMADO', 'FALSO_POSITIVO'))
    PRINT 'Check Constraint CK_RegistrosCalidad_EstadoRevision creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidad_Confianza')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [CK_RegistrosCalidad_Confianza] CHECK ([Confianza] IS NULL OR ([Confianza] >= 0 AND [Confianza] <= 1))
    PRINT 'Check Constraint CK_RegistrosCalidad_Confianza creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidad_ValorMedido')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [CK_RegistrosCalidad_ValorMedido] CHECK ([ValorMedido] IS NULL OR [ValorMedido] >= 0)
    PRINT 'Check Constraint CK_RegistrosCalidad_ValorMedido creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidad_ImagenContentType')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [CK_RegistrosCalidad_ImagenContentType] CHECK ([ImagenContentType] IS NULL OR [ImagenContentType] IN ('image/jpeg', 'image/png'))
    PRINT 'Check Constraint CK_RegistrosCalidad_ImagenContentType creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidad_Revision')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [CK_RegistrosCalidad_Revision] CHECK (([EstadoRevision] = 'PENDIENTE' AND [RevisadoPorUsuarioId] IS NULL AND [FechaHoraRevision] IS NULL) OR ([EstadoRevision] <> 'PENDIENTE' AND [RevisadoPorUsuarioId] IS NOT NULL AND [FechaHoraRevision] IS NOT NULL))
    PRINT 'Check Constraint CK_RegistrosCalidad_Revision creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidadRevisiones_EstadoAnterior')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidadRevisiones]
    ADD CONSTRAINT [CK_RegistrosCalidadRevisiones_EstadoAnterior] CHECK ([EstadoAnterior] IN ('PENDIENTE', 'CONFIRMADO', 'FALSO_POSITIVO'))
    PRINT 'Check Constraint CK_RegistrosCalidadRevisiones_EstadoAnterior creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_RegistrosCalidadRevisiones_EstadoNuevo')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidadRevisiones]
    ADD CONSTRAINT [CK_RegistrosCalidadRevisiones_EstadoNuevo] CHECK ([EstadoNuevo] IN ('CONFIRMADO', 'FALSO_POSITIVO'))
    PRINT 'Check Constraint CK_RegistrosCalidadRevisiones_EstadoNuevo creado.'
END
GO

-- ============================================================================
-- FOREIGN KEYS (sin cascada: nada se borra)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_PuntosControlCalidad_Lineas_LineaId')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
    ADD CONSTRAINT [FK_PuntosControlCalidad_Lineas_LineaId] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_PuntosControlCalidad_Lineas_LineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidad_Lineas_LineaId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [FK_RegistrosCalidad_Lineas_LineaId] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidad_Lineas_LineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidad_PuntosControlCalidad_PuntoControlCalidadId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [FK_RegistrosCalidad_PuntosControlCalidad_PuntoControlCalidadId] FOREIGN KEY ([PuntoControlCalidadId])
    REFERENCES [mnt].[PuntosControlCalidad] ([PuntoControlCalidadId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidad_PuntosControlCalidad_PuntoControlCalidadId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidad_Turnos_TurnoId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [FK_RegistrosCalidad_Turnos_TurnoId] FOREIGN KEY ([TurnoId])
    REFERENCES [mnt].[Turnos] ([TurnoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidad_Turnos_TurnoId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidad_TiposDefectoCalidad_TipoDefectoCalidadId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [FK_RegistrosCalidad_TiposDefectoCalidad_TipoDefectoCalidadId] FOREIGN KEY ([TipoDefectoCalidadId])
    REFERENCES [mnt].[TiposDefectoCalidad] ([TipoDefectoCalidadId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidad_TiposDefectoCalidad_TipoDefectoCalidadId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidad_Productos_ProductoId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [FK_RegistrosCalidad_Productos_ProductoId] FOREIGN KEY ([ProductoId])
    REFERENCES [mnt].[Productos] ([ProductoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidad_Productos_ProductoId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidad_Detenciones_DetencionId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidad]
    ADD CONSTRAINT [FK_RegistrosCalidad_Detenciones_DetencionId] FOREIGN KEY ([DetencionId])
    REFERENCES [mnt].[Detenciones] ([DetencionId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidad_Detenciones_DetencionId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_RegistrosCalidadRevisiones_RegistrosCalidad_RegistroCalidadId')
BEGIN
    ALTER TABLE [mnt].[RegistrosCalidadRevisiones]
    ADD CONSTRAINT [FK_RegistrosCalidadRevisiones_RegistrosCalidad_RegistroCalidadId] FOREIGN KEY ([RegistroCalidadId])
    REFERENCES [mnt].[RegistrosCalidad] ([RegistroCalidadId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_RegistrosCalidadRevisiones_RegistrosCalidad_RegistroCalidadId creada.'
END
GO

-- ============================================================================
-- ÍNDICES
-- ============================================================================

-- El detector busca el punto por código
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_PuntosControlCalidad_Codigo' AND object_id = OBJECT_ID(N'[mnt].[PuntosControlCalidad]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PuntosControlCalidad_Codigo]
    ON [mnt].[PuntosControlCalidad] ([Codigo])
    PRINT 'Índice IX_PuntosControlCalidad_Codigo creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_PuntosControlCalidad_LineaId' AND object_id = OBJECT_ID(N'[mnt].[PuntosControlCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PuntosControlCalidad_LineaId]
    ON [mnt].[PuntosControlCalidad] ([LineaId])
    PRINT 'Índice IX_PuntosControlCalidad_LineaId creado.'
END
GO

-- El detector busca el defecto por código
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TiposDefectoCalidad_Codigo' AND object_id = OBJECT_ID(N'[mnt].[TiposDefectoCalidad]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_TiposDefectoCalidad_Codigo]
    ON [mnt].[TiposDefectoCalidad] ([Codigo])
    PRINT 'Índice IX_TiposDefectoCalidad_Codigo creado.'
END
GO

-- Grilla: orden y filtro por fecha
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_FechaHoraDeteccion' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidad_FechaHoraDeteccion]
    ON [mnt].[RegistrosCalidad] ([FechaHoraDeteccion])
    PRINT 'Índice IX_RegistrosCalidad_FechaHoraDeteccion creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_LineaId_FechaHoraDeteccion' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidad_LineaId_FechaHoraDeteccion]
    ON [mnt].[RegistrosCalidad] ([LineaId], [FechaHoraDeteccion])
    PRINT 'Índice IX_RegistrosCalidad_LineaId_FechaHoraDeteccion creado.'
END
GO

-- Pendientes de revisión
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_EstadoRevision_FechaHoraDeteccion' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidad_EstadoRevision_FechaHoraDeteccion]
    ON [mnt].[RegistrosCalidad] ([EstadoRevision], [FechaHoraDeteccion])
    PRINT 'Índice IX_RegistrosCalidad_EstadoRevision_FechaHoraDeteccion creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_PuntoControlCalidadId' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidad_PuntoControlCalidadId]
    ON [mnt].[RegistrosCalidad] ([PuntoControlCalidadId])
    PRINT 'Índice IX_RegistrosCalidad_PuntoControlCalidadId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_TipoDefectoCalidadId' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidad_TipoDefectoCalidadId]
    ON [mnt].[RegistrosCalidad] ([TipoDefectoCalidadId])
    PRINT 'Índice IX_RegistrosCalidad_TipoDefectoCalidadId creado.'
END
GO

-- Cruce con detenciones
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_DetencionId' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidad_DetencionId]
    ON [mnt].[RegistrosCalidad] ([DetencionId])
    PRINT 'Índice IX_RegistrosCalidad_DetencionId creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_ImagenNombreArchivo' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_RegistrosCalidad_ImagenNombreArchivo]
    ON [mnt].[RegistrosCalidad] ([ImagenNombreArchivo])
    WHERE [ImagenNombreArchivo] IS NOT NULL
    PRINT 'Índice IX_RegistrosCalidad_ImagenNombreArchivo creado.'
END
GO

-- Reintentos del detector: no duplicar
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidad_IdentificadorExterno' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidad]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_RegistrosCalidad_IdentificadorExterno]
    ON [mnt].[RegistrosCalidad] ([IdentificadorExterno])
    WHERE [IdentificadorExterno] IS NOT NULL
    PRINT 'Índice IX_RegistrosCalidad_IdentificadorExterno creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_RegistrosCalidadRevisiones_RegistroCalidadId' AND object_id = OBJECT_ID(N'[mnt].[RegistrosCalidadRevisiones]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegistrosCalidadRevisiones_RegistroCalidadId]
    ON [mnt].[RegistrosCalidadRevisiones] ([RegistroCalidadId])
    PRINT 'Índice IX_RegistrosCalidadRevisiones_RegistroCalidadId creado.'
END
GO

-- ============================================================================
-- CARGA DE MAESTROS (comentado a propósito)
-- Los maestros se administran desde la Web: Maestros -> Calidad (rol ADMINISTRADOR).
-- Si se prefiere cargarlos por script cuando llegue la ficha de criterios del
-- cliente, descomentar, reemplazar los <...> por los valores reales y ejecutar.
-- NO hay defectos de ejemplo reales: los <...> son solo el formato.
-- Los códigos son los que va a mandar el detector (mayúsculas, sin espacios).
-- ============================================================================
/*
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-23';

-- Puntos de control (cámaras)
DECLARE @Puntos TABLE ([Linea] NVARCHAR(100) NOT NULL, [Codigo] NVARCHAR(50) NOT NULL,
    [Nombre] NVARCHAR(100) NOT NULL, [Descripcion] NVARCHAR(500) NULL);
INSERT INTO @Puntos VALUES
-- (N'<Nombre de la línea>', N'<CODIGO-CAMARA>', N'<Nombre visible>', N'<Dónde está ubicada>');

INSERT INTO [mnt].[PuntosControlCalidad] ([LineaId], [Codigo], [Nombre], [Descripcion], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT l.[LineaId], UPPER(p.[Codigo]), p.[Nombre], p.[Descripcion], 1,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
FROM @Puntos p
JOIN [mnt].[Lineas] l ON l.[Nombre] = p.[Linea]
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PuntosControlCalidad] x WHERE x.[Codigo] = UPPER(p.[Codigo]));
PRINT CONCAT(N'Puntos de control de calidad cargados: ', @@ROWCOUNT, N'.');

-- Tipos de defecto (de la ficha de criterios)
DECLARE @Defectos TABLE ([Codigo] NVARCHAR(50) NOT NULL, [Nombre] NVARCHAR(100) NOT NULL,
    [Descripcion] NVARCHAR(500) NULL, [Unidad] NVARCHAR(20) NULL);
INSERT INTO @Defectos VALUES
-- (N'<CODIGO>', N'<Nombre del defecto>', N'<Criterio de la ficha>', N'<mm | paquetes | NULL>');

INSERT INTO [mnt].[TiposDefectoCalidad] ([Codigo], [Nombre], [Descripcion], [UnidadMedida], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT UPPER(d.[Codigo]), d.[Nombre], d.[Descripcion], d.[Unidad], 1,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
FROM @Defectos d
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[TiposDefectoCalidad] x WHERE x.[Codigo] = UPPER(d.[Codigo]));
PRINT CONCAT(N'Tipos de defecto cargados: ', @@ROWCOUNT, N'.');
GO
*/

PRINT 'Script 23_t611_calidad_defectos.sql completado.'
GO
