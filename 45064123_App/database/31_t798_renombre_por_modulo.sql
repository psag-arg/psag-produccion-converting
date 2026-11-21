-- ==============================================================================
-- Script: 31_t798_renombre_por_modulo.sql
-- Descripción: T-798 etapa 1 · Nombres por módulo (docs/diseno/T-798_modelo_produccion.md, 2.2 y 2.8).
--   1. Renombra todas las tablas de negocio de [mnt] con el prefijo de su módulo
--      (ABM_, PRD_, CAM_, DET_, MNT_, CPR_, INS_, CAL_). El esquema sigue siendo [mnt].
--      AspNet* y Auditoria no cambian. Las tablas nuevas de etapas posteriores no se crean acá.
--      sp_rename no cambia los nombres de PK, FK, índices ni CHECKs: quedan con el nombre viejo.
--   2. Renombra las columnas de esta etapa:
--      ProductoId -> ProductoMaestroId (ABM_ProductosMaestro, PRD_ParametrosLineaProducto,
--      CPR_Estandares, CPR_Registros, CAL_Registros) y ABM_OperadoresLinea.TurnoId -> TurnoHabitualId.
--   3. ABM_OperadoresLinea.TurnoHabitualId: si la tabla no tenía TurnoId, la agrega (INT NULL) con su FK.
--   4. CAM_Paradas: UmbralSegundos INT NULL y SuperaUmbral BIT NULL (T-784; ex script 32, no desplegado).
--   5. CAL_PuntosControl: CHECK de MetodoConteo con BOLSONES (T-785; ex script 33, no desplegado).
--   6. Vistas de reportes (VW_*): se re-crean apuntando a las tablas con el nombre nuevo.
-- Versión: 1.0
-- Requiere: todos los anteriores (hasta 30). Los scripts 32 y 33 viejos (T-784/T-785) NO se corren.
-- Idempotente: si una tabla o columna ya tiene el nombre nuevo, no hace nada.
-- Correr ANTES de publicar la API de T-798 (la API lee los nombres nuevos) y con la API detenida.
-- Deshacer: 31_t798_renombre_por_modulo_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ============================================================================
-- 1. Tablas: nombre por módulo (sección 2.2). Todo o nada (transacción).
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Tablas TABLE ([Orden] INT IDENTITY(1,1), [Desde] SYSNAME, [Hacia] SYSNAME);
INSERT INTO @Tablas ([Desde], [Hacia]) VALUES
    (N'Lineas', N'ABM_Lineas'),
    (N'Equipos', N'ABM_Equipos'),
    (N'Turnos', N'ABM_Turnos'),
    (N'Productos', N'ABM_ProductosMaestro'),
    (N'TiposFalla', N'ABM_TiposFalla'),
    (N'DetallesFalla', N'ABM_DetallesFalla'),
    (N'UsuariosLineas', N'ABM_OperadoresLinea'),
    (N'CalendariosLinea', N'PRD_ProgramacionLinea'),
    (N'CalendariosLineaTurnos', N'PRD_ProgramacionLineaTurnos'),
    (N'TiemposProgramadosDia', N'PRD_ExcepcionesHorasDia'),
    (N'LineasProductos', N'PRD_ParametrosLineaProducto'),
    (N'ConteosCamaraMinuto', N'CAM_ConteosMinuto'),
    (N'ParadasCamara', N'CAM_Paradas'),
    (N'AlertasCamara', N'CAM_Alertas'),
    (N'Detenciones', N'DET_Detenciones'),
    (N'DetencionesEstadoHistorial', N'DET_HistorialEstados'),
    (N'DetencionesNotas', N'DET_Notas'),
    (N'AnalisisCambios', N'DET_AnalisisCambios'),
    (N'AnalisisAdjuntos', N'DET_AnalisisAdjuntos'),
    (N'ReglasAnalisis', N'DET_ReglasAnalisis'),
    (N'TareasMantenimiento', N'MNT_Tareas'),
    (N'GruposParametro', N'CPR_GruposParametro'),
    (N'Parametros', N'CPR_Parametros'),
    (N'EstandaresParametro', N'CPR_Estandares'),
    (N'RegistrosProceso', N'CPR_Registros'),
    (N'RegistrosProcesoDetalle', N'CPR_RegistrosDetalle'),
    (N'PlanesInspeccion', N'INS_Planes'),
    (N'ItemsPlanInspeccion', N'INS_ItemsPlan'),
    (N'EjecucionesInspeccion', N'INS_Ejecuciones'),
    (N'EjecucionesInspeccionDetalle', N'INS_EjecucionesDetalle'),
    (N'HallazgosNoOk', N'INS_HallazgosNoOk'),
    (N'PuntosControlCalidad', N'CAL_PuntosControl'),
    (N'TiposDefectoCalidad', N'CAL_TiposDefecto'),
    (N'RegistrosCalidad', N'CAL_Registros'),
    (N'RegistrosCalidadRevisiones', N'CAL_RegistrosRevisiones');

DECLARE @Desde SYSNAME, @Hacia SYSNAME, @Objeto NVARCHAR(300);
DECLARE cTablas CURSOR LOCAL FAST_FORWARD FOR SELECT [Desde], [Hacia] FROM @Tablas ORDER BY [Orden];
OPEN cTablas;
FETCH NEXT FROM cTablas INTO @Desde, @Hacia;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'mnt.' + QUOTENAME(@Hacia), N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'mnt.' + QUOTENAME(@Desde), N'U') IS NOT NULL
            PRINT CONCAT(N'AVISO: existen mnt.', @Desde, N' y mnt.', @Hacia, N'. No se toca: revisar a mano.');
        ELSE
            PRINT CONCAT(N'mnt.', @Hacia, N' ya existe: sin cambios.');
    END
    ELSE IF OBJECT_ID(N'mnt.' + QUOTENAME(@Desde), N'U') IS NOT NULL
    BEGIN
        SET @Objeto = N'mnt.' + QUOTENAME(@Desde);
        EXEC sys.sp_rename @objname = @Objeto, @newname = @Hacia, @objtype = N'OBJECT';
        PRINT CONCAT(N'Tabla mnt.', @Desde, N' renombrada a mnt.', @Hacia, N'.');
    END
    ELSE
        PRINT CONCAT(N'AVISO: no existe mnt.', @Desde, N' ni mnt.', @Hacia, N' (tabla no instalada): sin cambios.');

    FETCH NEXT FROM cTablas INTO @Desde, @Hacia;
END
CLOSE cTablas;
DEALLOCATE cTablas;

COMMIT TRANSACTION;
GO

-- ============================================================================
-- 2. Columnas renombradas en esta etapa (sección 2.8).
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Columnas TABLE ([Orden] INT IDENTITY(1,1), [Tabla] SYSNAME, [Desde] SYSNAME, [Hacia] SYSNAME);
INSERT INTO @Columnas ([Tabla], [Desde], [Hacia]) VALUES
    (N'ABM_ProductosMaestro', N'ProductoId', N'ProductoMaestroId'),
    (N'PRD_ParametrosLineaProducto', N'ProductoId', N'ProductoMaestroId'),
    (N'CPR_Estandares', N'ProductoId', N'ProductoMaestroId'),
    (N'CPR_Registros', N'ProductoId', N'ProductoMaestroId'),
    (N'CAL_Registros', N'ProductoId', N'ProductoMaestroId'),
    (N'ABM_OperadoresLinea', N'TurnoId', N'TurnoHabitualId');

DECLARE @Tabla SYSNAME, @Desde SYSNAME, @Hacia SYSNAME, @Objeto NVARCHAR(400);
DECLARE cColumnas CURSOR LOCAL FAST_FORWARD FOR SELECT [Tabla], [Desde], [Hacia] FROM @Columnas ORDER BY [Orden];
OPEN cColumnas;
FETCH NEXT FROM cColumnas INTO @Tabla, @Desde, @Hacia;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'mnt.' + QUOTENAME(@Tabla), N'U') IS NULL
        PRINT CONCAT(N'AVISO: no existe mnt.', @Tabla, N': columna ', @Desde, N' sin cambios.');
    ELSE IF COL_LENGTH(N'mnt.' + QUOTENAME(@Tabla), @Hacia) IS NOT NULL
        PRINT CONCAT(N'Columna mnt.', @Tabla, N'.', @Hacia, N' ya existe: sin cambios.');
    ELSE IF COL_LENGTH(N'mnt.' + QUOTENAME(@Tabla), @Desde) IS NOT NULL
    BEGIN
        SET @Objeto = N'mnt.' + QUOTENAME(@Tabla) + N'.' + QUOTENAME(@Desde);
        EXEC sys.sp_rename @objname = @Objeto, @newname = @Hacia, @objtype = N'COLUMN';
        PRINT CONCAT(N'Columna mnt.', @Tabla, N'.', @Desde, N' renombrada a ', @Hacia, N'.');
    END
    ELSE
        PRINT CONCAT(N'AVISO: mnt.', @Tabla, N' no tiene ', @Desde, N' ni ', @Hacia, N'.');

    FETCH NEXT FROM cColumnas INTO @Tabla, @Desde, @Hacia;
END
CLOSE cColumnas;
DEALLOCATE cColumnas;

COMMIT TRANSACTION;
GO

-- ============================================================================
-- 3. ABM_OperadoresLinea.TurnoHabitualId (turno habitual, opcional) con FK a ABM_Turnos.
--    Los scripts de instalación no crean UsuariosLineas.TurnoId: si no estaba, se agrega vacía.
-- ============================================================================
IF OBJECT_ID(N'mnt.ABM_OperadoresLinea', N'U') IS NOT NULL
   AND COL_LENGTH(N'mnt.ABM_OperadoresLinea', N'TurnoHabitualId') IS NULL
BEGIN
    ALTER TABLE [mnt].[ABM_OperadoresLinea] ADD [TurnoHabitualId] INT NULL;
    PRINT 'Columna ABM_OperadoresLinea.TurnoHabitualId agregada (vacía).'
END
GO

IF OBJECT_ID(N'mnt.ABM_OperadoresLinea', N'U') IS NOT NULL
   AND OBJECT_ID(N'mnt.ABM_Turnos', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1
                   FROM sys.foreign_key_columns fkc
                   WHERE fkc.parent_object_id = OBJECT_ID(N'mnt.ABM_OperadoresLinea')
                     AND fkc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'mnt.ABM_OperadoresLinea'), N'TurnoHabitualId', 'ColumnId')
                     AND fkc.referenced_object_id = OBJECT_ID(N'mnt.ABM_Turnos'))
BEGIN
    ALTER TABLE [mnt].[ABM_OperadoresLinea]
        ADD CONSTRAINT [FK_ABM_OperadoresLinea_ABM_Turnos_TurnoHabitualId]
        FOREIGN KEY ([TurnoHabitualId]) REFERENCES [mnt].[ABM_Turnos] ([TurnoId]);
    PRINT 'FK FK_ABM_OperadoresLinea_ABM_Turnos_TurnoHabitualId creada.'
END
ELSE
    PRINT 'FK de ABM_OperadoresLinea.TurnoHabitualId ya existía (o falta la tabla): sin cambios.'
GO

-- ============================================================================
-- 4. CAM_Paradas: marca de umbral de T-784 (toda microparada se registra; los reportes deciden).
--    UmbralSegundos = umbral efectivo de la cámara AL INGRESAR la parada (snapshot).
--    SuperaUmbral = la parada llegó a ese umbral. Las filas existentes quedan en NULL.
-- ============================================================================
IF OBJECT_ID(N'mnt.CAM_Paradas', N'U') IS NOT NULL AND COL_LENGTH(N'mnt.CAM_Paradas', N'UmbralSegundos') IS NULL
BEGIN
    ALTER TABLE [mnt].[CAM_Paradas] ADD [UmbralSegundos] INT NULL;
    PRINT 'Columna CAM_Paradas.UmbralSegundos agregada.'
END
ELSE
    PRINT 'Columna CAM_Paradas.UmbralSegundos ya existía (o falta la tabla).'
GO

IF OBJECT_ID(N'mnt.CAM_Paradas', N'U') IS NOT NULL AND COL_LENGTH(N'mnt.CAM_Paradas', N'SuperaUmbral') IS NULL
BEGIN
    ALTER TABLE [mnt].[CAM_Paradas] ADD [SuperaUmbral] BIT NULL;
    PRINT 'Columna CAM_Paradas.SuperaUmbral agregada.'
END
ELSE
    PRINT 'Columna CAM_Paradas.SuperaUmbral ya existía (o falta la tabla).'
GO

-- ============================================================================
-- 5. CAL_PuntosControl.MetodoConteo admite BOLSONES (T-785: la cámara 8 cuenta bolsones).
--    Se rehace el CHECK del script 28 (conserva su nombre).
-- ============================================================================
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_PuntosControlCalidad_MetodoConteo'
             AND parent_object_id = OBJECT_ID(N'mnt.CAL_PuntosControl')
             AND definition NOT LIKE N'%BOLSONES%')
BEGIN
    ALTER TABLE [mnt].[CAL_PuntosControl] DROP CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo];
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo anterior quitado.'
END
GO

IF OBJECT_ID(N'mnt.CAL_PuntosControl', N'U') IS NOT NULL
   AND COL_LENGTH(N'mnt.CAL_PuntosControl', N'MetodoConteo') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MetodoConteo')
BEGIN
    ALTER TABLE [mnt].[CAL_PuntosControl]
        ADD CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo]
        CHECK ([MetodoConteo] IS NULL OR [MetodoConteo] IN (N'CARRILES', N'MANGAS', N'BOLSONES'));
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo creado (CARRILES, MANGAS, BOLSONES).'
END
ELSE
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo ya admite BOLSONES (o falta la columna).'
GO

-- ============================================================================
-- 6. Vistas de reportes SQL (06 y 17): mismas columnas, tablas con el nombre nuevo.
--    Solo se alteran las que existen. La aplicación no las usa.
-- ============================================================================
-- VW_DetencionesDetalle
IF OBJECT_ID(N'mnt.VW_DetencionesDetalle', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesDetalle]
AS
SELECT
    d.[DetencionId],
    d.[Codigo],
    d.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[EquipoId],
    e.[Nombre] AS [EquipoNombre],
    d.[TipoFallaId],
    tf.[Nombre] AS [TipoFallaNombre],
    d.[DetalleFallaId],
    df.[Nombre] AS [DetalleFallaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    d.[MinutosDeclarados],
    d.[FechaHoraParada],
    d.[FechaHoraArranque],
    d.[OrigenTiempo],
    d.[MinutosEfectivos],
    d.[Descripcion],
    d.[EstadoActual],
    d.[AccionesRealizadas],
    d.[ResolucionDescripcion],
    d.[FechaHoraCierre],
    d.[CerradoPorUsuarioId],
    uc.[NombreCompleto] AS [CerradoPorNombre],
    d.[FechaHoraCreacion],
    d.[CreadoPorUsuarioId],
    ucr.[NombreCompleto] AS [CreadoPorNombre],
    d.[FechaHoraModificacion],
    d.[ModificadoPorUsuarioId],
    umo.[NombreCompleto] AS [ModificadoPorNombre],
    -- Indicador si esta abierto
    CASE WHEN d.[EstadoActual] != ''CERRADA'' THEN 1 ELSE 0 END AS [EstaAbierta],
    -- Cantidad de notas
    (SELECT COUNT(*) FROM [mnt].[DET_Notas] dn WHERE dn.[DetencionId] = d.[DetencionId]) AS [CantidadNotas]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
LEFT JOIN [mnt].[ABM_DetallesFalla] df ON d.[DetalleFallaId] = df.[DetalleFallaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] uc ON d.[CerradoPorUsuarioId] = uc.[Id]
LEFT JOIN [dbo].[AspNetUsers] ucr ON d.[CreadoPorUsuarioId] = ucr.[Id]
LEFT JOIN [dbo].[AspNetUsers] umo ON d.[ModificadoPorUsuarioId] = umo.[Id]
WHERE d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesDetalle actualizada con los nombres nuevos.'
END
ELSE
    PRINT 'Vista VW_DetencionesDetalle no existe: sin cambios.'
GO

-- VW_DetencionesAbiertas
IF OBJECT_ID(N'mnt.VW_DetencionesAbiertas', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesAbiertas]
AS
SELECT
    d.[DetencionId],
    d.[Codigo],
    d.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[EquipoId],
    e.[Nombre] AS [EquipoNombre],
    d.[TipoFallaId],
    tf.[Nombre] AS [TipoFallaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    d.[MinutosEfectivos],
    d.[Descripcion],
    d.[EstadoActual],
    d.[CreadoPorUsuarioId],
    u.[NombreCompleto] AS [CreadoPorNombre],
    d.[FechaHoraCreacion]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[ABM_Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[ABM_TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] u ON d.[CreadoPorUsuarioId] = u.[Id]
WHERE d.[EstadoActual] != ''CERRADA''
  AND d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesAbiertas actualizada con los nombres nuevos.'
END
ELSE
    PRINT 'Vista VW_DetencionesAbiertas no existe: sin cambios.'
GO

-- VW_DetencionesIndicadores
IF OBJECT_ID(N'mnt.VW_DetencionesIndicadores', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_DetencionesIndicadores]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[Fecha],
    t.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    COUNT(*) AS [TotalDetenciones],
    SUM(CASE WHEN d.[EstadoActual] = ''REGISTRADA'' THEN 1 ELSE 0 END) AS [DetencionesRegistradas],
    SUM(CASE WHEN d.[EstadoActual] = ''EN_SEGUIMIENTO'' THEN 1 ELSE 0 END) AS [DetencionesEnSeguimiento],
    SUM(CASE WHEN d.[EstadoActual] = ''CERRADA'' THEN 1 ELSE 0 END) AS [DetencionesCerradas],
    AVG(CAST(d.[MinutosEfectivos] AS FLOAT)) AS [DuracionPromedioMinutos],
    SUM(d.[MinutosEfectivos]) AS [TiempoTotalParadaMinutos]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[Fecha], t.[TurnoId], t.[Nombre]';
    PRINT 'Vista VW_DetencionesIndicadores actualizada con los nombres nuevos.'
END
ELSE
    PRINT 'Vista VW_DetencionesIndicadores no existe: sin cambios.'
GO

-- VW_ResumenTurnoActual
IF OBJECT_ID(N'mnt.VW_ResumenTurnoActual', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_ResumenTurnoActual]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    COUNT(*) AS [TotalDetenciones],
    SUM(d.[MinutosEfectivos]) AS [MinutosTotales],
    SUM(CASE WHEN d.[EstadoActual] != ''CERRADA'' THEN 1 ELSE 0 END) AS [DetencionesAbiertas]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[ABM_Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[ABM_Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Fecha] = CAST(GETDATE() AS DATE)
  AND d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[TurnoId], t.[Nombre], d.[Fecha]';
    PRINT 'Vista VW_ResumenTurnoActual actualizada con los nombres nuevos.'
END
ELSE
    PRINT 'Vista VW_ResumenTurnoActual no existe: sin cambios.'
GO

-- VW_LineasConEquipos
IF OBJECT_ID(N'mnt.VW_LineasConEquipos', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_LineasConEquipos]
AS
SELECT
    l.[LineaId],
    l.[Codigo] AS [LineaCodigo],
    l.[Nombre] AS [LineaNombre],
    l.[Descripcion] AS [LineaDescripcion],
    l.[Orden] AS [LineaOrden],
    l.[Activo] AS [LineaActiva],
    e.[EquipoId],
    e.[Codigo] AS [EquipoCodigo],
    e.[Nombre] AS [EquipoNombre],
    e.[Descripcion] AS [EquipoDescripcion],
    e.[Orden] AS [EquipoOrden],
    e.[Activo] AS [EquipoActivo]
FROM [mnt].[ABM_Lineas] l
LEFT JOIN [mnt].[ABM_Equipos] e ON l.[LineaId] = e.[LineaId]';
    PRINT 'Vista VW_LineasConEquipos actualizada con los nombres nuevos.'
END
ELSE
    PRINT 'Vista VW_LineasConEquipos no existe: sin cambios.'
GO

-- VW_UsuariosConLineas
IF OBJECT_ID(N'mnt.VW_UsuariosConLineas', N'V') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'ALTER VIEW [mnt].[VW_UsuariosConLineas]
AS
SELECT
    u.[Id] AS [UsuarioId],
    u.[UserName],
    u.[NombreCompleto],
    u.[Email],
    u.[Activo],
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    ul.[FechaAsignacion]
FROM [dbo].[AspNetUsers] u
LEFT JOIN [mnt].[ABM_OperadoresLinea] ul ON u.[Id] = ul.[UsuarioId]
LEFT JOIN [mnt].[ABM_Lineas] l ON ul.[LineaId] = l.[LineaId]';
    PRINT 'Vista VW_UsuariosConLineas actualizada con los nombres nuevos.'
END
ELSE
    PRINT 'Vista VW_UsuariosConLineas no existe: sin cambios.'
GO

PRINT 'Script 31_t798_renombre_por_modulo.sql completado.'
GO
