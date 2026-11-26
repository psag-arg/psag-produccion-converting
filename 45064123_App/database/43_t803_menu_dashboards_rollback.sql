-- ==============================================================================
-- Script: 43_t803_menu_dashboards_rollback.sql
-- Descripción: Deshace 43_t803_menu_dashboards.sql (T-803): vuelve las 6 opciones a como las dejaron el 39, el 41 y el 42
--   (Inicio con ruta '' en DASHBOARD; Reportes; Presupuesto vs. real y los dos tableros en PRODUCCION, el gerencial sin
--   ruta) y le devuelve VER de "Reportes" al OPERARIO en la fila que marcó el 43 (ModificadoPorUsuarioId = SCRIPT-43-T803,
--   si nadie la editó después). No borra filas.
--   Antes de correrlo, volver a publicar la API anterior (la de T-803 vuelve a aplicar el menú nuevo con su semilla al arrancar).
-- Idempotente: se puede ejecutar más de una vez.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NOT NULL AND OBJECT_ID(N'mnt.CFG_PermisosRol', N'U') IS NOT NULL
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Dashboard INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'DASHBOARD');
    DECLARE @Produccion INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'PRODUCCION');

    DECLARE @Menu TABLE (Codigo NVARCHAR(100) PRIMARY KEY, Nombre NVARCHAR(200), PadreId INT, Ruta NVARCHAR(200) NULL, Orden INT);
    INSERT INTO @Menu (Codigo, Nombre, PadreId, Ruta, Orden) VALUES
        (N'DASHBOARD.INICIO', N'Inicio', @Dashboard, N'', 1),
        (N'DASHBOARD.REPORTES', N'Reportes', @Dashboard, N'dashboard/reportes', 2),
        (N'DASHBOARD.HORAS_PROGRAMADAS', N'Horas programadas', @Dashboard, N'configuracion/horas-programadas', 3),
        (N'PRODUCCION.PRESUPUESTO_REAL', N'Presupuesto vs. real', @Produccion, N'dashboard/produccion', 4),
        (N'PRODUCCION.TABLERO', N'Tablero del turno (operador)', @Produccion, N'produccion/tablero', 5),
        (N'PRODUCCION.TABLERO_GERENCIAL', N'Tablero gerencial (cualquier línea y turno)', @Produccion, NULL, 6);

    UPDATE x SET x.[Nombre] = m.Nombre, x.[PadreId] = m.PadreId, x.[Ruta] = m.Ruta, x.[Orden] = m.Orden
    FROM [mnt].[CFG_Opciones] x
    JOIN @Menu m ON m.Codigo = x.[Codigo];
    PRINT CONCAT('Opciones del menú vueltas a 39/41/42: ', @@ROWCOUNT, '.');

    UPDATE p SET p.[PuedeVer] = 1, p.[FechaHoraModificacion] = SYSDATETIMEOFFSET(), p.[ModificadoPorUsuarioId] = N'SCRIPT-39-T802'
    FROM [mnt].[CFG_PermisosRol] p
    JOIN [mnt].[CFG_Opciones] o ON o.[OpcionId] = p.[OpcionId]
    JOIN [dbo].[AspNetRoles] r ON r.[Id] = p.[RoleId]
    WHERE o.[Codigo] = N'DASHBOARD.REPORTES' AND r.[NormalizedName] = N'OPERARIO' AND p.[ModificadoPorUsuarioId] = N'SCRIPT-43-T803';
    PRINT CONCAT('Permiso Reportes devuelto al OPERARIO: ', @@ROWCOUNT, ' fila(s).');

    COMMIT TRANSACTION;
END
ELSE
    PRINT 'No hay tablas de roles y permisos (script 39): nada que deshacer.'
GO
