-- ==============================================================================
-- Script: 06_create_views.sql
-- Descripcion: Crea vistas para consultas comunes
-- Version: 2.0 - Fase 1 (Detenciones con modelo de tiempo)
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- Vista: VW_DetencionesDetalle
-- Descripcion: Vista completa de detenciones con informacion relacionada
-- ============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_DetencionesDetalle')
    DROP VIEW [mnt].[VW_DetencionesDetalle]
GO

CREATE VIEW [mnt].[VW_DetencionesDetalle]
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
    CASE WHEN d.[EstadoActual] != 'CERRADA' THEN 1 ELSE 0 END AS [EstaAbierta],
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
GO

PRINT 'Vista VW_DetencionesDetalle creada exitosamente.'
GO

-- ============================================================================
-- Vista: VW_DetencionesAbiertas
-- Descripcion: Detenciones que no estan cerradas
-- ============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_DetencionesAbiertas')
    DROP VIEW [mnt].[VW_DetencionesAbiertas]
GO

CREATE VIEW [mnt].[VW_DetencionesAbiertas]
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
WHERE d.[EstadoActual] != 'CERRADA'
GO

PRINT 'Vista VW_DetencionesAbiertas creada exitosamente.'
GO

-- ============================================================================
-- Vista: VW_DetencionesIndicadores
-- Descripcion: Indicadores agregados por linea y periodo
-- ============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_DetencionesIndicadores')
    DROP VIEW [mnt].[VW_DetencionesIndicadores]
GO

CREATE VIEW [mnt].[VW_DetencionesIndicadores]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[Fecha],
    t.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    COUNT(*) AS [TotalDetenciones],
    SUM(CASE WHEN d.[EstadoActual] = 'REGISTRADA' THEN 1 ELSE 0 END) AS [DetencionesRegistradas],
    SUM(CASE WHEN d.[EstadoActual] = 'EN_SEGUIMIENTO' THEN 1 ELSE 0 END) AS [DetencionesEnSeguimiento],
    SUM(CASE WHEN d.[EstadoActual] = 'CERRADA' THEN 1 ELSE 0 END) AS [DetencionesCerradas],
    AVG(CAST(d.[MinutosEfectivos] AS FLOAT)) AS [DuracionPromedioMinutos],
    SUM(d.[MinutosEfectivos]) AS [TiempoTotalParadaMinutos]
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
GROUP BY l.[LineaId], l.[Nombre], d.[Fecha], t.[TurnoId], t.[Nombre]
GO

PRINT 'Vista VW_DetencionesIndicadores creada exitosamente.'
GO

-- ============================================================================
-- Vista: VW_LineasConEquipos
-- Descripcion: Lineas con sus equipos activos
-- ============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_LineasConEquipos')
    DROP VIEW [mnt].[VW_LineasConEquipos]
GO

CREATE VIEW [mnt].[VW_LineasConEquipos]
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
LEFT JOIN [mnt].[Equipos] e ON l.[LineaId] = e.[LineaId]
GO

PRINT 'Vista VW_LineasConEquipos creada exitosamente.'
GO

-- ============================================================================
-- Vista: VW_UsuariosConLineas
-- Descripcion: Usuarios con sus lineas asignadas
-- ============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_UsuariosConLineas')
    DROP VIEW [mnt].[VW_UsuariosConLineas]
GO

CREATE VIEW [mnt].[VW_UsuariosConLineas]
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
LEFT JOIN [mnt].[Lineas] l ON ul.[LineaId] = l.[LineaId]
GO

PRINT 'Vista VW_UsuariosConLineas creada exitosamente.'
GO

-- ============================================================================
-- Vista: VW_ResumenTurnoActual
-- Descripcion: Resumen de detenciones del turno actual por linea
-- ============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = N'VW_ResumenTurnoActual')
    DROP VIEW [mnt].[VW_ResumenTurnoActual]
GO

CREATE VIEW [mnt].[VW_ResumenTurnoActual]
AS
SELECT
    l.[LineaId],
    l.[Nombre] AS [LineaNombre],
    d.[TurnoId],
    t.[Nombre] AS [TurnoNombre],
    d.[Fecha],
    COUNT(*) AS [TotalDetenciones],
    SUM(d.[MinutosEfectivos]) AS [MinutosTotales],
    SUM(CASE WHEN d.[EstadoActual] != 'CERRADA' THEN 1 ELSE 0 END) AS [DetencionesAbiertas]
FROM [mnt].[Detenciones] d
INNER JOIN [mnt].[Lineas] l ON d.[LineaId] = l.[LineaId]
INNER JOIN [mnt].[Turnos] t ON d.[TurnoId] = t.[TurnoId]
WHERE d.[Fecha] = CAST(GETDATE() AS DATE)
GROUP BY l.[LineaId], l.[Nombre], d.[TurnoId], t.[Nombre], d.[Fecha]
GO

PRINT 'Vista VW_ResumenTurnoActual creada exitosamente.'
GO

PRINT 'Todas las vistas creadas exitosamente.'
GO
