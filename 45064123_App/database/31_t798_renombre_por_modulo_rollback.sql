-- ==============================================================================
-- Script: 31_t798_renombre_por_modulo_rollback.sql
-- Descripción: Deshace 31_t798_renombre_por_modulo.sql (T-798 etapa 1).
--   1. CHECK de MetodoConteo sin BOLSONES (solo si ninguna cámara usa BOLSONES; si no, avisa y lo deja).
--   2. ABM_OperadoresLinea: se deja la FK del turno habitual (la columna vuelve a llamarse TurnoId).
--   3. Columnas con el nombre anterior (ProductoMaestroId -> ProductoId, TurnoHabitualId -> TurnoId).
--   4. Tablas con el nombre anterior.
--   5. Vistas VW_* con la definición anterior.
--   NO quita CAM_Paradas.UmbralSegundos / SuperaUmbral (T-784): tienen el snapshot del umbral de
--   cada parada y la versión anterior de la API las ignora. Si hiciera falta, quitarlas a mano.
-- Requiere: haber corrido antes los rollback de 33 y 32 (si se corrieron 33 y 32).
-- Idempotente: si una tabla o columna ya tiene el nombre anterior, no hace nada.
-- Correr con la API detenida y volver a publicar la versión anterior de la API.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Seguridad: si todavía existe ABM_ProductosSku (script 32), primero hay que correr 32_t798_maestros_productos_rollback.sql.
IF OBJECT_ID(N'mnt.ABM_ProductosSku', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'Existe mnt.ABM_ProductosSku: correr antes 33_t798_datos_tango2_rollback.sql y 32_t798_maestros_productos_rollback.sql.', 16, 1);
    SET NOEXEC ON;
END
GO

-- ============================================================================
-- 1. CHECK de MetodoConteo como en el script 28 (CARRILES, MANGAS).
-- ============================================================================
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_PuntosControlCalidad_MetodoConteo'
             AND parent_object_id = OBJECT_ID(N'mnt.CAL_PuntosControl')
             AND definition LIKE N'%BOLSONES%')
BEGIN
    IF EXISTS (SELECT 1 FROM [mnt].[CAL_PuntosControl] WHERE [MetodoConteo] = N'BOLSONES')
        PRINT 'AVISO: hay cámaras con MetodoConteo = BOLSONES: el CHECK queda con BOLSONES.'
    ELSE
    BEGIN
        ALTER TABLE [mnt].[CAL_PuntosControl] DROP CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo];
        ALTER TABLE [mnt].[CAL_PuntosControl]
            ADD CONSTRAINT [CK_PuntosControlCalidad_MetodoConteo]
            CHECK ([MetodoConteo] IS NULL OR [MetodoConteo] IN (N'CARRILES', N'MANGAS'));
        PRINT 'Check CK_PuntosControlCalidad_MetodoConteo restaurado (CARRILES, MANGAS).'
    END
END
ELSE
    PRINT 'Check CK_PuntosControlCalidad_MetodoConteo sin BOLSONES (o no existe): sin cambios.'
GO

-- ============================================================================
-- 2. ABM_OperadoresLinea.TurnoHabitualId: la columna y su FK se conservan; en el paso 3 la
--    columna vuelve a llamarse TurnoId, que es lo que lee la versión anterior de la API.
-- ============================================================================
PRINT 'ABM_OperadoresLinea: el turno habitual se conserva (vuelve a llamarse TurnoId).'
GO

-- ============================================================================
-- 3. Columnas: nombre anterior (sobre las tablas con el nombre nuevo).
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Columnas TABLE ([Orden] INT IDENTITY(1,1), [Tabla] SYSNAME, [Desde] SYSNAME, [Hacia] SYSNAME);
INSERT INTO @Columnas ([Tabla], [Desde], [Hacia]) VALUES
    (N'ABM_ProductosMaestro', N'ProductoMaestroId', N'ProductoId'),
    (N'PRD_ParametrosLineaProducto', N'ProductoMaestroId', N'ProductoId'),
    (N'CPR_Estandares', N'ProductoMaestroId', N'ProductoId'),
    (N'CPR_Registros', N'ProductoMaestroId', N'ProductoId'),
    (N'CAL_Registros', N'ProductoMaestroId', N'ProductoId'),
    (N'ABM_OperadoresLinea', N'TurnoHabitualId', N'TurnoId');

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
-- 4. Tablas: nombre anterior.
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Tablas TABLE ([Orden] INT IDENTITY(1,1), [Desde] SYSNAME, [Hacia] SYSNAME);
INSERT INTO @Tablas ([Desde], [Hacia]) VALUES
    (N'CAL_RegistrosRevisiones', N'RegistrosCalidadRevisiones'),
    (N'CAL_Registros', N'RegistrosCalidad'),
    (N'CAL_TiposDefecto', N'TiposDefectoCalidad'),
    (N'CAL_PuntosControl', N'PuntosControlCalidad'),
    (N'INS_HallazgosNoOk', N'HallazgosNoOk'),
    (N'INS_EjecucionesDetalle', N'EjecucionesInspeccionDetalle'),
    (N'INS_Ejecuciones', N'EjecucionesInspeccion'),
    (N'INS_ItemsPlan', N'ItemsPlanInspeccion'),
    (N'INS_Planes', N'PlanesInspeccion'),
    (N'CPR_RegistrosDetalle', N'RegistrosProcesoDetalle'),
    (N'CPR_Registros', N'RegistrosProceso'),
    (N'CPR_Estandares', N'EstandaresParametro'),
    (N'CPR_Parametros', N'Parametros'),
    (N'CPR_GruposParametro', N'GruposParametro'),
    (N'MNT_Tareas', N'TareasMantenimiento'),
    (N'DET_ReglasAnalisis', N'ReglasAnalisis'),
    (N'DET_AnalisisAdjuntos', N'AnalisisAdjuntos'),
    (N'DET_AnalisisCambios', N'AnalisisCambios'),
    (N'DET_Notas', N'DetencionesNotas'),
    (N'DET_HistorialEstados', N'DetencionesEstadoHistorial'),
    (N'DET_Detenciones', N'Detenciones'),
    (N'CAM_Alertas', N'AlertasCamara'),
    (N'CAM_Paradas', N'ParadasCamara'),
    (N'CAM_ConteosMinuto', N'ConteosCamaraMinuto'),
    (N'PRD_ParametrosLineaProducto', N'LineasProductos'),
    (N'PRD_ExcepcionesHorasDia', N'TiemposProgramadosDia'),
    (N'PRD_ProgramacionLineaTurnos', N'CalendariosLineaTurnos'),
    (N'PRD_ProgramacionLinea', N'CalendariosLinea'),
    (N'ABM_OperadoresLinea', N'UsuariosLineas'),
    (N'ABM_DetallesFalla', N'DetallesFalla'),
    (N'ABM_TiposFalla', N'TiposFalla'),
    (N'ABM_ProductosMaestro', N'Productos'),
    (N'ABM_Turnos', N'Turnos'),
    (N'ABM_Equipos', N'Equipos'),
    (N'ABM_Lineas', N'Lineas');

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
-- 5. Vistas: definición anterior (scripts 06 y 17).
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
    (SELECT COUNT(*) FROM [mnt].[DetencionesNotas] dn WHERE dn.[DetencionId] = d.[DetencionId]) AS [CantidadNotas]
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
LEFT JOIN [mnt].[DetallesFalla] df ON d.[DetalleFallaId] = df.[DetalleFallaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] uc ON d.[CerradoPorUsuarioId] = uc.[Id]
LEFT JOIN [dbo].[AspNetUsers] ucr ON d.[CreadoPorUsuarioId] = ucr.[Id]
LEFT JOIN [dbo].[AspNetUsers] umo ON d.[ModificadoPorUsuarioId] = umo.[Id]
WHERE d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesDetalle restaurada con los nombres anteriores.'
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
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
LEFT JOIN [mnt].[Equipos] e ON d.[EquipoId] = e.[EquipoId]
LEFT JOIN [mnt].[TiposFalla] tf ON d.[TipoFallaId] = tf.[TipoFallaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
LEFT JOIN [dbo].[AspNetUsers] u ON d.[CreadoPorUsuarioId] = u.[Id]
WHERE d.[EstadoActual] != ''CERRADA''
  AND d.[Eliminada] = 0';
    PRINT 'Vista VW_DetencionesAbiertas restaurada con los nombres anteriores.'
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
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[Fecha], t.[TurnoId], t.[Nombre]';
    PRINT 'Vista VW_DetencionesIndicadores restaurada con los nombres anteriores.'
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
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Fecha] = CAST(GETDATE() AS DATE)
  AND d.[Eliminada] = 0
GROUP BY l.[LineaId], l.[Nombre], d.[TurnoId], t.[Nombre], d.[Fecha]';
    PRINT 'Vista VW_ResumenTurnoActual restaurada con los nombres anteriores.'
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
FROM [mnt].[Lineas] l
LEFT JOIN [mnt].[Equipos] e ON l.[LineaId] = e.[LineaId]';
    PRINT 'Vista VW_LineasConEquipos restaurada con los nombres anteriores.'
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
LEFT JOIN [mnt].[UsuariosLineas] ul ON u.[Id] = ul.[UsuarioId]
LEFT JOIN [mnt].[Lineas] l ON ul.[LineaId] = l.[LineaId]';
    PRINT 'Vista VW_UsuariosConLineas restaurada con los nombres anteriores.'
END
ELSE
    PRINT 'Vista VW_UsuariosConLineas no existe: sin cambios.'
GO

PRINT 'Script 31_t798_renombre_por_modulo_rollback.sql completado.'
GO

SET NOEXEC OFF
GO
