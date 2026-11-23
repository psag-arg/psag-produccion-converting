-- ==============================================================================
-- Script: database/dev/seed_previo_t798.sql
-- SOLO DESARROLLO / TEST. NO va en el paquete de producción. DATOS INVENTADOS.
--
-- Datos de prueba parecidos a producción sobre el esquema VIEJO (02-30, antes del 31),
-- para probar la conversión T-798 (31 a 38) y sus rollbacks.
-- Orden: 02..08, dev/seed_previo_t798_maestros.sql, 17, 18, 11, 12, 19..30 y recién ahí este.
--
-- Carga (todo en TANGO 2 salvo donde se indica; días de producción 18 y 19/11/2026):
--   * Usuarios Identity (sin contraseña: no sirven para login) con roles, y UsuariosLineas.
--   * Programación de TANGO 2 (CalendariosLinea vigente 01/11: lun-vie 3 turnos, sáb T1+T2)
--     y una excepción de horas el 19/11 (16 h).
--   * Puntos de control: T2-CAM09 (cámara, CARRILES, activa), T2-CAM10 (cámara, MANGAS,
--     crea detenciones) y T2-CAL01 (sólo calidad); tipos de defecto.
--   * ConteosCamaraMinuto de 2 días (05:00 del 18 a 05:00 del 20, hora de planta) en UTC:
--     4 carriles de la 09 y 1 de la 10. TurnoId como lo calculaba la app (primer turno que
--     contiene la hora: 05-06 = T1), salvo 05:00-05:59 del 19 en la 10 (T3, para probar la
--     excepción) y 12:00-12:09 del 19 en la 09 (NULL, sin turno).
--   * ParadasCamara, AlertasCamara.
--   * Detenciones manuales y de cámara: con y sin hora, una 05:20 en T3, varias después de
--     medianoche, una eliminada lógicamente, otras líneas; historial de estados y notas.
--   * Registros de Control de Proceso (con detalle) y de Calidad (con revisiones).
-- Las horas manuales van con offset -03:00 y las de cámara en UTC (+00:00), como las guarda
-- cada origen.
-- Idempotente: si ya está cargado (usuario operario.t2), no hace nada.
-- Uso: sqlcmd -S <servidor> -d PSAG_Mantenimiento_DEV -C -I -b -f 65001 -i seed_previo_t798.sql
-- ==============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'%[_]DEV' AND DB_NAME() NOT LIKE N'%[_]TEST' AND DB_NAME() NOT LIKE N'%[_]PRUEBA'
BEGIN
    RAISERROR(N'seed DEV: la base actual no es de DEV/TEST/PRUEBA. No se carga nada.', 16, 1);
    SET NOEXEC ON;
END
GO
IF OBJECT_ID(N'mnt.Detenciones', N'U') IS NULL
BEGIN
    RAISERROR(N'seed DEV: es para el esquema viejo (antes del 31) y no existe mnt.Detenciones.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE UserName = N'operario.t2')
BEGIN
    PRINT N'seed DEV: ya estaba cargado. No se hace nada.';
    SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

DECLARE @Seed NVARCHAR(450) = N'SEED-DEV';
DECLARE @T2 INT = (SELECT LineaId FROM mnt.Lineas WHERE Nombre = N'TANGO 2');
DECLARE @T1L INT = (SELECT LineaId FROM mnt.Lineas WHERE Nombre = N'TANGO 1');
DECLARE @F2 INT = (SELECT LineaId FROM mnt.Lineas WHERE Nombre = N'FUTURA 2');
IF @T2 IS NULL OR @T1L IS NULL OR @F2 IS NULL
    THROW 50001, N'Faltan las líneas TANGO 2 / TANGO 1 / FUTURA 2: correr antes dev/seed_previo_t798_maestros.sql.', 1;

DECLARE @TU1 INT = (SELECT TurnoId FROM mnt.Turnos WHERE Codigo = N'T1');
DECLARE @TU2 INT = (SELECT TurnoId FROM mnt.Turnos WHERE Codigo = N'T2');
DECLARE @TU3 INT = (SELECT TurnoId FROM mnt.Turnos WHERE Codigo = N'T3');
DECLARE @Prod INT = (SELECT ProductoId FROM mnt.Productos WHERE Codigo = N'HIGIENICO 30 MTS');
IF @Prod IS NULL THROW 50002, N'Falta el producto HIGIENICO 30 MTS (script 24).', 1;

DECLARE @EqCor INT = (SELECT EquipoId FROM mnt.Equipos WHERE LineaId = @T2 AND Nombre = N'Cortadora');
DECLARE @EqEmp INT = (SELECT EquipoId FROM mnt.Equipos WHERE LineaId = @T2 AND Nombre = N'Empaquetadora');
DECLARE @EqDes INT = (SELECT EquipoId FROM mnt.Equipos WHERE LineaId = @T2 AND Nombre = N'Desenrollador');
DECLARE @EqT1 INT = (SELECT EquipoId FROM mnt.Equipos WHERE LineaId = @T1L AND Nombre = N'Cortadora');
DECLARE @EqF2 INT = (SELECT EquipoId FROM mnt.Equipos WHERE LineaId = @F2 AND Nombre = N'Plegadora');
DECLARE @TfMec INT = (SELECT TipoFallaId FROM mnt.TiposFalla WHERE Codigo = N'TF-MEC');
DECLARE @TfEle INT = (SELECT TipoFallaId FROM mnt.TiposFalla WHERE Codigo = N'TF-ELE');
DECLARE @TfMat INT = (SELECT TipoFallaId FROM mnt.TiposFalla WHERE Codigo = N'TF-MAT');
DECLARE @TfAju INT = (SELECT TipoFallaId FROM mnt.TiposFalla WHERE Codigo = N'TF-AJU');
DECLARE @TfCpr INT = (SELECT TipoFallaId FROM mnt.TiposFalla WHERE Codigo = N'TF-CPR');

-- ---------------------------------------------------------------- Usuarios Identity
DECLARE @UOp1 NVARCHAR(450) = N'd0e1a001-0000-4000-8000-000000000001';
DECLARE @UOp2 NVARCHAR(450) = N'd0e1a001-0000-4000-8000-000000000002';
DECLARE @UOp3 NVARCHAR(450) = N'd0e1a001-0000-4000-8000-000000000003';
DECLARE @USup NVARCHAR(450) = N'd0e1a001-0000-4000-8000-000000000004';
DECLARE @UMnt NVARCHAR(450) = N'd0e1a001-0000-4000-8000-000000000005';

INSERT INTO dbo.AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash,
    SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
    NombreCompleto, Activo, RequiereCambioPassword, FechaCreacion)
SELECT u.Id, u.UserName, UPPER(u.UserName), u.UserName + N'@psag.dev', UPPER(u.UserName + N'@psag.dev'), 1, NULL,
       CONVERT(NVARCHAR(36), NEWID()), CONVERT(NVARCHAR(36), NEWID()), 0, 0, 1, 0, u.Nombre, 1, 1, '2026-10-01T08:00:00-03:00'
FROM (VALUES (@UOp1, N'operario.t2', N'Juan Pérez (prueba)'),
             (@UOp2, N'operario.t2b', N'María Gómez (prueba)'),
             (@UOp3, N'operario.f2', N'Carlos Ruiz (prueba)'),
             (@USup, N'supervisor.dev', N'Laura Díaz (prueba)'),
             (@UMnt, N'mantenimiento.dev', N'Pedro Sosa (prueba)')) u(Id, UserName, Nombre);

INSERT INTO dbo.AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM (VALUES (@UOp1, N'OPERARIO'), (@UOp2, N'OPERARIO'), (@UOp3, N'OPERARIO'),
             (@USup, N'SUPERVISOR'), (@UMnt, N'MANTENIMIENTO')) u(Id, Rol)
JOIN dbo.AspNetRoles r ON r.Name = u.Rol;

-- ---------------------------------------------------------------- UsuariosLineas
INSERT INTO mnt.UsuariosLineas (UsuarioId, LineaId, FechaAsignacion, AsignadoPorUsuarioId)
VALUES (@UOp1, @T2, '2026-10-01T08:00:00-03:00', @Seed),
       (@UOp2, @T2, '2026-10-01T08:00:00-03:00', @Seed),
       (@UOp2, @T1L, '2026-10-01T08:00:00-03:00', @Seed),
       (@UOp3, @F2, '2026-10-01T08:00:00-03:00', @Seed),
       (@USup, @T2, '2026-10-01T08:00:00-03:00', @Seed);
DECLARE @ULOp1 INT = (SELECT UsuarioLineaId FROM mnt.UsuariosLineas WHERE UsuarioId = @UOp1 AND LineaId = @T2);
DECLARE @ULOp2 INT = (SELECT UsuarioLineaId FROM mnt.UsuariosLineas WHERE UsuarioId = @UOp2 AND LineaId = @T2);
DECLARE @ULOp2T1 INT = (SELECT UsuarioLineaId FROM mnt.UsuariosLineas WHERE UsuarioId = @UOp2 AND LineaId = @T1L);

-- ---------------------------------------------------------------- Programación de TANGO 2
INSERT INTO mnt.CalendariosLinea (LineaId, VigenteDesde, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId)
VALUES (@T2, '2026-11-01', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed);
DECLARE @Cal INT = SCOPE_IDENTITY();
INSERT INTO mnt.CalendariosLineaTurnos (CalendarioLineaId, DiaSemana, TurnoId)
SELECT @Cal, d.n, t.TurnoId
FROM (VALUES (1), (2), (3), (4), (5)) d(n) CROSS JOIN (VALUES (@TU1), (@TU2), (@TU3)) t(TurnoId)
UNION ALL SELECT @Cal, 6, @TU1 UNION ALL SELECT @Cal, 6, @TU2;
INSERT INTO mnt.TiemposProgramadosDia (LineaId, Fecha, Horas, Motivo, Origen, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId)
VALUES (@T2, '2026-11-19', 16, N'Parada programada de mantenimiento (prueba)', N'EXCEPCION', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed);

-- ---------------------------------------------------------------- Puntos de control y defectos
INSERT INTO mnt.PuntosControlCalidad (LineaId, Codigo, Nombre, Descripcion, Activo, FechaHoraCreacion, FechaHoraModificacion,
    CreadoPorUsuarioId, ModificadoPorUsuarioId, EquipoId, NumeroCamara, CreaDetenciones, MinutosUmbralDetencion, ObjetivoPorHora,
    MetodoConteo, SegundosUmbralDetencion)
VALUES (@T2, N'T2-CAM09', N'Cámara 09 salida cortadora', N'Cuenta rollos por carril', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed,
        @EqCor, 9, 0, 5, NULL, N'CARRILES', NULL),
       (@T2, N'T2-CAM10', N'Cámara 10 mangas', N'Cuenta mangas y crea detenciones', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed,
        @EqDes, 10, 1, 5, 1400, N'MANGAS', 120),
       (@T2, N'T2-CAL01', N'Control visual embolsadora', N'Punto de calidad (sin cámara de conteo)', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed,
        NULL, NULL, 0, 5, NULL, NULL, NULL);
DECLARE @PC09 INT = (SELECT PuntoControlCalidadId FROM mnt.PuntosControlCalidad WHERE Codigo = N'T2-CAM09');
DECLARE @PC10 INT = (SELECT PuntoControlCalidadId FROM mnt.PuntosControlCalidad WHERE Codigo = N'T2-CAM10');
DECLARE @PCC1 INT = (SELECT PuntoControlCalidadId FROM mnt.PuntosControlCalidad WHERE Codigo = N'T2-CAL01');

INSERT INTO mnt.TiposDefectoCalidad (Codigo, Nombre, Descripcion, UnidadMedida, Activo, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId)
VALUES (N'ROLLO-FLOJO', N'Rollo flojo', NULL, NULL, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed),
       (N'DIAMETRO', N'Diámetro fuera de rango', NULL, N'mm', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @Seed, @Seed);
DECLARE @DefFlojo INT = (SELECT TipoDefectoCalidadId FROM mnt.TiposDefectoCalidad WHERE Codigo = N'ROLLO-FLOJO');
DECLARE @DefDiam INT = (SELECT TipoDefectoCalidadId FROM mnt.TiposDefectoCalidad WHERE Codigo = N'DIAMETRO');

-- ---------------------------------------------------------------- Paradas de cámara (UTC)
-- Identificador: <punto>-<inicio UTC>
INSERT INTO mnt.ParadasCamara (LineaId, PuntoControlCalidadId, TurnoId, FechaHoraInicio, FechaHoraFin, DuracionSegundos, Tipo, Carriles,
    InicioIncierto, FinIncierto, IdentificadorExterno, FechaHoraCreacion)
SELECT @T2, p.Punto, p.Turno, p.Ini, p.Fin, DATEDIFF(SECOND, p.Ini, p.Fin), p.Tipo, p.Carriles, 0, 0, p.Ident, p.Fin
FROM (VALUES
    -- 18/11 07:10-07:35 local (T1), la 09 (coincide con la detención manual 1)
    (@PC09, @TU1, CAST('2026-11-18T10:10:00+00:00' AS DATETIMEOFFSET), CAST('2026-11-18T10:35:00+00:00' AS DATETIMEOFFSET), N'PARADA', N'1,2,3,4', N'T2-CAM09-20261118T101000Z'),
    -- 18/11 10:00-10:03 local (T1), acumulación en la 10
    (@PC10, @TU1, '2026-11-18T13:00:00+00:00', '2026-11-18T13:03:00+00:00', N'ACUMULACION', N'1', N'T2-CAM10-20261118T130000Z'),
    -- 18/11 16:00-16:02 local (T2), sin mangas
    (@PC10, @TU2, '2026-11-18T19:00:00+00:00', '2026-11-18T19:02:00+00:00', N'SIN_MANGAS', N'1', N'T2-CAM10-20261118T190000Z'),
    -- 19/11 02:10-02:25 local (T3 del 18, después de medianoche): generó la detención de cámara 9
    (@PC10, @TU3, '2026-11-19T05:10:00+00:00', '2026-11-19T05:25:00+00:00', N'PARADA', N'1', N'T2-CAM10-20261119T051000Z'),
    -- 19/11 03:00-03:05 local (T3 del 18)
    (@PC10, @TU3, '2026-11-19T06:00:00+00:00', '2026-11-19T06:05:00+00:00', N'PARADA', N'1', N'T2-CAM10-20261119T060000Z'),
    -- 19/11 05:30-05:34 local, materializada en T3 (excepción 05-06 → T1 del 19)
    (@PC10, @TU3, '2026-11-19T08:30:00+00:00', '2026-11-19T08:34:00+00:00', N'PARADA', N'1', N'T2-CAM10-20261119T083000Z'),
    -- 19/11 12:00-12:09 local, sin turno (NULL)
    (@PC09, NULL, '2026-11-19T15:00:00+00:00', '2026-11-19T15:09:00+00:00', N'PARADA', N'1,2', N'T2-CAM09-20261119T150000Z'),
    -- 19/11 15:00-15:12 local (T2): generó la detención de cámara 8
    (@PC10, @TU2, '2026-11-19T18:00:00+00:00', '2026-11-19T18:12:00+00:00', N'PARADA', N'1', N'T2-CAM10-20261119T180000Z')
) p(Punto, Turno, Ini, Fin, Tipo, Carriles, Ident);

-- ---------------------------------------------------------------- Conteos por minuto (UTC), 2 días
;WITH n AS (
    SELECT TOP (2880) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS i
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
), m AS (
    SELECT DATEADD(MINUTE, i, CAST('2026-11-18T08:00:00+00:00' AS DATETIMEOFFSET)) AS Minuto FROM n
), c AS (
    SELECT @PC09 AS Punto, v.Carril FROM (VALUES (1), (2), (3), (4)) v(Carril)
    UNION ALL SELECT @PC10, 1
), x AS (
    SELECT m.Minuto, c.Punto, c.Carril,
           CAST(SWITCHOFFSET(m.Minuto, '-03:00') AS TIME) AS HoraLocal,
           CAST(SWITCHOFFSET(m.Minuto, '-03:00') AS DATE) AS FechaLocal,
           CASE WHEN EXISTS (SELECT 1 FROM mnt.ParadasCamara p
                             WHERE p.PuntoControlCalidadId = c.Punto AND m.Minuto >= p.FechaHoraInicio AND m.Minuto < p.FechaHoraFin
                               AND p.Tipo <> N'ACUMULACION') THEN 1 ELSE 0 END AS Parado,
           CASE WHEN EXISTS (SELECT 1 FROM mnt.ParadasCamara p
                             WHERE p.PuntoControlCalidadId = c.Punto AND m.Minuto >= p.FechaHoraInicio AND m.Minuto < p.FechaHoraFin
                               AND p.Tipo = N'ACUMULACION') THEN 1 ELSE 0 END AS Acum
    FROM m CROSS JOIN c
)
INSERT INTO mnt.ConteosCamaraMinuto (Minuto, LineaId, PuntoControlCalidadId, TurnoId, Carril, Rollos, SegundosMarcha, SegundosParada,
    SegundosAcumulacion, SegundosSinSenal, Estado, FechaHoraCreacion)
SELECT x.Minuto, @T2, x.Punto,
       CASE
           WHEN x.Punto = @PC10 AND x.FechaLocal = '2026-11-19' AND x.HoraLocal >= '05:00' AND x.HoraLocal < '06:00' THEN @TU3
           WHEN x.Punto = @PC09 AND x.FechaLocal = '2026-11-19' AND x.HoraLocal >= '12:00' AND x.HoraLocal < '12:10' THEN NULL
           WHEN x.HoraLocal >= '05:00' AND x.HoraLocal < '13:00' THEN @TU1
           WHEN x.HoraLocal >= '13:00' AND x.HoraLocal < '21:00' THEN @TU2
           ELSE @TU3 END,
       x.Carril,
       CASE WHEN x.Parado = 1 OR x.Acum = 1 THEN 0
            WHEN x.Punto = @PC10 THEN 19 + (DATEPART(MINUTE, x.Minuto) % 4)
            ELSE 20 + ((DATEPART(MINUTE, x.Minuto) + x.Carril) % 5) END,
       CASE WHEN x.Parado = 1 OR x.Acum = 1 THEN 0 ELSE 60 END,
       CASE WHEN x.Parado = 1 THEN 60 ELSE 0 END,
       CASE WHEN x.Acum = 1 THEN 60 ELSE 0 END,
       0,
       CASE WHEN x.Parado = 1 THEN N'PARADA' WHEN x.Acum = 1 THEN N'ACUMULACION' ELSE N'MARCHA' END,
       DATEADD(SECOND, 65, x.Minuto)
FROM x;

-- ---------------------------------------------------------------- Alertas de cámara (UTC)
INSERT INTO mnt.AlertasCamara (PuntoControlCalidadId, LineaId, Tipo, FechaHoraInicio, FechaHoraFin, IdentificadorExterno, Detalle, FechaHoraCreacion, FechaHoraActualizacion)
VALUES (@PC09, @T2, N'CONGELADA', '2026-11-18T17:00:00+00:00', '2026-11-18T17:05:00+00:00', N'T2-CAM09-ALERTA-20261118T170000Z', N'Imagen congelada 5 min (prueba)', '2026-11-18T17:00:30+00:00', '2026-11-18T17:05:10+00:00'),
       (@PC10, @T2, N'SIN_VIDEO', '2026-11-20T01:00:00+00:00', NULL, N'T2-CAM10-ALERTA-20261120T010000Z', N'Sin video (prueba, abierta)', '2026-11-20T01:00:30+00:00', '2026-11-20T01:00:30+00:00');

-- ---------------------------------------------------------------- Detenciones
-- Hora manual con -03:00; cámara en UTC.
INSERT INTO mnt.Detenciones (Codigo, LineaId, EquipoId, TipoFallaId, DetalleFallaId, TurnoId, Fecha, MinutosDeclarados, FechaHoraParada,
    FechaHoraArranque, OrigenTiempo, MinutosEfectivos, Descripcion, EstadoActual, FechaHoraCierre, CerradoPorUsuarioId,
    FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId, Eliminada, FechaHoraEliminacion,
    EliminadaPorUsuarioId, MotivoEliminacion, UsuarioLineaId, OrigenRegistro, IdentificadorParadaCamara, PuntoControlCalidadId,
    FechaHoraArranqueDetectada)
SELECT d.Codigo, d.Linea, d.Equipo, d.Tipo, NULL, d.Turno, d.Fecha, d.MinDecl, d.Parada, d.Arranque,
       CASE WHEN d.Parada IS NULL THEN 'DECLARADO' ELSE 'CALCULADO' END, d.MinEf, d.Descr, d.Estado,
       CASE WHEN d.Estado = N'CERRADA' THEN DATEADD(HOUR, 2, d.Creada) END,
       CASE WHEN d.Estado = N'CERRADA' THEN @UMnt END,
       d.Creada, d.Creada, d.Creador, d.Creador,
       d.Elim, CASE WHEN d.Elim = 1 THEN DATEADD(HOUR, 1, d.Creada) END, CASE WHEN d.Elim = 1 THEN @USup END,
       CASE WHEN d.Elim = 1 THEN N'Cargada dos veces (prueba)' END,
       d.UL, d.Origen, d.IdCam, CASE WHEN d.IdCam IS NOT NULL THEN @PC10 END,
       CASE WHEN d.IdCam IS NOT NULL THEN d.Arranque END
FROM (VALUES
 -- 1: manual con hora, T1 18/11
 (N'DET-20261118-000001', @T2, @EqCor, @TfMec, @TU1, CAST('2026-11-18' AS DATE), 25, CAST('2026-11-18T07:10:00-03:00' AS DATETIMEOFFSET), CAST('2026-11-18T07:35:00-03:00' AS DATETIMEOFFSET), 25, N'Atasco en la cortadora', N'REGISTRADA', CAST('2026-11-18T07:40:00-03:00' AS DATETIMEOFFSET), @UOp1, 0, @ULOp1, 'MANUAL', CAST(NULL AS NVARCHAR(100))),
 -- 2: manual sin hora, T2 18/11
 (N'DET-20261118-000002', @T2, @EqEmp, @TfMat, @TU2, '2026-11-18', 15, NULL, NULL, 15, N'Falta de film', N'REGISTRADA', '2026-11-18T18:00:00-03:00', @UOp2, 0, @ULOp2, 'MANUAL', NULL),
 -- 3: manual T3 18/11 que cruza medianoche (23:40-00:20)
 (N'DET-20261118-000003', @T2, @EqDes, @TfEle, @TU3, '2026-11-18', 40, '2026-11-18T23:40:00-03:00', '2026-11-19T00:20:00-03:00', 40, N'Corte del variador del desenrollador', N'CERRADA', '2026-11-19T00:30:00-03:00', @UOp1, 0, @ULOp1, 'MANUAL', NULL),
 -- 4: manual T3 después de medianoche (01:30 del 19), Fecha tipeada = 19 (la producción es del 18)
 (N'DET-20261119-000001', @T2, @EqCor, @TfAju, @TU3, '2026-11-19', 20, '2026-11-19T01:30:00-03:00', '2026-11-19T01:50:00-03:00', 20, N'Ajuste de cuchilla', N'REGISTRADA', '2026-11-19T02:00:00-03:00', @UOp1, 0, @ULOp1, 'MANUAL', NULL),
 -- 5: manual 05:20 del 19 materializada en T3 (horario viejo 21-06), Fecha 18 → excepción: T1 del 19
 (N'DET-20261119-000002', @T2, @EqEmp, @TfMec, @TU3, '2026-11-18', 25, '2026-11-19T05:20:00-03:00', '2026-11-19T05:45:00-03:00', 25, N'Empaquetadora trabada al cierre del turno noche', N'REGISTRADA', '2026-11-19T05:50:00-03:00', @UOp1, 0, @ULOp1, 'MANUAL', NULL),
 -- 6: manual T3 sin hora, 18/11, en seguimiento
 (N'DET-20261118-000004', @T2, @EqDes, @TfMec, @TU3, '2026-11-18', 30, NULL, NULL, 30, N'Ruido en rodamiento', N'EN_SEGUIMIENTO', '2026-11-19T04:00:00-03:00', @UOp2, 0, @ULOp2, 'MANUAL', NULL),
 -- 7: manual eliminada lógicamente, T1 19/11
 (N'DET-20261119-000003', @T2, @EqCor, @TfMec, @TU1, '2026-11-19', 10, '2026-11-19T08:00:00-03:00', '2026-11-19T08:10:00-03:00', 10, N'Duplicada', N'REGISTRADA', '2026-11-19T08:15:00-03:00', @UOp1, 1, @ULOp1, 'MANUAL', NULL),
 -- 8: de cámara 10, T2 19/11 15:00-15:12 local (UTC), pendiente de justificar
 (N'DET-20261119-000004', @T2, @EqDes, NULL, @TU2, '2026-11-19', NULL, '2026-11-19T18:00:00+00:00', '2026-11-19T18:12:00+00:00', 12, N'Detención automática por cámara T2-CAM10', N'PENDIENTE_JUSTIFICACION', '2026-11-19T18:12:30+00:00', N'SYSTEM-CAMARA', 0, NULL, 'CAMARA', N'T2-CAM10-20261119T180000Z'),
 -- 9: de cámara 10, T3 del 18 a las 02:10 del 19 (UTC), Fecha = la de la parada (19), justificada
 (N'DET-20261119-000005', @T2, @EqDes, @TfMat, @TU3, '2026-11-19', NULL, '2026-11-19T05:10:00+00:00', '2026-11-19T05:25:00+00:00', 15, N'Falta de bobina madre (cámara)', N'REGISTRADA', '2026-11-19T05:25:30+00:00', N'SYSTEM-CAMARA', 0, @ULOp1, 'CAMARA', N'T2-CAM10-20261119T051000Z'),
 -- 10: TANGO 1, manual con hora, T2 19/11
 (N'DET-20261119-000006', @T1L, @EqT1, @TfCpr, @TU2, '2026-11-19', 45, '2026-11-19T14:00:00-03:00', '2026-11-19T14:45:00-03:00', 45, N'Cambio de producto', N'REGISTRADA', '2026-11-19T14:50:00-03:00', @UOp2, 0, @ULOp2T1, 'MANUAL', NULL),
 -- 11: FUTURA 2, manual sin hora y sin maquinista, T1 18/11
 (N'DET-20261118-000005', @F2, @EqF2, @TfMec, @TU1, '2026-11-18', 35, NULL, NULL, 35, N'Plegadora desalineada', N'REGISTRADA', '2026-11-18T12:00:00-03:00', @UOp3, 0, NULL, 'MANUAL', NULL),
 -- 12: TANGO 2, manual con hora, T2 19/11, sin maquinista
 (N'DET-20261119-000007', @T2, @EqCor, @TfAju, @TU2, '2026-11-19', 30, '2026-11-19T19:00:00-03:00', '2026-11-19T19:30:00-03:00', 30, N'Ajuste de perforado', N'REGISTRADA', '2026-11-19T19:35:00-03:00', @USup, 0, NULL, 'MANUAL', NULL)
) d(Codigo, Linea, Equipo, Tipo, Turno, Fecha, MinDecl, Parada, Arranque, MinEf, Descr, Estado, Creada, Creador, Elim, UL, Origen, IdCam);

-- Historial: alta de cada una + cambios
INSERT INTO mnt.DetencionesEstadoHistorial (DetencionId, EstadoAnterior, EstadoNuevo, FechaHora, UsuarioId, Observacion, OrigenAccion)
SELECT DetencionId, NULL, CASE WHEN OrigenRegistro = 'CAMARA' THEN N'PENDIENTE_JUSTIFICACION' ELSE N'REGISTRADA' END,
       FechaHoraCreacion, CreadoPorUsuarioId, N'Alta', CASE WHEN OrigenRegistro = 'CAMARA' THEN N'CAMARA' ELSE N'WEB' END
FROM mnt.Detenciones;
INSERT INTO mnt.DetencionesEstadoHistorial (DetencionId, EstadoAnterior, EstadoNuevo, FechaHora, UsuarioId, Observacion, OrigenAccion)
SELECT DetencionId, N'PENDIENTE_JUSTIFICACION', N'REGISTRADA', DATEADD(MINUTE, 20, FechaHoraCreacion), @UOp1, N'Justificada por el operario', N'WEB'
FROM mnt.Detenciones WHERE Codigo = N'DET-20261119-000005'
UNION ALL
SELECT DetencionId, N'REGISTRADA', N'EN_SEGUIMIENTO', DATEADD(HOUR, 1, FechaHoraCreacion), @UMnt, N'Mantenimiento lo toma', N'WEB'
FROM mnt.Detenciones WHERE Codigo IN (N'DET-20261118-000003', N'DET-20261118-000004')
UNION ALL
SELECT DetencionId, N'EN_SEGUIMIENTO', N'CERRADA', DATEADD(HOUR, 2, FechaHoraCreacion), @UMnt, N'Variador reemplazado', N'WEB'
FROM mnt.Detenciones WHERE Codigo = N'DET-20261118-000003';

INSERT INTO mnt.DetencionesNotas (DetencionId, Texto, UsuarioId, FechaHora, TipoNota, EsNotaDeCierre)
SELECT DetencionId, N'Se revisó el variador: falla de la fuente.', @UMnt, DATEADD(MINUTE, 90, FechaHoraCreacion), 'Tecnica', 0
FROM mnt.Detenciones WHERE Codigo = N'DET-20261118-000003'
UNION ALL
SELECT DetencionId, N'Cerrada con repuesto de pañol.', @UMnt, DATEADD(HOUR, 2, FechaHoraCreacion), 'Cierre', 1
FROM mnt.Detenciones WHERE Codigo = N'DET-20261118-000003'
UNION ALL
SELECT DetencionId, N'El maquinista avisa que vuelve a pasar en el T3.', @UOp1, DATEADD(MINUTE, 5, FechaHoraCreacion), 'Operativa', 0
FROM mnt.Detenciones WHERE Codigo = N'DET-20261119-000002';

-- ---------------------------------------------------------------- Control de Proceso
INSERT INTO mnt.RegistrosProceso (LineaId, ProductoId, TurnoId, Fecha, UsuarioId, FechaHoraInicio, FechaHoraFin, Estado,
    FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId)
VALUES (@T2, @Prod, @TU1, '2026-11-18', @UOp1, '2026-11-18T06:00:00-03:00', '2026-11-18T06:20:00-03:00', 'COMPLETO', '2026-11-18T06:00:00-03:00', '2026-11-18T06:20:00-03:00', @UOp1, @UOp1),
       (@T2, @Prod, @TU2, '2026-11-18', @UOp2, '2026-11-18T14:00:00-03:00', NULL, 'PARCIAL', '2026-11-18T14:00:00-03:00', '2026-11-18T14:10:00-03:00', @UOp2, @UOp2),
       -- T3 del 18 cargado después de medianoche
       (@T2, @Prod, @TU3, '2026-11-18', @UOp1, '2026-11-19T00:30:00-03:00', '2026-11-19T00:45:00-03:00', 'CERRADO', '2026-11-19T00:30:00-03:00', '2026-11-19T00:45:00-03:00', @UOp1, @UOp1),
       -- 05:30 del 19 materializado en T3 con Fecha 18 (excepción → T1 del 19)
       (@T2, NULL, @TU3, '2026-11-18', @UOp1, '2026-11-19T05:30:00-03:00', '2026-11-19T05:40:00-03:00', 'COMPLETO', '2026-11-19T05:30:00-03:00', '2026-11-19T05:40:00-03:00', @UOp1, @UOp1),
       (@T2, @Prod, @TU2, '2026-11-19', @UOp2, '2026-11-19T15:00:00-03:00', '2026-11-19T15:30:00-03:00', 'COMPLETO', '2026-11-19T15:00:00-03:00', '2026-11-19T15:30:00-03:00', @UOp2, @UOp2);

INSERT INTO mnt.RegistrosProcesoDetalle (RegistroProcesoId, ParametroId, ValorEstandarSnapshot, ValorReal, ValorRealTexto, FueraDeEstandar, Observacion, FechaHoraRegistro, EstandarConRango)
SELECT r.RegistroProcesoId, p.ParametroId, e.ValorEstandarTexto,
       CASE WHEN p.TipoDato = 'NUMERICO' THEN CAST(10 + p.ParametroId % 7 AS DECIMAL(18,4)) END,
       CASE WHEN p.TipoDato <> 'NUMERICO' THEN N'Sin novedad' END,
       CASE WHEN p.ParametroId % 11 = 0 THEN 1 ELSE 0 END, NULL, DATEADD(MINUTE, 5, r.FechaHoraInicio),
       CASE WHEN r.RegistroProcesoId % 2 = 0 THEN CASE WHEN e.ValorMin IS NOT NULL OR e.ValorMax IS NOT NULL THEN 1 ELSE 0 END END
FROM mnt.RegistrosProceso r
JOIN mnt.Parametros p ON p.ParametroId IN (SELECT TOP (8) ParametroId FROM mnt.Parametros ORDER BY ParametroId DESC)
LEFT JOIN mnt.EstandaresParametro e ON e.LineaId = r.LineaId AND e.ProductoId = @Prod AND e.ParametroId = p.ParametroId;

-- ---------------------------------------------------------------- Calidad
INSERT INTO mnt.RegistrosCalidad (FechaHoraDeteccion, LineaId, PuntoControlCalidadId, TurnoId, TipoDefectoCalidadId, Origen, Confianza,
    ValorMedido, UnidadMedida, ProductoId, DetencionId, Observaciones, IdentificadorExterno, EstadoRevision, RevisadoPorUsuarioId,
    FechaHoraRevision, FechaHoraCreacion, FechaHoraModificacion, CreadoPorUsuarioId, ModificadoPorUsuarioId)
VALUES ('2026-11-18T09:00:00-03:00', @T2, @PCC1, @TU1, @DefFlojo, N'MANUAL', NULL, NULL, NULL, @Prod, NULL, N'Rollo flojo en carril 2', NULL, N'CONFIRMADO', @USup, '2026-11-18T10:00:00-03:00', '2026-11-18T09:00:00-03:00', '2026-11-18T10:00:00-03:00', @UOp1, @USup),
       ('2026-11-18T22:30:00-03:00', @T2, @PCC1, @TU3, @DefDiam, N'MANUAL', NULL, 121.5, N'mm', @Prod, NULL, NULL, NULL, N'PENDIENTE', NULL, NULL, '2026-11-18T22:30:00-03:00', '2026-11-18T22:30:00-03:00', @UOp1, @UOp1),
       -- T3 del 18 después de medianoche
       ('2026-11-19T00:40:00-03:00', @T2, @PCC1, @TU3, @DefFlojo, N'MANUAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'FALSO_POSITIVO', @USup, '2026-11-19T07:00:00-03:00', '2026-11-19T00:40:00-03:00', '2026-11-19T07:00:00-03:00', @UOp1, @USup),
       -- sin turno
       ('2026-11-19T14:00:00-03:00', @T2, @PCC1, NULL, @DefDiam, N'MANUAL', NULL, 118, N'mm', @Prod, NULL, NULL, NULL, N'PENDIENTE', NULL, NULL, '2026-11-19T14:00:00-03:00', '2026-11-19T14:00:00-03:00', @UOp2, @UOp2),
       -- automático de la cámara 09 (UTC), ligado a la detención de cámara 8
       ('2026-11-19T13:00:00+00:00', @T2, @PC09, @TU1, @DefFlojo, N'AUTOMATICO', 0.87, NULL, NULL, @Prod, NULL, NULL, N'T2-CAM09-CAL-20261119T130000Z', N'PENDIENTE', NULL, NULL, '2026-11-19T13:00:05+00:00', '2026-11-19T13:00:05+00:00', N'SYSTEM-CAMARA', N'SYSTEM-CAMARA');

INSERT INTO mnt.RegistrosCalidadRevisiones (RegistroCalidadId, EstadoAnterior, EstadoNuevo, Comentario, UsuarioId, FechaHora)
SELECT RegistroCalidadId, N'PENDIENTE', EstadoRevision, N'Revisado (prueba)', RevisadoPorUsuarioId, FechaHoraRevision
FROM mnt.RegistrosCalidad WHERE EstadoRevision <> N'PENDIENTE';

COMMIT;

PRINT N'seed DEV cargado:';
SELECT 'Detenciones' AS Tabla, COUNT(*) AS Filas FROM mnt.Detenciones
UNION ALL SELECT 'DetencionesEstadoHistorial', COUNT(*) FROM mnt.DetencionesEstadoHistorial
UNION ALL SELECT 'DetencionesNotas', COUNT(*) FROM mnt.DetencionesNotas
UNION ALL SELECT 'ConteosCamaraMinuto', COUNT(*) FROM mnt.ConteosCamaraMinuto
UNION ALL SELECT 'ParadasCamara', COUNT(*) FROM mnt.ParadasCamara
UNION ALL SELECT 'AlertasCamara', COUNT(*) FROM mnt.AlertasCamara
UNION ALL SELECT 'RegistrosProceso', COUNT(*) FROM mnt.RegistrosProceso
UNION ALL SELECT 'RegistrosProcesoDetalle', COUNT(*) FROM mnt.RegistrosProcesoDetalle
UNION ALL SELECT 'RegistrosCalidad', COUNT(*) FROM mnt.RegistrosCalidad
UNION ALL SELECT 'UsuariosLineas', COUNT(*) FROM mnt.UsuariosLineas
UNION ALL SELECT 'AspNetUsers', COUNT(*) FROM dbo.AspNetUsers;
GO
SET NOEXEC OFF;
GO
