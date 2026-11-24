-- ==============================================================================
-- Script: 44_turno_automatico_rollback.sql
-- Descripción: Deshace 44_turno_automatico.sql (turno automático).
--   Antes de correrlo, volver a publicar la API anterior (T-806): la API del turno automático usa las columnas.
--   1. DET_Detenciones: respalda los vínculos (DetencionId, DetencionAnteriorId) en mnt.T44_RespaldoDetencionesPartes y saca
--      el índice UX_DET_Detenciones_DetencionAnteriorId, la FK y la columna DetencionAnteriorId. Las partes quedan como
--      detenciones sueltas (no se borra ninguna: insert-only).
--   2. PRD_TurnosProduccionProductos: respalda los tramos HEREDADO (Id, ConfirmadoPorUsuarioId, FechaHoraConfirmacion) en
--      mnt.T44_RespaldoTramosHeredados, los pasa a Origen LOGIN (el CHECK viejo no admite HEREDADO), vuelve el CHECK de
--      origen a ('LOGIN', 'CAMBIO', 'SUPERVISOR') y saca ConfirmadoPorUsuarioId y FechaHoraConfirmacion.
--   3. ABM_Turnos: saca el CHECK, el DEFAULT y la columna ToleranciaCierreOperadorMinutos.
--   Los respaldos no se borran.
-- Idempotente. Correr con la API detenida.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ----------------------------------------------------------------------------- 1. Detenciones
IF COL_LENGTH(N'mnt.DET_Detenciones', N'DetencionAnteriorId') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'mnt.T44_RespaldoDetencionesPartes', N'U') IS NULL
        CREATE TABLE [mnt].[T44_RespaldoDetencionesPartes] (
            [DetencionId]         BIGINT NOT NULL CONSTRAINT [PK_T44_RespaldoDetencionesPartes] PRIMARY KEY,
            [DetencionAnteriorId] BIGINT NOT NULL,
            [FechaRespaldo]       DATETIMEOFFSET NOT NULL
        );
    EXEC sys.sp_executesql N'
        INSERT INTO [mnt].[T44_RespaldoDetencionesPartes] ([DetencionId], [DetencionAnteriorId], [FechaRespaldo])
        SELECT d.[DetencionId], d.[DetencionAnteriorId], SYSDATETIMEOFFSET()
        FROM [mnt].[DET_Detenciones] d
        WHERE d.[DetencionAnteriorId] IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM [mnt].[T44_RespaldoDetencionesPartes] r WHERE r.[DetencionId] = d.[DetencionId]);';
    PRINT 'Vínculos de partes respaldados en mnt.T44_RespaldoDetencionesPartes.'
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'UX_DET_Detenciones_DetencionAnteriorId')
BEGIN
    DROP INDEX [UX_DET_Detenciones_DetencionAnteriorId] ON [mnt].[DET_Detenciones];
    PRINT 'Indice UX_DET_Detenciones_DetencionAnteriorId eliminado.'
END
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId')
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] DROP CONSTRAINT [FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId];
    PRINT 'FK FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId eliminada.'
END
GO

IF COL_LENGTH(N'mnt.DET_Detenciones', N'DetencionAnteriorId') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] DROP COLUMN [DetencionAnteriorId];
    PRINT 'Columna DET_Detenciones.DetencionAnteriorId eliminada.'
END
GO

-- ----------------------------------------------------------------------------- 2. Tramos heredados
IF COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'ConfirmadoPorUsuarioId') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'mnt.T44_RespaldoTramosHeredados', N'U') IS NULL
        CREATE TABLE [mnt].[T44_RespaldoTramosHeredados] (
            [TurnoProduccionProductoId] BIGINT NOT NULL CONSTRAINT [PK_T44_RespaldoTramosHeredados] PRIMARY KEY,
            [ConfirmadoPorUsuarioId]    NVARCHAR(450) NULL,
            [FechaHoraConfirmacion]     DATETIMEOFFSET NULL,
            [FechaRespaldo]             DATETIMEOFFSET NOT NULL
        );
    EXEC sys.sp_executesql N'
        INSERT INTO [mnt].[T44_RespaldoTramosHeredados] ([TurnoProduccionProductoId], [ConfirmadoPorUsuarioId], [FechaHoraConfirmacion], [FechaRespaldo])
        SELECT p.[TurnoProduccionProductoId], p.[ConfirmadoPorUsuarioId], p.[FechaHoraConfirmacion], SYSDATETIMEOFFSET()
        FROM [mnt].[PRD_TurnosProduccionProductos] p
        WHERE p.[Origen] = N''HEREDADO''
          AND NOT EXISTS (SELECT 1 FROM [mnt].[T44_RespaldoTramosHeredados] r WHERE r.[TurnoProduccionProductoId] = p.[TurnoProduccionProductoId]);
        UPDATE [mnt].[PRD_TurnosProduccionProductos] SET [Origen] = N''LOGIN'' WHERE [Origen] = N''HEREDADO'';';
    PRINT 'Tramos HEREDADO respaldados en mnt.T44_RespaldoTramosHeredados y pasados a LOGIN.'
END
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_TurnosProduccionProductos_Origen' AND definition LIKE N'%HEREDADO%')
BEGIN
    BEGIN TRANSACTION;
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] DROP CONSTRAINT [CK_PRD_TurnosProduccionProductos_Origen];
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] ADD CONSTRAINT [CK_PRD_TurnosProduccionProductos_Origen]
        CHECK ([Origen] IN (N'LOGIN', N'CAMBIO', N'SUPERVISOR'));
    COMMIT TRANSACTION;
    PRINT 'CHECK CK_PRD_TurnosProduccionProductos_Origen vuelto a LOGIN, CAMBIO, SUPERVISOR.'
END
GO

IF COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'FechaHoraConfirmacion') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] DROP COLUMN [FechaHoraConfirmacion];
    PRINT 'Columna PRD_TurnosProduccionProductos.FechaHoraConfirmacion eliminada.'
END
GO

IF COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'ConfirmadoPorUsuarioId') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] DROP COLUMN [ConfirmadoPorUsuarioId];
    PRINT 'Columna PRD_TurnosProduccionProductos.ConfirmadoPorUsuarioId eliminada.'
END
GO

-- ----------------------------------------------------------------------------- 3. Tolerancia del operario
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ABM_Turnos_ToleranciaCierreOperador')
BEGIN
    ALTER TABLE [mnt].[ABM_Turnos] DROP CONSTRAINT [CK_ABM_Turnos_ToleranciaCierreOperador];
    PRINT 'CHECK CK_ABM_Turnos_ToleranciaCierreOperador eliminado.'
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_ABM_Turnos_ToleranciaCierreOperador')
BEGIN
    ALTER TABLE [mnt].[ABM_Turnos] DROP CONSTRAINT [DF_ABM_Turnos_ToleranciaCierreOperador];
    PRINT 'DEFAULT DF_ABM_Turnos_ToleranciaCierreOperador eliminado.'
END
GO

IF COL_LENGTH(N'mnt.ABM_Turnos', N'ToleranciaCierreOperadorMinutos') IS NOT NULL
BEGIN
    ALTER TABLE [mnt].[ABM_Turnos] DROP COLUMN [ToleranciaCierreOperadorMinutos];
    PRINT 'Columna ABM_Turnos.ToleranciaCierreOperadorMinutos eliminada.'
END
GO

PRINT '44_turno_automatico_rollback.sql terminado.'
GO
