-- ============================================================================
-- T-541 · Horas programadas por día: diagnóstico de turnos (25 h en Tango 2)
-- T-542 · Calendario base de horas programadas por línea
-- Requiere 17_t76_ajustes.sql. Idempotente: se puede ejecutar más de una vez.
--
-- Parte 1 (T-541): muestra los turnos y cuántas horas suman los activos. Las horas
-- programadas de un día son la suma de la duración de sus turnos, así que un horario
-- superpuesto (T3 21:00-06:00 con T1 desde las 05:00) da 25 h. La única corrección es
-- OPCIONAL y puntual: fin de T3 a las 05:00, sólo con @CorregirFinT3 = 1.
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

DECLARE @MinutosActivos INT = (
    SELECT ISNULL(SUM(CASE WHEN [HoraFin] > [HoraInicio]
                           THEN DATEDIFF(MINUTE, [HoraInicio], [HoraFin])
                           ELSE 1440 - DATEDIFF(MINUTE, [HoraFin], [HoraInicio]) END), 0)
    FROM [mnt].[Turnos]
    WHERE [Activo] = 1);

PRINT CONCAT('Los turnos activos suman ', @MinutosActivos, ' min (', CAST(@MinutosActivos / 60.0 AS DECIMAL(5,2)), ' h) por día.');

IF @MinutosActivos <= 1440
    PRINT 'Turnos activos dentro de 24 h: no hay nada que corregir.'
ELSE
BEGIN
    PRINT 'ATENCIÓN: los turnos activos suman más de 24 h (se superponen o hay uno de más).'
    PRINT 'Revisar la grilla de arriba y corregir en Configuración → Turnos.'
END
GO

-- Corrección puntual. Dejar en 0 para sólo diagnosticar.
-- En planta T1 es 05:00-13:00, T2 13:00-21:00 y T3 quedó cargado 21:00-06:00: se pisa una hora
-- con T1 y el día suma 25 h. Con 1 deja T3 en 21:00-05:00 (no toca nada más ni otros horarios).
-- CONFIRMAR CON LA PLANTA ANTES DE ACTIVARLO.
-- Las detenciones ya cargadas no cambian de turno (TurnoId queda materializado).
DECLARE @CorregirFinT3 BIT = 0;

IF @CorregirFinT3 = 1
BEGIN
    UPDATE [mnt].[Turnos]
    SET [HoraFin] = '05:00',
        [FechaHoraModificacion] = SYSDATETIMEOFFSET(),
        [ModificadoPorUsuarioId] = N'SCRIPT-19'
    WHERE [Codigo] = N'T3'
      AND [HoraInicio] = '21:00'
      AND [HoraFin] = '06:00';
    PRINT CONCAT('Fin de T3 corregido a 05:00: ', @@ROWCOUNT, ' fila(s).');
END
ELSE
    PRINT 'Corrección de T3 desactivada (@CorregirFinT3 = 0).'
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
