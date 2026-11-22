-- ==============================================================================
-- Script: 33_t798_datos_tango2_rollback.sql
-- Descripción: Deshace 33_t798_datos_tango2.sql (T-798 etapa 1).
--   1. Quita los parámetros de Tango 2 vigentes desde 2026-11-01 de HIGIENICO 30 MTS y 50 MTS.
--   2. Productos creados por el 33 (CreadoPorUsuarioId = 'SCRIPT-33-T798'): se eliminan si nada los usa
--      (parámetros, SKUs, estándares, registros de proceso o de calidad). Si se usan, quedan y se avisa.
--   3. HIGIENICO 30 MTS que ya existía: vuelve a dejar sin dato MetrosPorManga, RollosPorManga y
--      RollosPorPaquete si tienen los valores del 33, y la descripción si es la que puso el 33
--      (el 33 solo la completaba si estaba vacía). Lo mismo para HIGIENICO 50 MTS si ya existía.
-- Idempotente: se puede ejecutar más de una vez.
-- Correr antes de 32_t798_maestros_productos_rollback.sql y después de 34_t798_turnos_produccion_rollback.sql (se verifica).
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

IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'MetrosPorManga') IS NULL
   OR COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'VigenteDesde') IS NULL
BEGIN
    PRINT 'No están las columnas del 32: no hay nada del 33 para deshacer.'
    SET NOEXEC ON;
END
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-33-T798';
DECLARE @Ahora DATETIMEOFFSET = SYSDATETIMEOFFSET();
DECLARE @LineaId INT = (SELECT [LineaId] FROM [mnt].[ABM_Lineas] WHERE [Nombre] = N'Tango 2');
DECLARE @Descripcion30 NVARCHAR(500) = N'HIGI' + NCHAR(201) + N'NICO SH 30 mts x 4 ' + NCHAR(8211) + N' PH1 20';
DECLARE @Descripcion50 NVARCHAR(500) = N'HIGI' + NCHAR(201) + N'NICO SH 50 mts x 4 ' + NCHAR(8211) + N' PH1 17';

-- 1. Parámetros desde 2026-11-01.
DELETE pl
FROM [mnt].[PRD_ParametrosLineaProducto] pl
JOIN [mnt].[ABM_ProductosMaestro] p ON p.[ProductoMaestroId] = pl.[ProductoMaestroId]
WHERE pl.[LineaId] = @LineaId
  AND pl.[VigenteDesde] = '20261101'
  AND p.[Codigo] IN (N'HIGIENICO 30 MTS', N'HIGIENICO 50 MTS');
PRINT CONCAT(N'Parámetros de Tango 2 desde 2026-11-01 quitados: ', @@ROWCOUNT, N'.');

-- 2. Productos creados por el 33 que nada usa.
DELETE p
FROM [mnt].[ABM_ProductosMaestro] p
WHERE p.[Codigo] IN (N'HIGIENICO 30 MTS', N'HIGIENICO 50 MTS')
  AND p.[CreadoPorUsuarioId] = @Usuario
  AND NOT EXISTS (SELECT 1 FROM [mnt].[PRD_ParametrosLineaProducto] x WHERE x.[ProductoMaestroId] = p.[ProductoMaestroId])
  AND NOT EXISTS (SELECT 1 FROM [mnt].[ABM_ProductosSku] x WHERE x.[ProductoMaestroId] = p.[ProductoMaestroId])
  AND NOT EXISTS (SELECT 1 FROM [mnt].[CPR_Estandares] x WHERE x.[ProductoMaestroId] = p.[ProductoMaestroId])
  AND NOT EXISTS (SELECT 1 FROM [mnt].[CPR_Registros] x WHERE x.[ProductoMaestroId] = p.[ProductoMaestroId])
  AND NOT EXISTS (SELECT 1 FROM [mnt].[CAL_Registros] x WHERE x.[ProductoMaestroId] = p.[ProductoMaestroId]);
PRINT CONCAT(N'Productos creados por el 33 eliminados: ', @@ROWCOUNT, N'.');

IF EXISTS (SELECT 1 FROM [mnt].[ABM_ProductosMaestro]
           WHERE [Codigo] IN (N'HIGIENICO 30 MTS', N'HIGIENICO 50 MTS') AND [CreadoPorUsuarioId] = @Usuario)
    PRINT 'AVISO: algún producto creado por el 33 ya se usa (parámetros, SKU, estándares o registros): queda cargado.'

-- 3. Datos que el 33 completó en productos que ya existían.
UPDATE [mnt].[ABM_ProductosMaestro]
SET [MetrosPorManga] = CASE WHEN [MetrosPorManga] = 30 THEN NULL ELSE [MetrosPorManga] END,
    [RollosPorManga] = CASE WHEN [RollosPorManga] = 28 THEN NULL ELSE [RollosPorManga] END,
    [RollosPorPaquete] = CASE WHEN [RollosPorPaquete] = 4 THEN NULL ELSE [RollosPorPaquete] END,
    [Descripcion] = CASE WHEN [Descripcion] = @Descripcion30 THEN NULL ELSE [Descripcion] END,
    [FechaHoraModificacion] = @Ahora,
    [ModificadoPorUsuarioId] = N'SCRIPT-33-T798-ROLLBACK'
WHERE [Codigo] = N'HIGIENICO 30 MTS'
  AND [CreadoPorUsuarioId] <> @Usuario
  AND ([MetrosPorManga] = 30 OR [RollosPorManga] = 28 OR [RollosPorPaquete] = 4 OR [Descripcion] = @Descripcion30);
PRINT CONCAT(N'HIGIENICO 30 MTS: datos del 33 quitados en ', @@ROWCOUNT, N' fila(s).');

UPDATE [mnt].[ABM_ProductosMaestro]
SET [MetrosPorManga] = CASE WHEN [MetrosPorManga] = 50 THEN NULL ELSE [MetrosPorManga] END,
    [RollosPorPaquete] = CASE WHEN [RollosPorPaquete] = 4 THEN NULL ELSE [RollosPorPaquete] END,
    [Descripcion] = CASE WHEN [Descripcion] = @Descripcion50 THEN NULL ELSE [Descripcion] END,
    [FechaHoraModificacion] = @Ahora,
    [ModificadoPorUsuarioId] = N'SCRIPT-33-T798-ROLLBACK'
WHERE [Codigo] = N'HIGIENICO 50 MTS'
  AND [CreadoPorUsuarioId] <> @Usuario
  AND ([MetrosPorManga] = 50 OR [RollosPorPaquete] = 4 OR [Descripcion] = @Descripcion50);
PRINT CONCAT(N'HIGIENICO 50 MTS: datos del 33 quitados en ', @@ROWCOUNT, N' fila(s).');

COMMIT TRANSACTION;
GO

PRINT 'Script 33_t798_datos_tango2_rollback.sql completado.'
GO

SET NOEXEC OFF
GO
