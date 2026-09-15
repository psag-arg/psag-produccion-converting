-- =============================================
-- Eje 3: Inspecciones de Maquina
-- Tablas para planes de inspeccion, ejecuciones y hallazgos NO OK
-- =============================================

USE PSAG_Mantenimiento;
GO

-- =============================================
-- Tabla: PlanesInspeccion
-- Plan de inspeccion para un equipo/linea
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlanesInspeccion' AND schema_id = SCHEMA_ID('mnt'))
BEGIN
    CREATE TABLE [mnt].[PlanesInspeccion] (
        [PlanInspeccionId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [EquipoId] INT NULL,
        [PuestoResponsable] TINYINT NOT NULL,  -- 1=MAQUINISTA, 2=AYUDANTE, 3=PACKAGING
        [Codigo] NVARCHAR(20) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [FrecuenciaDias] INT NOT NULL DEFAULT 1,  -- 1=diario, 7=semanal, etc.
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_PlanesInspeccion] PRIMARY KEY CLUSTERED ([PlanInspeccionId]),
        CONSTRAINT [FK_PlanesInspeccion_Lineas] FOREIGN KEY ([LineaId])
            REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_PlanesInspeccion_Equipos] FOREIGN KEY ([EquipoId])
            REFERENCES [mnt].[Equipos]([EquipoId]),
        CONSTRAINT [UQ_PlanesInspeccion_Codigo] UNIQUE ([Codigo])
    );

    CREATE INDEX [IX_PlanesInspeccion_LineaPuesto]
        ON [mnt].[PlanesInspeccion]([LineaId], [PuestoResponsable])
        WHERE [Activo] = 1;

    PRINT 'Tabla [mnt].[PlanesInspeccion] creada.';
END
GO

-- =============================================
-- Tabla: ItemsPlanInspeccion
-- Items del checklist de cada plan
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ItemsPlanInspeccion' AND schema_id = SCHEMA_ID('mnt'))
BEGIN
    CREATE TABLE [mnt].[ItemsPlanInspeccion] (
        [ItemPlanInspeccionId] INT IDENTITY(1,1) NOT NULL,
        [PlanInspeccionId] INT NOT NULL,
        [NumeroItem] INT NOT NULL,
        [Descripcion] NVARCHAR(300) NOT NULL,
        [CondicionEstandar] NVARCHAR(300) NOT NULL,
        [Metodo] TINYINT NOT NULL DEFAULT 1,  -- 1=VISUAL, 2=TACTO, 3=MEDICION
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_ItemsPlanInspeccion] PRIMARY KEY CLUSTERED ([ItemPlanInspeccionId]),
        CONSTRAINT [FK_ItemsPlanInspeccion_Planes] FOREIGN KEY ([PlanInspeccionId])
            REFERENCES [mnt].[PlanesInspeccion]([PlanInspeccionId])
    );

    CREATE INDEX [IX_ItemsPlanInspeccion_Plan]
        ON [mnt].[ItemsPlanInspeccion]([PlanInspeccionId])
        WHERE [Activo] = 1;

    PRINT 'Tabla [mnt].[ItemsPlanInspeccion] creada.';
END
GO

-- =============================================
-- Tabla: EjecucionesInspeccion
-- Registro de cada ejecucion de un plan
-- R18: INSERT-ONLY
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EjecucionesInspeccion' AND schema_id = SCHEMA_ID('mnt'))
BEGIN
    CREATE TABLE [mnt].[EjecucionesInspeccion] (
        [EjecucionInspeccionId] BIGINT IDENTITY(1,1) NOT NULL,
        [PlanInspeccionId] INT NOT NULL,
        [TurnoId] INT NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [Fecha] DATE NOT NULL,
        [FechaHoraInicio] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [FechaHoraFin] DATETIMEOFFSET NULL,
        [Completada] BIT NOT NULL DEFAULT 0,
        [CantidadOk] INT NOT NULL DEFAULT 0,
        [CantidadNoOk] INT NOT NULL DEFAULT 0,
        [ObservacionesGenerales] NVARCHAR(1000) NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_EjecucionesInspeccion] PRIMARY KEY CLUSTERED ([EjecucionInspeccionId]),
        CONSTRAINT [FK_EjecucionesInspeccion_Planes] FOREIGN KEY ([PlanInspeccionId])
            REFERENCES [mnt].[PlanesInspeccion]([PlanInspeccionId]),
        CONSTRAINT [FK_EjecucionesInspeccion_Turnos] FOREIGN KEY ([TurnoId])
            REFERENCES [mnt].[Turnos]([TurnoId])
    );

    CREATE INDEX [IX_EjecucionesInspeccion_PlanFecha]
        ON [mnt].[EjecucionesInspeccion]([PlanInspeccionId], [Fecha] DESC);

    CREATE INDEX [IX_EjecucionesInspeccion_Fecha]
        ON [mnt].[EjecucionesInspeccion]([Fecha] DESC);

    PRINT 'Tabla [mnt].[EjecucionesInspeccion] creada.';
END
GO

-- =============================================
-- Tabla: EjecucionesInspeccionDetalle
-- Detalle de cada item inspeccionado
-- R16: Si Resultado=NO_OK, Observacion es obligatoria
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EjecucionesInspeccionDetalle' AND schema_id = SCHEMA_ID('mnt'))
BEGIN
    CREATE TABLE [mnt].[EjecucionesInspeccionDetalle] (
        [EjecucionInspeccionDetalleId] BIGINT IDENTITY(1,1) NOT NULL,
        [EjecucionInspeccionId] BIGINT NOT NULL,
        [ItemPlanInspeccionId] INT NOT NULL,
        [Resultado] TINYINT NOT NULL,  -- 1=OK, 2=NO_OK
        [Observacion] NVARCHAR(500) NULL,
        [FechaHoraRegistro] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_EjecucionesInspeccionDetalle] PRIMARY KEY CLUSTERED ([EjecucionInspeccionDetalleId]),
        CONSTRAINT [FK_EjecucionesInspeccionDetalle_Ejecuciones] FOREIGN KEY ([EjecucionInspeccionId])
            REFERENCES [mnt].[EjecucionesInspeccion]([EjecucionInspeccionId]),
        CONSTRAINT [FK_EjecucionesInspeccionDetalle_Items] FOREIGN KEY ([ItemPlanInspeccionId])
            REFERENCES [mnt].[ItemsPlanInspeccion]([ItemPlanInspeccionId]),
        CONSTRAINT [CK_EjecucionesInspeccionDetalle_NoOkObservacion]
            CHECK ([Resultado] = 1 OR ([Resultado] = 2 AND [Observacion] IS NOT NULL AND LEN([Observacion]) > 0))
    );

    CREATE INDEX [IX_EjecucionesInspeccionDetalle_Ejecucion]
        ON [mnt].[EjecucionesInspeccionDetalle]([EjecucionInspeccionId]);

    PRINT 'Tabla [mnt].[EjecucionesInspeccionDetalle] creada.';
END
GO

-- =============================================
-- Tabla: HallazgosNoOk
-- Panel de hallazgos NO OK para seguimiento
-- R17: Se marcan como resueltos, no se borran
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HallazgosNoOk' AND schema_id = SCHEMA_ID('mnt'))
BEGIN
    CREATE TABLE [mnt].[HallazgosNoOk] (
        [HallazgoNoOkId] BIGINT IDENTITY(1,1) NOT NULL,
        [EjecucionInspeccionDetalleId] BIGINT NOT NULL,
        [LineaId] INT NOT NULL,  -- Denormalizado para consultas
        [EquipoId] INT NULL,
        [UsuarioReporteId] NVARCHAR(450) NOT NULL,
        [FechaHoraHallazgo] DATETIMEOFFSET NOT NULL,
        [Descripcion] NVARCHAR(1000) NOT NULL,
        [NivelCriticidad] TINYINT NULL,  -- 1-5
        [Resuelto] BIT NOT NULL DEFAULT 0,
        [UsuarioResolucionId] NVARCHAR(450) NULL,
        [FechaHoraResolucion] DATETIMEOFFSET NULL,
        [ObservacionResolucion] NVARCHAR(1000) NULL,
        [RequiereAccionCorrectiva] BIT NOT NULL DEFAULT 0,
        [AccionCorrectivaDescripcion] NVARCHAR(500) NULL,
        CONSTRAINT [PK_HallazgosNoOk] PRIMARY KEY CLUSTERED ([HallazgoNoOkId]),
        CONSTRAINT [FK_HallazgosNoOk_Detalles] FOREIGN KEY ([EjecucionInspeccionDetalleId])
            REFERENCES [mnt].[EjecucionesInspeccionDetalle]([EjecucionInspeccionDetalleId]),
        CONSTRAINT [FK_HallazgosNoOk_Lineas] FOREIGN KEY ([LineaId])
            REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_HallazgosNoOk_Equipos] FOREIGN KEY ([EquipoId])
            REFERENCES [mnt].[Equipos]([EquipoId])
    );

    CREATE INDEX [IX_HallazgosNoOk_LineaPendientes]
        ON [mnt].[HallazgosNoOk]([LineaId], [Resuelto])
        WHERE [Resuelto] = 0;

    CREATE INDEX [IX_HallazgosNoOk_Pendientes]
        ON [mnt].[HallazgosNoOk]([Resuelto], [FechaHoraHallazgo] DESC)
        WHERE [Resuelto] = 0;

    PRINT 'Tabla [mnt].[HallazgosNoOk] creada.';
END
GO

PRINT 'Tablas de Eje 3 - Inspecciones creadas correctamente.';
GO
