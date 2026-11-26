-- ==============================================================================
-- Script: 40_t802_obligado_turno_rollback.sql
-- Descripción: Deshace 40_t802_obligado_turno.sql (T-802 parte 2):
--   1. Elimina el índice UX_PRD_TurnosProduccionOperadores_Vigente.
--   2. Vuelve a agregar ABM_OperadoresLinea.TurnoHabitualId (INT NULL) con la FK
--      FK_ABM_OperadoresLinea_ABM_Turnos_TurnoHabitualId y le devuelve los valores de mnt.T802_RespaldoTurnoHabitual
--      (las habilitaciones creadas después del 40 quedan sin turno habitual). El respaldo no se borra.
--   3. CONFIG.MAQUINISTAS vuelve a VER,ALTA,MODIFICACION,BAJA y el ADMINISTRADOR recupera alta/modificación/baja.
--   Antes de correrlo, volver a publicar la API anterior (T-798 o T-802 parte 1): la API de la parte 2 no usa la columna.
-- Idempotente. Correr con la API detenida.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.PRD_TurnosProduccionOperadores')
                                       AND name = N'UX_PRD_TurnosProduccionOperadores_Vigente')
BEGIN
    DROP INDEX [UX_PRD_TurnosProduccionOperadores_Vigente] ON [mnt].[PRD_TurnosProduccionOperadores];
    PRINT 'Indice UX_PRD_TurnosProduccionOperadores_Vigente eliminado.'
END
ELSE
    PRINT 'UX_PRD_TurnosProduccionOperadores_Vigente no existe.'
GO

IF OBJECT_ID(N'mnt.ABM_OperadoresLinea', N'U') IS NOT NULL
   AND COL_LENGTH(N'mnt.ABM_OperadoresLinea', N'TurnoHabitualId') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_OperadoresLinea] ADD [TurnoHabitualId] INT NULL;
    PRINT 'Columna ABM_OperadoresLinea.TurnoHabitualId agregada.'
END
GO

IF COL_LENGTH(N'mnt.ABM_OperadoresLinea', N'TurnoHabitualId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk
                   JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                   WHERE fk.parent_object_id = OBJECT_ID(N'mnt.ABM_OperadoresLinea')
                     AND fkc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'mnt.ABM_OperadoresLinea'), N'TurnoHabitualId', 'ColumnId'))
BEGIN
    ALTER TABLE [mnt].[ABM_OperadoresLinea]
        ADD CONSTRAINT [FK_ABM_OperadoresLinea_ABM_Turnos_TurnoHabitualId]
        FOREIGN KEY ([TurnoHabitualId]) REFERENCES [mnt].[ABM_Turnos] ([TurnoId]);
    PRINT 'FK FK_ABM_OperadoresLinea_ABM_Turnos_TurnoHabitualId creada.'
END
GO

IF OBJECT_ID(N'mnt.T802_RespaldoTurnoHabitual', N'U') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE ol SET [TurnoHabitualId] = r.[TurnoHabitualId]
        FROM [mnt].[ABM_OperadoresLinea] ol
        JOIN [mnt].[T802_RespaldoTurnoHabitual] r ON r.[UsuarioLineaId] = ol.[UsuarioLineaId]
        WHERE ol.[TurnoHabitualId] IS NULL AND r.[TurnoHabitualId] IS NOT NULL
          AND EXISTS (SELECT 1 FROM [mnt].[ABM_Turnos] t WHERE t.[TurnoId] = r.[TurnoHabitualId]);';
    PRINT CONCAT('Turno habitual restaurado: ', @@ROWCOUNT, ' fila(s).');
END
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NOT NULL
BEGIN
    UPDATE [mnt].[CFG_Opciones]
    SET [AccionesAplicables] = N'VER,ALTA,MODIFICACION,BAJA', [Nombre] = N'Maquinistas'
    WHERE [Codigo] = N'CONFIG.MAQUINISTAS';

    UPDATE p SET [PuedeAlta] = 1, [PuedeModificar] = 1, [PuedeBaja] = 1,
                 [FechaHoraModificacion] = SYSDATETIMEOFFSET(), [ModificadoPorUsuarioId] = N'SCRIPT-40-T802-ROLLBACK'
    FROM [mnt].[CFG_PermisosRol] p
    JOIN [mnt].[CFG_Opciones] o ON o.[OpcionId] = p.[OpcionId]
    JOIN [dbo].[AspNetRoles] r ON r.[Id] = p.[RoleId]
    WHERE o.[Codigo] = N'CONFIG.MAQUINISTAS' AND r.[Name] = N'ADMINISTRADOR' AND p.[PuedeVer] = 1;
    PRINT 'CONFIG.MAQUINISTAS vuelve a alta/modificacion/baja (ADMINISTRADOR).'
END
GO
