-- ==============================================================================
-- Script: 38_t798_conversion_turnos_rollback.sql
-- Descripción: Deshace 38_t798_conversion_turnos.sql (T-798 etapa 7).
--   1. Turnos recalculados: vuelven MinutosProgramados y ObservacionCierre anteriores (mnt.PRD_ConversionTurnosT798).
--   2. Registros que colgó el 38 (mnt.PRD_ConversionRegistrosT798): TurnoProduccionId vuelve a NULL, sólo si sigue
--      apuntando al turno que puso el 38.
--   3. Turnos que creó el 38: se eliminan si ya nada los usa (detenciones, conteos, paradas, registros de proceso y
--      calidad, operadores, productos). Si algo los usa, quedan y se avisa.
--   4. Elimina las dos tablas de respaldo.
-- Es el primero del rollback de T-798 (orden inverso: 38, 37, 36, 35, 34, 33, 32, 31).
-- Idempotente: se puede ejecutar más de una vez.
-- Correr con la API y PSAG_ConteoCamara detenidos.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NULL AND OBJECT_ID(N'mnt.PRD_ConversionRegistrosT798', N'U') IS NULL
BEGIN
    PRINT 'No hay respaldo del 38: nada para deshacer.'
    SET NOEXEC ON;
END
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Minutos programados y observación anteriores.
IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NOT NULL
BEGIN
    UPDATE tp
    SET tp.[MinutosProgramados] = r.[MinutosProgramadosAntes],
        tp.[ObservacionCierre] = r.[ObservacionCierreAntes],
        tp.[FechaHoraModificacion] = SYSDATETIMEOFFSET(),
        tp.[ModificadoPorUsuarioId] = N'SISTEMA'
    FROM [mnt].[PRD_TurnosProduccion] tp
    INNER JOIN [mnt].[PRD_ConversionTurnosT798] r ON r.[TurnoProduccionId] = tp.[TurnoProduccionId]
    WHERE r.[Recalculado] = 1 AND r.[MinutosProgramadosAntes] IS NOT NULL;
    PRINT CONCAT('Turnos con minutos programados restaurados: ', @@ROWCOUNT);
END

-- 2. Registros colgados por el 38.
IF OBJECT_ID(N'mnt.PRD_ConversionRegistrosT798', N'U') IS NOT NULL
BEGIN
    UPDATE x SET x.[TurnoProduccionId] = NULL
    FROM [mnt].[CPR_Registros] x
    INNER JOIN [mnt].[PRD_ConversionRegistrosT798] r ON r.[Tabla] = 'CPR_Registros' AND r.[RegistroId] = x.[RegistroProcesoId]
    WHERE x.[TurnoProduccionId] = r.[TurnoProduccionId];
    PRINT CONCAT('Registros de Control de Proceso sin turno de producción otra vez: ', @@ROWCOUNT);

    UPDATE x SET x.[TurnoProduccionId] = NULL
    FROM [mnt].[CAL_Registros] x
    INNER JOIN [mnt].[PRD_ConversionRegistrosT798] r ON r.[Tabla] = 'CAL_Registros' AND r.[RegistroId] = x.[RegistroCalidadId]
    WHERE x.[TurnoProduccionId] = r.[TurnoProduccionId];
    PRINT CONCAT('Registros de Calidad sin turno de producción otra vez: ', @@ROWCOUNT);

    UPDATE x SET x.[TurnoProduccionId] = NULL
    FROM [mnt].[CAM_ConteosMinuto] x
    INNER JOIN [mnt].[PRD_ConversionRegistrosT798] r ON r.[Tabla] = 'CAM_ConteosMinuto' AND r.[RegistroId] = x.[ConteoCamaraMinutoId]
    WHERE x.[TurnoProduccionId] = r.[TurnoProduccionId];
    PRINT CONCAT('Conteos de cámara sin turno de producción otra vez: ', @@ROWCOUNT);

    UPDATE x SET x.[TurnoProduccionId] = NULL
    FROM [mnt].[CAM_Paradas] x
    INNER JOIN [mnt].[PRD_ConversionRegistrosT798] r ON r.[Tabla] = 'CAM_Paradas' AND r.[RegistroId] = x.[ParadaCamaraId]
    WHERE x.[TurnoProduccionId] = r.[TurnoProduccionId];
    PRINT CONCAT('Paradas de cámara sin turno de producción otra vez: ', @@ROWCOUNT);
END

-- 3. Turnos creados por el 38 que ya nada usa.
IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NOT NULL
BEGIN
    DELETE tp
    FROM [mnt].[PRD_TurnosProduccion] tp
    INNER JOIN [mnt].[PRD_ConversionTurnosT798] r ON r.[TurnoProduccionId] = tp.[TurnoProduccionId] AND r.[CreadoPorEsteScript] = 1
    WHERE NOT EXISTS (SELECT 1 FROM [mnt].[DET_Detenciones] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[CAM_ConteosMinuto] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[CAM_Paradas] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[CPR_Registros] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[CAL_Registros] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionOperadores] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId])
      AND NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionProductos] x WHERE x.[TurnoProduccionId] = tp.[TurnoProduccionId]);
    PRINT CONCAT('Turnos creados por el 38 eliminados: ', @@ROWCOUNT);

    DECLARE @Quedan INT = (SELECT COUNT(*) FROM [mnt].[PRD_ConversionTurnosT798] r
                           INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = r.[TurnoProduccionId]
                           WHERE r.[CreadoPorEsteScript] = 1);
    IF @Quedan > 0
        PRINT CONCAT('AVISO: ', @Quedan, ' turnos creados por el 38 quedan porque se usan (se cargaron datos después).');
END

-- 4. Respaldos.
IF OBJECT_ID(N'mnt.PRD_ConversionRegistrosT798', N'U') IS NOT NULL
    DROP TABLE [mnt].[PRD_ConversionRegistrosT798];
IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NOT NULL
    DROP TABLE [mnt].[PRD_ConversionTurnosT798];
PRINT 'Respaldos del 38 eliminados.'

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
PRINT 'Script 38_t798_conversion_turnos_rollback.sql terminado.'
GO
