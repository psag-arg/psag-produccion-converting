-- ==============================================================================
-- Script: 34_t798_turnos_produccion.sql
-- Descripción: T-798 etapa 2 · Turno de producción y sus hijas (docs/diseno/T-798_modelo_produccion.md, 2.6).
--   1. PRD_TurnosProduccion: el evento (línea + turno de ABM_Turnos + fecha de producción = día de inicio).
--      Inicio/Fin programados y MinutosProgramados son snapshot. Estado ABIERTO / CERRADO / CERRADO_AUTOMATICO /
--      SIN_APERTURA y OrigenApertura LOGIN / SUPERVISOR / SISTEMA con CHECK. RowVersion.
--      Único (LineaId, TurnoId, FechaProduccion) y UN SOLO ABIERTO POR LÍNEA (índice único filtrado).
--   2. PRD_TurnosProduccionOperadores: relevos (operador = usuario, desde/hasta, origen LOGIN / SUPERVISOR,
--      motivo obligatorio si es SUPERVISOR, AnuladoPorId = corrección insert-only). Reemplaza MaquinistasTurno
--      de T-783 (que no se desplegó).
--   3. PRD_TurnosProduccionProductos: tramos de producto con snapshot del parámetro vigente (objetivos de mangas y
--      bolsones, minutos de referencia, metros por manga, ParametroLineaProductoId), SKU opcional, origen
--      LOGIN / CAMBIO / SUPERVISOR, AnuladoPorId.
--   4. PRD_ProduccionesTurno: real oficial por tramo y magnitud (MANGAS, ROLLOS, PAQUETES, BOLSONES, PALLETS).
--      Se crea VACÍA (se llena en la etapa 5).
--   Mismos nombres, tipos y CHECKs que TurnoProduccionConfigurations.cs de la API. NO carga datos (la conversión
--   de los datos existentes es la etapa 7).
-- Versión: 1.0
-- Requiere: 31, 32 (y 33 si se quieren los productos de Tango 2).
-- Idempotente: se puede ejecutar más de una vez (crea solo lo que falta).
-- Correr con la API detenida, antes de publicar la API de T-798 etapa 2.
-- Deshacer: 34_t798_turnos_produccion_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.ABM_Lineas', N'U') IS NULL
   OR OBJECT_ID(N'mnt.ABM_Turnos', N'U') IS NULL
   OR OBJECT_ID(N'mnt.ABM_ProductosSku', N'U') IS NULL
   OR COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'ParametroLineaProductoId') IS NULL
BEGIN
    RAISERROR(N'Falta correr 31_t798_renombre_por_modulo.sql y 32_t798_maestros_productos.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

-- PRD_ParametrosLineaProducto necesita PK (o UNIQUE) en ParametroLineaProductoId para la FK de los tramos.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto')
      AND (i.is_primary_key = 1 OR i.is_unique = 1)
      AND c.name = N'ParametroLineaProductoId'
      AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id = i.object_id AND x.index_id = i.index_id) = 1)
BEGIN
    RAISERROR(N'PRD_ParametrosLineaProducto no tiene PK en ParametroLineaProductoId: revisar el 32.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 1. PRD_TurnosProduccion
-- ============================================================================
IF OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_TurnosProduccion] (
        [TurnoProduccionId] BIGINT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [TurnoId] INT NOT NULL,
        [FechaProduccion] DATE NOT NULL,
        [InicioProgramado] DATETIMEOFFSET NOT NULL,
        [FinProgramado] DATETIMEOFFSET NOT NULL,
        [MinutosProgramados] INT NOT NULL,
        [InicioReal] DATETIMEOFFSET NULL,
        [FinReal] DATETIMEOFFSET NULL,
        [Estado] NVARCHAR(20) NOT NULL,
        [OrigenApertura] NVARCHAR(20) NOT NULL,
        [AbiertoPorUsuarioId] NVARCHAR(450) NULL,
        [CerradoPorUsuarioId] NVARCHAR(450) NULL,
        [ObservacionCierre] NVARCHAR(500) NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_PRD_TurnosProduccion_FechaHoraCreacion] DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_PRD_TurnosProduccion_FechaHoraModificacion] DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_PRD_TurnosProduccion] PRIMARY KEY ([TurnoProduccionId]),
        CONSTRAINT [FK_PRD_TurnosProduccion_ABM_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[ABM_Lineas] ([LineaId]),
        CONSTRAINT [FK_PRD_TurnosProduccion_ABM_Turnos] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[ABM_Turnos] ([TurnoId]),
        CONSTRAINT [CK_PRD_TurnosProduccion_Estado]
            CHECK ([Estado] IN (N'ABIERTO', N'CERRADO', N'CERRADO_AUTOMATICO', N'SIN_APERTURA')),
        CONSTRAINT [CK_PRD_TurnosProduccion_OrigenApertura] CHECK ([OrigenApertura] IN (N'LOGIN', N'SUPERVISOR', N'SISTEMA')),
        CONSTRAINT [CK_PRD_TurnosProduccion_MinutosProgramados] CHECK ([MinutosProgramados] >= 0)
    );
    PRINT 'Tabla mnt.PRD_TurnosProduccion creada.'
END
ELSE
    PRINT 'Tabla mnt.PRD_TurnosProduccion ya existía.'
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.PRD_TurnosProduccion') AND name = N'UX_PRD_TurnosProduccion_Linea_Turno_Fecha')
    CREATE UNIQUE INDEX [UX_PRD_TurnosProduccion_Linea_Turno_Fecha] ON [mnt].[PRD_TurnosProduccion] ([LineaId], [TurnoId], [FechaProduccion]);
GO

-- Un solo turno ABIERTO por línea.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.PRD_TurnosProduccion') AND name = N'UX_PRD_TurnosProduccion_LineaAbierto')
    CREATE UNIQUE INDEX [UX_PRD_TurnosProduccion_LineaAbierto] ON [mnt].[PRD_TurnosProduccion] ([LineaId]) WHERE [Estado] = N'ABIERTO';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.PRD_TurnosProduccion') AND name = N'IX_PRD_TurnosProduccion_FechaProduccion')
    CREATE INDEX [IX_PRD_TurnosProduccion_FechaProduccion] ON [mnt].[PRD_TurnosProduccion] ([FechaProduccion]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.PRD_TurnosProduccion') AND name = N'IX_PRD_TurnosProduccion_TurnoId')
    CREATE INDEX [IX_PRD_TurnosProduccion_TurnoId] ON [mnt].[PRD_TurnosProduccion] ([TurnoId]);
GO

-- ============================================================================
-- 2. PRD_TurnosProduccionOperadores
-- ============================================================================
IF OBJECT_ID(N'mnt.PRD_TurnosProduccionOperadores', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_TurnosProduccionOperadores] (
        [TurnoProduccionOperadorId] BIGINT IDENTITY(1,1) NOT NULL,
        [TurnoProduccionId] BIGINT NOT NULL,
        [OperadorUsuarioId] NVARCHAR(450) NOT NULL,
        [Desde] DATETIMEOFFSET NOT NULL,
        [Hasta] DATETIMEOFFSET NULL,
        [Origen] NVARCHAR(20) NOT NULL,
        [RegistradoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHoraRegistro] DATETIMEOFFSET NOT NULL,
        [Motivo] NVARCHAR(500) NULL,
        [AnuladoPorId] BIGINT NULL,
        CONSTRAINT [PK_PRD_TurnosProduccionOperadores] PRIMARY KEY ([TurnoProduccionOperadorId]),
        CONSTRAINT [FK_PRD_TurnosProduccionOperadores_PRD_TurnosProduccion] FOREIGN KEY ([TurnoProduccionId])
            REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]),
        CONSTRAINT [FK_PRD_TurnosProduccionOperadores_AnuladoPor] FOREIGN KEY ([AnuladoPorId])
            REFERENCES [mnt].[PRD_TurnosProduccionOperadores] ([TurnoProduccionOperadorId]),
        CONSTRAINT [CK_PRD_TurnosProduccionOperadores_Origen] CHECK ([Origen] IN (N'LOGIN', N'SUPERVISOR')),
        CONSTRAINT [CK_PRD_TurnosProduccionOperadores_Motivo] CHECK ([Origen] <> N'SUPERVISOR' OR [Motivo] IS NOT NULL)
    );
    CREATE INDEX [IX_PRD_TurnosProduccionOperadores_TurnoProduccionId] ON [mnt].[PRD_TurnosProduccionOperadores] ([TurnoProduccionId]);
    CREATE INDEX [IX_PRD_TurnosProduccionOperadores_OperadorUsuarioId] ON [mnt].[PRD_TurnosProduccionOperadores] ([OperadorUsuarioId]);
    CREATE INDEX [IX_PRD_TurnosProduccionOperadores_AnuladoPorId] ON [mnt].[PRD_TurnosProduccionOperadores] ([AnuladoPorId]);
    PRINT 'Tabla mnt.PRD_TurnosProduccionOperadores creada.'
END
ELSE
    PRINT 'Tabla mnt.PRD_TurnosProduccionOperadores ya existía.'
GO

-- ============================================================================
-- 3. PRD_TurnosProduccionProductos
-- ============================================================================
IF OBJECT_ID(N'mnt.PRD_TurnosProduccionProductos', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_TurnosProduccionProductos] (
        [TurnoProduccionProductoId] BIGINT IDENTITY(1,1) NOT NULL,
        [TurnoProduccionId] BIGINT NOT NULL,
        [ProductoMaestroId] INT NOT NULL,
        [ProductoSkuId] INT NULL,
        [Desde] DATETIMEOFFSET NOT NULL,
        [Hasta] DATETIMEOFFSET NULL,
        [ParametroLineaProductoId] INT NOT NULL,
        [MangasObjetivoTurno] INT NULL,
        [BolsonesObjetivoTurno] INT NULL,
        [MinutosTurnoReferencia] INT NOT NULL,
        [MetrosPorManga] DECIMAL(9,2) NULL,
        [Origen] NVARCHAR(20) NOT NULL,
        [DeclaradoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHoraRegistro] DATETIMEOFFSET NOT NULL,
        [Motivo] NVARCHAR(500) NULL,
        [AnuladoPorId] BIGINT NULL,
        CONSTRAINT [PK_PRD_TurnosProduccionProductos] PRIMARY KEY ([TurnoProduccionProductoId]),
        CONSTRAINT [FK_PRD_TurnosProduccionProductos_PRD_TurnosProduccion] FOREIGN KEY ([TurnoProduccionId])
            REFERENCES [mnt].[PRD_TurnosProduccion] ([TurnoProduccionId]),
        CONSTRAINT [FK_PRD_TurnosProduccionProductos_ABM_ProductosMaestro] FOREIGN KEY ([ProductoMaestroId])
            REFERENCES [mnt].[ABM_ProductosMaestro] ([ProductoMaestroId]),
        CONSTRAINT [FK_PRD_TurnosProduccionProductos_ABM_ProductosSku] FOREIGN KEY ([ProductoSkuId])
            REFERENCES [mnt].[ABM_ProductosSku] ([ProductoSkuId]),
        CONSTRAINT [FK_PRD_TurnosProduccionProductos_PRD_ParametrosLineaProducto] FOREIGN KEY ([ParametroLineaProductoId])
            REFERENCES [mnt].[PRD_ParametrosLineaProducto] ([ParametroLineaProductoId]),
        CONSTRAINT [FK_PRD_TurnosProduccionProductos_AnuladoPor] FOREIGN KEY ([AnuladoPorId])
            REFERENCES [mnt].[PRD_TurnosProduccionProductos] ([TurnoProduccionProductoId]),
        CONSTRAINT [CK_PRD_TurnosProduccionProductos_Origen] CHECK ([Origen] IN (N'LOGIN', N'CAMBIO', N'SUPERVISOR')),
        CONSTRAINT [CK_PRD_TurnosProduccionProductos_Motivo] CHECK ([Origen] <> N'SUPERVISOR' OR [Motivo] IS NOT NULL),
        CONSTRAINT [CK_PRD_TurnosProduccionProductos_MinutosTurno] CHECK ([MinutosTurnoReferencia] > 0)
    );
    CREATE INDEX [IX_PRD_TurnosProduccionProductos_TurnoProduccionId] ON [mnt].[PRD_TurnosProduccionProductos] ([TurnoProduccionId]);
    CREATE INDEX [IX_PRD_TurnosProduccionProductos_ProductoMaestroId] ON [mnt].[PRD_TurnosProduccionProductos] ([ProductoMaestroId]);
    CREATE INDEX [IX_PRD_TurnosProduccionProductos_ParametroLineaProductoId] ON [mnt].[PRD_TurnosProduccionProductos] ([ParametroLineaProductoId]);
    CREATE INDEX [IX_PRD_TurnosProduccionProductos_AnuladoPorId] ON [mnt].[PRD_TurnosProduccionProductos] ([AnuladoPorId]);
    PRINT 'Tabla mnt.PRD_TurnosProduccionProductos creada.'
END
ELSE
    PRINT 'Tabla mnt.PRD_TurnosProduccionProductos ya existía.'
GO

-- ============================================================================
-- 4. PRD_ProduccionesTurno (vacía hasta la etapa 5)
-- ============================================================================
IF OBJECT_ID(N'mnt.PRD_ProduccionesTurno', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_ProduccionesTurno] (
        [ProduccionTurnoId] BIGINT IDENTITY(1,1) NOT NULL,
        [TurnoProduccionProductoId] BIGINT NOT NULL,
        [Magnitud] NVARCHAR(10) NOT NULL,
        [CantidadCamara] DECIMAL(12,1) NULL,
        [MinutosConDatos] INT NULL,
        [CantidadOficial] DECIMAL(12,1) NULL,
        [Origen] NVARCHAR(20) NOT NULL,
        [EstadoValidacion] NVARCHAR(20) NOT NULL,
        [ValidadoPorUsuarioId] NVARCHAR(450) NULL,
        [FechaHoraValidacion] DATETIMEOFFSET NULL,
        [RegistradoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHoraRegistro] DATETIMEOFFSET NOT NULL,
        [AnuladoPorId] BIGINT NULL,
        CONSTRAINT [PK_PRD_ProduccionesTurno] PRIMARY KEY ([ProduccionTurnoId]),
        CONSTRAINT [FK_PRD_ProduccionesTurno_PRD_TurnosProduccionProductos] FOREIGN KEY ([TurnoProduccionProductoId])
            REFERENCES [mnt].[PRD_TurnosProduccionProductos] ([TurnoProduccionProductoId]),
        CONSTRAINT [FK_PRD_ProduccionesTurno_AnuladoPor] FOREIGN KEY ([AnuladoPorId])
            REFERENCES [mnt].[PRD_ProduccionesTurno] ([ProduccionTurnoId]),
        CONSTRAINT [CK_PRD_ProduccionesTurno_Magnitud]
            CHECK ([Magnitud] IN (N'MANGAS', N'ROLLOS', N'PAQUETES', N'BOLSONES', N'PALLETS')),
        CONSTRAINT [CK_PRD_ProduccionesTurno_Origen] CHECK ([Origen] IN (N'CAMARA', N'CAMARA_CORREGIDA', N'MANUAL')),
        CONSTRAINT [CK_PRD_ProduccionesTurno_EstadoValidacion] CHECK ([EstadoValidacion] IN (N'PENDIENTE', N'VALIDADA', N'SIN_DATOS'))
    );
    CREATE INDEX [IX_PRD_ProduccionesTurno_Producto_Magnitud] ON [mnt].[PRD_ProduccionesTurno] ([TurnoProduccionProductoId], [Magnitud]);
    CREATE INDEX [IX_PRD_ProduccionesTurno_AnuladoPorId] ON [mnt].[PRD_ProduccionesTurno] ([AnuladoPorId]);
    PRINT 'Tabla mnt.PRD_ProduccionesTurno creada (vacía).'
END
ELSE
    PRINT 'Tabla mnt.PRD_ProduccionesTurno ya existía.'
GO

SET NOEXEC OFF;
GO
PRINT 'Script 34_t798_turnos_produccion.sql terminado.'
GO
