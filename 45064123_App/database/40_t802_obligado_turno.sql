-- ==============================================================================
-- Script: 40_t802_obligado_turno.sql
-- Descripción: T-802 parte 2 · Obligado a turno y alta de usuario en un solo circuito.
--   1. ABM_OperadoresLinea pasa a ser sólo la HABILITACIÓN usuario–línea (se carga desde Configuración → Usuarios):
--      se saca TurnoHabitualId (con su FK y sus índices). Antes se respalda en mnt.T802_RespaldoTurnoHabitual
--      (UsuarioLineaId, UsuarioId, LineaId, TurnoHabitualId) para poder volver atrás. El turno lo declara el operario al
--      abrirlo (el turno en curso por hora); ya no hay turno fijo.
--   2. PRD_TurnosProduccionOperadores: índice único filtrado UX_PRD_TurnosProduccionOperadores_Vigente
--      (TurnoProduccionId) WHERE Hasta IS NULL AND AnuladoPorId IS NULL = un solo operador vigente por turno (dos
--      operarios no pueden tener el mismo turno a la vez; el relevo cierra el tramo anterior). Si hubiera datos que lo
--      violan, NO se crea y se avisa (ver la verificación en deploy/T-802_DEPLOY.md).
--   3. Configuración → Maquinistas queda sólo como CONSULTA: la opción CONFIG.MAQUINISTAS pasa a aplicar sólo VER (y se
--      sacan ALTA/MODIFICACION/BAJA de la matriz). Lo mismo que ya hace el 39 corregido, para bases que corrieron el 39
--      anterior.
-- Versión: 1.0
-- Requiere: 31 (ABM_OperadoresLinea), 34 (PRD_TurnosProduccionOperadores) y 39 (CFG_).
-- Idempotente. Correr con la API detenida, DESPUÉS del 39 y ANTES de publicar la API de T-802.
-- Correr con: sqlcmd -C -I -b -f 65001.
-- Deshacer: 40_t802_obligado_turno_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.ABM_OperadoresLinea', N'U') IS NULL
   OR OBJECT_ID(N'mnt.PRD_TurnosProduccionOperadores', N'U') IS NULL
   OR OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NULL
BEGIN
    RAISERROR(N'Faltan tablas de T-798 o T-802 parte 1 (scripts 31, 34 y 39).', 16, 1);
    SET NOEXEC ON;
END
GO

-- ----------------------------------------------------------------------------- 1. TurnoHabitualId
IF COL_LENGTH(N'mnt.ABM_OperadoresLinea', N'TurnoHabitualId') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'mnt.T802_RespaldoTurnoHabitual', N'U') IS NULL
    BEGIN
        CREATE TABLE [mnt].[T802_RespaldoTurnoHabitual] (
            [UsuarioLineaId]  INT NOT NULL CONSTRAINT [PK_T802_RespaldoTurnoHabitual] PRIMARY KEY,
            [UsuarioId]       NVARCHAR(450) NOT NULL,
            [LineaId]         INT NOT NULL,
            [TurnoHabitualId] INT NULL,
            [FechaRespaldo]   DATETIMEOFFSET(7) NOT NULL
        );
    END

    EXEC sys.sp_executesql N'
        INSERT INTO [mnt].[T802_RespaldoTurnoHabitual] ([UsuarioLineaId], [UsuarioId], [LineaId], [TurnoHabitualId], [FechaRespaldo])
        SELECT ol.[UsuarioLineaId], ol.[UsuarioId], ol.[LineaId], ol.[TurnoHabitualId], SYSDATETIMEOFFSET()
        FROM [mnt].[ABM_OperadoresLinea] ol
        WHERE NOT EXISTS (SELECT 1 FROM [mnt].[T802_RespaldoTurnoHabitual] r WHERE r.[UsuarioLineaId] = ol.[UsuarioLineaId]);';
    PRINT 'Turno habitual respaldado en mnt.T802_RespaldoTurnoHabitual.'

    DECLARE @sql NVARCHAR(MAX) = N'';
    DECLARE @col INT = COLUMNPROPERTY(OBJECT_ID(N'mnt.ABM_OperadoresLinea'), N'TurnoHabitualId', 'ColumnId');

    -- FKs que usan la columna
    SELECT @sql += N'ALTER TABLE [mnt].[ABM_OperadoresLinea] DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID(N'mnt.ABM_OperadoresLinea') AND fkc.parent_column_id = @col;

    -- Índices (no PK) que la usan
    SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON [mnt].[ABM_OperadoresLinea];' + CHAR(10)
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'mnt.ABM_OperadoresLinea') AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
      AND EXISTS (SELECT 1 FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.column_id = @col);

    -- Default (si tuviera)
    SELECT @sql += N'ALTER TABLE [mnt].[ABM_OperadoresLinea] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(10)
    FROM sys.default_constraints dc
    WHERE dc.parent_object_id = OBJECT_ID(N'mnt.ABM_OperadoresLinea') AND dc.parent_column_id = @col;

    SET @sql += N'ALTER TABLE [mnt].[ABM_OperadoresLinea] DROP COLUMN [TurnoHabitualId];';
    EXEC sys.sp_executesql @sql;

    COMMIT TRANSACTION;
    PRINT 'Columna ABM_OperadoresLinea.TurnoHabitualId eliminada (con su FK e indices).'
END
ELSE
    PRINT 'ABM_OperadoresLinea.TurnoHabitualId ya no existe: sin cambios.'
GO

-- ----------------------------------------------------------------------------- 2. Un solo operador vigente por turno
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.PRD_TurnosProduccionOperadores')
                                          AND name = N'UX_PRD_TurnosProduccionOperadores_Vigente')
BEGIN
    DECLARE @duplicados INT = (
        SELECT COUNT(*) FROM (
            SELECT [TurnoProduccionId] FROM [mnt].[PRD_TurnosProduccionOperadores]
            WHERE [Hasta] IS NULL AND [AnuladoPorId] IS NULL
            GROUP BY [TurnoProduccionId] HAVING COUNT(*) > 1) d);
    IF @duplicados = 0
    BEGIN
        CREATE UNIQUE INDEX [UX_PRD_TurnosProduccionOperadores_Vigente]
            ON [mnt].[PRD_TurnosProduccionOperadores] ([TurnoProduccionId])
            WHERE [Hasta] IS NULL AND [AnuladoPorId] IS NULL;
        PRINT 'Indice UX_PRD_TurnosProduccionOperadores_Vigente creado (un operador vigente por turno).'
    END
    ELSE
        RAISERROR(N'AVISO: %d turno(s) tienen mas de un operador vigente: NO se creo UX_PRD_TurnosProduccionOperadores_Vigente. Corregir el operador desde Turnos de produccion y volver a correr el 40.', 10, 1, @duplicados) WITH NOWAIT;
END
ELSE
    PRINT 'UX_PRD_TurnosProduccionOperadores_Vigente ya existe.'
GO

-- ----------------------------------------------------------------------------- 3. Maquinistas sólo consulta
BEGIN TRANSACTION;

UPDATE [mnt].[CFG_Opciones]
SET [AccionesAplicables] = N'VER', [Nombre] = N'Maquinistas (consulta)'
WHERE [Codigo] = N'CONFIG.MAQUINISTAS'
  AND ([AccionesAplicables] <> N'VER' OR [Nombre] <> N'Maquinistas (consulta)');
PRINT CONCAT('CFG_Opciones CONFIG.MAQUINISTAS -> solo VER: ', @@ROWCOUNT, ' fila(s).');

UPDATE p
SET [PuedeAlta] = 0, [PuedeModificar] = 0, [PuedeBaja] = 0,
    [FechaHoraModificacion] = SYSDATETIMEOFFSET(), [ModificadoPorUsuarioId] = N'SCRIPT-40-T802'
FROM [mnt].[CFG_PermisosRol] p
JOIN [mnt].[CFG_Opciones] o ON o.[OpcionId] = p.[OpcionId]
WHERE o.[Codigo] = N'CONFIG.MAQUINISTAS' AND (p.[PuedeAlta] = 1 OR p.[PuedeModificar] = 1 OR p.[PuedeBaja] = 1);
PRINT CONCAT('CFG_PermisosRol CONFIG.MAQUINISTAS sin alta/modificacion/baja: ', @@ROWCOUNT, ' fila(s).');

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
