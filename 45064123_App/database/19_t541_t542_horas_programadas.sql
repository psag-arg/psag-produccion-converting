-- ============================================================================
-- T-541 · Horas programadas por día: diagnóstico de turnos (25 h en Tango 2)
-- T-542 · Calendario base de horas programadas por línea
-- Requiere 17_t76_ajustes.sql. Idempotente: se puede ejecutar más de una vez.
--
-- Parte 1 (T-541): muestra los turnos y cuántas horas suman los activos. La app ya
-- no cuenta dos veces una hora en la que se superponen dos turnos (nunca pasa de
-- 24 h), pero un horario superpuesto sigue siendo un dato mal cargado: una parada
-- en la hora compartida cae en el primer turno que la contiene. La corrección de
-- horarios es OPCIONAL y sólo corre con @AplicarCorreccionTurnos = 1.
--
-- Parte 2 (T-542): tablas mnt.CalendariosLinea y mnt.CalendariosLineaTurnos.
-- Sin filas, cada línea sigue con el estándar (turnos activos todos los días).
-- ============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- PARTE 1 · Diagnóstico de turnos (T-541)
-- ============================================================================
PRINT 'Turnos cargados (minutos = duración de cada turno; los nocturnos cruzan medianoche):'

SELECT
    t.[TurnoId],
    t.[Codigo],
    t.[Nombre],
    CONVERT(CHAR(5), t.[HoraInicio], 108) AS [HoraInicio],
    CONVERT(CHAR(5), t.[HoraFin], 108) AS [HoraFin],
    t.[Activo],
    CASE WHEN t.[HoraFin] > t.[HoraInicio]
         THEN DATEDIFF(MINUTE, t.[HoraInicio], t.[HoraFin])
         ELSE 1440 - DATEDIFF(MINUTE, t.[HoraFin], t.[HoraInicio])
    END AS [Minutos]
FROM [mnt].[Turnos] t
ORDER BY t.[Activo] DESC, t.[Orden], t.[HoraInicio];
GO

-- Corrección condicional. Dejar en 0 para sólo diagnosticar.
-- Con 1, y SOLO si los turnos activos suman más de 24 h, vuelve T1/T2/T3 a los horarios
-- de 07_seed_catalogs.sql (06-14, 14-22, 22-06) y desactiva (no borra) los demás turnos
-- activos. Antes de ponerlo en 1, confirmar con la planta que esos son los horarios reales.
-- Las detenciones ya cargadas no cambian de turno (TurnoId queda materializado).
DECLARE @AplicarCorreccionTurnos BIT = 0;

DECLARE @MinutosActivos INT = (
    SELECT ISNULL(SUM(CASE WHEN [HoraFin] > [HoraInicio]
                           THEN DATEDIFF(MINUTE, [HoraInicio], [HoraFin])
                           ELSE 1440 - DATEDIFF(MINUTE, [HoraFin], [HoraInicio]) END), 0)
    FROM [mnt].[Turnos]
    WHERE [Activo] = 1);

PRINT CONCAT('Los turnos activos suman ', @MinutosActivos, ' min (', CAST(@MinutosActivos / 60.0 AS DECIMAL(5,2)), ' h) por día.');

IF @MinutosActivos <= 1440
BEGIN
    PRINT 'Turnos activos dentro de 24 h: no hay nada que corregir.'
END
ELSE IF @AplicarCorreccionTurnos = 0
BEGIN
    PRINT 'ATENCIÓN: los turnos activos suman más de 24 h (se superponen o hay uno de más).'
    PRINT 'Revisar la grilla de arriba y corregir en Configuración → Turnos, o volver a ejecutar'
    PRINT 'este script con @AplicarCorreccionTurnos = 1 para dejar T1/T2/T3 como en el seed.'
END
ELSE IF (SELECT COUNT(*) FROM [mnt].[Turnos] WHERE [Codigo] IN (N'T1', N'T2', N'T3')) <> 3
BEGIN
    RAISERROR(N'No existen los turnos T1, T2 y T3: corregir los horarios a mano en Configuración → Turnos.', 16, 1);
END
ELSE
BEGIN
    BEGIN TRANSACTION;

    UPDATE [mnt].[Turnos]
    SET [HoraInicio] = CASE [Codigo] WHEN N'T1' THEN '06:00:00' WHEN N'T2' THEN '14:00:00' ELSE '22:00:00' END,
        [HoraFin]    = CASE [Codigo] WHEN N'T1' THEN '14:00:00' WHEN N'T2' THEN '22:00:00' ELSE '06:00:00' END,
        [Activo] = 1,
        [FechaHoraModificacion] = SYSDATETIMEOFFSET(),
        [ModificadoPorUsuarioId] = N'SCRIPT-19'
    WHERE [Codigo] IN (N'T1', N'T2', N'T3')
      AND (   ([Codigo] = N'T1' AND ([HoraInicio] <> '06:00:00' OR [HoraFin] <> '14:00:00' OR [Activo] = 0))
           OR ([Codigo] = N'T2' AND ([HoraInicio] <> '14:00:00' OR [HoraFin] <> '22:00:00' OR [Activo] = 0))
           OR ([Codigo] = N'T3' AND ([HoraInicio] <> '22:00:00' OR [HoraFin] <> '06:00:00' OR [Activo] = 0)));
    PRINT CONCAT('Turnos T1/T2/T3 corregidos: ', @@ROWCOUNT, '.');

    UPDATE [mnt].[Turnos]
    SET [Activo] = 0,
        [FechaHoraModificacion] = SYSDATETIMEOFFSET(),
        [ModificadoPorUsuarioId] = N'SCRIPT-19'
    WHERE [Activo] = 1 AND [Codigo] NOT IN (N'T1', N'T2', N'T3');
    PRINT CONCAT('Otros turnos activos desactivados: ', @@ROWCOUNT, '.');

    COMMIT TRANSACTION;
END
GO
