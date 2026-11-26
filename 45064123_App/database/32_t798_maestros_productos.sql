-- ==============================================================================
-- Script: 32_t798_maestros_productos.sql
-- Descripción: T-798 etapa 1 · Maestros de producción (docs/diseno/T-798_modelo_produccion.md, 2.5 y 2.7).
--   1. ABM_ProductosMaestro: TipoProducto NVARCHAR(30) NULL, MetrosPorManga DECIMAL(9,2) NULL,
--      RollosPorManga INT NULL y RollosPorPaquete INT NULL (sin datos: los carga el 33 o Maestros).
--   2. ABM_ProductosSku (nueva): SKU de un producto maestro, FK NOT NULL al maestro. TipoSku BOLSON o
--      PALLET; PaquetesPorBolson solo BOLSON; SkuContenidoId (el SKU bolsón) y BolsonesPorPallet solo
--      PALLET; cantidades nulas o mayores que 0; CodigoSku único. NO carga SKUs.
--   3. PRD_ParametrosLineaProducto (ex LineasProductos, renombrada por el 31) = PRESUPUESTO:
--      PK propia ParametroLineaProductoId, VigenteDesde DATE, Habilitado BIT, MangasObjetivoTurno y
--      BolsonesObjetivoTurno INT NULL, MinutosTurnoReferencia INT NOT NULL DEFAULT 480; único
--      (LineaId, ProductoMaestroId, VigenteDesde). Las filas existentes quedan con
--      VigenteDesde = 2026-01-01, Habilitado = 1 y sin objetivos (las sigue listando Control de Proceso).
--   4. ABM_TiposFalla.CuentaParaIndicadores BIT NOT NULL DEFAULT 1 (todas cuentan, como hasta ahora;
--      qué tipos no cuentan lo define el cliente en Maestros).
--      Y ABM_TiposFalla.RequiereDescripcion BIT NOT NULL DEFAULT 0 si falta (la lee EF; 04 no la creaba).
--   5. ABM_Turnos: horarios de planta T1 05:00-13:00, T2 13:00-21:00, T3 21:00-05:00 (8 h cada uno),
--      por Codigo. Los registros ya cargados no cambian de turno (TurnoId materializado).
-- Versión: 1.0
-- Requiere: 31_t798_renombre_por_modulo.sql.
-- Idempotente: se puede ejecutar más de una vez.
-- Correr ANTES de publicar la API de T-798 y con la API detenida.
-- Deshacer: 32_t798_maestros_productos_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.ABM_ProductosMaestro', N'U') IS NULL
   OR COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'ProductoMaestroId') IS NULL
   OR OBJECT_ID(N'mnt.ABM_Lineas', N'U') IS NULL
   OR OBJECT_ID(N'mnt.ABM_Turnos', N'U') IS NULL
   OR OBJECT_ID(N'mnt.ABM_TiposFalla', N'U') IS NULL
BEGIN
    RAISERROR(N'Falta correr 31_t798_renombre_por_modulo.sql (no están las tablas con el nombre nuevo).', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 1. ABM_ProductosMaestro: columnas nuevas (vacías).
-- ============================================================================
IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'TipoProducto') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_ProductosMaestro] ADD [TipoProducto] NVARCHAR(30) NULL;
    PRINT 'Columna ABM_ProductosMaestro.TipoProducto agregada.'
END
GO

IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'MetrosPorManga') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_ProductosMaestro] ADD [MetrosPorManga] DECIMAL(9,2) NULL;
    PRINT 'Columna ABM_ProductosMaestro.MetrosPorManga agregada.'
END
GO

IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'RollosPorManga') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_ProductosMaestro] ADD [RollosPorManga] INT NULL;
    PRINT 'Columna ABM_ProductosMaestro.RollosPorManga agregada.'
END
GO

IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'RollosPorPaquete') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_ProductosMaestro] ADD [RollosPorPaquete] INT NULL;
    PRINT 'Columna ABM_ProductosMaestro.RollosPorPaquete agregada.'
END
GO

-- ============================================================================
-- 2. ABM_ProductosSku (mismos CHECKs que ProductoSkuConfiguration de la API).
-- ============================================================================
IF OBJECT_ID(N'mnt.ABM_ProductosSku', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[ABM_ProductosSku] (
        [ProductoSkuId] INT IDENTITY(1,1) NOT NULL,
        [ProductoMaestroId] INT NOT NULL,
        [CodigoSku] NVARCHAR(50) NOT NULL,
        [Descripcion] NVARCHAR(200) NULL,
        [TipoSku] NVARCHAR(10) NOT NULL,
        [PaquetesPorBolson] INT NULL,
        [SkuContenidoId] INT NULL,
        [BolsonesPorPallet] INT NULL,
        [Activo] BIT NOT NULL CONSTRAINT [DF_ABM_ProductosSku_Activo] DEFAULT 1,
        CONSTRAINT [PK_ABM_ProductosSku] PRIMARY KEY ([ProductoSkuId]),
        CONSTRAINT [UQ_ABM_ProductosSku_CodigoSku] UNIQUE ([CodigoSku]),
        CONSTRAINT [FK_ABM_ProductosSku_ABM_ProductosMaestro] FOREIGN KEY ([ProductoMaestroId])
            REFERENCES [mnt].[ABM_ProductosMaestro] ([ProductoMaestroId]),
        CONSTRAINT [FK_ABM_ProductosSku_SkuContenido] FOREIGN KEY ([SkuContenidoId])
            REFERENCES [mnt].[ABM_ProductosSku] ([ProductoSkuId]),
        CONSTRAINT [CK_ABM_ProductosSku_TipoSku] CHECK ([TipoSku] IN (N'BOLSON', N'PALLET')),
        CONSTRAINT [CK_ABM_ProductosSku_Bolson]
            CHECK ([TipoSku] <> N'BOLSON' OR ([SkuContenidoId] IS NULL AND [BolsonesPorPallet] IS NULL)),
        CONSTRAINT [CK_ABM_ProductosSku_Pallet] CHECK ([TipoSku] <> N'PALLET' OR [PaquetesPorBolson] IS NULL),
        CONSTRAINT [CK_ABM_ProductosSku_PaquetesPorBolson] CHECK ([PaquetesPorBolson] IS NULL OR [PaquetesPorBolson] > 0),
        CONSTRAINT [CK_ABM_ProductosSku_BolsonesPorPallet] CHECK ([BolsonesPorPallet] IS NULL OR [BolsonesPorPallet] > 0)
    );
    CREATE INDEX [IX_ABM_ProductosSku_ProductoMaestroId] ON [mnt].[ABM_ProductosSku] ([ProductoMaestroId]);
    PRINT 'Tabla mnt.ABM_ProductosSku creada (vacía).'
END
ELSE
    PRINT 'Tabla mnt.ABM_ProductosSku ya existía.'
GO

-- ============================================================================
-- 3. PRD_ParametrosLineaProducto: de relación línea-producto a parametrización con vigencia.
-- ============================================================================

-- 3.a Si no hubiera LineasProductos (base sin el script 11), se crea vacía con la forma de antes.
IF OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_ParametrosLineaProducto] (
        [LineaId] INT NOT NULL,
        [ProductoMaestroId] INT NOT NULL,
        CONSTRAINT [FK_PRD_ParametrosLineaProducto_ABM_Lineas] FOREIGN KEY ([LineaId])
            REFERENCES [mnt].[ABM_Lineas] ([LineaId]),
        CONSTRAINT [FK_PRD_ParametrosLineaProducto_ABM_ProductosMaestro] FOREIGN KEY ([ProductoMaestroId])
            REFERENCES [mnt].[ABM_ProductosMaestro] ([ProductoMaestroId])
    );
    PRINT 'Tabla mnt.PRD_ParametrosLineaProducto creada (no existía LineasProductos).'
END
GO

-- 3.b Columnas nuevas. Las filas existentes toman los valores por defecto.
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'ParametroLineaProductoId') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] ADD [ParametroLineaProductoId] INT IDENTITY(1,1) NOT NULL;
    PRINT 'Columna PRD_ParametrosLineaProducto.ParametroLineaProductoId agregada (numera las filas existentes).'
END
GO

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'VigenteDesde') IS NULL
BEGIN
    -- El DEFAULT solo sirve para completar las filas existentes: se quita abajo (la API siempre manda la fecha).
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto]
        ADD [VigenteDesde] DATE NOT NULL CONSTRAINT [DF_PRD_ParametrosLineaProducto_VigenteDesde_T798] DEFAULT ('20260101');
    PRINT 'Columna PRD_ParametrosLineaProducto.VigenteDesde agregada (filas existentes: 2026-01-01).'
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_PRD_ParametrosLineaProducto_VigenteDesde_T798')
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [DF_PRD_ParametrosLineaProducto_VigenteDesde_T798];
    PRINT 'Default transitorio de VigenteDesde quitado.'
END
GO

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'Habilitado') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto]
        ADD [Habilitado] BIT NOT NULL CONSTRAINT [DF_PRD_ParametrosLineaProducto_Habilitado] DEFAULT 1;
    PRINT 'Columna PRD_ParametrosLineaProducto.Habilitado agregada (filas existentes: 1).'
END
GO

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'MangasObjetivoTurno') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] ADD [MangasObjetivoTurno] INT NULL;
    PRINT 'Columna PRD_ParametrosLineaProducto.MangasObjetivoTurno agregada (vacía).'
END
GO

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'BolsonesObjetivoTurno') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] ADD [BolsonesObjetivoTurno] INT NULL;
    PRINT 'Columna PRD_ParametrosLineaProducto.BolsonesObjetivoTurno agregada (vacía).'
END
GO

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'MinutosTurnoReferencia') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto]
        ADD [MinutosTurnoReferencia] INT NOT NULL CONSTRAINT [DF_PRD_ParametrosLineaProducto_MinutosTurnoReferencia] DEFAULT 480;
    PRINT 'Columna PRD_ParametrosLineaProducto.MinutosTurnoReferencia agregada (480).'
END
GO

-- 3.c CHECKs (los mismos que ParametroLineaProductoConfiguration).
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_ParametrosLineaProducto_Mangas')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] ADD CONSTRAINT [CK_PRD_ParametrosLineaProducto_Mangas]
        CHECK ([MangasObjetivoTurno] IS NULL OR [MangasObjetivoTurno] >= 0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_ParametrosLineaProducto_Bolsones')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] ADD CONSTRAINT [CK_PRD_ParametrosLineaProducto_Bolsones]
        CHECK ([BolsonesObjetivoTurno] IS NULL OR [BolsonesObjetivoTurno] >= 0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_ParametrosLineaProducto_MinutosTurno')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] ADD CONSTRAINT [CK_PRD_ParametrosLineaProducto_MinutosTurno]
        CHECK ([MinutosTurnoReferencia] > 0);
GO

-- 3.d PK propia: la PK vieja (LineaId, ProductoId) se reemplaza por ParametroLineaProductoId.
DECLARE @PkActual SYSNAME =
    (SELECT kc.name FROM sys.key_constraints kc
     WHERE kc.parent_object_id = OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto') AND kc.type = 'PK');
DECLARE @PkEsNueva BIT = CASE WHEN EXISTS (
    SELECT 1
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    WHERE kc.parent_object_id = OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto') AND kc.type = 'PK'
    GROUP BY kc.name
    HAVING COUNT(*) = 1
       AND MAX(COL_NAME(ic.object_id, ic.column_id)) = N'ParametroLineaProductoId') THEN 1 ELSE 0 END;
DECLARE @Sql NVARCHAR(400);

IF @PkActual IS NOT NULL AND @PkEsNueva = 0
BEGIN
    SET @Sql = N'ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT ' + QUOTENAME(@PkActual) + N';';
    EXEC sys.sp_executesql @Sql;
    PRINT CONCAT(N'PK anterior ', @PkActual, N' (LineaId, ProductoId) quitada.');
END

IF @PkEsNueva = 0
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto]
        ADD CONSTRAINT [PK_PRD_ParametrosLineaProducto] PRIMARY KEY ([ParametroLineaProductoId]);
    PRINT 'PK PK_PRD_ParametrosLineaProducto (ParametroLineaProductoId) creada.'
END
ELSE
    PRINT 'PK de PRD_ParametrosLineaProducto ya es ParametroLineaProductoId.'
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto')
                 AND name = N'UQ_PRD_ParametrosLineaProducto_Linea_Producto_Vigencia')
BEGIN
    CREATE UNIQUE INDEX [UQ_PRD_ParametrosLineaProducto_Linea_Producto_Vigencia]
        ON [mnt].[PRD_ParametrosLineaProducto] ([LineaId], [ProductoMaestroId], [VigenteDesde]);
    PRINT 'Índice único (LineaId, ProductoMaestroId, VigenteDesde) creado.'
END
GO

-- ============================================================================
-- 4. ABM_TiposFalla.CuentaParaIndicadores (por defecto 1: todas cuentan, como hasta ahora).
-- ============================================================================
IF COL_LENGTH(N'mnt.ABM_TiposFalla', N'CuentaParaIndicadores') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_TiposFalla]
        ADD [CuentaParaIndicadores] BIT NOT NULL CONSTRAINT [DF_ABM_TiposFalla_CuentaParaIndicadores] DEFAULT 1;
    PRINT 'Columna ABM_TiposFalla.CuentaParaIndicadores agregada (todos los tipos en 1).'
END
ELSE
    PRINT 'Columna ABM_TiposFalla.CuentaParaIndicadores ya existía.'
GO

-- ABM_TiposFalla.RequiereDescripcion: la mapea el modelo de EF (TipoFalla) desde siempre, pero 04_create_tables.sql
-- no la crea (sólo la tiene DetallesFalla). Si falta, la API falla al leer tipos de falla. Se agrega con 0.
-- El rollback no la quita (la versión anterior de la API también la lee).
IF COL_LENGTH(N'mnt.ABM_TiposFalla', N'RequiereDescripcion') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_TiposFalla]
        ADD [RequiereDescripcion] BIT NOT NULL CONSTRAINT [DF_ABM_TiposFalla_RequiereDescripcion] DEFAULT 0;
    PRINT 'Columna ABM_TiposFalla.RequiereDescripcion agregada (faltaba; todos los tipos en 0).'
END
ELSE
    PRINT 'Columna ABM_TiposFalla.RequiereDescripcion ya existía.'
GO

-- ============================================================================
-- 5. ABM_Turnos: horarios de planta confirmados (T1 05-13, T2 13-21, T3 21-05, 8 h cada uno).
--    Se imprime el horario anterior de cada turno que cambia (queda en el log del deploy).
-- ============================================================================
DECLARE @Turnos TABLE ([Codigo] NVARCHAR(20), [HoraInicio] TIME(0), [HoraFin] TIME(0));
INSERT INTO @Turnos ([Codigo], [HoraInicio], [HoraFin]) VALUES
    (N'T1', '05:00', '13:00'),
    (N'T2', '13:00', '21:00'),
    (N'T3', '21:00', '05:00');

DECLARE @Cambios TABLE ([Codigo] NVARCHAR(20), [InicioAntes] TIME(0), [FinAntes] TIME(0), [Inicio] TIME(0), [Fin] TIME(0));

UPDATE t
SET t.[HoraInicio] = n.[HoraInicio],
    t.[HoraFin] = n.[HoraFin],
    t.[FechaHoraModificacion] = SYSDATETIMEOFFSET(),
    t.[ModificadoPorUsuarioId] = N'SCRIPT-32-T798'
OUTPUT inserted.[Codigo], deleted.[HoraInicio], deleted.[HoraFin], inserted.[HoraInicio], inserted.[HoraFin]
    INTO @Cambios ([Codigo], [InicioAntes], [FinAntes], [Inicio], [Fin])
FROM [mnt].[ABM_Turnos] t
JOIN @Turnos n ON n.[Codigo] = t.[Codigo]
WHERE t.[HoraInicio] <> n.[HoraInicio] OR t.[HoraFin] <> n.[HoraFin];

DECLARE @Linea NVARCHAR(200);
DECLARE cCambios CURSOR LOCAL FAST_FORWARD FOR
    SELECT CONCAT(N'Turno ', [Codigo], N': ', CONVERT(CHAR(5), [InicioAntes], 108), N'-', CONVERT(CHAR(5), [FinAntes], 108),
                  N' -> ', CONVERT(CHAR(5), [Inicio], 108), N'-', CONVERT(CHAR(5), [Fin], 108))
    FROM @Cambios ORDER BY [Codigo];
OPEN cCambios;
FETCH NEXT FROM cCambios INTO @Linea;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT @Linea;
    FETCH NEXT FROM cCambios INTO @Linea;
END
CLOSE cCambios;
DEALLOCATE cCambios;

DECLARE @CantidadCambios INT = (SELECT COUNT(*) FROM @Cambios);
PRINT CONCAT(N'Turnos corregidos: ', @CantidadCambios, N'.');

IF EXISTS (SELECT 1 FROM @Turnos n WHERE NOT EXISTS (SELECT 1 FROM [mnt].[ABM_Turnos] t WHERE t.[Codigo] = n.[Codigo]))
    PRINT N'AVISO: falta algún turno T1/T2/T3 en ABM_Turnos (no se crea: revisar en Configuración → Turnos).'
GO

PRINT 'Script 32_t798_maestros_productos.sql completado.'
GO

SET NOEXEC OFF
GO
