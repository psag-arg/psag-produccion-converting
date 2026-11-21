-- ==============================================================================
-- Script: 33_t798_datos_tango2.sql
-- Descripción: T-798 etapa 1 · Datos de negocio de TANGO 2 confirmados por el cliente
--   (docs/diseno/T-798_modelo_produccion.md, 2.0 punto 9 y 2.5).
--   1. Producto HIGIENICO 30 MTS (ya existe en producción, script 24; si no existe se crea con
--      Nombre 'Higiénico 30 mts'): MetrosPorManga 30, RollosPorManga 28, RollosPorPaquete 4.
--      Descripcion 'HIGIÉNICO SH 30 mts x 4 – PH1 20' solo si está vacía.
--   2. Producto HIGIENICO 50 MTS (se crea si no existe): Nombre 'Higiénico 50 mts',
--      Descripcion 'HIGIÉNICO SH 50 mts x 4 – PH1 17', MetrosPorManga 50, RollosPorPaquete 4,
--      RollosPorManga sin dato (NULL).
--   3. Parámetros (presupuesto) de Tango 2 vigentes desde 2026-11-01, habilitados, 480 min:
--      SH 30 -> 11200 mangas y 6500 bolsones por turno; SH 50 -> 6720 mangas y 4700 bolsones.
--   NO carga SKUs ni paquetes por bolsón (no hay dato del cliente).
--
--   SUPUESTO A CONFIRMAR: la equivalencia "HIGIENICO 30 MTS = SH 30 x 4" (el producto que cargó el
--   script 24 desde la planilla "Control Proceso Tango 2 (30mts)" es el SH 30 x 4 de los objetivos).
--   Lo mismo para el SH 50 x 4 con el código nuevo HIGIENICO 50 MTS.
--
--   La línea se busca por Nombre = 'Tango 2'. Solo inserta lo que falta (por código y por
--   línea + producto + vigencia); los datos de producto se completan en cada corrida.
--   Acentos con NCHAR: se puede correr con sqlcmd -I -b (y -f 65001).
-- Versión: 1.0
-- Requiere: 31_t798_renombre_por_modulo.sql y 32_t798_maestros_productos.sql.
-- Idempotente: se puede ejecutar más de una vez.
-- Deshacer: 33_t798_datos_tango2_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'mnt.ABM_ProductosMaestro', N'MetrosPorManga') IS NULL
   OR COL_LENGTH(N'mnt.PRD_ParametrosLineaProducto', N'VigenteDesde') IS NULL
BEGIN
    RAISERROR(N'Falta correr 31_t798_renombre_por_modulo.sql y 32_t798_maestros_productos.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

IF NOT EXISTS (SELECT 1 FROM [mnt].[ABM_Lineas] WHERE [Nombre] = N'Tango 2')
BEGIN
    RAISERROR(N'No existe la línea con Nombre = ''Tango 2'' en mnt.ABM_Lineas: revisar el nombre en Maestros.', 16, 1);
    SET NOEXEC ON;
END
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-33-T798';
DECLARE @Ahora DATETIMEOFFSET = SYSDATETIMEOFFSET();
DECLARE @LineaId INT = (SELECT [LineaId] FROM [mnt].[ABM_Lineas] WHERE [Nombre] = N'Tango 2');

-- Textos con acentos: é = NCHAR(233), É = NCHAR(201), guion largo – = NCHAR(8211).
DECLARE @Nombre30 NVARCHAR(100) = N'Higi' + NCHAR(233) + N'nico 30 mts';
DECLARE @Nombre50 NVARCHAR(100) = N'Higi' + NCHAR(233) + N'nico 50 mts';
DECLARE @Descripcion30 NVARCHAR(500) = N'HIGI' + NCHAR(201) + N'NICO SH 30 mts x 4 ' + NCHAR(8211) + N' PH1 20';
DECLARE @Descripcion50 NVARCHAR(500) = N'HIGI' + NCHAR(201) + N'NICO SH 50 mts x 4 ' + NCHAR(8211) + N' PH1 17';

-- ----------------------------------------------------------------------------
-- 1. HIGIENICO 30 MTS
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [mnt].[ABM_ProductosMaestro] WHERE [Codigo] = N'HIGIENICO 30 MTS')
BEGIN
    INSERT INTO [mnt].[ABM_ProductosMaestro]
        ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [MetrosPorManga], [RollosPorManga], [RollosPorPaquete],
         [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES
        (N'HIGIENICO 30 MTS', @Nombre30, @Descripcion30, 1, 1, 30, 28, 4, @Ahora, @Ahora, @Usuario, @Usuario);
    PRINT 'Producto HIGIENICO 30 MTS creado.'
END
ELSE
BEGIN
    UPDATE [mnt].[ABM_ProductosMaestro]
    SET [MetrosPorManga] = 30,
        [RollosPorManga] = 28,
        [RollosPorPaquete] = 4,
        [Descripcion] = CASE WHEN NULLIF(LTRIM(RTRIM([Descripcion])), N'') IS NULL THEN @Descripcion30 ELSE [Descripcion] END,
        [FechaHoraModificacion] = @Ahora,
        [ModificadoPorUsuarioId] = @Usuario
    WHERE [Codigo] = N'HIGIENICO 30 MTS'
      AND (   [MetrosPorManga] IS NULL OR [MetrosPorManga] <> 30
           OR [RollosPorManga] IS NULL OR [RollosPorManga] <> 28
           OR [RollosPorPaquete] IS NULL OR [RollosPorPaquete] <> 4
           OR NULLIF(LTRIM(RTRIM([Descripcion])), N'') IS NULL);
    PRINT CONCAT(N'Producto HIGIENICO 30 MTS completado (metros, rollos, descripción si vacía): ', @@ROWCOUNT, N' fila(s).');
END

-- ----------------------------------------------------------------------------
-- 2. HIGIENICO 50 MTS (RollosPorManga sin dato)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [mnt].[ABM_ProductosMaestro] WHERE [Codigo] = N'HIGIENICO 50 MTS')
BEGIN
    INSERT INTO [mnt].[ABM_ProductosMaestro]
        ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [MetrosPorManga], [RollosPorManga], [RollosPorPaquete],
         [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES
        (N'HIGIENICO 50 MTS', @Nombre50, @Descripcion50, 2, 1, 50, NULL, 4, @Ahora, @Ahora, @Usuario, @Usuario);
    PRINT 'Producto HIGIENICO 50 MTS creado.'
END
ELSE
BEGIN
    UPDATE [mnt].[ABM_ProductosMaestro]
    SET [MetrosPorManga] = 50,
        [RollosPorPaquete] = 4,
        [Descripcion] = CASE WHEN NULLIF(LTRIM(RTRIM([Descripcion])), N'') IS NULL THEN @Descripcion50 ELSE [Descripcion] END,
        [FechaHoraModificacion] = @Ahora,
        [ModificadoPorUsuarioId] = @Usuario
    WHERE [Codigo] = N'HIGIENICO 50 MTS'
      AND (   [MetrosPorManga] IS NULL OR [MetrosPorManga] <> 50
           OR [RollosPorPaquete] IS NULL OR [RollosPorPaquete] <> 4
           OR NULLIF(LTRIM(RTRIM([Descripcion])), N'') IS NULL);
    PRINT CONCAT(N'Producto HIGIENICO 50 MTS ya existía; completado: ', @@ROWCOUNT, N' fila(s).');
END

-- ----------------------------------------------------------------------------
-- 3. Parámetros de Tango 2 vigentes desde 2026-11-01 (presupuesto por turno de 480 min).
-- ----------------------------------------------------------------------------
DECLARE @Parametros TABLE ([Codigo] NVARCHAR(50), [Mangas] INT, [Bolsones] INT);
INSERT INTO @Parametros ([Codigo], [Mangas], [Bolsones]) VALUES
    (N'HIGIENICO 30 MTS', 11200, 6500),
    (N'HIGIENICO 50 MTS', 6720, 4700);

INSERT INTO [mnt].[PRD_ParametrosLineaProducto]
    ([LineaId], [ProductoMaestroId], [VigenteDesde], [Habilitado], [MangasObjetivoTurno], [BolsonesObjetivoTurno], [MinutosTurnoReferencia])
SELECT @LineaId, p.[ProductoMaestroId], '20261101', 1, x.[Mangas], x.[Bolsones], 480
FROM @Parametros x
JOIN [mnt].[ABM_ProductosMaestro] p ON p.[Codigo] = x.[Codigo]
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PRD_ParametrosLineaProducto] e
                  WHERE e.[LineaId] = @LineaId
                    AND e.[ProductoMaestroId] = p.[ProductoMaestroId]
                    AND e.[VigenteDesde] = '20261101');
PRINT CONCAT(N'Parámetros de Tango 2 desde 2026-11-01 cargados: ', @@ROWCOUNT, N' (de 2).');

COMMIT TRANSACTION;
GO

PRINT 'Script 33_t798_datos_tango2.sql completado.'
GO

SET NOEXEC OFF
GO
