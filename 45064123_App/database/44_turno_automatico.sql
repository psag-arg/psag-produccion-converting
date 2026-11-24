-- ==============================================================================
-- Script: 44_turno_automatico.sql
-- Descripción: Turno automático (pedido de Emiliano, 24/11; docs/diseno/T-798_modelo_produccion.md, sección
--   "Turno de máquina vs. sesión del operario"). El TURNO es de la MÁQUINA: el job de la API lo abre y lo cierra a sus
--   horas; el operario entra, sale o lo relevan (novedades del turno) y tiene una tolerancia para terminar lo suyo.
--   1. ABM_Turnos.ToleranciaCierreOperadorMinutos INT NOT NULL DEFAULT 30 (CHECK 0–240): minutos después del fin del turno
--      en que el operario que lo terminó todavía puede declarar / corregir las detenciones de SU turno. Por turno, editable
--      en Configuración → Turnos. Las filas existentes quedan en 30 (el valor que tenía TurnosProduccion:ToleranciaCierreAutomaticoMinutos).
--   2. PRD_TurnosProduccionProductos: nuevo origen de tramo HEREDADO (el turno que abre el sistema hereda el producto del
--      turno anterior como SUPUESTO) y la confirmación: ConfirmadoPorUsuarioId NVARCHAR(450) NULL y
--      FechaHoraConfirmacion DATETIMEOFFSET NULL (un HEREDADO sin confirmar = producto supuesto). El CHECK
--      CK_PRD_TurnosProduccionProductos_Origen pasa a ('LOGIN', 'CAMBIO', 'SUPERVISOR', 'HEREDADO').
--   3. DET_Detenciones.DetencionAnteriorId BIGINT NULL, FK a sí misma (FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId)
--      e índice único filtrado UX_DET_Detenciones_DetencionAnteriorId (WHERE DetencionAnteriorId IS NOT NULL): una parada
--      de cámara que sigue abierta al cierre del turno de máquina se corta en dos detenciones vinculadas (cada parte en su
--      turno, cada una se declara aparte; una sola continuación por parte).
--   SIN_APERTURA queda en el CHECK del estado de PRD_TurnosProduccion como historia (ya no se genera). No migra datos.
-- Versión: 1.0
-- Requiere: 31–43 (T-798, T-802, T-770, T-803); va después de T-806/T-803.
-- Idempotente. Correr con la API detenida y ANTES de publicar la API del turno automático (la API nueva usa las columnas).
-- Correr con: sqlcmd -C -I -b -f 65001. Nada de SQL Agent: el job es el de la API (TurnosProduccionJob).
-- Deshacer: 44_turno_automatico_rollback.sql (volver antes a la API anterior).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.ABM_Turnos', N'U') IS NULL
   OR OBJECT_ID(N'mnt.PRD_TurnosProduccionProductos', N'U') IS NULL
   OR OBJECT_ID(N'mnt.DET_Detenciones', N'U') IS NULL
BEGIN
    RAISERROR(N'Faltan tablas de T-798 (scripts 31–38): correr antes los scripts anteriores.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ----------------------------------------------------------------------------- 1. Tolerancia del operario por turno
IF COL_LENGTH(N'mnt.ABM_Turnos', N'ToleranciaCierreOperadorMinutos') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_Turnos] ADD [ToleranciaCierreOperadorMinutos] INT NOT NULL
        CONSTRAINT [DF_ABM_Turnos_ToleranciaCierreOperador] DEFAULT (30);
    PRINT 'Columna ABM_Turnos.ToleranciaCierreOperadorMinutos agregada (30 en los turnos existentes).'
END
ELSE
    PRINT 'ABM_Turnos.ToleranciaCierreOperadorMinutos ya existía.'
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ABM_Turnos_ToleranciaCierreOperador')
BEGIN
    ALTER TABLE [mnt].[ABM_Turnos] ADD CONSTRAINT [CK_ABM_Turnos_ToleranciaCierreOperador]
        CHECK ([ToleranciaCierreOperadorMinutos] >= 0 AND [ToleranciaCierreOperadorMinutos] <= 240);
    PRINT 'CHECK CK_ABM_Turnos_ToleranciaCierreOperador creado.'
END
GO

-- ----------------------------------------------------------------------------- 2. Producto heredado y confirmación
IF COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'ConfirmadoPorUsuarioId') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] ADD [ConfirmadoPorUsuarioId] NVARCHAR(450) NULL;
    PRINT 'Columna PRD_TurnosProduccionProductos.ConfirmadoPorUsuarioId agregada.'
END
GO

IF COL_LENGTH(N'mnt.PRD_TurnosProduccionProductos', N'FechaHoraConfirmacion') IS NULL
BEGIN
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] ADD [FechaHoraConfirmacion] DATETIMEOFFSET NULL;
    PRINT 'Columna PRD_TurnosProduccionProductos.FechaHoraConfirmacion agregada.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_PRD_TurnosProduccionProductos_Origen' AND definition LIKE N'%HEREDADO%')
BEGIN
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PRD_TurnosProduccionProductos_Origen')
        ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] DROP CONSTRAINT [CK_PRD_TurnosProduccionProductos_Origen];
    ALTER TABLE [mnt].[PRD_TurnosProduccionProductos] ADD CONSTRAINT [CK_PRD_TurnosProduccionProductos_Origen]
        CHECK ([Origen] IN (N'LOGIN', N'CAMBIO', N'SUPERVISOR', N'HEREDADO'));
    COMMIT TRANSACTION;
    PRINT 'CHECK CK_PRD_TurnosProduccionProductos_Origen con HEREDADO.'
END
ELSE
    PRINT 'CK_PRD_TurnosProduccionProductos_Origen ya admite HEREDADO.'
GO

-- ----------------------------------------------------------------------------- 3. Detenciones que cruzan de turno
IF COL_LENGTH(N'mnt.DET_Detenciones', N'DetencionAnteriorId') IS NULL
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] ADD [DetencionAnteriorId] BIGINT NULL;
    PRINT 'Columna DET_Detenciones.DetencionAnteriorId agregada.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId')
BEGIN
    ALTER TABLE [mnt].[DET_Detenciones] ADD CONSTRAINT [FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId]
        FOREIGN KEY ([DetencionAnteriorId]) REFERENCES [mnt].[DET_Detenciones] ([DetencionId]);
    PRINT 'FK FK_DET_Detenciones_DET_Detenciones_DetencionAnteriorId creada.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.DET_Detenciones') AND name = N'UX_DET_Detenciones_DetencionAnteriorId')
BEGIN
    CREATE UNIQUE INDEX [UX_DET_Detenciones_DetencionAnteriorId] ON [mnt].[DET_Detenciones] ([DetencionAnteriorId])
        WHERE [DetencionAnteriorId] IS NOT NULL;
    PRINT 'Indice UX_DET_Detenciones_DetencionAnteriorId creado.'
END
GO

SET NOEXEC OFF;
GO
PRINT '44_turno_automatico.sql terminado.'
GO
