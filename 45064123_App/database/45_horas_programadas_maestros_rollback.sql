-- ==============================================================================
-- Script: 45_horas_programadas_maestros_rollback.sql
-- Descripción: Deshace 45_horas_programadas_maestros.sql: "Horas programadas" vuelve a DASHBOARD (orden 5, ruta
--   configuracion/horas-programadas, como la dejó el 43) y Cámaras / Calidad vuelven a los órdenes 10 / 11 de MAESTROS.
--   No toca permisos. Antes, volver a publicar la API anterior (la API de este paquete vuelve a aplicar el menú al arrancar).
-- Idempotente.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NOT NULL
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Dashboard INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'DASHBOARD');
    DECLARE @Maestros INT = (SELECT [OpcionId] FROM [mnt].[CFG_Opciones] WHERE [Codigo] = N'MAESTROS');

    DECLARE @Menu TABLE (Codigo NVARCHAR(100) PRIMARY KEY, PadreId INT, Ruta NVARCHAR(200), Orden INT);
    INSERT INTO @Menu (Codigo, PadreId, Ruta, Orden) VALUES
        (N'DASHBOARD.HORAS_PROGRAMADAS', @Dashboard, N'configuracion/horas-programadas', 5),
        (N'MAESTROS.CAMARAS', @Maestros, N'maestros/camaras', 10),
        (N'MAESTROS.CALIDAD', @Maestros, N'maestros/calidad', 11);

    UPDATE x SET x.[PadreId] = m.PadreId, x.[Ruta] = m.Ruta, x.[Orden] = m.Orden
    FROM [mnt].[CFG_Opciones] x
    JOIN @Menu m ON m.Codigo = x.[Codigo]
    WHERE ISNULL(x.[PadreId], 0) <> m.PadreId OR ISNULL(x.[Ruta], N'?') <> m.Ruta OR x.[Orden] <> m.Orden;
    PRINT CONCAT('Opciones del menú vueltas al 43: ', @@ROWCOUNT, '.');

    COMMIT TRANSACTION;
END
GO
