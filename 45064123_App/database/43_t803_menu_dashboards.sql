-- ==============================================================================
-- Script: 43_t803_menu_dashboards.sql
-- Descripción: T-803 · Reorganización de dashboards y menú. Sólo navegación (catálogo de opciones del menú) y un
--   permiso; no crea tablas ni toca datos de negocio.
--   La sección DASHBOARD queda SÓLO con:
--     1 DASHBOARD.REPORTES                "Dashboard Admin"                    dashboard/reportes
--     2 PRODUCCION.PRESUPUESTO_REAL       "Presupuesto vs. real"               dashboard/produccion          (venía de PRODUCCION)
--     3 PRODUCCION.TABLERO                "Tablero del turno"                  produccion/tablero            (venía de PRODUCCION)
--     4 PRODUCCION.TABLERO_GERENCIAL      "Tablero de producción (gerencial)"  produccion/tablero/gerencial  (venía de PRODUCCION, sin ruta)
--     5 DASHBOARD.HORAS_PROGRAMADAS       "Horas programadas"                  configuracion/horas-programadas
--   y DASHBOARD.INICIO (el viejo "Dashboard de Control", / y /dashboard) pasa a "Inicio (sin menú: lecturas del dashboard)",
--   SIN ruta y orden 9: no aparece en el menú (la raíz / de la Web redirige según el usuario); conserva sus permisos
--   porque sigue habilitando las lecturas de api/dashboard.
--   Los códigos no cambian (los usan los [Permiso] de la API y CFG_PermisosRol): cambia el padre, el nombre, la ruta y el orden.
--   Permiso: "Dashboard Admin" deja de ser del OPERARIO (VER = 0), SÓLO si esa fila no se editó desde el configurador
--   (ModificadoPorUsuarioId = SCRIPT-39-T802 o SYSTEM-SEED); la fila queda marcada SCRIPT-43-T803 (la busca el rollback).
--   Mismos datos que CatalogoPermisos.cs (Todas y Quitados) y la semilla de la API (lo verifica un test contra SQL Server).
-- Versión: 1.0
-- Requiere: 39, 41 y 42 (va después del 42).
-- Idempotente: las opciones se actualizan por código (sólo nombre, padre, ruta y orden de las 6 que cambian; no toca
--   AccionesAplicables ni Activo); el permiso del operario sólo se cambia si sigue como lo dejó la carga. No pisa lo
--   editado desde Configuración → Roles y permisos. Si se vuelve a correr el 39, 41 o 42, volver a correr el 43
--   (la API de T-803 igual lo corrige al arrancar con su semilla).
-- Correr con: sqlcmd -C -I -b -f 65001. Se puede correr con la API andando (la API vieja muestra el menú nuevo).
-- Deshacer: 43_t803_menu_dashboards_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NULL OR OBJECT_ID(N'mnt.CFG_PermisosRol', N'U') IS NULL
BEGIN
    RAISERROR(N'Faltan las tablas de roles y permisos (script 39).', 16, 1);
    SET NOEXEC ON;
END
GO

IF (SELECT COUNT(*) FROM [mnt].[CFG_Opciones] WHERE [Codigo] IN (N'DASHBOARD', N'DASHBOARD.INICIO', N'DASHBOARD.REPORTES',
        N'DASHBOARD.HORAS_PROGRAMADAS', N'PRODUCCION.PRESUPUESTO_REAL', N'PRODUCCION.TABLERO', N'PRODUCCION.TABLERO_GERENCIAL')) <> 7
BEGIN
    RAISERROR(N'Faltan opciones del catálogo: correr antes el 39, el 41 y el 42.', 16, 1);
    SET NOEXEC ON;
END
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Ahora DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-43-T803';
DECLARE @Dashboard INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'DASHBOARD');

-- 1) Menú: nombre, padre, ruta y orden de las opciones que cambian
DECLARE @Menu TABLE (Codigo NVARCHAR(100) PRIMARY KEY, Nombre NVARCHAR(200), Ruta NVARCHAR(200) NULL, Orden INT);
INSERT INTO @Menu (Codigo, Nombre, Ruta, Orden) VALUES
    (N'DASHBOARD.REPORTES', N'Dashboard Admin', N'dashboard/reportes', 1),
    (N'PRODUCCION.PRESUPUESTO_REAL', N'Presupuesto vs. real', N'dashboard/produccion', 2),
    (N'PRODUCCION.TABLERO', N'Tablero del turno', N'produccion/tablero', 3),
    (N'PRODUCCION.TABLERO_GERENCIAL', N'Tablero de producción (gerencial)', N'produccion/tablero/gerencial', 4),
    (N'DASHBOARD.HORAS_PROGRAMADAS', N'Horas programadas', N'configuracion/horas-programadas', 5),
    (N'DASHBOARD.INICIO', N'Inicio (sin menú: lecturas del dashboard)', NULL, 9);

UPDATE x SET x.[Nombre] = m.Nombre, x.[PadreId] = @Dashboard, x.[Ruta] = m.Ruta, x.[Orden] = m.Orden
FROM [mnt].[CFG_Opciones] x
JOIN @Menu m ON m.Codigo = x.[Codigo]
WHERE x.[Nombre] <> m.Nombre OR ISNULL(x.[PadreId], 0) <> @Dashboard OR ISNULL(x.[Ruta], N'?') <> ISNULL(m.Ruta, N'?') OR x.[Orden] <> m.Orden;
PRINT CONCAT('Opciones del menú actualizadas: ', @@ROWCOUNT, '.');

-- 2) Dashboard Admin no es del operario (sólo si la fila sigue como la dejó la carga del 39 o la semilla)
UPDATE p SET p.[PuedeVer] = 0, p.[PuedeAlta] = 0, p.[PuedeModificar] = 0, p.[PuedeBaja] = 0,
    p.[FechaHoraModificacion] = @Ahora, p.[ModificadoPorUsuarioId] = @Usuario
FROM [mnt].[CFG_PermisosRol] p
JOIN [mnt].[CFG_Opciones] o ON o.[OpcionId] = p.[OpcionId]
JOIN [dbo].[AspNetRoles] r ON r.[Id] = p.[RoleId]
WHERE o.[Codigo] = N'DASHBOARD.REPORTES' AND r.[NormalizedName] = N'OPERARIO' AND p.[PuedeVer] = 1
  AND p.[ModificadoPorUsuarioId] IN (N'SCRIPT-39-T802', N'SYSTEM-SEED');
PRINT CONCAT('Permiso Dashboard Admin quitado al OPERARIO: ', @@ROWCOUNT, ' fila(s).');

COMMIT TRANSACTION;
GO

SELECT p.[Codigo] AS Seccion, o.[Orden], o.[Codigo], o.[Nombre], o.[Ruta]
FROM [mnt].[CFG_Opciones] o
JOIN [mnt].[CFG_Opciones] p ON p.[OpcionId] = o.[PadreId]
WHERE p.[Codigo] = N'DASHBOARD'
ORDER BY o.[Orden];
GO
