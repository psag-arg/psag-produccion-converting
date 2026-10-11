-- ==============================================================================
-- Script: 07_seed_catalogs.sql
-- Descripcion: Pobla los catalogos iniciales del sistema
-- Version: 2.0 - Fase 1 (Detenciones con modelo de tiempo)
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

DECLARE @AdminUserId NVARCHAR(450) = 'SYSTEM-SEED'

-- ============================================================================
-- LINEAS DE PRODUCCION (Converting - 6 lineas)
-- ============================================================================
PRINT 'Insertando lineas de produccion...'

IF NOT EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [Codigo] = N'L01')
BEGIN
    INSERT INTO [mnt].[Lineas] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'L01', N'Linea 1', N'Linea de produccion Converting 1', 1, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Linea 1 insertada'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [Codigo] = N'L02')
BEGIN
    INSERT INTO [mnt].[Lineas] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'L02', N'Linea 2', N'Linea de produccion Converting 2', 2, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Linea 2 insertada'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [Codigo] = N'L03')
BEGIN
    INSERT INTO [mnt].[Lineas] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'L03', N'Linea 3', N'Linea de produccion Converting 3', 3, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Linea 3 insertada'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [Codigo] = N'L04')
BEGIN
    INSERT INTO [mnt].[Lineas] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'L04', N'Linea 4', N'Linea de produccion Converting 4', 4, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Linea 4 insertada'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [Codigo] = N'L05')
BEGIN
    INSERT INTO [mnt].[Lineas] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'L05', N'Linea 5', N'Linea de produccion Converting 5', 5, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Linea 5 insertada'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [Codigo] = N'L06')
BEGIN
    INSERT INTO [mnt].[Lineas] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'L06', N'Linea 6', N'Linea de produccion Converting 6', 6, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Linea 6 insertada'
END
GO

-- ============================================================================
-- TURNOS DE TRABAJO
-- ============================================================================
PRINT 'Insertando turnos de trabajo...'

DECLARE @AdminUserId NVARCHAR(450) = 'SYSTEM-SEED'

IF NOT EXISTS (SELECT 1 FROM [mnt].[Turnos] WHERE [Codigo] = N'T1')
BEGIN
    INSERT INTO [mnt].[Turnos] ([Codigo], [Nombre], [HoraInicio], [HoraFin], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'T1', N'Turno 1 (Mañana)', '06:00:00', '14:00:00', 1, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Turno 1 insertado'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Turnos] WHERE [Codigo] = N'T2')
BEGIN
    INSERT INTO [mnt].[Turnos] ([Codigo], [Nombre], [HoraInicio], [HoraFin], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'T2', N'Turno 2 (Tarde)', '14:00:00', '22:00:00', 2, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Turno 2 insertado'
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[Turnos] WHERE [Codigo] = N'T3')
BEGIN
    INSERT INTO [mnt].[Turnos] ([Codigo], [Nombre], [HoraInicio], [HoraFin], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'T3', N'Turno 3 (Noche)', '22:00:00', '06:00:00', 3, 1, @AdminUserId, @AdminUserId)
    PRINT '  - Turno 3 insertado'
END

PRINT 'Turnos insertados.'
GO

-- ============================================================================
-- TIPOS DE FALLA (antes TiposEvento)
-- ============================================================================
PRINT 'Insertando tipos de falla...'

DECLARE @AdminUserId NVARCHAR(450) = 'SYSTEM-SEED'

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-MEC')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-MEC', N'Falla Mecanica', N'Fallas relacionadas con componentes mecanicos', 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-ELE')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-ELE', N'Falla Electrica', N'Fallas relacionadas con el sistema electrico', 2, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-CTL')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-CTL', N'Falla de Control', N'Fallas en sistemas de control y automatizacion', 3, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-CPR')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-CPR', N'Cambio de Producto', N'Parada programada por cambio de producto', 4, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-MPR')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-MPR', N'Mantenimiento Preventivo', N'Parada programada por mantenimiento preventivo', 5, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-MAT')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-MAT', N'Falta de Material', N'Parada por falta de materia prima o insumos', 6, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-CAL')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-CAL', N'Problema de Calidad', N'Parada por problemas de calidad del producto', 7, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-AJU')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-AJU', N'Ajuste de Maquina', N'Parada por ajustes menores de la maquina', 8, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[TiposFalla] WHERE [Codigo] = N'TF-OTR')
BEGIN
    INSERT INTO [mnt].[TiposFalla] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (N'TF-OTR', N'Otro', N'Otros tipos de parada no clasificados', 99, 1, @AdminUserId, @AdminUserId)
END

PRINT 'Tipos de falla insertados.'
GO

-- ============================================================================
-- DETALLES DE FALLA (antes Causas) - Genericos (sin EquipoId ni TipoFallaId)
-- ============================================================================
PRINT 'Insertando detalles de falla genericos...'

DECLARE @AdminUserId NVARCHAR(450) = 'SYSTEM-SEED'

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-DES')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-DES', N'Desgaste de componente', N'Desgaste natural de piezas o componentes', 1, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-ROT')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-ROT', N'Rotura de pieza', N'Rotura o fractura de componentes', 2, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-LUB')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-LUB', N'Falta de lubricacion', N'Deficiencia en la lubricacion de componentes', 3, 0, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-COR')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-COR', N'Cortocircuito', N'Cortocircuito en el sistema electrico', 4, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-SOB')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-SOB', N'Sobrecalentamiento', N'Temperatura excesiva en componentes', 5, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-SEN')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-SEN', N'Error de sensor', N'Falla o desconfiguracion de sensores', 6, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-OPE')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-OPE', N'Error de operacion', N'Error humano en la operacion del equipo', 7, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-MPD')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-MPD', N'Materia prima defectuosa', N'Problemas con la calidad de la materia prima', 8, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-REP')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-REP', N'Falta de repuestos', N'No se dispone de repuestos necesarios', 9, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-DET')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-DET', N'Causa por determinar', N'La causa raiz aun no ha sido identificada', 98, 1, 1, @AdminUserId, @AdminUserId)
END

IF NOT EXISTS (SELECT 1 FROM [mnt].[DetallesFalla] WHERE [Codigo] = N'DF-OTR')
BEGIN
    INSERT INTO [mnt].[DetallesFalla] ([EquipoId], [TipoFallaId], [Codigo], [Nombre], [Descripcion], [Orden], [RequiereDescripcion], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, NULL, N'DF-OTR', N'Otro', N'Otros detalles no clasificados', 99, 1, 1, @AdminUserId, @AdminUserId)
END

PRINT 'Detalles de falla insertados.'
GO

-- ============================================================================
-- EQUIPOS POR LINEA (antes Maquinas)
-- ============================================================================
PRINT 'Insertando equipos de ejemplo...'

DECLARE @AdminUserId NVARCHAR(450) = 'SYSTEM-SEED'
DECLARE @LineaId INT

-- Equipos genericos por linea (se pueden personalizar despues)
DECLARE @LineaCursor CURSOR
SET @LineaCursor = CURSOR FOR
    SELECT [LineaId] FROM [mnt].[Lineas]

OPEN @LineaCursor
FETCH NEXT FROM @LineaCursor INTO @LineaId

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Desenrollador
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND [Codigo] = N'DES-' + CAST(@LineaId AS NVARCHAR(2)))
    BEGIN
        INSERT INTO [mnt].[Equipos] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@LineaId, N'DES-' + CAST(@LineaId AS NVARCHAR(2)), N'Desenrollador', N'Equipo desenrollador de bobina', 1, 1, @AdminUserId, @AdminUserId)
    END

    -- Impresora
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND [Codigo] = N'IMP-' + CAST(@LineaId AS NVARCHAR(2)))
    BEGIN
        INSERT INTO [mnt].[Equipos] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@LineaId, N'IMP-' + CAST(@LineaId AS NVARCHAR(2)), N'Impresora', N'Equipo de impresion', 2, 1, @AdminUserId, @AdminUserId)
    END

    -- Cortadora
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND [Codigo] = N'COR-' + CAST(@LineaId AS NVARCHAR(2)))
    BEGIN
        INSERT INTO [mnt].[Equipos] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@LineaId, N'COR-' + CAST(@LineaId AS NVARCHAR(2)), N'Cortadora', N'Equipo de corte', 3, 1, @AdminUserId, @AdminUserId)
    END

    -- Plegadora
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND [Codigo] = N'PLE-' + CAST(@LineaId AS NVARCHAR(2)))
    BEGIN
        INSERT INTO [mnt].[Equipos] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@LineaId, N'PLE-' + CAST(@LineaId AS NVARCHAR(2)), N'Plegadora', N'Equipo de plegado', 4, 1, @AdminUserId, @AdminUserId)
    END

    -- Empaquetadora
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Equipos] WHERE [LineaId] = @LineaId AND [Codigo] = N'EMP-' + CAST(@LineaId AS NVARCHAR(2)))
    BEGIN
        INSERT INTO [mnt].[Equipos] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@LineaId, N'EMP-' + CAST(@LineaId AS NVARCHAR(2)), N'Empaquetadora', N'Equipo de empaquetado', 5, 1, @AdminUserId, @AdminUserId)
    END

    FETCH NEXT FROM @LineaCursor INTO @LineaId
END

CLOSE @LineaCursor
DEALLOCATE @LineaCursor

PRINT 'Equipos de ejemplo insertados.'
GO

PRINT 'Todos los catalogos poblados exitosamente.'
GO
