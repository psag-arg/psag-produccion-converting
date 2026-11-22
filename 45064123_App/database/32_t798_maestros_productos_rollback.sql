-- ==============================================================================
-- Script: 32_t798_maestros_productos_rollback.sql
-- Descripción: Deshace 32_t798_maestros_productos.sql (T-798 etapa 1).
--   1. ABM_ProductosSku: se elimina solo si está vacía (si tiene SKUs, se detiene: revisar a mano).
--   2. PRD_ParametrosLineaProducto vuelve a ser la relación línea-producto: PK (LineaId, ProductoMaestroId)
--      (el 31 rollback la renombra a ProductoId). Si hay más de una vigencia para la misma línea y producto
--      (por ejemplo, la del 33 o una cargada desde la Web), se detiene: correr antes el rollback del 33
--      o dejar una fila por línea y producto. Se quitan vigencia, habilitado, objetivos e Id propio.
--      Las filas deshabilitadas (Habilitado = 0) también se quitan: antes no existía el concepto.
--   3. ABM_TiposFalla.CuentaParaIndicadores: se quita.
--   4. ABM_ProductosMaestro: se quitan TipoProducto, MetrosPorManga, RollosPorManga y RollosPorPaquete.
--   5. ABM_Turnos: NO se vuelven a los horarios anteriores salvo que se pida (@RestaurarTurnosAnteriores = 1):
--      el script 32 dejó en el log el horario anterior de cada turno que cambió. Con 1 se ponen los del
--      diseño (T-798, 2.2): T1 05-13, T2 13-21 y T3 21-06 (el T3 de 9 h que se pisaba con el T1).
-- Requiere: 34_t798_turnos_produccion_rollback.sql (se verifica) y 33_t798_datos_tango2_rollback.sql corridos antes.
-- Idempotente: se puede ejecutar más de una vez.
-- Correr con la API detenida. Después, si se vuelve atrás todo, 31_t798_renombre_por_modulo_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Orden inverso: antes va 34_t798_turnos_produccion_rollback.sql (los tramos de producto apuntan a los parámetros).
IF OBJECT_ID(N'mnt.PRD_TurnosProduccionProductos', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'Correr antes 34_t798_turnos_produccion_rollback.sql (orden inverso). No se cambió nada.', 16, 1);
    SET NOEXEC ON;
END
GO

-- Seguridad: no se pierden SKUs ni vigencias cargadas.
-- (Las consultas sobre columnas que pueden no existir van con sp_executesql: así el lote compila igual.)
DECLARE @Hay BIT = 0;

IF OBJECT_ID(N'mnt.ABM_ProductosSku', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT @Hay = CASE WHEN EXISTS (SELECT 1 FROM [mnt].[ABM_ProductosSku]) THEN 1 ELSE 0 END;',
        N'@Hay BIT OUTPUT', @Hay = @Hay OUTPUT;
IF @Hay = 1
BEGIN
    RAISERROR(N'mnt.ABM_ProductosSku tiene SKUs cargados: no se deshace. Revisar a mano.', 16, 1);
    SET NOEXEC ON;
END
GO

DECLARE @Hay BIT = 0;

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'VigenteDesde') IS NOT NULL
   AND COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'ProductoMaestroId') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT @Hay = CASE WHEN EXISTS (
            SELECT 1 FROM [mnt].[PRD_ParametrosLineaProducto]
            GROUP BY [LineaId], [ProductoMaestroId] HAVING COUNT(*) > 1) THEN 1 ELSE 0 END;',
        N'@Hay BIT OUTPUT', @Hay = @Hay OUTPUT;
IF @Hay = 1
BEGIN
    RAISERROR(N'PRD_ParametrosLineaProducto tiene más de una vigencia por línea y producto: correr antes 33_t798_datos_tango2_rollback.sql o dejar una fila por línea y producto.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 1. ABM_ProductosSku (vacía).
-- ============================================================================
IF OBJECT_ID(N'mnt.ABM_ProductosSku', N'U') IS NOT NULL
BEGIN
    DROP TABLE [mnt].[ABM_ProductosSku];
    PRINT 'Tabla mnt.ABM_ProductosSku eliminada (estaba vacía).'
END
ELSE
    PRINT 'Tabla mnt.ABM_ProductosSku no existe.'
GO

-- ============================================================================
-- 2. PRD_ParametrosLineaProducto -> relación línea-producto.
-- ============================================================================
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'Habilitado') IS NOT NULL
BEGIN
    DECLARE @Quitadas INT;
    EXEC sys.sp_executesql N'DELETE FROM [mnt].[PRD_ParametrosLineaProducto] WHERE [Habilitado] = 0; SET @Quitadas = @@ROWCOUNT;',
        N'@Quitadas INT OUTPUT', @Quitadas = @Quitadas OUTPUT;
    PRINT CONCAT(N'Filas deshabilitadas quitadas: ', @Quitadas, N'.');
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto')
             AND name = N'UQ_PRD_ParametrosLineaProducto_Linea_Producto_Vigencia')
BEGIN
    DROP INDEX [UQ_PRD_ParametrosLineaProducto_Linea_Producto_Vigencia] ON [mnt].[PRD_ParametrosLineaProducto];
    PRINT 'Índice único por vigencia quitado.'
END
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_PRD_ParametrosLineaProducto')
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [PK_PRD_ParametrosLineaProducto];
    PRINT 'PK PK_PRD_ParametrosLineaProducto quitada.'
END
GO

IF OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.key_constraints
                   WHERE parent_object_id = OBJECT_ID(N'mnt.PRD_ParametrosLineaProducto') AND type = 'PK')
BEGIN
    -- Mismo nombre que creó el script 11.
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto]
        ADD CONSTRAINT [PK_LineasProductos] PRIMARY KEY ([LineaId], [ProductoMaestroId]);
    PRINT 'PK PK_LineasProductos (LineaId, ProductoMaestroId) restaurada.'
END
GO

-- CHECKs y defaults, después las columnas.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_ParametrosLineaProducto_Mangas')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [CK_PRD_ParametrosLineaProducto_Mangas];
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_ParametrosLineaProducto_Bolsones')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [CK_PRD_ParametrosLineaProducto_Bolsones];
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_ParametrosLineaProducto_MinutosTurno')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [CK_PRD_ParametrosLineaProducto_MinutosTurno];
GO
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_PRD_ParametrosLineaProducto_Habilitado')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [DF_PRD_ParametrosLineaProducto_Habilitado];
GO
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_PRD_ParametrosLineaProducto_MinutosTurnoReferencia')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [DF_PRD_ParametrosLineaProducto_MinutosTurnoReferencia];
GO
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_PRD_ParametrosLineaProducto_VigenteDesde_T798')
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP CONSTRAINT [DF_PRD_ParametrosLineaProducto_VigenteDesde_T798];
GO

IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'MinutosTurnoReferencia') IS NOT NULL
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP COLUMN [MinutosTurnoReferencia];
GO
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'BolsonesObjetivoTurno') IS NOT NULL
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP COLUMN [BolsonesObjetivoTurno];
GO
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'MangasObjetivoTurno') IS NOT NULL
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP COLUMN [MangasObjetivoTurno];
GO
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'Habilitado') IS NOT NULL
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP COLUMN [Habilitado];
GO
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'VigenteDesde') IS NOT NULL
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP COLUMN [VigenteDesde];
GO
IF COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'ParametroLineaProductoId') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[PRD_ParametrosLineaProducto] DROP COLUMN [ParametroLineaProductoId];
    PRINT 'PRD_ParametrosLineaProducto: vigencia, habilitado, objetivos e Id propio quitados.'
END
GO

-- ============================================================================
-- 3. ABM_TiposFalla.CuentaParaIndicadores.
-- ============================================================================
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_ABM_TiposFalla_CuentaParaIndicadores')
    ALTER TABLE [mnt].[ABM_TiposFalla] DROP CONSTRAINT [DF_ABM_TiposFalla_CuentaParaIndicadores];
GO
IF COL_LENGTH(N'mnt.ABM_TiposFalla', N'CuentaParaIndicadores') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[ABM_TiposFalla] DROP COLUMN [CuentaParaIndicadores];
    PRINT 'Columna ABM_TiposFalla.CuentaParaIndicadores quitada.'
END
GO

-- ============================================================================
-- 4. ABM_ProductosMaestro: columnas de T-798.
-- ============================================================================
IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'RollosPorPaquete') IS NOT NULL
    ALTER TABLE [mnt].[ABM_ProductosMaestro] DROP COLUMN [RollosPorPaquete];
GO
IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'RollosPorManga') IS NOT NULL
    ALTER TABLE [mnt].[ABM_ProductosMaestro] DROP COLUMN [RollosPorManga];
GO
IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'MetrosPorManga') IS NOT NULL
    ALTER TABLE [mnt].[ABM_ProductosMaestro] DROP COLUMN [MetrosPorManga];
GO
IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'TipoProducto') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[ABM_ProductosMaestro] DROP COLUMN [TipoProducto];
    PRINT 'ABM_ProductosMaestro: TipoProducto, MetrosPorManga, RollosPorManga y RollosPorPaquete quitados.'
END
GO

-- ============================================================================
-- 5. ABM_Turnos (opcional).
-- ============================================================================
DECLARE @RestaurarTurnosAnteriores BIT = 0;

IF @RestaurarTurnosAnteriores = 1
BEGIN
    UPDATE [mnt].[ABM_Turnos]
    SET [HoraFin] = '06:00',
        [FechaHoraModificacion] = SYSDATETIMEOFFSET(),
        [ModificadoPorUsuarioId] = N'SCRIPT-32-T798-ROLLBACK'
    WHERE [Codigo] = N'T3' AND [HoraInicio] = '21:00' AND [HoraFin] = '05:00';
    PRINT CONCAT(N'T3 vuelto a 21:00-06:00: ', @@ROWCOUNT, N' fila(s). T1 y T2 no cambian (05-13 y 13-21).');
END
ELSE
    PRINT 'Turnos sin cambios (@RestaurarTurnosAnteriores = 0). Ver en el log del 32 el horario anterior.'
GO

PRINT 'Script 32_t798_maestros_productos_rollback.sql completado.'
GO

SET NOEXEC OFF
GO
