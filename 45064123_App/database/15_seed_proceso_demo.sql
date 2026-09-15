-- ==============================================================================
-- Script: 15_seed_proceso_demo.sql
-- Descripción: Datos de demo para Variables de proceso (del PPT - Futura II)
-- Versión: 1.0
-- NOTA: Solo para desarrollo. Los datos reales los completa el cliente.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

DECLARE @SystemUser NVARCHAR(450) = 'system'

-- ============================================================================
-- Producto de demo: Higiénico 30 mts
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [mnt].[Productos] WHERE [Codigo] = 'HIG-30M')
BEGIN
    INSERT INTO [mnt].[Productos] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES ('HIG-30M', 'Higiénico 30 mts', 'Papel higiénico 30 metros', 1, 1, @SystemUser, @SystemUser)
    PRINT 'Producto Higiénico 30 mts creado.'
END

-- Asociar producto a Línea 1 (Futura II en demo)
IF EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [LineaId] = 1)
AND EXISTS (SELECT 1 FROM [mnt].[Productos] WHERE [Codigo] = 'HIG-30M')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[LineasProductos] WHERE [LineaId] = 1 AND [ProductoId] = (SELECT [ProductoId] FROM [mnt].[Productos] WHERE [Codigo] = 'HIG-30M'))
    BEGIN
        INSERT INTO [mnt].[LineasProductos] ([LineaId], [ProductoId])
        SELECT 1, [ProductoId] FROM [mnt].[Productos] WHERE [Codigo] = 'HIG-30M'
        PRINT 'Producto asociado a Línea 1.'
    END
END

-- ============================================================================
-- Grupos de parámetros (pestañas) - datos del PPT Futura II
-- ============================================================================

-- Grupo 1: PARÁMETROS - Producto y Máquina
IF NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] WHERE [Codigo] = 'PROD-MAQ')
BEGIN
    INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, 'PROD-MAQ', 'PARÁMETROS - Producto y Máquina', 'Datos del producto y máquina', 1, 1, @SystemUser, @SystemUser)
    PRINT 'Grupo PARÁMETROS - Producto y Máquina creado.'
END

-- Grupo 2: CALIDAD DE MANGA
IF NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] WHERE [Codigo] = 'CAL-MANGA')
BEGIN
    INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, 'CAL-MANGA', 'CALIDAD DE MANGA', 'Parámetros de calidad de manga', 2, 1, @SystemUser, @SystemUser)
    PRINT 'Grupo CALIDAD DE MANGA creado.'
END

-- Grupo 3: PARÁMETROS - Grupo Gofrador
IF NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] WHERE [Codigo] = 'GOFRADOR')
BEGIN
    INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, 'GOFRADOR', 'PARÁMETROS - Grupo Gofrador', 'Parámetros del gofrador', 3, 1, @SystemUser, @SystemUser)
    PRINT 'Grupo PARÁMETROS - Grupo Gofrador creado.'
END

-- Grupo 4: PARÁMETROS - Rebobinadora
IF NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] WHERE [Codigo] = 'REBOB')
BEGIN
    INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, 'REBOB', 'PARÁMETROS - Rebobinadora', 'Parámetros de la rebobinadora', 4, 1, @SystemUser, @SystemUser)
    PRINT 'Grupo PARÁMETROS - Rebobinadora creado.'
END

-- Grupo 5: PARÁMETROS - Presiones Neumáticas
IF NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] WHERE [Codigo] = 'PRESIONES')
BEGIN
    INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (NULL, 'PRESIONES', 'PARÁMETROS - Presiones Neumáticas', 'Presiones neumáticas de seguridad', 5, 1, @SystemUser, @SystemUser)
    PRINT 'Grupo PARÁMETROS - Presiones Neumáticas creado.'
END
GO

-- ============================================================================
-- Parámetros - Datos del PPT
-- ============================================================================
DECLARE @SystemUser NVARCHAR(450) = 'system'
DECLARE @GrupoId INT

-- GRUPO: PARÁMETROS - Producto y Máquina
SELECT @GrupoId = [GrupoParametroId] FROM [mnt].[GruposParametro] WHERE [Codigo] = 'PROD-MAQ'
IF @GrupoId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'NUM-ROTURAS')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'DATOS PRODUCTO', 'NUM-ROTURAS', 'Número de roturas (precortes)', NULL, 'NUMERICO', 1, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'LARGO-PERF')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'DATOS PRODUCTO', 'LARGO-PERF', 'Largo de la Perforación', 'mm', 'NUMERICO', 2, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'DIAM-LOG')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'DATOS PRODUCTO', 'DIAM-LOG', 'Diámetro de Log', 'mm', 'NUMERICO', 3, 1, @SystemUser, @SystemUser)
END

-- GRUPO: CALIDAD DE MANGA
SELECT @GrupoId = [GrupoParametroId] FROM [mnt].[GruposParametro] WHERE [Codigo] = 'CAL-MANGA'
IF @GrupoId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-LS')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'DIÁMETRO DE MANGA', 'MANGA-LS', 'Lado Servicio', 'mm', 'NUMERICO', 1, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-C')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'DIÁMETRO DE MANGA', 'MANGA-C', 'Centro', 'mm', 'NUMERICO', 2, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-LO')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'DIÁMETRO DE MANGA', 'MANGA-LO', 'Lado Operador', 'mm', 'NUMERICO', 3, 1, @SystemUser, @SystemUser)
END

-- GRUPO: Gofrador
SELECT @GrupoId = [GrupoParametroId] FROM [mnt].[GruposParametro] WHERE [Codigo] = 'GOFRADOR'
IF @GrupoId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'PRES-GOF-SUP')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'Presión de Gofrado', 'PRES-GOF-SUP', 'Rodillo Superior (Presión Trabajo)', 'bar', 'RANGO', 1, 1, @SystemUser, @SystemUser)
END

-- GRUPO: Rebobinadora
SELECT @GrupoId = [GrupoParametroId] FROM [mnt].[GruposParametro] WHERE [Codigo] = 'REBOB'
IF @GrupoId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'PREC-LT')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'PRECORTE', 'PREC-LT', 'Lado Transmisión', NULL, 'RANGO', 1, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'PREC-LS')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'PRECORTE', 'PREC-LS', 'Lado Servicio', NULL, 'RANGO', 2, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'PRES-SOPLO')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'PRESIÓN DE SOPLO', 'PRES-SOPLO', 'Presión de soplo', 'bar', 'NUMERICO', 3, 1, @SystemUser, @SystemUser)
END

-- GRUPO: Presiones Neumáticas
SELECT @GrupoId = [GrupoParametroId] FROM [mnt].[GruposParametro] WHERE [Codigo] = 'PRESIONES'
IF @GrupoId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'CIL-PRENSA')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'PRESIONES', 'CIL-PRENSA', 'Cilindro Prensa Diatrol', 'bar', 'NUMERICO', 1, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'ZAFE-CARGA')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'PRESIONES', 'ZAFE-CARGA', 'Zafe Acumulador-Carga', 'bar', 'NUMERICO', 2, 1, @SystemUser, @SystemUser)

    IF NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] WHERE [Codigo] = 'ZAFE-DESC')
        INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato], [Orden], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        VALUES (@GrupoId, 'PRESIONES', 'ZAFE-DESC', 'Zafe Acumulador-Descarga', 'bar', 'NUMERICO', 3, 1, @SystemUser, @SystemUser)
END

PRINT 'Parámetros de demo creados.'
GO

-- ============================================================================
-- Estándares para Línea 1 + Higiénico 30 mts (datos del PPT)
-- ============================================================================
DECLARE @SystemUser NVARCHAR(450) = 'system'
DECLARE @LineaId INT = 1
DECLARE @ProductoId INT

SELECT @ProductoId = [ProductoId] FROM [mnt].[Productos] WHERE [Codigo] = 'HIG-30M'

IF @ProductoId IS NOT NULL AND EXISTS (SELECT 1 FROM [mnt].[Lineas] WHERE [LineaId] = @LineaId)
BEGIN
    -- NUM-ROTURAS: STD = 152
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'NUM-ROTURAS'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '152', 152, NULL, 0, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'NUM-ROTURAS'

    -- LARGO-PERF: STD = 202
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'LARGO-PERF'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '202', 202, NULL, 0, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'LARGO-PERF'

    -- DIAM-LOG: STD = 98.5
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'DIAM-LOG'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '98,5', 98.5, NULL, 0, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'DIAM-LOG'

    -- MANGA-LS, MANGA-C, MANGA-LO: STD = 100
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-LS'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '100', 100, NULL, 0, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-LS'

    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-C'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '100', 100, NULL, 0, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-C'

    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-LO'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '100', 100, NULL, 0, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'MANGA-LO'

    -- PRES-GOF-SUP: STD = 3.8 - 4.1 (bar) - RANGO
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'PRES-GOF-SUP'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '3,8 - 4,1 (bar)', 3.8, 4.1, NULL, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'PRES-GOF-SUP'

    -- PREC-LT, PREC-LS: STD = 21-22 - RANGO
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'PREC-LT'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '21-22', 21, 22, NULL, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'PREC-LT'

    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'PREC-LS'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '21-22', 21, 22, NULL, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'PREC-LS'

    -- PRES-SOPLO: STD = 4.9
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'PRES-SOPLO'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '4,9', 4.9, NULL, 0.1, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'PRES-SOPLO'

    -- Presiones neumáticas
    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'CIL-PRENSA'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '2', 2, NULL, 0.1, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'CIL-PRENSA'

    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'ZAFE-CARGA'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '0,5', 0.5, NULL, 0.05, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'ZAFE-CARGA'

    IF NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId AND [ParametroId] = (SELECT [ParametroId] FROM [mnt].[Parametros] WHERE [Codigo] = 'ZAFE-DESC'))
        INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto], [ValorMin], [ValorMax], [Tolerancia], [Activo], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
        SELECT @LineaId, @ProductoId, [ParametroId], '1', 1, NULL, 0.1, 1, @SystemUser, @SystemUser
        FROM [mnt].[Parametros] WHERE [Codigo] = 'ZAFE-DESC'

    PRINT 'Estándares de demo creados para Línea 1 + Higiénico 30 mts.'
END
GO

PRINT 'Seed de Variables de proceso completado.'
GO
