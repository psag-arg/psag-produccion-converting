-- ==============================================================================
-- Script: 45_horas_programadas_maestros.sql
-- Descripción: Pedido de Emiliano (24/11) · "Horas programadas" pasa del menú DASHBOARD al menú MAESTROS. Sólo navegación
--   (CFG_Opciones): no crea tablas ni toca permisos.
--     DASHBOARD.HORAS_PROGRAMADAS  padre MAESTROS, orden 10, ruta maestros/horas-programadas (después de
--                                  "Parámetros línea + producto", 9)
--     MAESTROS.CAMARAS             orden 10 → 11
--     MAESTROS.CALIDAD             orden 11 → 12
--   El CÓDIGO de la opción queda DASHBOARD.HORAS_PROGRAMADAS por compatibilidad: lo usan los [Permiso] de la API y las filas
--   de CFG_PermisosRol (los permisos ya asignados a ADMINISTRADOR y SUPERVISOR, o editados desde el configurador, no se
--   pierden). La página sigue respondiendo en configuracion/horas-programadas (alias en la Web).
--   Mismos datos que CatalogoPermisos.cs y la semilla de la API (lo verifica un test contra SQL Server).
-- Versión: 1.0
-- Requiere: 39 y 43 (va después del 43 y del 44). Si se vuelve a correr el 43 (que deja Horas programadas en DASHBOARD),
--   volver a correr el 45 (la API de este paquete igual lo corrige al arrancar con su semilla).
-- Idempotente (sólo actualiza si cambia algo). Se puede correr con la API andando.
-- Correr con: sqlcmd -C -I -b -f 65001.
-- Deshacer: 45_horas_programadas_maestros_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NULL
   OR (SELECT COUNT(*) FROM [mnt].[CFG_Opciones]
       WHERE [Codigo] IN (N'MAESTROS', N'DASHBOARD.HORAS_PROGRAMADAS', N'MAESTROS.CAMARAS', N'MAESTROS.CALIDAD')) <> 4
BEGIN
    RAISERROR(N'Faltan opciones del catálogo de permisos: correr antes el 39 y el 43.', 16, 1);
    SET NOEXEC ON;
END
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Maestros INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'MAESTROS');

DECLARE @Menu TABLE (Codigo NVARCHAR(100) PRIMARY KEY, Ruta NVARCHAR(200), Orden INT);
INSERT INTO @Menu (Codigo, Ruta, Orden) VALUES
    (N'DASHBOARD.HORAS_PROGRAMADAS', N'maestros/horas-programadas', 10),
    (N'MAESTROS.CAMARAS', N'maestros/camaras', 11),
    (N'MAESTROS.CALIDAD', N'maestros/calidad', 12);

UPDATE x SET x.[PadreId] = @Maestros, x.[Ruta] = m.Ruta, x.[Orden] = m.Orden
FROM [mnt].[CFG_Opciones] x
JOIN @Menu m ON m.Codigo = x.[Codigo]
WHERE ISNULL(x.[PadreId], 0) <> @Maestros OR ISNULL(x.[Ruta], N'?') <> m.Ruta OR x.[Orden] <> m.Orden;
PRINT CONCAT('Opciones del menú actualizadas: ', @@ROWCOUNT, '.');

COMMIT TRANSACTION;
GO

SELECT p.[Codigo] AS Seccion, o.[Orden], o.[Codigo], o.[Nombre], o.[Ruta]
FROM [mnt].[CFG_Opciones] o
JOIN [mnt].[CFG_Opciones] p ON p.[OpcionId] = o.[PadreId]
WHERE p.[Codigo] IN (N'DASHBOARD', N'MAESTROS')
ORDER BY p.[Codigo], o.[Orden];
GO

SET NOEXEC OFF;
GO
