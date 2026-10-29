-- ==============================================================================
-- Script: 24_t545_plantilla_control_proceso_tango2.sql
-- Descripción: T-545 · Plantilla de Control de Proceso de TANGO 2 cargada desde la
--              planilla del cliente "Control Proceso Tango 2 (30mts)" (el título
--              interno de la hoja dice "Futura II", pero es la de Tango 2).
-- Versión: 1.0 - T-545
-- Requiere: 11_create_tables_proceso.sql y 21_t545_control_de_proceso.sql
-- Idempotente: se puede ejecutar más de una vez. Identifica todo por código y
--              solo INSERTA lo que falta (no pisa lo que se haya editado después
--              desde Maestros > Control de Proceso). No borra nada.
--
-- Encoding: el archivo es UTF-8, pero los acentos de los DATOS van con NCHAR(n)
-- (NCHAR(193) = Á, NCHAR(225) = á, NCHAR(233) = é, NCHAR(237) = í, NCHAR(243) = ó,
-- NCHAR(211) = Ó, NCHAR(250) = ú), así que se graban bien aunque se corra sin -f.
-- Para que también se lean bien los comentarios, correrlo con:
--     sqlcmd -S <servidor> -E -I -b -f 65001 -i 24_t545_plantilla_control_proceso_tango2.sql
--
-- Qué carga (usuario de auditoría SCRIPT-24):
--   * Producto HIGIENICO 30 MTS ("Higiénico 30 mts") asociado a TANGO 2.
--   * 6 grupos (pestañas) en el orden de la hoja, códigos T2-G01..T2-G06.
--   * 57 puntos de control: 56 NUMERICO + 1 TEXTO (Observaciones del bloque
--     Tensión del Papel), todos Frecuencia POR_TURNO. Sección = subtítulo de la hoja.
--     Unidad solo donde la hoja la dice (Bar, mm). Sin mín/máx en la plantilla:
--     los límites van en el estándar del producto.
--   * 54 estándares (EstandaresParametro) para TANGO 2 + HIGIENICO 30 MTS, con
--     ValorEstandarTexto = el STD tal como figura en la hoja:
--       - rango "a - b"     -> ValorMin = a, ValorMax = b (inclusivo: a y b son OK).
--       - "> 10mm"          -> ValorMin = 10, sin máximo ni tolerancia. El validador
--                              compara con >= (10 exacto NO es desvío; 9,9 sí).
--       - valor único (202) -> solo ValorEstandarTexto, sin mín/máx/tolerancia: es una
--                              referencia que se muestra al cargar y NO marca desvío.
--       - "-"               -> sin fila de estándar (Velocidad de Máquina, Calidad del
--                              Papel): el punto se carga sin estándar.
--   Cómo lee el estándar la app: RangoControl.DesdeEstandar (Domain/Services/
--   PuntoControlValidador.cs). El "valor exacto" es ValorMin con Tolerancia = 0.
--
-- Los VALUES de @Grupos / @Puntos los lee también el test de integración
-- PlantillaTango2Tests (tests/PSAG.Mantenimiento.Api.IntegrationTests): si se
-- editan, mantener una fila por línea con el mismo formato.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-24';
DECLARE @Ahora DATETIMEOFFSET = SYSDATETIMEOFFSET();
DECLARE @LineaNombre NVARCHAR(100) = N'TANGO 2';
DECLARE @ProductoCodigo NVARCHAR(50) = N'HIGIENICO 30 MTS';
DECLARE @ProductoNombre NVARCHAR(100) = N'Higi' + NCHAR(233) + N'nico 30 mts';

-- La línea se busca por nombre (en producción tiene Codigo vacío). Tiene que haber una sola.
DECLARE @CantLineas INT = (SELECT COUNT(*) FROM [mnt].[Lineas] WHERE UPPER(LTRIM(RTRIM([Nombre]))) = @LineaNombre);
IF @CantLineas <> 1
BEGIN
    RAISERROR(N'Se esperaba exactamente una linea con Nombre = TANGO 2 en mnt.Lineas y hay %d. No se cargo nada.', 16, 1, @CantLineas);
    RETURN;
END

DECLARE @LineaId INT = (SELECT [LineaId] FROM [mnt].[Lineas] WHERE UPPER(LTRIM(RTRIM([Nombre]))) = @LineaNombre);
PRINT CONCAT(N'Linea TANGO 2: LineaId = ', @LineaId, N'.');

-- ----------------------------------------------------------------------------
-- Datos de la planilla
--   @Grupos: Codigo, Nombre (pestaña), Orden (orden de la hoja)
--   @Puntos: CodigoGrupo, Seccion (subtítulo), Codigo, Nombre, TipoDato, Unidad,
--            Orden (correlativo dentro del grupo: define también el orden de las
--            secciones en pantalla), Estandar (STD de la hoja; NULL = "-"),
--            Minimo, Maximo (límites del estándar; NULL = sin límite)
-- ----------------------------------------------------------------------------
DECLARE @Grupos TABLE (
    [Codigo] NVARCHAR(50) NOT NULL PRIMARY KEY,
    [Nombre] NVARCHAR(100) NOT NULL,
    [Orden] INT NOT NULL);

DECLARE @Puntos TABLE (
    [CodigoGrupo] NVARCHAR(50) NOT NULL,
    [Seccion] NVARCHAR(100) NULL,
    [Codigo] NVARCHAR(50) NOT NULL PRIMARY KEY,
    [Nombre] NVARCHAR(200) NOT NULL,
    [TipoDato] VARCHAR(20) NOT NULL,
    [Unidad] NVARCHAR(50) NULL,
    [Orden] INT NOT NULL,
    [Estandar] NVARCHAR(100) NULL,
    [Minimo] DECIMAL(18,4) NULL,
    [Maximo] DECIMAL(18,4) NULL);

    INSERT INTO @Grupos ([Codigo], [Nombre], [Orden]) VALUES
        (N'T2-G01', N'PAR' + NCHAR(193) + N'METROS - Producto y M' + NCHAR(225) + N'quina', 1),
        (N'T2-G02', N'CALIDAD DE MANGA', 2),
        (N'T2-G03', N'PAR' + NCHAR(193) + N'METROS - Grupo Gofrador', 3),
        (N'T2-G04', N'PAR' + NCHAR(193) + N'METROS - Tensi' + NCHAR(243) + N'n del Papel', 4),
        (N'T2-G05', N'PAR' + NCHAR(193) + N'METROS - Rebobinadora', 5),
        (N'T2-G06', N'PAR' + NCHAR(193) + N'METROS - Presiones Neum' + NCHAR(225) + N'ticas (Seguridades)', 6);

    INSERT INTO @Puntos ([CodigoGrupo], [Seccion], [Codigo], [Nombre], [TipoDato], [Unidad], [Orden], [Estandar], [Minimo], [Maximo]) VALUES
        (N'T2-G01', N'DATOS PRODUCTO', N'T2-ROTURAS', N'N' + NCHAR(250) + N'mero de roturas', N'NUMERICO', NULL, 10, N'152 - 153', 152, 153),
        (N'T2-G01', N'DATOS PRODUCTO', N'T2-LARGO-PERF', N'Largo de la Perforaci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 20, N'202', NULL, NULL),
        (N'T2-G01', N'DATOS PRODUCTO', N'T2-DIAM-LOG', N'Di' + NCHAR(225) + N'metro de Log', N'NUMERICO', NULL, 30, N'98,5 - 99,5', 98.5, 99.5),
        (N'T2-G01', N'DATOS M' + NCHAR(193) + N'QUINA', N'T2-VEL-MAQ', N'Velocidad de M' + NCHAR(225) + N'quina', N'NUMERICO', NULL, 40, NULL, NULL, NULL),
        (N'T2-G01', N'DATOS M' + NCHAR(193) + N'QUINA', N'T2-CALIDAD-PAPEL', N'Calidad del Papel', N'NUMERICO', NULL, 50, NULL, NULL, NULL),
        (N'T2-G01', N'DATOS M' + NCHAR(193) + N'QUINA', N'T2-DIAM-TAMPON', N'Di' + NCHAR(225) + N'metro tampon', N'NUMERICO', NULL, 60, N'43', NULL, NULL),

        (N'T2-G02', N'DI' + NCHAR(193) + N'METRO DE MANGA', N'T2-MANGA-LS', N'Lado Servicio', N'NUMERICO', NULL, 10, N'100', NULL, NULL),
        (N'T2-G02', N'DI' + NCHAR(193) + N'METRO DE MANGA', N'T2-MANGA-C', N'Centro', N'NUMERICO', NULL, 20, N'100', NULL, NULL),
        (N'T2-G02', N'DI' + NCHAR(193) + N'METRO DE MANGA', N'T2-MANGA-LO', N'Lado Operador', N'NUMERICO', NULL, 30, N'100', NULL, NULL),
        (N'T2-G02', N'CONTROL DE RABO', N'T2-RABO-LS', N'Lado Servicio', N'NUMERICO', N'mm', 40, N'> 10mm', 10, NULL),
        (N'T2-G02', N'CONTROL DE RABO', N'T2-RABO-C', N'Centro', N'NUMERICO', N'mm', 50, N'> 10mm', 10, NULL),
        (N'T2-G02', N'CONTROL DE RABO', N'T2-RABO-LO', N'Lado Operador', N'NUMERICO', N'mm', 60, N'> 10mm', 10, NULL),
        (N'T2-G02', N'ROLLO (Diferencia altura)', N'T2-ROLLO-PRIMERO', N'Altura "Primer" rollo', N'NUMERICO', N'mm', 70, N'100', NULL, NULL),
        (N'T2-G02', N'ROLLO (Diferencia altura)', N'T2-ROLLO-ULTIMO', N'Altura "' + NCHAR(250) + N'ltimo" rollo', N'NUMERICO', N'mm', 80, N'100', NULL, NULL),

        (N'T2-G03', N'Presi' + NCHAR(243) + N'n de Gofrado', N'T2-GOF-PRES-TRABAJO', N'Presi' + NCHAR(243) + N'n Trabajo', N'NUMERICO', N'Bar', 10, N'3,8 - 4,1', 3.8, 4.1),
        (N'T2-G03', N'Presi' + NCHAR(243) + N'n de Gofrado', N'T2-GOF-VAR-700', N'Variaci' + NCHAR(243) + N'n 700 mts', N'NUMERICO', NULL, 20, N'0,7-1', 0.7, 1),
        (N'T2-G03', N'Presi' + NCHAR(243) + N'n de Gofrado', N'T2-GOF-ALIVIO', N'Alivio', N'NUMERICO', NULL, 30, N'0,4', NULL, NULL),

        (N'T2-G04', N'Celda de Carga (L1)', N'T2-CC-L1-MARCHA', N'Marcha', N'NUMERICO', NULL, 10, N'1,6', NULL, NULL),
        (N'T2-G04', N'Celda de Carga (L1)', N'T2-CC-L1-JOG', N'Jog', N'NUMERICO', NULL, 20, N'0,5', NULL, NULL),
        (N'T2-G04', N'Celda de Carga (L1)', N'T2-CC-L1-PASAJE', N'Pasaje Papel', N'NUMERICO', NULL, 30, N'0,8', NULL, NULL),
        (N'T2-G04', N'Celda de Carga (L2)', N'T2-CC-L2-MARCHA', N'Marcha', N'NUMERICO', NULL, 40, N'1,6', NULL, NULL),
        (N'T2-G04', N'Celda de Carga (L2)', N'T2-CC-L2-JOG', N'Jog', N'NUMERICO', NULL, 50, N'0,5', NULL, NULL),
        (N'T2-G04', N'Celda de Carga (L2)', N'T2-CC-L2-PASAJE', N'Pasaje Papel', N'NUMERICO', NULL, 60, N'0,8', NULL, NULL),
        (N'T2-G04', N'C. Carga Rebob (L3)', N'T2-CC-L3-MARCHA', N'Marcha', N'NUMERICO', NULL, 70, N'1,15', NULL, NULL),
        (N'T2-G04', N'C. Carga Rebob (L3)', N'T2-CC-L3-JOG', N'Jog', N'NUMERICO', NULL, 80, N'0,5', NULL, NULL),
        (N'T2-G04', N'C. Carga Rebob (L3)', N'T2-CC-L3-PASAJE', N'Pasaje Papel', N'NUMERICO', NULL, 90, N'0,8', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 7 (Motor Rodillo Prensa)', N'T2-SR7-MARCHA', N'Marcha', N'NUMERICO', NULL, 100, N'-0,7', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 7 (Motor Rodillo Prensa)', N'T2-SR7-JOG', N'Jog', N'NUMERICO', NULL, 110, N'-0,3', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 7 (Motor Rodillo Prensa)', N'T2-SR7-PASAJE', N'Pasaje Papel', N'NUMERICO', NULL, 120, N'0', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 9 (Motor Rodillo Papel)', N'T2-SR9-MARCHA', N'Marcha', N'NUMERICO', NULL, 130, N'-0,5', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 9 (Motor Rodillo Papel)', N'T2-SR9-JOG', N'Jog', N'NUMERICO', NULL, 140, N'-0,4', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 9 (Motor Rodillo Papel)', N'T2-SR9-PASAJE', N'Pasaje Papel', N'NUMERICO', NULL, 150, N'-0,5', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 10 (Motor Rodillo Superior)', N'T2-SR10-MARCHA', N'Marcha', N'NUMERICO', NULL, 160, N'0,6', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 10 (Motor Rodillo Superior)', N'T2-SR10-JOG', N'Jog', N'NUMERICO', NULL, 170, N'0,3', NULL, NULL),
        (N'T2-G04', N'Slide Rodillo 10 (Motor Rodillo Superior)', N'T2-SR10-PASAJE', N'Pasaje Papel', N'NUMERICO', NULL, 180, N'0,2', NULL, NULL),
        (N'T2-G04', N'Observaciones', N'T2-TENSION-OBS', N'Observaciones', N'TEXTO', NULL, 190, NULL, NULL, NULL),

        (N'T2-G05', N'PRECORTE', N'T2-PRECORTE-LT', N'Lado Transmisi' + NCHAR(243) + N'n', N'NUMERICO', NULL, 10, N'21-22', 21, 22),
        (N'T2-G05', N'PRECORTE', N'T2-PRECORTE-LS', N'Lado Servicio', N'NUMERICO', NULL, 20, N'21-22', 21, 22),
        (N'T2-G05', N'PRESI' + NCHAR(211) + N'N DE SOPLO', N'T2-SOPLO', N'Presi' + NCHAR(243) + N'n de soplo', N'NUMERICO', NULL, 30, N'4,9', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO SUPERIOR (AIR-ROLL)', N'T2-AIRROLL-ANTICIPACION', N'Anticipaci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 40, N'5', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO SUPERIOR (AIR-ROLL)', N'T2-AIRROLL-COMPENSACION', N'Compensaci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 50, N'0', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO SUPERIOR (AIR-ROLL)', N'T2-AIRROLL-DURACION', N'Duraci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 60, N'40', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO INFERIOR (DESACELERACI' + NCHAR(211) + N'N)', N'T2-RINF-AV-DURACION', N'Alta velocidad - Duraci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 70, N'250', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO INFERIOR (DESACELERACI' + NCHAR(211) + N'N)', N'T2-RINF-AV-PORCENTAJE', N'Alta velocidad - Porcentaje', N'NUMERICO', NULL, 80, N'5', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO INFERIOR (DESACELERACI' + NCHAR(211) + N'N)', N'T2-RINF-BV-DURACION', N'Baja velocidad - Duraci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 90, N'400', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO INFERIOR (DESACELERACI' + NCHAR(211) + N'N)', N'T2-RINF-BV-PORCENTAJE', N'Baja velocidad - Porcentaje', N'NUMERICO', NULL, 100, N'15', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO INFERIOR (DESACELERACI' + NCHAR(211) + N'N)', N'T2-RINF-REG-DURACION', N'Regreso - Duraci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 110, N'1400', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N RODILLO INFERIOR (DESACELERACI' + NCHAR(211) + N'N)', N'T2-RINF-REG-PORCENTAJE', N'Regreso - Porcentaje', N'NUMERICO', NULL, 120, N'1,4', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N DIATROL', N'T2-DIATROL-ANTICIP-DESC', N'Anticipaci' + NCHAR(243) + N'n Descenso', N'NUMERICO', NULL, 130, N'-200', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N DIATROL', N'T2-DIATROL-DURACION-DESC', N'Duraci' + NCHAR(243) + N'n Descenso', N'NUMERICO', NULL, 140, N'3600', NULL, NULL),
        (N'T2-G05', N'GESTI' + NCHAR(211) + N'N DIATROL', N'T2-DIATROL-CAMBIO-PMIN', N'Cambio Punto M' + NCHAR(237) + N'nimo', N'NUMERICO', NULL, 150, N'1', NULL, NULL),
        (N'T2-G05', N'ACELERACION DE PRENSA', N'T2-ACEL-INICIAL', N'Inicial', N'NUMERICO', NULL, 160, N'2,5', NULL, NULL),
        (N'T2-G05', N'ACELERACION DE PRENSA', N'T2-ACEL-MAXIMA', N'M' + NCHAR(225) + N'xima', N'NUMERICO', NULL, 170, N'5', NULL, NULL),
        (N'T2-G05', N'ACELERACION DE PRENSA', N'T2-ACEL-INICIO', N'Inicio Aceleraci' + NCHAR(243) + N'n', N'NUMERICO', NULL, 180, N'250', NULL, NULL),

        (N'T2-G06', N'PRESIONES', N'T2-NEU-CIL-DIATROL', N'Cilindro Prensa Diatrol', N'NUMERICO', NULL, 10, N'2', NULL, NULL),
        (N'T2-G06', N'PRESIONES', N'T2-NEU-ZAFE-CARGA', N'Zafe Acumulador-Carga', N'NUMERICO', NULL, 20, N'0,5', NULL, NULL),
        (N'T2-G06', N'PRESIONES', N'T2-NEU-ZAFE-DESCARGA', N'Zafe Acumulador-Descarga', N'NUMERICO', NULL, 30, N'1', NULL, NULL);

-- Controles de los datos de arriba (por si alguien los edita a mano)
IF EXISTS (SELECT 1 FROM @Puntos p WHERE NOT EXISTS (SELECT 1 FROM @Grupos g WHERE g.[Codigo] = p.[CodigoGrupo]))
   OR EXISTS (SELECT 1 FROM @Puntos WHERE [Minimo] > [Maximo])
   OR EXISTS (SELECT 1 FROM @Puntos WHERE [TipoDato] NOT IN ('NUMERICO', 'OK_NOOK', 'TEXTO'))
   OR EXISTS (SELECT 1 FROM @Puntos WHERE [TipoDato] <> 'NUMERICO'
                                      AND ([Minimo] IS NOT NULL OR [Maximo] IS NOT NULL OR [Unidad] IS NOT NULL))
BEGIN
    RAISERROR(N'Los datos de @Puntos no son consistentes (grupo inexistente, minimo > maximo, tipo invalido o limites en un punto no numerico). No se cargo nada.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- ----------------------------------------------------------------------------
-- Producto y su asociación a la línea
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [mnt].[Productos] WHERE [Codigo] = @ProductoCodigo)
BEGIN
    INSERT INTO [mnt].[Productos] ([Codigo], [Nombre], [Descripcion], [Orden], [Activo],
        [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
    VALUES (@ProductoCodigo, @ProductoNombre, NULL, 1, 1, @Ahora, @Ahora, @Usuario, @Usuario);
    PRINT N'Producto HIGIENICO 30 MTS creado.';
END
ELSE
    PRINT N'Producto HIGIENICO 30 MTS ya existia (no se modifica).';

DECLARE @ProductoId INT = (SELECT [ProductoId] FROM [mnt].[Productos] WHERE [Codigo] = @ProductoCodigo);

IF NOT EXISTS (SELECT 1 FROM [mnt].[LineasProductos] WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId)
BEGIN
    INSERT INTO [mnt].[LineasProductos] ([LineaId], [ProductoId]) VALUES (@LineaId, @ProductoId);
    PRINT N'Producto asociado a TANGO 2.';
END

-- ----------------------------------------------------------------------------
-- Grupos (pestañas) que falten: identificados por línea + código
-- ----------------------------------------------------------------------------
INSERT INTO [mnt].[GruposParametro] ([LineaId], [Codigo], [Nombre], [Descripcion], [Orden], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT @LineaId, g.[Codigo], g.[Nombre], NULL, g.[Orden], 1, @Ahora, @Ahora, @Usuario, @Usuario
FROM @Grupos g
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[GruposParametro] gp
                  WHERE gp.[LineaId] = @LineaId AND gp.[Codigo] = g.[Codigo]);

PRINT CONCAT(N'Grupos creados: ', @@ROWCOUNT, N'.');

-- ----------------------------------------------------------------------------
-- Puntos de control que falten: identificados por código dentro de la línea.
-- Sin ValorMin / ValorMax en la plantilla: los límites son del estándar del producto.
-- ----------------------------------------------------------------------------
INSERT INTO [mnt].[Parametros] ([GrupoParametroId], [Seccion], [Codigo], [Nombre], [UnidadMedida], [TipoDato],
    [ValorMin], [ValorMax], [Frecuencia], [Orden], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT gp.[GrupoParametroId], p.[Seccion], p.[Codigo], p.[Nombre], p.[Unidad], p.[TipoDato],
       NULL, NULL, 'POR_TURNO', p.[Orden], 1,
       @Ahora, @Ahora, @Usuario, @Usuario
FROM @Puntos p
JOIN (SELECT [Codigo], MIN([GrupoParametroId]) AS [GrupoParametroId]
      FROM [mnt].[GruposParametro]
      WHERE [LineaId] = @LineaId
      GROUP BY [Codigo]) gp
    ON gp.[Codigo] = p.[CodigoGrupo]
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[Parametros] x
                  JOIN [mnt].[GruposParametro] y ON y.[GrupoParametroId] = x.[GrupoParametroId]
                  WHERE y.[LineaId] = @LineaId AND x.[Codigo] = p.[Codigo]);

PRINT CONCAT(N'Puntos de control creados: ', @@ROWCOUNT, N'.');

-- ----------------------------------------------------------------------------
-- Estándares de HIGIENICO 30 MTS en TANGO 2 que falten (solo puntos con STD distinto de "-")
-- ----------------------------------------------------------------------------
INSERT INTO [mnt].[EstandaresParametro] ([LineaId], [ProductoId], [ParametroId], [ValorEstandarTexto],
    [ValorMin], [ValorMax], [Tolerancia], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT @LineaId, @ProductoId, pl.[ParametroId], p.[Estandar],
       p.[Minimo], p.[Maximo], NULL, 1,
       @Ahora, @Ahora, @Usuario, @Usuario
FROM @Puntos p
JOIN (SELECT x.[Codigo], MIN(x.[ParametroId]) AS [ParametroId]
      FROM [mnt].[Parametros] x
      JOIN [mnt].[GruposParametro] y ON y.[GrupoParametroId] = x.[GrupoParametroId]
      WHERE y.[LineaId] = @LineaId
      GROUP BY x.[Codigo]) pl
    ON pl.[Codigo] = p.[Codigo]
WHERE p.[Estandar] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [mnt].[EstandaresParametro] e
                  WHERE e.[LineaId] = @LineaId AND e.[ProductoId] = @ProductoId AND e.[ParametroId] = pl.[ParametroId]);

PRINT CONCAT(N'Estandares creados: ', @@ROWCOUNT, N'.');

COMMIT TRANSACTION;

-- Resumen de lo que queda en la base (incluye lo cargado en corridas anteriores)
DECLARE @TotGrupos INT = (SELECT COUNT(*) FROM [mnt].[GruposParametro]
                          WHERE [LineaId] = @LineaId AND [Codigo] IN (SELECT [Codigo] FROM @Grupos));
DECLARE @TotPuntos INT = (SELECT COUNT(*) FROM [mnt].[Parametros] x
                          JOIN [mnt].[GruposParametro] y ON y.[GrupoParametroId] = x.[GrupoParametroId]
                          WHERE y.[LineaId] = @LineaId AND x.[Codigo] IN (SELECT [Codigo] FROM @Puntos));
DECLARE @TotEstandares INT = (SELECT COUNT(*) FROM [mnt].[EstandaresParametro]
                              WHERE [LineaId] = @LineaId AND [ProductoId] = @ProductoId);

PRINT CONCAT(N'TANGO 2 queda con ', @TotGrupos, N' grupos, ', @TotPuntos, N' puntos y ',
             @TotEstandares, N' estandares para HIGIENICO 30 MTS (esperado: 6, 57 y 54).');
GO

PRINT 'Script 24_t545_plantilla_control_proceso_tango2.sql completado.'
GO
