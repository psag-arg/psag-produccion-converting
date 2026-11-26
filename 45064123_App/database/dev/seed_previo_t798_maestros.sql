-- ==============================================================================
-- Script: database/dev/seed_previo_t798_maestros.sql
-- SOLO DESARROLLO / TEST. NO va en el paquete de producción. Datos inventados.
-- Reproduce lo que en producción se cargó desde la app (Maestros / Configuración)
-- después de 07 y 08 y antes de 18: nombres reales de las líneas y horarios de
-- turnos de planta (T1 05-13, T2 13-21 y el T3 cargado 21-06, como en producción).
-- Va DESPUÉS de 08 y ANTES de 18 (18 y 24 buscan la línea "Tango 2" por nombre).
-- Idempotente.
-- ==============================================================================
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE N'%[_]DEV' AND DB_NAME() NOT LIKE N'%[_]TEST' AND DB_NAME() NOT LIKE N'%[_]PRUEBA'
BEGIN
    RAISERROR(N'seed DEV: la base actual no es de DEV/TEST/PRUEBA. No se carga nada.', 16, 1);
    SET NOEXEC ON;
END
GO

UPDATE [mnt].[Lineas] SET [Nombre] = N'TANGO 2', [Descripcion] = N'Línea Futura Tango 2 (higiénico)' WHERE [Codigo] = N'L01' AND [Nombre] <> N'TANGO 2';
UPDATE [mnt].[Lineas] SET [Nombre] = N'TANGO 1', [Descripcion] = N'Línea Tango 1' WHERE [Codigo] = N'L02' AND [Nombre] <> N'TANGO 1';
UPDATE [mnt].[Lineas] SET [Nombre] = N'FUTURA 2', [Descripcion] = N'Línea Futura 2' WHERE [Codigo] = N'L03' AND [Nombre] <> N'FUTURA 2';
-- Las demás líneas del 07 quedan inactivas (en planta hay 3 líneas de converting en uso)
UPDATE [mnt].[Lineas] SET [Activo] = 0 WHERE [Codigo] IN (N'L04', N'L05', N'L06') AND [Activo] = 1;

-- Turnos como están en producción (T3 21-06: el día suma 25 h, T-541)
UPDATE [mnt].[Turnos] SET [HoraInicio] = '05:00', [HoraFin] = '13:00', [ModificadoPorUsuarioId] = N'SEED-DEV' WHERE [Codigo] = N'T1';
UPDATE [mnt].[Turnos] SET [HoraInicio] = '13:00', [HoraFin] = '21:00', [ModificadoPorUsuarioId] = N'SEED-DEV' WHERE [Codigo] = N'T2';
UPDATE [mnt].[Turnos] SET [HoraInicio] = '21:00', [HoraFin] = '06:00', [ModificadoPorUsuarioId] = N'SEED-DEV' WHERE [Codigo] = N'T3';

SELECT [Codigo], [Nombre], [Activo] FROM [mnt].[Lineas] ORDER BY [Orden];
SELECT [Codigo], CONVERT(CHAR(5), [HoraInicio], 108) AS Inicio, CONVERT(CHAR(5), [HoraFin], 108) AS Fin FROM [mnt].[Turnos] ORDER BY [Orden];
GO
SET NOEXEC OFF;
GO
