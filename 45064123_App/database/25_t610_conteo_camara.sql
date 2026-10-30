-- ==============================================================================
-- Script: 25_t610_conteo_camara.sql
-- Descripción: T-610 Conteo de producción y detección de paradas con la cámara 09
--              (salida de la cortadora de la Tango 2 / Futura 2).
--              Tablas: mnt.ConteosCamaraMinuto (rollos por minuto y carril, con los
--              segundos en marcha / parada / acumulación / sin señal) y
--              mnt.ParadasCamara (paradas detectadas: inicio, fin, duración, tipo y
--              carriles). Las carga el servicio PSAG_ConteoCamara por
--              POST api/conteo-camara/ingesta. INSERT-ONLY: nada se edita ni se borra.
--              La cámara se identifica con un punto de control de calidad (T-611,
--              mnt.PuntosControlCalidad), que define la línea.
--              El cruce con detenciones (paradas sin detención cargada) se calcula
--              en la API al consultar: no se guarda.
-- Versión: 1.0 - T-610
-- Requiere: 04_create_tables.sql (Lineas, Turnos), 23_t611_calidad_defectos.sql
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga datos (ver el bloque comentado al final para dar de alta la cámara).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ============================================================================
-- Tabla: mnt.ConteosCamaraMinuto
-- Un registro por punto de control + minuto + carril (único: un reenvío no duplica).
-- Rollos = frecuencia de paso x segundos con paso regular, con un decimal.
-- ============================================================================
IF OBJECT_ID(N'[mnt].[ConteosCamaraMinuto]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[ConteosCamaraMinuto] (
        [ConteoCamaraMinutoId] BIGINT IDENTITY(1,1) NOT NULL,
        [Minuto] DATETIMEOFFSET NOT NULL,
        [LineaId] INT NOT NULL,
        [PuntoControlCalidadId] INT NOT NULL,
        [TurnoId] INT NULL,
        [Carril] TINYINT NOT NULL,
        [Rollos] DECIMAL(9,1) NOT NULL,
        [SegundosMarcha] SMALLINT NOT NULL,
        [SegundosParada] SMALLINT NOT NULL,
        [SegundosAcumulacion] SMALLINT NOT NULL,
        [SegundosSinSenal] SMALLINT NOT NULL,
        [Estado] NVARCHAR(20) NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_ConteosCamaraMinuto] PRIMARY KEY ([ConteoCamaraMinutoId]),
        CONSTRAINT [CK_ConteosCamaraMinuto_Estado] CHECK ([Estado] IN ('MARCHA', 'PARADA', 'ACUMULACION', 'SIN_SENAL')),
        CONSTRAINT [CK_ConteosCamaraMinuto_Carril] CHECK ([Carril] BETWEEN 1 AND 16),
        CONSTRAINT [CK_ConteosCamaraMinuto_Rollos] CHECK ([Rollos] >= 0),
        CONSTRAINT [CK_ConteosCamaraMinuto_Segundos] CHECK (
            [SegundosMarcha] >= 0 AND [SegundosParada] >= 0 AND [SegundosAcumulacion] >= 0 AND [SegundosSinSenal] >= 0
            AND [SegundosMarcha] + [SegundosParada] + [SegundosAcumulacion] + [SegundosSinSenal] = 60),
        CONSTRAINT [FK_ConteosCamaraMinuto_Lineas_LineaId] FOREIGN KEY ([LineaId])
            REFERENCES [mnt].[Lineas] ([LineaId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConteosCamaraMinuto_PuntosControlCalidad_PuntoControlCalidadId] FOREIGN KEY ([PuntoControlCalidadId])
            REFERENCES [mnt].[PuntosControlCalidad] ([PuntoControlCalidadId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConteosCamaraMinuto_Turnos_TurnoId] FOREIGN KEY ([TurnoId])
            REFERENCES [mnt].[Turnos] ([TurnoId]) ON DELETE NO ACTION
    );
    PRINT 'Tabla mnt.ConteosCamaraMinuto creada exitosamente.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_ConteosCamaraMinuto_Punto_Minuto_Carril')
BEGIN
    CREATE UNIQUE INDEX [UX_ConteosCamaraMinuto_Punto_Minuto_Carril]
        ON [mnt].[ConteosCamaraMinuto] ([PuntoControlCalidadId], [Minuto], [Carril]);
    PRINT 'Índice UX_ConteosCamaraMinuto_Punto_Minuto_Carril creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ConteosCamaraMinuto_Linea_Minuto')
BEGIN
    CREATE INDEX [IX_ConteosCamaraMinuto_Linea_Minuto]
        ON [mnt].[ConteosCamaraMinuto] ([LineaId], [Minuto])
        INCLUDE ([Carril], [Rollos], [Estado]);
    PRINT 'Índice IX_ConteosCamaraMinuto_Linea_Minuto creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ConteosCamaraMinuto_PuntoControlCalidadId')
    CREATE INDEX [IX_ConteosCamaraMinuto_PuntoControlCalidadId] ON [mnt].[ConteosCamaraMinuto] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ConteosCamaraMinuto_TurnoId')
    CREATE INDEX [IX_ConteosCamaraMinuto_TurnoId] ON [mnt].[ConteosCamaraMinuto] ([TurnoId]);
GO

-- ============================================================================
-- Tabla: mnt.ParadasCamara
-- Una parada cerrada por fila. Tipo PARADA = en algún momento pararon los 4
-- carriles (paró la cortadora); ACUMULACION = algunos carriles quietos o a los
-- tirones mientras otros seguían. Carriles = "1,2,3,4".
-- InicioIncierto: el servicio arrancó con la línea parada. FinIncierto: se cortó
-- el video antes del arranque.
-- ============================================================================
IF OBJECT_ID(N'[mnt].[ParadasCamara]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[ParadasCamara] (
        [ParadaCamaraId] BIGINT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [PuntoControlCalidadId] INT NOT NULL,
        [TurnoId] INT NULL,
        [FechaHoraInicio] DATETIMEOFFSET NOT NULL,
        [FechaHoraFin] DATETIMEOFFSET NOT NULL,
        [DuracionSegundos] INT NOT NULL,
        [Tipo] NVARCHAR(20) NOT NULL,
        [Carriles] NVARCHAR(50) NOT NULL,
        [InicioIncierto] BIT NOT NULL,
        [FinIncierto] BIT NOT NULL,
        [IdentificadorExterno] NVARCHAR(100) NOT NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_ParadasCamara] PRIMARY KEY ([ParadaCamaraId]),
        CONSTRAINT [CK_ParadasCamara_Tipo] CHECK ([Tipo] IN ('PARADA', 'ACUMULACION')),
        CONSTRAINT [CK_ParadasCamara_Duracion] CHECK ([DuracionSegundos] > 0),
        CONSTRAINT [FK_ParadasCamara_Lineas_LineaId] FOREIGN KEY ([LineaId])
            REFERENCES [mnt].[Lineas] ([LineaId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParadasCamara_PuntosControlCalidad_PuntoControlCalidadId] FOREIGN KEY ([PuntoControlCalidadId])
            REFERENCES [mnt].[PuntosControlCalidad] ([PuntoControlCalidadId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParadasCamara_Turnos_TurnoId] FOREIGN KEY ([TurnoId])
            REFERENCES [mnt].[Turnos] ([TurnoId]) ON DELETE NO ACTION
    );
    PRINT 'Tabla mnt.ParadasCamara creada exitosamente.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_ParadasCamara_IdentificadorExterno')
BEGIN
    CREATE UNIQUE INDEX [UX_ParadasCamara_IdentificadorExterno] ON [mnt].[ParadasCamara] ([IdentificadorExterno]);
    PRINT 'Índice UX_ParadasCamara_IdentificadorExterno creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ParadasCamara_Linea_Inicio')
BEGIN
    CREATE INDEX [IX_ParadasCamara_Linea_Inicio] ON [mnt].[ParadasCamara] ([LineaId], [FechaHoraInicio]);
    PRINT 'Índice IX_ParadasCamara_Linea_Inicio creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ParadasCamara_PuntoControlCalidadId')
    CREATE INDEX [IX_ParadasCamara_PuntoControlCalidadId] ON [mnt].[ParadasCamara] ([PuntoControlCalidadId]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ParadasCamara_TurnoId')
    CREATE INDEX [IX_ParadasCamara_TurnoId] ON [mnt].[ParadasCamara] ([TurnoId]);
GO

/*
-- ============================================================================
-- OPCIONAL: alta de la cámara 09 como punto de control (también se puede hacer
-- desde la Web: Maestros → Calidad → Cámaras). Revisar el nombre de la línea.
-- ============================================================================
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-25';
INSERT INTO [mnt].[PuntosControlCalidad] ([LineaId], [Codigo], [Nombre], [Descripcion], [Activo],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT l.[LineaId], N'T2-CAM09', N'Cámara 09 - salida cortadora',
       N'NVR 172.16.9.67 canal 09. Conteo de producción y paradas (T-610).', 1,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
FROM [mnt].[Lineas] l
WHERE l.[Nombre] = N'Tango 2'   -- <-- nombre de la línea en Maestros
  AND NOT EXISTS (SELECT 1 FROM [mnt].[PuntosControlCalidad] x WHERE x.[Codigo] = N'T2-CAM09');
PRINT CONCAT(N'Punto de control T2-CAM09 cargado: ', @@ROWCOUNT, N'.');
GO
*/

PRINT 'Script 25_t610_conteo_camara.sql completado.'
GO
