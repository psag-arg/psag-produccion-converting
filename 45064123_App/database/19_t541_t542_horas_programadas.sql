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

-- ============================================================================
-- PARTE 2 · Calendario base por línea (T-542)
-- ============================================================================

-- ============================================================================
-- Tabla: mnt.CalendariosLinea
-- Descripción: versión del calendario base de una línea, vigente desde una fecha.
-- Guardar crea una versión nueva; la de la misma fecha queda Activo = 0 (historial).
-- ============================================================================
IF OBJECT_ID(N'[mnt].[CalendariosLinea]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CalendariosLinea] (
        [CalendarioLineaId] INT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [VigenteDesde] DATE NOT NULL,
        [Activo] BIT NOT NULL CONSTRAINT [DF_CalendariosLinea_Activo] DEFAULT 1,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraModificacion] DATETIMEOFFSET NOT NULL,
        [CreadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_CalendariosLinea] PRIMARY KEY ([CalendarioLineaId])
    )
    PRINT 'Tabla mnt.CalendariosLinea creada exitosamente.'
END
GO

-- ============================================================================
-- Tabla: mnt.CalendariosLineaTurnos
-- Descripción: turnos que la línea trabaja cada día de la semana en una versión.
-- DiaSemana: 0 = domingo ... 6 = sábado. Un día sin filas = la línea no trabaja (0 h).
-- ============================================================================
IF OBJECT_ID(N'[mnt].[CalendariosLineaTurnos]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CalendariosLineaTurnos] (
        [CalendarioLineaTurnoId] INT IDENTITY(1,1) NOT NULL,
        [CalendarioLineaId] INT NOT NULL,
        [DiaSemana] INT NOT NULL,
        [TurnoId] INT NOT NULL,
        CONSTRAINT [PK_CalendariosLineaTurnos] PRIMARY KEY ([CalendarioLineaTurnoId])
    )
    PRINT 'Tabla mnt.CalendariosLineaTurnos creada exitosamente.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_CalendariosLineaTurnos_DiaSemana')
BEGIN
    ALTER TABLE [mnt].[CalendariosLineaTurnos]
    ADD CONSTRAINT [CK_CalendariosLineaTurnos_DiaSemana] CHECK ([DiaSemana] BETWEEN 0 AND 6)
    PRINT 'Check Constraint CK_CalendariosLineaTurnos_DiaSemana creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_CalendariosLinea_Lineas_LineaId')
BEGIN
    ALTER TABLE [mnt].[CalendariosLinea]
    ADD CONSTRAINT [FK_CalendariosLinea_Lineas_LineaId] FOREIGN KEY ([LineaId])
    REFERENCES [mnt].[Lineas] ([LineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_CalendariosLinea_Lineas_LineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_CalendariosLineaTurnos_CalendariosLinea_CalendarioLineaId')
BEGIN
    ALTER TABLE [mnt].[CalendariosLineaTurnos]
    ADD CONSTRAINT [FK_CalendariosLineaTurnos_CalendariosLinea_CalendarioLineaId] FOREIGN KEY ([CalendarioLineaId])
    REFERENCES [mnt].[CalendariosLinea] ([CalendarioLineaId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_CalendariosLineaTurnos_CalendariosLinea_CalendarioLineaId creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_CalendariosLineaTurnos_Turnos_TurnoId')
BEGIN
    ALTER TABLE [mnt].[CalendariosLineaTurnos]
    ADD CONSTRAINT [FK_CalendariosLineaTurnos_Turnos_TurnoId] FOREIGN KEY ([TurnoId])
    REFERENCES [mnt].[Turnos] ([TurnoId])
    ON DELETE NO ACTION
    PRINT 'Foreign Key FK_CalendariosLineaTurnos_Turnos_TurnoId creada.'
END
GO

-- Una versión activa por línea y fecha (las reemplazadas quedan inactivas).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_CalendariosLinea_LineaId_VigenteDesde' AND object_id = OBJECT_ID(N'[mnt].[CalendariosLinea]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_CalendariosLinea_LineaId_VigenteDesde]
    ON [mnt].[CalendariosLinea] ([LineaId], [VigenteDesde])
    WHERE [Activo] = 1
    PRINT 'Índice UX_CalendariosLinea_LineaId_VigenteDesde creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_CalendariosLineaTurnos_Calendario_Dia_Turno' AND object_id = OBJECT_ID(N'[mnt].[CalendariosLineaTurnos]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_CalendariosLineaTurnos_Calendario_Dia_Turno]
    ON [mnt].[CalendariosLineaTurnos] ([CalendarioLineaId], [DiaSemana], [TurnoId])
    PRINT 'Índice UX_CalendariosLineaTurnos_Calendario_Dia_Turno creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_CalendariosLineaTurnos_TurnoId' AND object_id = OBJECT_ID(N'[mnt].[CalendariosLineaTurnos]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CalendariosLineaTurnos_TurnoId]
    ON [mnt].[CalendariosLineaTurnos] ([TurnoId])
    PRINT 'Índice IX_CalendariosLineaTurnos_TurnoId creado.'
END
GO

PRINT 'Script 19 (T-541/T-542) terminado.'
GO
