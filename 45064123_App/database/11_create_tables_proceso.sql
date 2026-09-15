-- ==============================================================================
-- Script: 11_create_tables_proceso.sql
-- Descripción: Tablas para Eje 2 - Variables de proceso
-- Versión: 1.0
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Tabla: mnt.Productos
-- Descripción: Productos que se fabrican en las líneas
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Productos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Productos] (
        [ProductoId] INT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_Productos] PRIMARY KEY ([ProductoId]),
        CONSTRAINT [UQ_Productos_Codigo] UNIQUE ([Codigo])
    )
    PRINT 'Tabla mnt.Productos creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.LineasProductos
-- Descripción: Relación muchos a muchos entre líneas y productos
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[LineasProductos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[LineasProductos] (
        [LineaId] INT NOT NULL,
        [ProductoId] INT NOT NULL,
        CONSTRAINT [PK_LineasProductos] PRIMARY KEY ([LineaId], [ProductoId]),
        CONSTRAINT [FK_LineasProductos_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_LineasProductos_Productos] FOREIGN KEY ([ProductoId]) REFERENCES [mnt].[Productos]([ProductoId])
    )
    PRINT 'Tabla mnt.LineasProductos creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.GruposParametro
-- Descripción: Grupos de parámetros (pestañas en la UI)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[GruposParametro]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[GruposParametro] (
        [GrupoParametroId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NULL,  -- NULL = aplica a todas las líneas
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_GruposParametro] PRIMARY KEY ([GrupoParametroId]),
        CONSTRAINT [FK_GruposParametro_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas]([LineaId])
    )
    PRINT 'Tabla mnt.GruposParametro creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.Parametros
-- Descripción: Parámetros de proceso a verificar
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Parametros]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Parametros] (
        [ParametroId] INT IDENTITY(1,1) NOT NULL,
        [GrupoParametroId] INT NOT NULL,
        [Seccion] NVARCHAR(100) NULL,  -- Nivel intermedio de agrupación visual
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(200) NOT NULL,
        [UnidadMedida] NVARCHAR(50) NULL,
        [TipoDato] VARCHAR(20) NOT NULL DEFAULT 'NUMERICO',  -- NUMERICO, RANGO, TEXTO
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_Parametros] PRIMARY KEY ([ParametroId]),
        CONSTRAINT [FK_Parametros_GruposParametro] FOREIGN KEY ([GrupoParametroId]) REFERENCES [mnt].[GruposParametro]([GrupoParametroId]),
        CONSTRAINT [CK_Parametros_TipoDato] CHECK ([TipoDato] IN ('NUMERICO', 'RANGO', 'TEXTO'))
    )
    PRINT 'Tabla mnt.Parametros creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.EstandaresParametro
-- Descripción: Valores estándar de parámetros por línea y producto
-- "El que cambia el parámetro es el producto"
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[EstandaresParametro]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[EstandaresParametro] (
        [EstandarParametroId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [ProductoId] INT NOT NULL,
        [ParametroId] INT NOT NULL,
        [ValorEstandarTexto] NVARCHAR(100) NULL,  -- Para mostrar (ej: "3.8 - 4.1 (bar)")
        [ValorMin] DECIMAL(18,4) NULL,
        [ValorMax] DECIMAL(18,4) NULL,
        [Tolerancia] DECIMAL(18,4) NULL,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_EstandaresParametro] PRIMARY KEY ([EstandarParametroId]),
        CONSTRAINT [FK_EstandaresParametro_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_EstandaresParametro_Productos] FOREIGN KEY ([ProductoId]) REFERENCES [mnt].[Productos]([ProductoId]),
        CONSTRAINT [FK_EstandaresParametro_Parametros] FOREIGN KEY ([ParametroId]) REFERENCES [mnt].[Parametros]([ParametroId]),
        CONSTRAINT [UQ_EstandaresParametro_LineaProductoParametro] UNIQUE ([LineaId], [ProductoId], [ParametroId])
    )
    PRINT 'Tabla mnt.EstandaresParametro creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.RegistrosProceso
-- Descripción: Cabecera de registros de variables de proceso
-- Un registro por línea + producto + turno + fecha
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[RegistrosProceso]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[RegistrosProceso] (
        [RegistroProcesoId] BIGINT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [ProductoId] INT NOT NULL,
        [TurnoId] INT NOT NULL,
        [Fecha] DATE NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHoraInicio] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraFin] DATETIMEOFFSET NULL,
        [Estado] VARCHAR(20) NOT NULL DEFAULT 'PARCIAL',  -- PARCIAL, COMPLETO, CERRADO
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_RegistrosProceso] PRIMARY KEY ([RegistroProcesoId]),
        CONSTRAINT [FK_RegistrosProceso_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_RegistrosProceso_Productos] FOREIGN KEY ([ProductoId]) REFERENCES [mnt].[Productos]([ProductoId]),
        CONSTRAINT [FK_RegistrosProceso_Turnos] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[Turnos]([TurnoId]),
        CONSTRAINT [UQ_RegistrosProceso_LineaProductoTurnoFecha] UNIQUE ([LineaId], [ProductoId], [TurnoId], [Fecha]),
        CONSTRAINT [CK_RegistrosProceso_Estado] CHECK ([Estado] IN ('PARCIAL', 'COMPLETO', 'CERRADO'))
    )
    PRINT 'Tabla mnt.RegistrosProceso creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.RegistrosProcesoDetalle
-- Descripción: Detalle de valores capturados
-- Guarda el estándar vigente como SNAPSHOT (no se recalcula)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[RegistrosProcesoDetalle]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[RegistrosProcesoDetalle] (
        [RegistroProcesoDetalleId] BIGINT IDENTITY(1,1) NOT NULL,
        [RegistroProcesoId] BIGINT NOT NULL,
        [ParametroId] INT NOT NULL,
        [ValorEstandarSnapshot] NVARCHAR(100) NULL,  -- SNAPSHOT del estándar al momento del registro
        [ValorReal] DECIMAL(18,4) NULL,
        [ValorRealTexto] NVARCHAR(200) NULL,  -- Para parámetros de tipo TEXTO
        [FueraDeEstandar] BIT NOT NULL DEFAULT 0,  -- Se calcula en BACKEND
        [Observacion] NVARCHAR(1000) NULL,  -- Obligatoria si FueraDeEstandar = 1
        [FechaHoraRegistro] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_RegistrosProcesoDetalle] PRIMARY KEY ([RegistroProcesoDetalleId]),
        CONSTRAINT [FK_RegistrosProcesoDetalle_RegistrosProceso] FOREIGN KEY ([RegistroProcesoId]) REFERENCES [mnt].[RegistrosProceso]([RegistroProcesoId]),
        CONSTRAINT [FK_RegistrosProcesoDetalle_Parametros] FOREIGN KEY ([ParametroId]) REFERENCES [mnt].[Parametros]([ParametroId]),
        CONSTRAINT [UQ_RegistrosProcesoDetalle_RegistroParametro] UNIQUE ([RegistroProcesoId], [ParametroId])
    )
    PRINT 'Tabla mnt.RegistrosProcesoDetalle creada exitosamente.'
END
GO

-- ============================================================================
-- Índices adicionales
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RegistrosProceso_LineaId')
    CREATE INDEX [IX_RegistrosProceso_LineaId] ON [mnt].[RegistrosProceso] ([LineaId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RegistrosProceso_ProductoId')
    CREATE INDEX [IX_RegistrosProceso_ProductoId] ON [mnt].[RegistrosProceso] ([ProductoId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RegistrosProceso_Fecha')
    CREATE INDEX [IX_RegistrosProceso_Fecha] ON [mnt].[RegistrosProceso] ([Fecha]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RegistrosProceso_Estado')
    CREATE INDEX [IX_RegistrosProceso_Estado] ON [mnt].[RegistrosProceso] ([Estado]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RegistrosProcesoDetalle_FueraDeEstandar')
    CREATE INDEX [IX_RegistrosProcesoDetalle_FueraDeEstandar] ON [mnt].[RegistrosProcesoDetalle] ([FueraDeEstandar]) WHERE [FueraDeEstandar] = 1;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EstandaresParametro_LineaProducto')
    CREATE INDEX [IX_EstandaresParametro_LineaProducto] ON [mnt].[EstandaresParametro] ([LineaId], [ProductoId]);

PRINT 'Índices de Variables de proceso creados exitosamente.'
GO

PRINT 'Todas las tablas de Variables de proceso creadas exitosamente.'
GO
