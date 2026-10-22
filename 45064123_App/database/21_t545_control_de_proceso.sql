-- ==============================================================================
-- Script: 21_t545_control_de_proceso.sql
-- Descripción: T-545 · La pestaña "Registros" pasa a llamarse "Control de Proceso".
--              Plantilla de puntos de control por línea (tipo de dato, rango mín/máx,
--              unidad y frecuencia) administrable desde Maestros, producto opcional
--              en la carga y corrección del nombre del Turno 1 (Mañana).
-- Versión: 1.0 - T-545
-- Idempotente: se puede ejecutar más de una vez.
-- No borra ni migra datos: los registros de proceso ya cargados siguen igual.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Parametros: rango y frecuencia de la plantilla de la línea
-- ============================================================================
IF COL_LENGTH(N'mnt.Parametros', N'ValorMin') IS NULL
BEGIN
    ALTER TABLE [mnt].[Parametros] ADD [ValorMin] DECIMAL(18,4) NULL
    PRINT 'Columna mnt.Parametros.ValorMin creada.'
END
GO

IF COL_LENGTH(N'mnt.Parametros', N'ValorMax') IS NULL
BEGIN
    ALTER TABLE [mnt].[Parametros] ADD [ValorMax] DECIMAL(18,4) NULL
    PRINT 'Columna mnt.Parametros.ValorMax creada.'
END
GO

-- Sin CHECK a propósito: los valores los valida la app (enum FrecuenciaControl) y así
-- sumar una frecuencia nueva no pide otro script.
IF COL_LENGTH(N'mnt.Parametros', N'Frecuencia') IS NULL
BEGIN
    ALTER TABLE [mnt].[Parametros]
    ADD [Frecuencia] VARCHAR(30) NOT NULL CONSTRAINT [DF_Parametros_Frecuencia] DEFAULT 'POR_TURNO'
    PRINT 'Columna mnt.Parametros.Frecuencia creada (POR_TURNO para los existentes).'
END
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE [name] = N'CK_Parametros_TipoDato'
             AND [parent_object_id] = OBJECT_ID(N'[mnt].[Parametros]')
             AND [definition] NOT LIKE N'%OK_NOOK%')
BEGIN
    ALTER TABLE [mnt].[Parametros] DROP CONSTRAINT [CK_Parametros_TipoDato]
    PRINT 'CHECK CK_Parametros_TipoDato anterior eliminado.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE [name] = N'CK_Parametros_TipoDato'
                 AND [parent_object_id] = OBJECT_ID(N'[mnt].[Parametros]'))
BEGIN
    ALTER TABLE [mnt].[Parametros] WITH CHECK
    ADD CONSTRAINT [CK_Parametros_TipoDato] CHECK ([TipoDato] IN ('NUMERICO', 'RANGO', 'TEXTO', 'OK_NOOK'))
    PRINT 'CHECK CK_Parametros_TipoDato con OK_NOOK creado.'
END
GO

-- ============================================================================
-- RegistrosProceso.ProductoId opcional (control solo con la plantilla de la línea)
-- Para cambiar la nulabilidad hay que soltar los índices / UNIQUE sobre la columna
-- y volver a crearlos. La FK a Productos no molesta y queda como está.
-- El UNIQUE de SQL Server trata los NULL como iguales: sigue habiendo un solo
-- control por línea + turno + fecha cuando no hay producto.
-- ============================================================================
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE [object_id] = OBJECT_ID(N'[mnt].[RegistrosProceso]')
             AND [name] = N'ProductoId'
             AND [is_nullable] = 0)
BEGIN
    BEGIN TRANSACTION;

    DECLARE @sql NVARCHAR(MAX) = N'';

    -- UNIQUE constraints sobre ProductoId
    SELECT @sql += N'ALTER TABLE [mnt].[RegistrosProceso] DROP CONSTRAINT ' + QUOTENAME(kc.[name]) + N';' + CHAR(10)
    FROM sys.key_constraints kc
    WHERE kc.[parent_object_id] = OBJECT_ID(N'[mnt].[RegistrosProceso]')
      AND kc.[type] = 'UQ'
      AND EXISTS (SELECT 1 FROM sys.index_columns ic
                  JOIN sys.columns c ON c.[object_id] = ic.[object_id] AND c.[column_id] = ic.[column_id]
                  WHERE ic.[object_id] = kc.[parent_object_id] AND ic.[index_id] = kc.[unique_index_id]
                    AND c.[name] = N'ProductoId');

    -- Índices sueltos sobre ProductoId (no PK ni UNIQUE constraint)
    SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.[name]) + N' ON [mnt].[RegistrosProceso];' + CHAR(10)
    FROM sys.indexes i
    WHERE i.[object_id] = OBJECT_ID(N'[mnt].[RegistrosProceso]')
      AND i.[is_primary_key] = 0
      AND i.[is_unique_constraint] = 0
      AND i.[name] IS NOT NULL
      AND EXISTS (SELECT 1 FROM sys.index_columns ic
                  JOIN sys.columns c ON c.[object_id] = ic.[object_id] AND c.[column_id] = ic.[column_id]
                  WHERE ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id]
                    AND c.[name] = N'ProductoId');

    PRINT @sql;
    EXEC sp_executesql @sql;

    ALTER TABLE [mnt].[RegistrosProceso] ALTER COLUMN [ProductoId] INT NULL;

    COMMIT TRANSACTION;
    PRINT 'Columna mnt.RegistrosProceso.ProductoId ahora admite NULL.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints
               WHERE [name] = N'UQ_RegistrosProceso_LineaProductoTurnoFecha'
                 AND [parent_object_id] = OBJECT_ID(N'[mnt].[RegistrosProceso]'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE [object_id] = OBJECT_ID(N'[mnt].[RegistrosProceso]')
                     AND [name] = N'UQ_RegistrosProceso_LineaProductoTurnoFecha')
BEGIN
    ALTER TABLE [mnt].[RegistrosProceso]
    ADD CONSTRAINT [UQ_RegistrosProceso_LineaProductoTurnoFecha] UNIQUE ([LineaId], [ProductoId], [TurnoId], [Fecha])
    PRINT 'UNIQUE UQ_RegistrosProceso_LineaProductoTurnoFecha recreado.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [object_id] = OBJECT_ID(N'[mnt].[RegistrosProceso]')
                 AND [name] = N'IX_RegistrosProceso_ProductoId')
BEGIN
    CREATE INDEX [IX_RegistrosProceso_ProductoId] ON [mnt].[RegistrosProceso] ([ProductoId]);
    PRINT 'Índice IX_RegistrosProceso_ProductoId recreado.'
END
GO

-- ============================================================================
-- Turnos: "Turno 1 (Manana)" -> "Turno 1 (Mañana)"
-- La ñ va con NCHAR(241) para que no dependa de cómo se abra el archivo
-- (justamente así se perdió la primera vez). Solo toca el nombre exacto.
-- ============================================================================
UPDATE [mnt].[Turnos]
SET [Nombre] = N'Turno 1 (Ma' + NCHAR(241) + N'ana)',
    [FechaHoraModificacion] = SYSDATETIMEOFFSET()
WHERE [Nombre] = N'Turno 1 (Manana)';

PRINT CONCAT(N'Turnos corregidos a Mañana: ', @@ROWCOUNT, N'.');
GO

-- ============================================================================
-- PLANTILLA DE CARGA DE PUNTOS DE CONTROL (comentada)
-- ----------------------------------------------------------------------------
-- La planilla del cliente "Control Proceso Tango 2.xls" todavía no llegó, así que
-- Tango 2 queda SIN puntos. Se pueden cargar a mano desde la app
-- (Maestros > Puntos de control de proceso) o, cuando llegue la planilla, completar
-- la tabla @Puntos de abajo (una fila por punto), descomentar el bloque y ejecutarlo.
-- Es idempotente: no duplica grupos (línea + nombre) ni puntos (grupo + nombre).
--
-- Columnas:
--   Grupo       agrupador que se ve como pestaña (ej. equipo: 'Rebobinadora')
--   OrdenGrupo  orden de la pestaña
--   Seccion     subtítulo dentro del grupo (opcional, NULL)
--   Nombre      punto de control tal cual figura en la planilla
--   TipoDato    'NUMERICO' | 'OK_NOOK' | 'TEXTO'
--   Unidad      solo NUMERICO (ej. 'bar', 'm/min'), si no NULL
--   Minimo      solo NUMERICO, NULL = sin piso
--   Maximo      solo NUMERICO, NULL = sin techo
--   Frecuencia  'POR_TURNO' | 'INICIO_TURNO' | 'CAMBIO_PRODUCTO' | 'CADA_HORA' | 'CADA_2_HORAS' | 'DIARIA'
--   Orden       orden del punto dentro del grupo
-- ============================================================================
/*
DECLARE @Linea NVARCHAR(100) = N'Tango 2';
DECLARE @Usuario NVARCHAR(450) = N'SYSTEM';
DECLARE @LineaId INT = (SELECT TOP (1) [LineaId] FROM [mnt].[Lineas] WHERE [Nombre] = @Linea);

IF @LineaId IS NULL
    RAISERROR(N'No existe la línea indicada en mnt.Lineas.', 16, 1);
ELSE
BEGIN
    DECLARE @Puntos TABLE (
        [Grupo] NVARCHAR(100) NOT NULL, [OrdenGrupo] INT NOT NULL, [Seccion] NVARCHAR(100) NULL,
        [Nombre] NVARCHAR(200) NOT NULL, [TipoDato] VARCHAR(20) NOT NULL, [Unidad] NVARCHAR(50) NULL,
        [Minimo] DECIMAL(18,4) NULL, [Maximo] DECIMAL(18,4) NULL, [Frecuencia] VARCHAR(30) NOT NULL, [Orden] INT NOT NULL);

    -- Ejemplo de formato (NO son puntos reales: reemplazar por los de la planilla)
    INSERT INTO @Puntos VALUES
    -- (N'<Grupo>',     1, NULL,         N'<Punto numérico>', 'NUMERICO', N'bar', 3.8, 4.1, 'POR_TURNO', 1),
    -- (N'<Grupo>',     1, N'<Sección>', N'<Punto OK/NO OK>', 'OK_NOOK',  NULL,   NULL, NULL, 'INICIO_TURNO', 2),
    -- (N'<Grupo>',     1, NULL,         N'<Punto de texto>', 'TEXTO',    NULL,   NULL, NULL, 'POR_TURNO', 3);

    -- Grupos que falten
    INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Orden], [Activo],
        [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    SELECT @LineaId,
           CONCAT(N'CP-L', @LineaId, N'-G', RIGHT(CONCAT(N'00', ROW_NUMBER() OVER (ORDER BY g.[OrdenGrupo], g.[Grupo])
               + (SELECT COUNT(*) FROM [mnt].[GruposParametro] WHERE [LineaId] = @LineaId)), 2)),
           g.[Grupo], g.[OrdenGrupo], 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
    FROM (SELECT DISTINCT [Grupo], [OrdenGrupo] FROM @Puntos) g
    WHERE NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] gp WHERE gp.[LineaId] = @LineaId AND gp.[Nombre] = g.[Grupo]);

    -- Puntos que falten
    INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato],
        [ValorMin], [ValorMax], [Frecuencia], [Orden], [Activo],
        [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    SELECT gp.[GrupoParametroId], p.[Seccion],
           CONCAT(N'CP-L', @LineaId, N'-', RIGHT(CONCAT(N'000', ROW_NUMBER() OVER (ORDER BY p.[OrdenGrupo], p.[Orden], p.[Nombre])
               + (SELECT COUNT(*) FROM [mnt].[Parametros] x JOIN [mnt].[GruposParametro] y ON y.[GrupoParametroId] = x.[GrupoParametroId]
                  WHERE y.[LineaId] = @LineaId)), 3)),
           p.[Nombre], p.[Unidad], p.[TipoDato], p.[Minimo], p.[Maximo], p.[Frecuencia], p.[Orden], 1,
           SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
    FROM @Puntos p
    JOIN [mnt].[GruposParametro] gp ON gp.[LineaId] = @LineaId AND gp.[Nombre] = p.[Grupo]
    WHERE NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] x WHERE x.[GrupoParametroId] = gp.[GrupoParametroId] AND x.[Nombre] = p.[Nombre]);

    PRINT CONCAT(N'Puntos de control cargados: ', @@ROWCOUNT, N'.');
END
GO
*/

PRINT 'Script 21_t545_control_de_proceso.sql completado.'
GO
