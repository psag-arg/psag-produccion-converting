-- ==============================================================================
-- Script: 31_maquinista_turno.sql
-- Descripción: Maquinista de cada turno de cada línea (T-783, definición de Emiliano del 20/11/2026).
--   Al loguearse, el OPERARIO confirma si es el maquinista del turno en curso de su línea
--   (tiene que tener asignación en Maestros → Maquinistas para ese turno, o sin turno fijo).
--   El supervisor lo corrige desde la Web: Detenciones → Maquinistas del turno.
--   mnt.MaquinistasTurno: INSERT-ONLY. Cada confirmación o corrección es un registro nuevo;
--   el vigente de (línea, turno, día del turno) es el más reciente. El historial queda.
--   Las detenciones automáticas de la cámara (y las manuales sin maquinista elegido) toman
--   al vigente; si no hay, se sigue usando la asignación como hasta ahora.
-- Versión: 1.0
-- Requiere: 01-02 (Lineas, Turnos, UsuariosLineas).
-- Idempotente: se puede ejecutar más de una vez.
-- NO carga datos.
-- Correr antes de publicar la API (la API lee la tabla).
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[mnt].[MaquinistasTurno]') AND type in (N'U'))
BEGIN
    CREATE TABLE [mnt].[MaquinistasTurno] (
        [MaquinistaTurnoId] BIGINT IDENTITY(1,1) NOT NULL,
        [LineaId] INT NOT NULL,
        [TurnoId] INT NOT NULL,
        [FechaTurno] DATE NOT NULL,
        [UsuarioLineaId] INT NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [Origen] NVARCHAR(20) NOT NULL,
        [FechaHora] DATETIMEOFFSET NOT NULL,
        [RegistradoPorUsuarioId] NVARCHAR(450) NOT NULL,
        [Observacion] NVARCHAR(500) NULL,
        CONSTRAINT [PK_MaquinistasTurno] PRIMARY KEY ([MaquinistaTurnoId]),
        CONSTRAINT [CK_MaquinistasTurno_Origen] CHECK ([Origen] IN ('LOGIN', 'SUPERVISOR')),
        CONSTRAINT [FK_MaquinistasTurno_Lineas] FOREIGN KEY ([LineaId]) REFERENCES [mnt].[Lineas] ([LineaId]),
        CONSTRAINT [FK_MaquinistasTurno_Turnos] FOREIGN KEY ([TurnoId]) REFERENCES [mnt].[Turnos] ([TurnoId]),
        CONSTRAINT [FK_MaquinistasTurno_UsuariosLineas] FOREIGN KEY ([UsuarioLineaId]) REFERENCES [mnt].[UsuariosLineas] ([UsuarioLineaId])
    );
    PRINT 'Tabla mnt.MaquinistasTurno creada.'
END
ELSE
    PRINT 'Tabla mnt.MaquinistasTurno ya existía.'
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MaquinistasTurno_Fecha_Linea_Turno' AND object_id = OBJECT_ID(N'[mnt].[MaquinistasTurno]'))
BEGIN
    CREATE INDEX [IX_MaquinistasTurno_Fecha_Linea_Turno] ON [mnt].[MaquinistasTurno] ([FechaTurno], [LineaId], [TurnoId]);
    PRINT 'Índice IX_MaquinistasTurno_Fecha_Linea_Turno creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MaquinistasTurno_UsuarioLinea' AND object_id = OBJECT_ID(N'[mnt].[MaquinistasTurno]'))
BEGIN
    CREATE INDEX [IX_MaquinistasTurno_UsuarioLinea] ON [mnt].[MaquinistasTurno] ([UsuarioLineaId]);
    PRINT 'Índice IX_MaquinistasTurno_UsuarioLinea creado.'
END
GO
