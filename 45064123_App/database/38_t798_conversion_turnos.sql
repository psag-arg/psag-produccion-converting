-- ==============================================================================
-- Script: 38_t798_conversion_turnos.sql
-- Descripción: T-798 etapa 7 · Cierre de la conversión opción B (docs/diseno/T-798_modelo_produccion.md, 2.11).
--   Los scripts 35 y 36 ya colgaron del turno de producción las detenciones, los conteos y las paradas que tenían
--   turno materializado. Este script completa lo que faltaba:
--   1. Registros de Control de Proceso (CPR_Registros) y de Calidad (CAL_Registros) sin TurnoProduccionId: se
--      cuelgan del turno de producción (si falta, se crea CERRADO por SISTEMA, sin operador ni producto: "a
--      completar"). Turno: el materializado en el registro (TurnoId); fecha de producción = día de inicio del turno
--      según la hora del registro (CPR: FechaHoraInicio; CAL: FechaHoraDeteccion; hora de planta UTC-3, el T3
--      después de medianoche es del día anterior). Un registro de calidad sin TurnoId toma el turno activo cuyo
--      horario contiene la hora.
--      Conteos y paradas de cámara que quedaron sin turno de producción (no tenían TurnoId): mismo criterio, por hora.
--      EXCEPCIÓN documentada (igual que 35 y 36): lo de 05:00 a 06:00 que quedó materializado en el T3 por el horario
--      viejo 21-06 pasa al T1 de ese día.
--   2. MinutosProgramados de los turnos convertidos (ObservacionCierre "Convertido (T-798 etapa ...", creados en 0):
--      se recalculan desde la programación con la misma regla que la API (EficienciaCamaraCalculador.
--      MinutosProgramadosTurno): 0 si el turno está inactivo o la versión vigente de PRD_ProgramacionLinea no lo
--      incluye ese día de la semana (sin versión vigente cuentan todos los turnos activos); sin excepción del día,
--      la duración del turno; con excepción (PRD_ExcepcionesHorasDia), las horas del día repartidas entre los turnos
--      del día en proporción a su duración, sin pasar la duración del turno. Se anota en ObservacionCierre.
--   Todo queda en mnt.PRD_ConversionTurnosT798 (turnos creados o recalculados, con el valor anterior) y
--   mnt.PRD_ConversionRegistrosT798 (registros colgados por este script): los usa el rollback.
--   No crea PRD_ProduccionesTurno ni tramos de producto (2.11, punto 5: la eficiencia histórica dice "sin objetivo").
-- Versión: 1.0
-- Requiere: 31 a 36 (37 es independiente).
-- Idempotente: se puede ejecutar más de una vez (hace solo lo que falta). Cada paso va en transacción.
-- Correr con la API y PSAG_ConteoCamara detenidos, después del 37 y antes de publicar la API de T-798.
-- Deshacer: 38_t798_conversion_turnos_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.PRD_TurnosProduccion', N'U') IS NULL
   OR OBJECT_ID(N'mnt.CAM_Camaras', N'U') IS NULL
   OR COL_LENGTH(N'mnt.CPR_Registros', N'TurnoProduccionId') IS NULL
   OR COL_LENGTH(N'mnt.CAL_Registros', N'TurnoProduccionId') IS NULL
   OR COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoProduccionId') IS NULL
   OR COL_LENGTH(N'mnt.CAM_ConteosMinuto', N'TurnoId') IS NOT NULL
   OR OBJECT_ID(N'mnt.PRD_ProgramacionLinea', N'U') IS NULL
BEGIN
    RAISERROR(N'Falta correr (completos) 31 a 36 de T-798.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 0. Tablas de respaldo (las usa el rollback).
-- ============================================================================
IF OBJECT_ID(N'mnt.PRD_ConversionTurnosT798', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_ConversionTurnosT798] (
        [TurnoProduccionId] BIGINT NOT NULL,
        [CreadoPorEsteScript] BIT NOT NULL,
        [Recalculado] BIT NOT NULL CONSTRAINT [DF_PRD_ConversionTurnosT798_Recalculado] DEFAULT 0,
        [MinutosProgramadosAntes] INT NULL,
        [ObservacionCierreAntes] NVARCHAR(500) NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_PRD_ConversionTurnosT798_FechaHora] DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_PRD_ConversionTurnosT798] PRIMARY KEY ([TurnoProduccionId])
    );
    PRINT 'Tabla de respaldo mnt.PRD_ConversionTurnosT798 creada.'
END
GO

IF OBJECT_ID(N'mnt.PRD_ConversionRegistrosT798', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[PRD_ConversionRegistrosT798] (
        [Tabla] VARCHAR(20) NOT NULL,       -- CPR_Registros / CAL_Registros / CAM_ConteosMinuto / CAM_Paradas
        [RegistroId] BIGINT NOT NULL,
        [TurnoProduccionId] BIGINT NOT NULL,
        [Excepcion0506] BIT NOT NULL,
        CONSTRAINT [PK_PRD_ConversionRegistrosT798] PRIMARY KEY ([Tabla], [RegistroId])
    );
    PRINT 'Tabla de respaldo mnt.PRD_ConversionRegistrosT798 creada.'
END
GO

-- ============================================================================
-- 1. Registros sin turno de producción → turno de producción. Todo o nada.
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Pendientes: turno materializado (si lo hay), instante y fecha declarada.
CREATE TABLE #Pendientes (
    [Tabla] VARCHAR(20) COLLATE DATABASE_DEFAULT NOT NULL, [RegistroId] BIGINT NOT NULL, [LineaId] INT NOT NULL,  -- sin COLLATE toma la de tempdb
    [TurnoMaterializadoId] INT NULL, [Instante] DATETIMEOFFSET NULL, [FechaDeclarada] DATE NULL);

INSERT INTO #Pendientes
SELECT 'CPR_Registros', r.[RegistroProcesoId], r.[LineaId], r.[TurnoId], r.[FechaHoraInicio], r.[Fecha]
FROM [mnt].[CPR_Registros] r WHERE r.[TurnoProduccionId] IS NULL;

INSERT INTO #Pendientes
SELECT 'CAL_Registros', r.[RegistroCalidadId], r.[LineaId], r.[TurnoId], r.[FechaHoraDeteccion], NULL
FROM [mnt].[CAL_Registros] r WHERE r.[TurnoProduccionId] IS NULL;

INSERT INTO #Pendientes
SELECT 'CAM_ConteosMinuto', m.[ConteoCamaraMinutoId], c.[LineaId], NULL, m.[Minuto], NULL
FROM [mnt].[CAM_ConteosMinuto] m INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = m.[CamaraId]
WHERE m.[TurnoProduccionId] IS NULL;

INSERT INTO #Pendientes
SELECT 'CAM_Paradas', p.[ParadaCamaraId], c.[LineaId], NULL, p.[FechaHoraInicio], NULL
FROM [mnt].[CAM_Paradas] p INNER JOIN [mnt].[CAM_Camaras] c ON c.[CamaraId] = p.[CamaraId]
WHERE p.[TurnoProduccionId] IS NULL;

-- Turno y fecha de producción de cada pendiente.
SELECT q.[Tabla], q.[RegistroId], q.[LineaId],
       CASE WHEN x.[Excepcion0506] = 1 THEN t1.[TurnoId] ELSE COALESCE(q.[TurnoMaterializadoId], porHora.[TurnoId]) END AS [TurnoId],
       CASE
           WHEN q.[Instante] IS NULL THEN q.[FechaDeclarada]
           WHEN x.[Excepcion0506] = 1 THEN h.[FechaLocal]
           WHEN tu.[HoraFin] <= tu.[HoraInicio] AND h.[HoraLocal] < tu.[HoraInicio] THEN DATEADD(DAY, -1, h.[FechaLocal])
           ELSE h.[FechaLocal]
       END AS [FechaProduccion],
       x.[Excepcion0506]
INTO #Conversion
FROM #Pendientes q
OUTER APPLY (SELECT CAST(SWITCHOFFSET(q.[Instante], '-03:00') AS TIME) AS [HoraLocal],
                    CAST(SWITCHOFFSET(q.[Instante], '-03:00') AS DATE) AS [FechaLocal]) h
-- Sin turno materializado: el turno activo cuyo horario contiene la hora (un nocturno son dos tramos del día).
OUTER APPLY (SELECT TOP 1 t.[TurnoId] FROM [mnt].[ABM_Turnos] t
             WHERE q.[TurnoMaterializadoId] IS NULL AND h.[HoraLocal] IS NOT NULL AND t.[Activo] = 1
               AND ((t.[HoraInicio] < t.[HoraFin] AND h.[HoraLocal] >= t.[HoraInicio] AND h.[HoraLocal] < t.[HoraFin])
                    OR (t.[HoraFin] <= t.[HoraInicio] AND (h.[HoraLocal] >= t.[HoraInicio] OR h.[HoraLocal] < t.[HoraFin])))
             ORDER BY t.[HoraInicio]) porHora
OUTER APPLY (SELECT TOP 1 [TurnoId], [HoraInicio] FROM [mnt].[ABM_Turnos] WHERE [Codigo] = N'T1') t1
LEFT JOIN [mnt].[ABM_Turnos] tm ON tm.[TurnoId] = q.[TurnoMaterializadoId]
CROSS APPLY (SELECT CASE WHEN tm.[Codigo] = N'T3' AND t1.[TurnoId] IS NOT NULL AND h.[HoraLocal] IS NOT NULL
                              AND h.[HoraLocal] >= t1.[HoraInicio] AND h.[HoraLocal] < CAST('06:00' AS TIME)
                         THEN 1 ELSE 0 END AS [Excepcion0506]) x
LEFT JOIN [mnt].[ABM_Turnos] tu
    ON tu.[TurnoId] = CASE WHEN x.[Excepcion0506] = 1 THEN t1.[TurnoId] ELSE COALESCE(q.[TurnoMaterializadoId], porHora.[TurnoId]) END;

DECLARE @SinTurno INT = (SELECT COUNT(*) FROM #Conversion WHERE [TurnoId] IS NULL OR [FechaProduccion] IS NULL);
IF @SinTurno > 0
    PRINT CONCAT('AVISO: ', @SinTurno, ' registros sin hora ni turno: quedan sin turno de producción (se listan abajo).');
SELECT [Tabla], [RegistroId], [LineaId] AS [RegistroSinTurnoDeProduccion_LineaId]
FROM #Conversion WHERE [TurnoId] IS NULL OR [FechaProduccion] IS NULL;
DELETE FROM #Conversion WHERE [TurnoId] IS NULL OR [FechaProduccion] IS NULL;

DECLARE @Excepcion0506 INT = (SELECT COUNT(*) FROM #Conversion WHERE [Excepcion0506] = 1);
PRINT CONCAT('Registros de 05:00 a 06:00 materializados en el T3 que pasan al T1 (excepción 2.11): ', @Excepcion0506);

-- Turnos de producción que faltan (CERRADO por SISTEMA, sin operador ni producto: "a completar").
DECLARE @Creados TABLE ([TurnoProduccionId] BIGINT PRIMARY KEY);
INSERT INTO [mnt].[PRD_TurnosProduccion]
    ([LineaId], [TurnoId], [FechaProduccion], [InicioProgramado], [FinProgramado], [MinutosProgramados],
     [InicioReal], [FinReal], [Estado], [OrigenApertura], [ObservacionCierre], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
OUTPUT inserted.[TurnoProduccionId] INTO @Creados
SELECT k.[LineaId], k.[TurnoId], k.[FechaProduccion], v.[Inicio], v.[Fin], 0, v.[Inicio], v.[Fin],
       N'CERRADO', N'SISTEMA',
       N'Convertido (T-798 etapa 7) desde registros de proceso, calidad y cámara: minutos programados a recalcular en la etapa 7.',
       N'SISTEMA', N'SISTEMA'
FROM (SELECT DISTINCT [LineaId], [TurnoId], [FechaProduccion] FROM #Conversion) k
INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = k.[TurnoId]
CROSS APPLY (SELECT
    TODATETIMEOFFSET(DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00' AS TIME), t.[HoraInicio]), CAST(k.[FechaProduccion] AS DATETIME2(0))), '-03:00') AS [Inicio],
    TODATETIMEOFFSET(DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00' AS TIME), t.[HoraFin]),
        CAST(CASE WHEN t.[HoraFin] <= t.[HoraInicio] THEN DATEADD(DAY, 1, k.[FechaProduccion]) ELSE k.[FechaProduccion] END AS DATETIME2(0))), '-03:00') AS [Fin]) v
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccion] tp
                  WHERE tp.[LineaId] = k.[LineaId] AND tp.[TurnoId] = k.[TurnoId] AND tp.[FechaProduccion] = k.[FechaProduccion]);
PRINT CONCAT('Turnos de producción creados por la conversión de registros: ', @@ROWCOUNT);

INSERT INTO [mnt].[PRD_ConversionTurnosT798] ([TurnoProduccionId], [CreadoPorEsteScript])
SELECT c.[TurnoProduccionId], 1 FROM @Creados c
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PRD_ConversionTurnosT798] r WHERE r.[TurnoProduccionId] = c.[TurnoProduccionId]);

-- Turno de producción de cada registro.
SELECT c.[Tabla], c.[RegistroId], tp.[TurnoProduccionId], c.[Excepcion0506]
INTO #Asignacion
FROM #Conversion c
INNER JOIN [mnt].[PRD_TurnosProduccion] tp
    ON tp.[LineaId] = c.[LineaId] AND tp.[TurnoId] = c.[TurnoId] AND tp.[FechaProduccion] = c.[FechaProduccion];

UPDATE r SET r.[TurnoProduccionId] = a.[TurnoProduccionId]
FROM [mnt].[CPR_Registros] r INNER JOIN #Asignacion a ON a.[Tabla] = 'CPR_Registros' AND a.[RegistroId] = r.[RegistroProcesoId]
WHERE r.[TurnoProduccionId] IS NULL;
PRINT CONCAT('Registros de Control de Proceso colgados de su turno de producción: ', @@ROWCOUNT);

UPDATE r SET r.[TurnoProduccionId] = a.[TurnoProduccionId]
FROM [mnt].[CAL_Registros] r INNER JOIN #Asignacion a ON a.[Tabla] = 'CAL_Registros' AND a.[RegistroId] = r.[RegistroCalidadId]
WHERE r.[TurnoProduccionId] IS NULL;
PRINT CONCAT('Registros de Calidad colgados de su turno de producción: ', @@ROWCOUNT);

UPDATE m SET m.[TurnoProduccionId] = a.[TurnoProduccionId]
FROM [mnt].[CAM_ConteosMinuto] m INNER JOIN #Asignacion a ON a.[Tabla] = 'CAM_ConteosMinuto' AND a.[RegistroId] = m.[ConteoCamaraMinutoId]
WHERE m.[TurnoProduccionId] IS NULL;
PRINT CONCAT('Conteos de cámara (sin turno materializado) colgados de su turno de producción: ', @@ROWCOUNT);

UPDATE p SET p.[TurnoProduccionId] = a.[TurnoProduccionId]
FROM [mnt].[CAM_Paradas] p INNER JOIN #Asignacion a ON a.[Tabla] = 'CAM_Paradas' AND a.[RegistroId] = p.[ParadaCamaraId]
WHERE p.[TurnoProduccionId] IS NULL;
PRINT CONCAT('Paradas de cámara (sin turno materializado) colgadas de su turno de producción: ', @@ROWCOUNT);

INSERT INTO [mnt].[PRD_ConversionRegistrosT798] ([Tabla], [RegistroId], [TurnoProduccionId], [Excepcion0506])
SELECT a.[Tabla], a.[RegistroId], a.[TurnoProduccionId], a.[Excepcion0506] FROM #Asignacion a
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[PRD_ConversionRegistrosT798] r WHERE r.[Tabla] = a.[Tabla] AND r.[RegistroId] = a.[RegistroId]);

DROP TABLE #Asignacion;
DROP TABLE #Conversion;
DROP TABLE #Pendientes;

COMMIT TRANSACTION;
GO

-- ============================================================================
-- 2. MinutosProgramados de los turnos convertidos, desde la programación. Todo o nada.
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Duración de cada turno en minutos (un nocturno cuenta entero en el día en que empieza).
;WITH Turnos AS (
    SELECT t.[TurnoId], t.[Activo],
           CASE WHEN DATEDIFF(MINUTE, t.[HoraInicio], t.[HoraFin]) <= 0
                THEN DATEDIFF(MINUTE, t.[HoraInicio], t.[HoraFin]) + 1440
                ELSE DATEDIFF(MINUTE, t.[HoraInicio], t.[HoraFin]) END AS [Duracion]
    FROM [mnt].[ABM_Turnos] t
), Candidatos AS (
    SELECT tp.[TurnoProduccionId], tp.[LineaId], tp.[TurnoId], tp.[FechaProduccion],
           -- DiaSemana como System.DayOfWeek: 0 = domingo (1900-01-07 fue domingo).
           DATEDIFF(DAY, CAST('19000107' AS DATE), tp.[FechaProduccion]) % 7 AS [DiaSemana],
           tu.[Activo] AS [TurnoActivo], tu.[Duracion],
           ver.[CalendarioLineaId], exc.[Horas]
    FROM [mnt].[PRD_TurnosProduccion] tp
    INNER JOIN Turnos tu ON tu.[TurnoId] = tp.[TurnoId]
    -- Versión vigente del plan: la de mayor VigenteDesde <= fecha, activa.
    OUTER APPLY (SELECT TOP 1 pl.[CalendarioLineaId] FROM [mnt].[PRD_ProgramacionLinea] pl
                 WHERE pl.[LineaId] = tp.[LineaId] AND pl.[Activo] = 1 AND pl.[VigenteDesde] <= tp.[FechaProduccion]
                 ORDER BY pl.[VigenteDesde] DESC) ver
    OUTER APPLY (SELECT TOP 1 e.[Horas] FROM [mnt].[PRD_ExcepcionesHorasDia] e
                 WHERE e.[LineaId] = tp.[LineaId] AND e.[Fecha] = tp.[FechaProduccion] AND e.[Activo] = 1
                 ORDER BY e.[TiempoProgramadoDiaId]) exc
    WHERE tp.[OrigenApertura] = N'SISTEMA'
      AND tp.[MinutosProgramados] = 0
      AND tp.[ObservacionCierre] LIKE N'Convertido (T-798 etapa %'
      AND tp.[ObservacionCierre] LIKE N'%minutos programados a recalcular en la etapa 7.%'
), Calculo AS (
    SELECT c.*,
           CASE WHEN c.[TurnoActivo] = 0 THEN 0
                WHEN c.[CalendarioLineaId] IS NULL THEN 1
                WHEN EXISTS (SELECT 1 FROM [mnt].[PRD_ProgramacionLineaTurnos] plt
                             WHERE plt.[CalendarioLineaId] = c.[CalendarioLineaId] AND plt.[DiaSemana] = c.[DiaSemana]
                               AND plt.[TurnoId] = c.[TurnoId]) THEN 1
                ELSE 0 END AS [Programado],
           (SELECT SUM(t2.[Duracion]) FROM Turnos t2
            WHERE t2.[Activo] = 1
              AND (c.[CalendarioLineaId] IS NULL
                   OR EXISTS (SELECT 1 FROM [mnt].[PRD_ProgramacionLineaTurnos] plt
                              WHERE plt.[CalendarioLineaId] = c.[CalendarioLineaId] AND plt.[DiaSemana] = c.[DiaSemana]
                                AND plt.[TurnoId] = t2.[TurnoId]))) AS [BaseDia],
           CAST(ROUND(c.[Horas] * 60, 0) AS INT) AS [MinutosDia]
    FROM Candidatos c
)
SELECT k.[TurnoProduccionId],
       CASE WHEN k.[Programado] = 0 THEN 0
            WHEN k.[Horas] IS NULL THEN k.[Duracion]
            WHEN k.[MinutosDia] <= 0 OR ISNULL(k.[BaseDia], 0) <= 0 THEN 0
            WHEN k.[MinutosDia] >= k.[BaseDia] THEN k.[Duracion]
            ELSE CAST(ROUND(CAST(k.[Duracion] AS DECIMAL(18, 6)) * k.[MinutosDia] / k.[BaseDia], 0) AS INT)
       END AS [Minutos]
INTO #Recalculo
FROM Calculo k;

MERGE [mnt].[PRD_ConversionTurnosT798] AS r
USING (SELECT x.[TurnoProduccionId], tp.[MinutosProgramados], tp.[ObservacionCierre]
       FROM #Recalculo x INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = x.[TurnoProduccionId]) AS s
ON r.[TurnoProduccionId] = s.[TurnoProduccionId]
WHEN MATCHED THEN UPDATE SET r.[Recalculado] = 1, r.[MinutosProgramadosAntes] = s.[MinutosProgramados], r.[ObservacionCierreAntes] = s.[ObservacionCierre]
WHEN NOT MATCHED THEN INSERT ([TurnoProduccionId], [CreadoPorEsteScript], [Recalculado], [MinutosProgramadosAntes], [ObservacionCierreAntes])
                      VALUES (s.[TurnoProduccionId], 0, 1, s.[MinutosProgramados], s.[ObservacionCierre]);  -- MERGE exige el ;

UPDATE tp
SET tp.[MinutosProgramados] = x.[Minutos],
    tp.[ObservacionCierre] = LEFT(REPLACE(tp.[ObservacionCierre], N'minutos programados a recalcular en la etapa 7.',
                                  CONCAT(N'minutos programados recalculados desde la programación (script 38): ', x.[Minutos], N'.')), 500),
    tp.[FechaHoraModificacion] = SYSDATETIMEOFFSET(),
    tp.[ModificadoPorUsuarioId] = N'SISTEMA'
FROM [mnt].[PRD_TurnosProduccion] tp
INNER JOIN #Recalculo x ON x.[TurnoProduccionId] = tp.[TurnoProduccionId];
PRINT CONCAT('Turnos convertidos con minutos programados recalculados: ', @@ROWCOUNT);

DECLARE @EnCero INT = (SELECT COUNT(*) FROM #Recalculo WHERE [Minutos] = 0);
PRINT CONCAT('  de ellos, fuera de programación (quedan en 0 min): ', @EnCero);

DROP TABLE #Recalculo;

COMMIT TRANSACTION;
GO

-- Resumen.
SELECT tp.[Estado], tp.[OrigenApertura],
       CASE WHEN tp.[ObservacionCierre] LIKE N'Convertido (T-798 etapa %' THEN 'CONVERTIDO' ELSE 'APP' END AS [Tipo],
       COUNT(*) AS [Turnos], SUM(tp.[MinutosProgramados]) AS [MinutosProgramados]
FROM [mnt].[PRD_TurnosProduccion] tp
GROUP BY tp.[Estado], tp.[OrigenApertura], CASE WHEN tp.[ObservacionCierre] LIKE N'Convertido (T-798 etapa %' THEN 'CONVERTIDO' ELSE 'APP' END;
GO

SET NOEXEC OFF;
GO
PRINT 'Script 38_t798_conversion_turnos.sql terminado.'
GO
