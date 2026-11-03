-- ==============================================================================
-- Script: 27_inspecciones_empaquetadora_tmc_tango2.sql
-- Descripción: Inspecciones con el mismo formato que Control de Proceso (reunión
--              03/11/2026 [78:17-79:44]) y plan de la EMPAQUETADORA TMC de TANGO 2
--              desde la planilla de Sebastián Ferretti (PSAG) "Estándares de
--              inspección - Línea Futura Tango 2".
-- Requiere: tablas base (04/05: Lineas, Equipos, Turnos). Crea las de 12 si faltan.
-- Idempotente: se puede ejecutar más de una vez. Solo crea lo que falta (tablas,
--              columnas, restricciones, índice) e INSERTA el plan y los puntos que
--              falten, identificados por código. No pisa lo que se haya editado
--              después desde Maestros > Planes de inspección. No borra nada.
--
-- Encoding: los acentos de los DATOS van con NCHAR(n), así que se graban bien aunque
-- se corra sin -f. Para leer bien los comentarios, correrlo con:
--     sqlcmd -S <servidor> -E -I -b -f 65001 -i 27_inspecciones_empaquetadora_tmc_tango2.sql
--
-- Qué hace:
--   1. Crea mnt.PlanesInspeccion, ItemsPlanInspeccion, EjecucionesInspeccion,
--      EjecucionesInspeccionDetalle y HallazgosNoOk si no existen (igual que el 12).
--   2. Agrega las columnas de la planilla:
--        ItemsPlanInspeccion: Codigo, ParteEquipo, MetodoDetalle, QuienRealiza
--          (1 MAQUINISTA, 2 AYUDANTE, 3 OP. PACKAGING, 4 MECÁNICO; las filas viejas toman
--          el responsable de su plan), FrecuenciaDias (1 = diaria, 10 = cada 10 días;
--          las viejas toman la del plan).
--        EjecucionesInspeccion: Puesto (quién realiza esa inspección; NULL = todos),
--          UsuarioCierreId (quién la cerró = firma).
--        EjecucionesInspeccionDetalle: EstandarSnapshot (estándar al responder).
--   3. Carga el plan INS-T2-EMP-TMC "EMPAQUETADORA TMC" en la línea TANGO 2 (buscada
--      por Nombre; tiene que haber una sola) con 13 puntos T2-EMP-01..T2-EMP-13:
--      11 diarios de OP. PACKAGING y 2 cada 10 días de MECÁNICO. Si la línea tiene un
--      único equipo cuyo nombre contiene "EMPAQUETADORA", el plan queda asociado a él;
--      si no, queda sin equipo (se puede asociar después desde Maestros).
--      Textos corregidos de la planilla: INSFECIONAR -> Inspeccionar, CEDENA -> Cadena,
--      regitro -> registro; el último punto (en la hoja "11") es el 13.
--
-- Los VALUES de @Items los lee también el test InspeccionEmpaquetadoraTests
-- (tests/PSAG.Mantenimiento.Api.IntegrationTests): mantener una fila por línea.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET NOCOUNT ON;
GO

-- ----------------------------------------------------------------------------
-- 1. Tablas (mismo diseño que 12_create_tables_inspeccion.sql)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[mnt].[PlanesInspeccion]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PlanesInspeccion] (
        [PlanInspeccionId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [EquipoId] INT NULL,
        [PuestoResponsable] TINYINT NOT NULL,
        [Codigo] NVARCHAR(20) NOT NULL,
        [Nombre] NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [FrecuenciaDias] INT NOT NULL DEFAULT 1,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        [FechaCreacion] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_PlanesInspeccion] PRIMARY KEY CLUSTERED ([PlanInspeccionId]),
        CONSTRAINT [FK_PlanesInspeccion_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_PlanesInspeccion_Equipos] FOREIGN KEY ([EquipoId]) REFERENCES [mnt].[Equipos]([EquipoId]),
        CONSTRAINT [UQ_PlanesInspeccion_Codigo] UNIQUE ([Codigo])
    );
    CREATE INDEX [IX_PlanesInspeccion_LineaPuesto] ON [mnt].[PlanesInspeccion]([LineaId], [PuestoResponsable]) WHERE [Activo] = 1;
    PRINT N'Tabla [mnt].[PlanesInspeccion] creada.';
END
GO

IF OBJECT_ID(N'[mnt].[ItemsPlanInspeccion]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[ItemsPlanInspeccion] (
        [ItemPlanInspeccionId] INT IDENTITY(1,1) NOT NULL,
        [PlanInspeccionId] INT NOT NULL,
        [NumeroItem] INT NOT NULL,
        [Descripcion] NVARCHAR(300) NOT NULL,
        [CondicionEstandar] NVARCHAR(300) NOT NULL,
        [Metodo] TINYINT NOT NULL DEFAULT 1,
        [Orden] INT NOT NULL DEFAULT 0,
        [Activo] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_ItemsPlanInspeccion] PRIMARY KEY CLUSTERED ([ItemPlanInspeccionId]),
        CONSTRAINT [FK_ItemsPlanInspeccion_Planes] FOREIGN KEY ([PlanInspeccionId]) REFERENCES [mnt].[PlanesInspeccion]([PlanInspeccionId])
    );
    CREATE INDEX [IX_ItemsPlanInspeccion_Plan] ON [mnt].[ItemsPlanInspeccion]([PlanInspeccionId]) WHERE [Activo] = 1;
    PRINT N'Tabla [mnt].[ItemsPlanInspeccion] creada.';
END
GO

IF OBJECT_ID(N'[mnt].[EjecucionesInspeccion]', N'U') IS NULL
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
        CONSTRAINT [FK_EjecucionesInspeccion_Planes] FOREIGN KEY ([PlanInspeccionId]) REFERENCES [mnt].[PlanesInspeccion]([PlanInspeccionId]),
        CONSTRAINT [FK_EjecucionesInspeccion_Turnos] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[Turnos]([TurnoId])
    );
    CREATE INDEX [IX_EjecucionesInspeccion_PlanFecha] ON [mnt].[EjecucionesInspeccion]([PlanInspeccionId], [Fecha] DESC);
    CREATE INDEX [IX_EjecucionesInspeccion_Fecha] ON [mnt].[EjecucionesInspeccion]([Fecha] DESC);
    PRINT N'Tabla [mnt].[EjecucionesInspeccion] creada.';
END
GO

IF OBJECT_ID(N'[mnt].[EjecucionesInspeccionDetalle]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[EjecucionesInspeccionDetalle] (
        [EjecucionInspeccionDetalleId] BIGINT IDENTITY(1,1) NOT NULL,
        [EjecucionInspeccionId] BIGINT NOT NULL,
        [ItemPlanInspeccionId] INT NOT NULL,
        [Resultado] TINYINT NOT NULL,
        [Observacion] NVARCHAR(500) NULL,
        [FechaHoraRegistro] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_EjecucionesInspeccionDetalle] PRIMARY KEY CLUSTERED ([EjecucionInspeccionDetalleId]),
        CONSTRAINT [FK_EjecucionesInspeccionDetalle_Ejecuciones] FOREIGN KEY ([EjecucionInspeccionId]) REFERENCES [mnt].[EjecucionesInspeccion]([EjecucionInspeccionId]),
        CONSTRAINT [FK_EjecucionesInspeccionDetalle_Items] FOREIGN KEY ([ItemPlanInspeccionId]) REFERENCES [mnt].[ItemsPlanInspeccion]([ItemPlanInspeccionId]),
        CONSTRAINT [CK_EjecucionesInspeccionDetalle_NoOkObservacion]
            CHECK ([Resultado] = 1 OR ([Resultado] = 2 AND [Observacion] IS NOT NULL AND LEN([Observacion]) > 0))
    );
    CREATE INDEX [IX_EjecucionesInspeccionDetalle_Ejecucion] ON [mnt].[EjecucionesInspeccionDetalle]([EjecucionInspeccionId]);
    PRINT N'Tabla [mnt].[EjecucionesInspeccionDetalle] creada.';
END
GO

IF OBJECT_ID(N'[mnt].[HallazgosNoOk]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[HallazgosNoOk] (
        [HallazgoNoOkId] BIGINT IDENTITY(1,1) NOT NULL,
        [EjecucionInspeccionDetalleId] BIGINT NOT NULL,
        [LineaId] INT NOT NULL,
        [EquipoId] INT NULL,
        [UsuarioReporteId] NVARCHAR(450) NOT NULL,
        [FechaHoraHallazgo] DATETIMEOFFSET NOT NULL,
        [Descripcion] NVARCHAR(1000) NOT NULL,
        [NivelCriticidad] INT NULL,
        [Resuelto] BIT NOT NULL DEFAULT 0,
        [UsuarioResolucionId] NVARCHAR(450) NULL,
        [FechaHoraResolucion] DATETIMEOFFSET NULL,
        [ObservacionResolucion] NVARCHAR(1000) NULL,
        [RequiereAccionCorrectiva] BIT NOT NULL DEFAULT 0,
        [AccionCorrectivaDescripcion] NVARCHAR(500) NULL,
        CONSTRAINT [PK_HallazgosNoOk] PRIMARY KEY CLUSTERED ([HallazgoNoOkId]),
        CONSTRAINT [FK_HallazgosNoOk_Detalles] FOREIGN KEY ([EjecucionInspeccionDetalleId]) REFERENCES [mnt].[EjecucionesInspeccionDetalle]([EjecucionInspeccionDetalleId]),
        CONSTRAINT [FK_HallazgosNoOk_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas]([LineaId]),
        CONSTRAINT [FK_HallazgosNoOk_Equipos] FOREIGN KEY ([EquipoId]) REFERENCES [mnt].[Equipos]([EquipoId])
    );
    CREATE INDEX [IX_HallazgosNoOk_LineaPendientes] ON [mnt].[HallazgosNoOk]([LineaId], [Resuelto]) WHERE [Resuelto] = 0;
    CREATE INDEX [IX_HallazgosNoOk_Pendientes] ON [mnt].[HallazgosNoOk]([Resuelto], [FechaHoraHallazgo] DESC) WHERE [Resuelto] = 0;
    PRINT N'Tabla [mnt].[HallazgosNoOk] creada.';
END
GO

-- ----------------------------------------------------------------------------
-- 2. Columnas nuevas (cada una en su lote: un ALTER que usa la columna recién
--    creada tiene que compilarse después)
-- ----------------------------------------------------------------------------
IF COL_LENGTH(N'mnt.ItemsPlanInspeccion', N'Codigo') IS NULL
BEGIN
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD [Codigo] NVARCHAR(50) NULL;
    PRINT N'Columna ItemsPlanInspeccion.Codigo agregada.';
END
GO

IF COL_LENGTH(N'mnt.ItemsPlanInspeccion', N'ParteEquipo') IS NULL
BEGIN
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD [ParteEquipo] NVARCHAR(150) NULL;
    PRINT N'Columna ItemsPlanInspeccion.ParteEquipo agregada.';
END
GO

IF COL_LENGTH(N'mnt.ItemsPlanInspeccion', N'MetodoDetalle') IS NULL
BEGIN
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD [MetodoDetalle] NVARCHAR(200) NULL;
    PRINT N'Columna ItemsPlanInspeccion.MetodoDetalle agregada.';
END
GO

IF COL_LENGTH(N'mnt.ItemsPlanInspeccion', N'QuienRealiza') IS NULL
BEGIN
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD [QuienRealiza] TINYINT NOT NULL
        CONSTRAINT [DF_ItemsPlanInspeccion_QuienRealiza] DEFAULT (3);
    -- Los puntos que ya existían los realiza el responsable de su plan.
    EXEC (N'UPDATE i SET i.[QuienRealiza] = p.[PuestoResponsable]
            FROM [mnt].[ItemsPlanInspeccion] i
            JOIN [mnt].[PlanesInspeccion] p ON p.[PlanInspeccionId] = i.[PlanInspeccionId];');
    PRINT N'Columna ItemsPlanInspeccion.QuienRealiza agregada.';
END
GO

IF COL_LENGTH(N'mnt.ItemsPlanInspeccion', N'FrecuenciaDias') IS NULL
BEGIN
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD [FrecuenciaDias] INT NOT NULL
        CONSTRAINT [DF_ItemsPlanInspeccion_FrecuenciaDias] DEFAULT (1);
    -- Los puntos que ya existían toman la frecuencia de su plan.
    EXEC (N'UPDATE i SET i.[FrecuenciaDias] = CASE WHEN p.[FrecuenciaDias] BETWEEN 1 AND 365 THEN p.[FrecuenciaDias] ELSE 1 END
            FROM [mnt].[ItemsPlanInspeccion] i
            JOIN [mnt].[PlanesInspeccion] p ON p.[PlanInspeccionId] = i.[PlanInspeccionId];');
    PRINT N'Columna ItemsPlanInspeccion.FrecuenciaDias agregada.';
END
GO

IF COL_LENGTH(N'mnt.EjecucionesInspeccion', N'Puesto') IS NULL
BEGIN
    ALTER TABLE [mnt].[EjecucionesInspeccion] ADD [Puesto] TINYINT NULL;
    PRINT N'Columna EjecucionesInspeccion.Puesto agregada.';
END
GO

IF COL_LENGTH(N'mnt.EjecucionesInspeccion', N'UsuarioCierreId') IS NULL
BEGIN
    ALTER TABLE [mnt].[EjecucionesInspeccion] ADD [UsuarioCierreId] NVARCHAR(450) NULL;
    PRINT N'Columna EjecucionesInspeccion.UsuarioCierreId agregada.';
END
GO

IF COL_LENGTH(N'mnt.EjecucionesInspeccionDetalle', N'EstandarSnapshot') IS NULL
BEGIN
    ALTER TABLE [mnt].[EjecucionesInspeccionDetalle] ADD [EstandarSnapshot] NVARCHAR(300) NULL;
    PRINT N'Columna EjecucionesInspeccionDetalle.EstandarSnapshot agregada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_ItemsPlanInspeccion_QuienRealiza')
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD CONSTRAINT [CK_ItemsPlanInspeccion_QuienRealiza] CHECK ([QuienRealiza] BETWEEN 1 AND 4);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_ItemsPlanInspeccion_FrecuenciaDias')
    ALTER TABLE [mnt].[ItemsPlanInspeccion] ADD CONSTRAINT [CK_ItemsPlanInspeccion_FrecuenciaDias] CHECK ([FrecuenciaDias] BETWEEN 1 AND 365);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_EjecucionesInspeccion_Puesto')
    ALTER TABLE [mnt].[EjecucionesInspeccion] ADD CONSTRAINT [CK_EjecucionesInspeccion_Puesto] CHECK ([Puesto] IS NULL OR [Puesto] BETWEEN 1 AND 4);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EjecucionesInspeccion_PlanFechaPuesto'
               AND [object_id] = OBJECT_ID(N'[mnt].[EjecucionesInspeccion]'))
    CREATE INDEX [IX_EjecucionesInspeccion_PlanFechaPuesto] ON [mnt].[EjecucionesInspeccion]([PlanInspeccionId], [Fecha], [Puesto]);
GO

-- ----------------------------------------------------------------------------
-- 3. Plan de la EMPAQUETADORA TMC de TANGO 2
-- ----------------------------------------------------------------------------
SET XACT_ABORT ON;

DECLARE @LineaNombre NVARCHAR(100) = N'TANGO 2';
DECLARE @PlanCodigo NVARCHAR(20) = N'INS-T2-EMP-TMC';

-- La línea se busca por nombre (en producción tiene Codigo vacío). Tiene que haber una sola.
DECLARE @CantLineas INT = (SELECT COUNT(*) FROM [mnt].[Lineas] WHERE UPPER(LTRIM(RTRIM([Nombre]))) = @LineaNombre);
IF @CantLineas <> 1
BEGIN
    RAISERROR(N'Se esperaba exactamente una linea con Nombre = TANGO 2 en mnt.Lineas y hay %d. No se cargo el plan.', 16, 1, @CantLineas);
    RETURN;
END

DECLARE @LineaId INT = (SELECT [LineaId] FROM [mnt].[Lineas] WHERE UPPER(LTRIM(RTRIM([Nombre]))) = @LineaNombre);

-- Equipo: se asocia solo si hay uno (y uno solo) con "EMPAQUETADORA" en el nombre. No se crean equipos.
DECLARE @EquipoId INT = NULL;
IF (SELECT COUNT(*) FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND UPPER([Nombre]) LIKE N'%EMPAQUETADORA%') = 1
    SET @EquipoId = (SELECT [EquipoId] FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND UPPER([Nombre]) LIKE N'%EMPAQUETADORA%');

PRINT CONCAT(N'Linea TANGO 2: LineaId = ', @LineaId, N'. Equipo empaquetadora: ', COALESCE(CAST(@EquipoId AS NVARCHAR(20)), N'(no encontrado, el plan queda sin equipo)'), N'.');

-- Datos de la planilla. @Items: Codigo, NumeroItem, ParteEquipo, Tareas, Estandar, MetodoDetalle,
-- QuienRealiza (3 = OP. PACKAGING, 4 = MECÁNICO), FrecuenciaDias, Orden.
DECLARE @Items TABLE (
    [Codigo] NVARCHAR(50) NOT NULL PRIMARY KEY,
    [NumeroItem] INT NOT NULL,
    [ParteEquipo] NVARCHAR(150) NOT NULL,
    [Tareas] NVARCHAR(300) NOT NULL,
    [Estandar] NVARCHAR(300) NOT NULL,
    [MetodoDetalle] NVARCHAR(200) NOT NULL,
    [QuienRealiza] TINYINT NOT NULL,
    [FrecuenciaDias] INT NOT NULL,
    [Orden] INT NOT NULL);

    INSERT INTO @Items ([Codigo], [NumeroItem], [ParteEquipo], [Tareas], [Estandar], [MetodoDetalle], [QuienRealiza], [FrecuenciaDias], [Orden]) VALUES
        (N'T2-EMP-01', 1, N'Difusores de rollos', N'Inspeccionar el estado de:' + NCHAR(10) + N'1) Correa tracci' + NCHAR(243) + N'n de rollo' + NCHAR(10) + N'2) Correa de mando' + NCHAR(10) + N'3) Correa dentada de apertura' + NCHAR(10) + N'4) Poleas', N'Sin excesivo desgaste / tensionadas y sin roturas', N'Control visual / tacto', 3, 1, 10),
        (N'T2-EMP-02', 2, N'Barras de introducci' + NCHAR(243) + N'n de rollos', N'Inspeccionar el estado de:' + NCHAR(10) + N'1) Barras' + NCHAR(10) + N'2) Correa de mando de barra simple', N'Barra sin rotura / correa tensionada y sin roturas', N'Control visual / tacto', 3, 1, 20),
        (N'T2-EMP-03', 3, N'Cadenas barra doble', N'Control de lubricaci' + NCHAR(243) + N'n', N'Cadenas lubricadas seg' + NCHAR(250) + N'n est' + NCHAR(225) + N'ndar semanal de mec' + NCHAR(225) + N'nicos', N'Revisi' + NCHAR(243) + N'n de "registro" en m' + NCHAR(225) + N'quina', 3, 1, 30),
        (N'T2-EMP-04', 4, N'Correa de mando barra doble (dentada)', N'Inspeccionar el estado', N'Correa tensa y sin desgaste', N'Control visual / tacto', 3, 1, 40),
        (N'T2-EMP-05', 5, N'Correa de mando plegador inferior', N'Inspeccionar el estado', N'Correa tensionada y sin roturas', N'Control visual / tacto', 3, 1, 50),
        (N'T2-EMP-06', 6, N'Correas lentas y r' + NCHAR(225) + N'pidas (film)', N'Inspeccionar el estado:' + NCHAR(10) + N'1) Correas' + NCHAR(10) + N'2) Poleas', N'Correa con "grip" y dibujo. Poleas con giro libre', N'Control visual / tacto', 3, 1, 60),
        (N'T2-EMP-07', 7, N'Sistema apertura de correas r' + NCHAR(225) + N'pidas', N'Inspeccionar el estado', N'Sin movimiento de brazos. Apriete de film parejo', N'Control visual / tacto', 3, 1, 70),
        (N'T2-EMP-08', 8, N'Carro plegador de cabeza', N'Inspeccionar el estado del rodamiento lineal (carro)', N'Sin movimiento (juego)', N'Control visual / tacto', 3, 1, 80),
        (N'T2-EMP-09', 9, N'Carro elevador de rollos', N'Inspeccionar el estado del rodamiento lineal (carro)', N'Sin movimiento (juego)', N'Control visual / tacto', 3, 1, 90),
        (N'T2-EMP-10', 10, N'Soldador inferior', N'Inspeccionar el estado', N'Verificar temperatura de trabajo / limpio y sin film pegado', N'Control visual / tacto', 3, 1, 100),
        (N'T2-EMP-11', 11, N'Rodillos de salida de soldador (banda tefl' + NCHAR(243) + N'n)', N'Inspeccionar el estado', N'Rodamientos con giro libre', N'Destensar banda y girar rodillos con guantes', 3, 1, 110),
        (N'T2-EMP-12', 12, N'Cadena pente', N'Control de lubricaci' + NCHAR(243) + N'n y tensi' + NCHAR(243) + N'n', N'Cadena lubricada y tensionada seg' + NCHAR(250) + N'n est' + NCHAR(225) + N'ndar semanal de mec' + NCHAR(225) + N'nicos', N'Revisi' + NCHAR(243) + N'n de "registro" en m' + NCHAR(225) + N'quina', 4, 10, 120),
        (N'T2-EMP-13', 13, N'Carro pente', N'Inspeccionar el estado', N'Barras ajustadas y rodamientos con giro libre', N'Quitar carros e inspeccionar', 4, 10, 130);

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE [Codigo] = @PlanCodigo)
BEGIN
    INSERT INTO [mnt].[PlanesInspeccion] ([LineaId], [EquipoId], [PuestoResponsable], [Codigo], [Nombre], [Descripcion], [FrecuenciaDias], [Orden], [Activo])
    VALUES (@LineaId, @EquipoId, 3, @PlanCodigo,
            N'EMPAQUETADORA TMC',
            N'Est' + NCHAR(225) + N'ndares de inspecci' + NCHAR(243) + N'n - L' + NCHAR(237) + N'nea Futura "Tango 2". Equipo: EMPAQUETADORA TMC. Responsable de la inspecci' + NCHAR(243) + N'n: Operador Packaging. Firma: Op. Packaging (planilla de Sebasti' + NCHAR(225) + N'n Ferretti).',
            1, 1, 1);
    PRINT N'Plan INS-T2-EMP-TMC creado.';
END
ELSE
    PRINT N'Plan INS-T2-EMP-TMC ya existia (no se modifica).';

DECLARE @PlanId INT = (SELECT [PlanInspeccionId] FROM [mnt].[PlanesInspeccion] WHERE [Codigo] = @PlanCodigo);

INSERT INTO [mnt].[ItemsPlanInspeccion]
    ([PlanInspeccionId], [NumeroItem], [Codigo], [ParteEquipo], [Descripcion], [CondicionEstandar], [Metodo], [MetodoDetalle], [QuienRealiza], [FrecuenciaDias], [Orden], [Activo])
SELECT @PlanId, i.[NumeroItem], i.[Codigo], i.[ParteEquipo], i.[Tareas], i.[Estandar], 1, i.[MetodoDetalle], i.[QuienRealiza], i.[FrecuenciaDias], i.[Orden], 1
FROM @Items i
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[ItemsPlanInspeccion] x WHERE x.[PlanInspeccionId] = @PlanId AND x.[Codigo] = i.[Codigo]);

PRINT CONCAT(N'Puntos insertados: ', @@ROWCOUNT, N' (de 13).');

COMMIT TRANSACTION;

DECLARE @Activos INT = (SELECT COUNT(*) FROM [mnt].[ItemsPlanInspeccion] WHERE [PlanInspeccionId] = @PlanId AND [Activo] = 1);
PRINT CONCAT(N'Plan INS-T2-EMP-TMC: ', @Activos, N' puntos activos.');
PRINT N'Script 27 completado.';
GO
