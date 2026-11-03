-- ==============================================================================
-- Script: 26_t610_detencion_automatica_camara.sql
-- Descripción: T-610 Detención automática por cámara (cámara 10, rebobinadora).
--   1. mnt.PuntosControlCalidad = "cámara asociada": cuerpo (EquipoId) que mira,
--      número de cámara en el grabador, si crea detenciones y el umbral en minutos
--      (5 por defecto). El administrador lo cambia en Maestros → Calidad, sin redeploy.
--   2. mnt.Detenciones: origen (MANUAL / CAMARA), parada de cámara que la originó
--      (único: idempotencia), cámara, y arranque detectado (sugerencia para el
--      operador). También UsuarioLineaId (maquinista) si la base no lo tenía.
--      La detención automática nace en PENDIENTE_JUSTIFICACION ("Pendiente de
--      declaración", estado que ya admite el script 17) con 0 minutos (CHECK >= 0)
--      y sin hora de arranque. Las detenciones manuales no cambian.
--   3. mnt.ParadasCamara admite el tipo SIN_MANGAS (rodillo girando, sin mangas).
--   4. mnt.AlertasCamara: cámara congelada o sin video (abre / cierra).
-- Versión: 1.0 - T-610
-- Requiere: 04, 17 (estado PENDIENTE_JUSTIFICACION), 23 (PuntosControlCalidad), 25.
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga datos (ver el bloque comentado al final para la cámara 10).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- Los índices únicos filtrados exigen estas opciones; sqlcmd arranca con QUOTED_IDENTIFIER OFF.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ============================================================================
-- 1. Cámara asociada en mnt.PuntosControlCalidad
-- ============================================================================
IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'EquipoId') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] ADD [EquipoId] INT NULL;
    PRINT 'Columna PuntosControlCalidad.EquipoId agregada.'
END
GO
IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'NumeroCamara') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad] ADD [NumeroCamara] INT NULL;
    PRINT 'Columna PuntosControlCalidad.NumeroCamara agregada.'
END
GO
IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'CreaDetenciones') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD [CreaDetenciones] BIT NOT NULL CONSTRAINT [DF_PuntosControlCalidad_CreaDetenciones] DEFAULT 0;
    PRINT 'Columna PuntosControlCalidad.CreaDetenciones agregada (apagada en las cámaras existentes).'
END
GO
IF COL_LENGTH(N'mnt.PuntosControlCalidad', N'MinutosUmbralDetencion') IS NULL
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD [MinutosUmbralDetencion] INT NOT NULL CONSTRAINT [DF_PuntosControlCalidad_MinutosUmbral] DEFAULT 5;
    PRINT 'Columna PuntosControlCalidad.MinutosUmbralDetencion agregada (5 minutos).'
END
GO
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_PuntosControlCalidad_MinutosUmbral')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [CK_PuntosControlCalidad_MinutosUmbral] CHECK ([MinutosUmbralDetencion] BETWEEN 1 AND 240);
    PRINT 'Check CK_PuntosControlCalidad_MinutosUmbral creado.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_PuntosControlCalidad_Equipos')
BEGIN
    ALTER TABLE [mnt].[PuntosControlCalidad]
        ADD CONSTRAINT [FK_PuntosControlCalidad_Equipos] FOREIGN KEY ([EquipoId]) REFERENCES [mnt].[Equipos] ([EquipoId]);
    PRINT 'FK FK_PuntosControlCalidad_Equipos creada.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_PuntosControlCalidad_EquipoId')
    CREATE INDEX [IX_PuntosControlCalidad_EquipoId] ON [mnt].[PuntosControlCalidad] ([EquipoId]);
GO

-- ============================================================================
-- 2. Detención automática en mnt.Detenciones
-- ============================================================================

-- Maquinista (UsuarioLineaId): lo usa la API desde antes; se agrega sólo si falta.
IF COL_LENGTH(N'mnt.Detenciones', N'UsuarioLineaId') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [UsuarioLineaId] INT NULL;
    PRINT 'Columna Detenciones.UsuarioLineaId agregada.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'mnt.Detenciones') AND referenced_object_id = OBJECT_ID(N'mnt.UsuariosLineas'))
BEGIN
    ALTER TABLE [mnt].[Detenciones]
        ADD CONSTRAINT [FK_Detenciones_UsuariosLineas] FOREIGN KEY ([UsuarioLineaId]) REFERENCES [mnt].[UsuariosLineas] ([UsuarioLineaId]);
    PRINT 'FK FK_Detenciones_UsuariosLineas creada.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'mnt.Detenciones') AND name IN (N'IX_Detenciones_UsuarioLineaId'))
    CREATE INDEX [IX_Detenciones_UsuarioLineaId] ON [mnt].[Detenciones] ([UsuarioLineaId]);
GO

IF COL_LENGTH(N'mnt.Detenciones', N'OrigenRegistro') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones]
        ADD [OrigenRegistro] VARCHAR(20) NOT NULL CONSTRAINT [DF_Detenciones_OrigenRegistro] DEFAULT 'MANUAL';
    PRINT 'Columna Detenciones.OrigenRegistro agregada (las existentes quedan MANUAL).'
END
GO
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_Detenciones_OrigenRegistro')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
        ADD CONSTRAINT [CK_Detenciones_OrigenRegistro] CHECK ([OrigenRegistro] IN ('MANUAL', 'CAMARA'));
    PRINT 'Check CK_Detenciones_OrigenRegistro creado.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'IdentificadorParadaCamara') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [IdentificadorParadaCamara] NVARCHAR(100) NULL;
    PRINT 'Columna Detenciones.IdentificadorParadaCamara agregada.'
END
GO
-- Idempotencia: una sola detención por parada de cámara (también entre eliminadas).
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_Detenciones_IdentificadorParadaCamara')
BEGIN
    CREATE UNIQUE INDEX [UX_Detenciones_IdentificadorParadaCamara]
        ON [mnt].[Detenciones] ([IdentificadorParadaCamara])
        WHERE [IdentificadorParadaCamara] IS NOT NULL;
    PRINT 'Índice UX_Detenciones_IdentificadorParadaCamara creado.'
END
GO

IF COL_LENGTH(N'mnt.Detenciones', N'PuntoControlCalidadId') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [PuntoControlCalidadId] INT NULL;
    PRINT 'Columna Detenciones.PuntoControlCalidadId agregada.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Detenciones_PuntosControlCalidad')
BEGIN
    ALTER TABLE [mnt].[Detenciones]
        ADD CONSTRAINT [FK_Detenciones_PuntosControlCalidad] FOREIGN KEY ([PuntoControlCalidadId]) REFERENCES [mnt].[PuntosControlCalidad] ([PuntoControlCalidadId]);
    PRINT 'FK FK_Detenciones_PuntosControlCalidad creada.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Detenciones_PuntoControlCalidadId')
    CREATE INDEX [IX_Detenciones_PuntoControlCalidadId] ON [mnt].[Detenciones] ([PuntoControlCalidadId]);
GO

IF COL_LENGTH(N'mnt.Detenciones', N'FechaHoraArranqueDetectada') IS NULL
BEGIN
    ALTER TABLE [mnt].[Detenciones] ADD [FechaHoraArranqueDetectada] DATETIMEOFFSET NULL;
    PRINT 'Columna Detenciones.FechaHoraArranqueDetectada agregada.'
END
GO

-- ============================================================================
-- 3. ParadasCamara: tipo SIN_MANGAS
-- ============================================================================
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_ParadasCamara_Tipo'
           AND definition NOT LIKE N'%SIN_MANGAS%')
BEGIN
    ALTER TABLE [mnt].[ParadasCamara] DROP CONSTRAINT [CK_ParadasCamara_Tipo];
    PRINT 'Check CK_ParadasCamara_Tipo eliminado (versión sin SIN_MANGAS).'
END
GO
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_ParadasCamara_Tipo')
BEGIN
    ALTER TABLE [mnt].[ParadasCamara]
        ADD CONSTRAINT [CK_ParadasCamara_Tipo] CHECK ([Tipo] IN ('PARADA', 'ACUMULACION', 'SIN_MANGAS'));
    PRINT 'Check CK_ParadasCamara_Tipo creado (con SIN_MANGAS).'
END
GO

-- ============================================================================
-- 4. mnt.AlertasCamara (cámara congelada / sin video)
-- ============================================================================
IF OBJECT_ID(N'[mnt].[AlertasCamara]', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[AlertasCamara] (
        [AlertaCamaraId] BIGINT IDENTITY(1,1) NOT NULL,
        [PuntoControlCalidadId] INT NOT NULL,
        [LineaId] INT NOT NULL,
        [Tipo] NVARCHAR(20) NOT NULL,
        [FechaHoraInicio] DATETIMEOFFSET NOT NULL,
        [FechaHoraFin] DATETIMEOFFSET NULL,
        [IdentificadorExterno] NVARCHAR(100) NOT NULL,
        [Detalle] NVARCHAR(500) NULL,
        [FechaHoraCreacion] DATETIMEOFFSET NOT NULL,
        [FechaHoraActualizacion] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_AlertasCamara] PRIMARY KEY ([AlertaCamaraId]),
        CONSTRAINT [CK_AlertasCamara_Tipo] CHECK ([Tipo] IN ('CONGELADA', 'SIN_VIDEO')),
        CONSTRAINT [CK_AlertasCamara_Fin] CHECK ([FechaHoraFin] IS NULL OR [FechaHoraFin] >= [FechaHoraInicio]),
        CONSTRAINT [FK_AlertasCamara_PuntosControlCalidad] FOREIGN KEY ([PuntoControlCalidadId]) REFERENCES [mnt].[PuntosControlCalidad] ([PuntoControlCalidadId]),
        CONSTRAINT [FK_AlertasCamara_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas] ([LineaId])
    );
    PRINT 'Tabla mnt.AlertasCamara creada.'
END
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_AlertasCamara_IdentificadorExterno')
    CREATE UNIQUE INDEX [UX_AlertasCamara_IdentificadorExterno] ON [mnt].[AlertasCamara] ([IdentificadorExterno]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AlertasCamara_Punto_Inicio')
    CREATE INDEX [IX_AlertasCamara_Punto_Inicio] ON [mnt].[AlertasCamara] ([PuntoControlCalidadId], [FechaHoraInicio]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AlertasCamara_LineaId')
    CREATE INDEX [IX_AlertasCamara_LineaId] ON [mnt].[AlertasCamara] ([LineaId]);
GO

/*
-- ============================================================================
-- OPCIONAL: alta de la cámara 10 (rebobinadora) con detención automática.
-- También se puede hacer desde la Web: Maestros → Calidad → Cámaras.
-- Revisar el nombre de la línea y del cuerpo (equipo) en Maestros.
-- ============================================================================
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-26';
INSERT INTO [mnt].[PuntosControlCalidad] ([LineaId], [Codigo], [Nombre], [Descripcion], [Activo],
    [EquipoId], [NumeroCamara], [CreaDetenciones], [MinutosUmbralDetencion],
    [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT l.[LineaId], N'T2-CAM10', N'Cámara 10 - rebobinadora',
       N'NVR 172.16.9.67 canal 10. Mangas por la mesa y rodillo: detención automática (T-610).', 1,
       (SELECT TOP 1 e.[EquipoId] FROM [mnt].[Equipos] e WHERE e.[LineaId] = l.[LineaId] AND e.[Nombre] LIKE N'%Rebobinadora%'),
       10, 1, 5,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Usuario, @Usuario
FROM [mnt].[Lineas] l
WHERE l.[Nombre] = N'Tango 2'   -- <-- nombre de la línea en Maestros
  AND NOT EXISTS (SELECT 1 FROM [mnt].[PuntosControlCalidad] x WHERE x.[Codigo] = N'T2-CAM10');
PRINT CONCAT(N'Punto de control T2-CAM10 cargado: ', @@ROWCOUNT, N'.');

-- La cámara 09 sigue sólo informando (CreaDetenciones = 0, el valor por defecto).
-- Asociarle el número de cámara (referencia):
UPDATE [mnt].[PuntosControlCalidad] SET [NumeroCamara] = 9 WHERE [Codigo] = N'T2-CAM09' AND [NumeroCamara] IS NULL;
GO
*/

PRINT 'Script 26_t610_detencion_automatica_camara.sql completado.'
GO
