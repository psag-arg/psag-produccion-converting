-- ==============================================================================
-- Script: 04_create_tables.sql
-- Descripción: Crea las tablas de negocio del sistema de mantenimiento
-- Versión: 2.0 - Fase 1 (Detenciones con modelo de tiempo)
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Tabla: mnt.Lineas
-- Descripción: Líneas de producción en Converting
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Lineas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Lineas] (
        [LineaId] INT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_Lineas] PRIMARY KEY ([LineaId])
    )
    PRINT 'Tabla mnt.Lineas creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.Equipos
-- Descripción: Equipos dentro de cada línea (antes Maquinas)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Equipos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Equipos] (
        [EquipoId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_Equipos] PRIMARY KEY ([EquipoId])
    )
    PRINT 'Tabla mnt.Equipos creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.TiposFalla
-- Descripción: Catálogo de tipos de falla (antes TiposEvento)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[TiposFalla]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[TiposFalla] (
        [TipoFallaId] INT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_TiposFalla] PRIMARY KEY ([TipoFallaId])
    )
    PRINT 'Tabla mnt.TiposFalla creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.DetallesFalla
-- Descripción: Detalles de falla (antes Causas). Filtrados por Equipo y/o TipoFalla.
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[DetallesFalla]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[DetallesFalla] (
        [DetalleFallaId] INT IDENTITY(1,1) NOT NULL,
        [EquipoId] INT NULL,
        [TipoFallaId] INT NULL,
        [Codigo] NVARCHAR(50) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [RequiereDescripcion] BIT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_DetallesFalla] PRIMARY KEY ([DetalleFallaId])
    )
    PRINT 'Tabla mnt.DetallesFalla creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.Turnos
-- Descripción: Catálogo de turnos de trabajo
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Turnos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Turnos] (
        [TurnoId] INT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(20) NOT NULL,
        [Nombre] NVARCHAR(50) NOT NULL,
        [HoraInicio] TIME NOT NULL,
        [HoraFin] TIME NOT NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_Turnos] PRIMARY KEY ([TurnoId])
    )
    PRINT 'Tabla mnt.Turnos creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.Detenciones
-- Descripción: Detenciones de línea (paradas de máquina)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Detenciones]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Detenciones] (
        [DetencionId] BIGINT IDENTITY(1,1) NOT NULL,
        [Codigo] NVARCHAR(30) NOT NULL,
        [LineaId] INT NOT NULL,
        [EquipoId] INT NULL,
        [TipoFallaId] INT NULL,
        [DetalleFallaId] INT NULL,
        [TurnoId] INT NOT NULL,
        [Fecha] DATE NOT NULL,

        -- Modelo de tiempo
        [MinutosDeclarados] INT NULL,
        [FechaHoraParada] DATETIMEOFFSET NULL,
        [FechaHoraArranque] DATETIMEOFFSET NULL,
        [OrigenTiempo] VARCHAR(20) NOT NULL DEFAULT 'DECLARADO',
        [MinutosEfectivos] INT NOT NULL,

        -- Datos de la detención
        [Descripcion] NVARCHAR(2000) NOT NULL,
        [EstadoActual] VARCHAR(20) NOT NULL DEFAULT 'REGISTRADA',
        [AccionesRealizadas] NVARCHAR(4000) NULL,
        [ResolucionDescripcion] NVARCHAR(4000) NULL,
        [FechaHoraCierre] DATETIMEOFFSET NULL,
        [CerradoPorUsuarioId] NVARCHAR(450) NULL,

        -- Auditoría y concurrencia
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_Detenciones] PRIMARY KEY ([DetencionId])
    )
    PRINT 'Tabla mnt.Detenciones creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.DetencionesEstadoHistorial
-- Descripción: Historial de cambios de estado (solo inserción)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[DetencionesEstadoHistorial]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[DetencionesEstadoHistorial] (
        [DetencionEstadoHistorialId] BIGINT IDENTITY(1,1) NOT NULL,
        [DetencionId] BIGINT NOT NULL,
        [EstadoAnterior] VARCHAR(20) NULL,
        [EstadoNuevo] VARCHAR(20) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [Observacion] NVARCHAR(2000) NULL,
        [OrigenAccion] NVARCHAR(50) NULL,
        [CorrelationId] NVARCHAR(50) NULL,
        CONSTRAINT [PK_DetencionesEstadoHistorial] PRIMARY KEY ([DetencionEstadoHistorialId])
    )
    PRINT 'Tabla mnt.DetencionesEstadoHistorial creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.DetencionesNotas
-- Descripción: Notas de seguimiento de detenciones (solo inserción)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[DetencionesNotas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[DetencionesNotas] (
        [DetencionNotaId] BIGINT IDENTITY(1,1) NOT NULL,
        [DetencionId] BIGINT NOT NULL,
        [Texto] NVARCHAR(4000) NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [TipoNota] VARCHAR(20) NOT NULL DEFAULT 'Operativa',
        [EsNotaDeCierre] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_DetencionesNotas] PRIMARY KEY ([DetencionNotaId])
    )
    PRINT 'Tabla mnt.DetencionesNotas creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.UsuariosLineas
-- Descripción: Asignación de usuarios a líneas
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[UsuariosLineas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[UsuariosLineas] (
        [UsuarioLineaId] INT IDENTITY(1,1) NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [LineaId] INT NOT NULL,
        [FechaAsignacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [AsignadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_UsuariosLineas] PRIMARY KEY ([UsuarioLineaId])
    )
    PRINT 'Tabla mnt.UsuariosLineas creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.Auditoria
-- Descripción: Registro de auditoría del sistema (solo inserción)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[Auditoria]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[Auditoria] (
        [AuditoriaId] BIGINT IDENTITY(1,1) NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [Accion] NVARCHAR(50) NOT NULL,
        [Entidad] NVARCHAR(100) NOT NULL,
        [EntidadId] NVARCHAR(100) NULL,
        [ValoresAnteriores] NVARCHAR(MAX) NULL,
        [ValoresNuevos] NVARCHAR(MAX) NULL,
        [DireccionIp] NVARCHAR(50) NULL,
        [CorrelationId] NVARCHAR(50) NULL,
        [Descripcion] NVARCHAR(500) NULL,
        CONSTRAINT [PK_Auditoria] PRIMARY KEY ([AuditoriaId])
    )
    PRINT 'Tabla mnt.Auditoria creada exitosamente.'
END
GO

PRINT 'Todas las tablas de negocio creadas exitosamente.'
GO
