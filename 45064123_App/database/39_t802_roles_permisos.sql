-- ==============================================================================
-- Script: 39_t802_roles_permisos.sql
-- Descripción: T-802 parte 1 · Roles y permisos configurables.
--   Crea (esquema mnt):
--     CFG_Opciones            catálogo de opciones del sistema (árbol = secciones del menú). No se edita desde la app.
--     CFG_PermisosRol         RoleId (AspNetRoles) + OpcionId + PuedeVer/PuedeAlta/PuedeModificar/PuedeBaja. Único (rol, opción).
--     CFG_RolesConfiguracion  RoleId (PK/FK) + ObligadoATurno + Descripcion.
--   Carga el catálogo y la MATRIZ MIGRADA: los permisos equivalentes a lo que hasta T-798 decía el código (políticas,
--   [Authorize(Roles=)], menú y chequeos por rol); ObligadoATurno = 1 sólo para OPERARIO. Ver docs/PERMISOS.md.
--   Mismos datos que CatalogoPermisos.cs (lo verifica un test de integración contra SQL Server) y que la semilla de la API.
--   No toca las tablas de Identity. Los cambios del configurador se auditan en mnt.Auditoria (CambioRol).
-- Versión: 1.0
-- Requiere: 03 y 08 (AspNetRoles con los 5 roles) y 31 (esquema mnt de T-798).
-- Idempotente: crea sólo lo que falta; las opciones se insertan o actualizan por código (nombre, padre, ruta, orden,
--   acciones aplicables); los permisos sólo se insertan para los pares (rol, opción) que no existen, así que nunca pisa
--   lo que se editó desde Configuración → Roles y permisos.
-- Correr con la API detenida, ANTES de publicar la API de T-802 (la API nueva no autoriza nada sin estas tablas).
-- Correr con: sqlcmd -C -I -b -f 65001 (los acentos van con NCHAR igual).
-- Deshacer: 39_t802_roles_permisos_rollback.sql.
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF SCHEMA_ID(N'mnt') IS NULL OR OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
BEGIN
    RAISERROR(N'Falta el esquema mnt o la tabla dbo.AspNetRoles (scripts 02, 03 y 08).', 16, 1);
    SET NOEXEC ON;
END
GO

-- ----------------------------------------------------------------------------- tablas
IF OBJECT_ID(N'mnt.CFG_Opciones', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CFG_Opciones] (
        [OpcionId]           INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CFG_Opciones] PRIMARY KEY,
        [Codigo]             NVARCHAR(100) NOT NULL,
        [Nombre]             NVARCHAR(150) NOT NULL,
        [PadreId]            INT NULL CONSTRAINT [FK_CFG_Opciones_Padre] REFERENCES [mnt].[CFG_Opciones] ([OpcionId]),
        [Ruta]               NVARCHAR(200) NULL,
        [Orden]              INT NOT NULL,
        [AccionesAplicables] NVARCHAR(50) NOT NULL,
        [Activo]             BIT NOT NULL CONSTRAINT [DF_CFG_Opciones_Activo] DEFAULT (1)
    );
    CREATE UNIQUE INDEX [UX_CFG_Opciones_Codigo] ON [mnt].[CFG_Opciones] ([Codigo]);
    CREATE INDEX [IX_CFG_Opciones_PadreId] ON [mnt].[CFG_Opciones] ([PadreId]);
    PRINT 'Tabla mnt.CFG_Opciones creada.'
END
ELSE
    PRINT 'mnt.CFG_Opciones ya existe.'
GO

IF OBJECT_ID(N'mnt.CFG_PermisosRol', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CFG_PermisosRol] (
        [PermisoRolId]           INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CFG_PermisosRol] PRIMARY KEY,
        [RoleId]                 NVARCHAR(450) NOT NULL CONSTRAINT [FK_CFG_PermisosRol_Rol] REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        [OpcionId]               INT NOT NULL CONSTRAINT [FK_CFG_PermisosRol_Opcion] REFERENCES [mnt].[CFG_Opciones] ([OpcionId]),
        [PuedeVer]               BIT NOT NULL,
        [PuedeAlta]              BIT NOT NULL,
        [PuedeModificar]         BIT NOT NULL,
        [PuedeBaja]              BIT NOT NULL,
        [FechaHoraModificacion]  DATETIMEOFFSET(7) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL
    );
    CREATE UNIQUE INDEX [UX_CFG_PermisosRol_Rol_Opcion] ON [mnt].[CFG_PermisosRol] ([RoleId], [OpcionId]);
    CREATE INDEX [IX_CFG_PermisosRol_OpcionId] ON [mnt].[CFG_PermisosRol] ([OpcionId]);
    PRINT 'Tabla mnt.CFG_PermisosRol creada.'
END
ELSE
    PRINT 'mnt.CFG_PermisosRol ya existe.'
GO

IF OBJECT_ID(N'mnt.CFG_RolesConfiguracion', N'U') IS NULL
BEGIN
    CREATE TABLE [mnt].[CFG_RolesConfiguracion] (
        [RoleId]                 NVARCHAR(450) NOT NULL CONSTRAINT [PK_CFG_RolesConfiguracion] PRIMARY KEY
                                 CONSTRAINT [FK_CFG_RolesConfiguracion_Rol] REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        [ObligadoATurno]         BIT NOT NULL,
        [Descripcion]            NVARCHAR(300) NULL,
        [FechaHoraModificacion]  DATETIMEOFFSET(7) NOT NULL,
        [ModificadoPorUsuarioId] NVARCHAR(450) NOT NULL
    );
    PRINT 'Tabla mnt.CFG_RolesConfiguracion creada.'
END
ELSE
    PRINT 'mnt.CFG_RolesConfiguracion ya existe.'
GO

-- ----------------------------------------------------------------------------- catálogo y matriz migrada
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'tempdb..#Opciones') IS NOT NULL DROP TABLE #Opciones;
IF OBJECT_ID(N'tempdb..#Matriz') IS NOT NULL DROP TABLE #Matriz;
-- Las tablas temporales toman la intercalación de tempdb: COLLATE DATABASE_DEFAULT para poder compararlas con las de la base.
CREATE TABLE #Opciones (Codigo NVARCHAR(100) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY, Nombre NVARCHAR(150) COLLATE DATABASE_DEFAULT NOT NULL, PadreCodigo NVARCHAR(100) COLLATE DATABASE_DEFAULT NULL,
    Ruta NVARCHAR(200) COLLATE DATABASE_DEFAULT NULL, Orden INT NOT NULL, AccionesAplicables NVARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL);
CREATE TABLE #Matriz (Rol NVARCHAR(256) COLLATE DATABASE_DEFAULT NOT NULL, Codigo NVARCHAR(100) COLLATE DATABASE_DEFAULT NOT NULL, PuedeVer BIT NOT NULL, PuedeAlta BIT NOT NULL,
    PuedeModificar BIT NOT NULL, PuedeBaja BIT NOT NULL, PRIMARY KEY (Rol, Codigo));

-- Generado desde PSAG.Mantenimiento.Domain/Permisos/CatalogoPermisos.cs (no editar a mano sin cambiar también el catálogo).
INSERT INTO #Opciones (Codigo, Nombre, PadreCodigo, Ruta, Orden, AccionesAplicables) VALUES
    (N'GENERAL', N'General (sin men' + NCHAR(250) + N')', NULL, NULL, 5, N''),
    (N'GENERAL.CATALOGOS', N'Consultas comunes (listas de l' + NCHAR(237) + N'neas, equipos, fallas, turnos, productos y operadores)', N'GENERAL', NULL, 1, N'VER'),
    (N'DASHBOARD', N'Dashboard', NULL, NULL, 10, N''),
    (N'DASHBOARD.INICIO', N'Inicio', N'DASHBOARD', N'', 1, N'VER'),
    (N'DASHBOARD.REPORTES', N'Reportes', N'DASHBOARD', N'dashboard/reportes', 2, N'VER'),
    (N'DASHBOARD.HORAS_PROGRAMADAS', N'Horas programadas', N'DASHBOARD', N'configuracion/horas-programadas', 3, N'VER,ALTA,BAJA'),
    (N'MAESTROS', N'Maestros', NULL, NULL, 20, N''),
    (N'MAESTROS.LINEAS', N'L' + NCHAR(237) + N'neas', N'MAESTROS', N'maestros/lineas', 1, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.EQUIPOS', N'Equipos', N'MAESTROS', N'maestros/equipos', 2, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.TIPOS_FALLA', N'Tipos de falla', N'MAESTROS', N'maestros/tipos-falla', 3, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.FALLAS', N'Fallas', N'MAESTROS', N'maestros/fallas', 4, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.REGLAS_ANALISIS', N'Reglas de an' + NCHAR(225) + N'lisis', N'MAESTROS', N'maestros/reglas-analisis', 5, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.CONTROL_PROCESO', N'Puntos de control de proceso', N'MAESTROS', N'maestros/control-proceso', 6, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.INSPECCIONES', N'Planes de inspecci' + NCHAR(243) + N'n', N'MAESTROS', N'maestros/inspecciones', 7, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.PRODUCTOS', N'Productos', N'MAESTROS', N'maestros/productos', 8, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.PARAMETROS', N'Par' + NCHAR(225) + N'metros l' + NCHAR(237) + N'nea + producto', N'MAESTROS', N'maestros/parametros-linea-producto', 9, N'VER,ALTA,MODIFICACION'),
    (N'MAESTROS.CAMARAS', N'C' + NCHAR(225) + N'maras', N'MAESTROS', N'maestros/camaras', 10, N'VER,ALTA,MODIFICACION,BAJA'),
    (N'MAESTROS.CALIDAD', N'Calidad (puntos y defectos)', N'MAESTROS', N'maestros/calidad', 11, N'VER,ALTA,MODIFICACION'),
    (N'DETENCIONES', N'Detenciones', NULL, NULL, 30, N''),
    (N'DETENCIONES.LISTADO', N'Ver detenciones', N'DETENCIONES', N'detenciones', 1, N'VER,ALTA,MODIFICACION,BAJA'),
    (N'DETENCIONES.PENDIENTES', N'Mis pendientes (an' + NCHAR(225) + N'lisis de falla)', N'DETENCIONES', N'detenciones/pendientes', 2, N'VER,MODIFICACION'),
    (N'DETENCIONES.SOLUCIONES', N'Base de soluciones', N'DETENCIONES', N'detenciones/soluciones', 3, N'VER'),
    (N'DETENCIONES.DECLARAR_MANUAL', N'Declarar detenci' + NCHAR(243) + N'n manual', N'DETENCIONES', N'detenciones/declarar-manual', 4, N'VER'),
    (N'DETENCIONES.SEGUIMIENTO', N'Seguimiento y cierre de detenciones', N'DETENCIONES', NULL, 5, N'MODIFICACION'),
    (N'DETENCIONES.NOTAS', N'Notas en detenciones', N'DETENCIONES', NULL, 6, N'ALTA'),
    (N'DETENCIONES.ANALISIS_CERRADO', N'Corregir an' + NCHAR(225) + N'lisis cerrado', N'DETENCIONES', NULL, 7, N'MODIFICACION'),
    (N'PRODUCCION', N'Producci' + NCHAR(243) + N'n', NULL, NULL, 40, N''),
    (N'PRODUCCION.INICIO_TURNO', N'Inicio de turno', N'PRODUCCION', N'produccion/inicio-turno', 1, N'VER'),
    (N'PRODUCCION.OPERACION_TURNO', N'Operar el turno (tomar, cambiar producto, relevar, cerrar)', N'PRODUCCION', NULL, 2, N'VER,ALTA,MODIFICACION'),
    (N'PRODUCCION.TURNOS', N'Turnos de producci' + NCHAR(243) + N'n (corregir turno cerrado y producci' + NCHAR(243) + N'n)', N'PRODUCCION', N'produccion/turnos', 3, N'VER,MODIFICACION'),
    (N'PRODUCCION.PRESUPUESTO_REAL', N'Presupuesto vs. real', N'PRODUCCION', N'dashboard/produccion', 4, N'VER'),
    (N'TAREAS', N'Tareas de mantenimiento', NULL, NULL, 50, N''),
    (N'TAREAS.PENDIENTES', N'Pendientes y programadas', N'TAREAS', N'tareas-mantenimiento', 1, N'VER,ALTA,MODIFICACION'),
    (N'TAREAS.HISTORIAL', N'Historial', N'TAREAS', N'tareas-mantenimiento/historial', 2, N'VER'),
    (N'CONTROL_PROCESO', N'Control de Proceso', NULL, NULL, 60, N''),
    (N'CONTROL_PROCESO.REGISTROS', N'Controles de proceso', N'CONTROL_PROCESO', N'control-proceso', 1, N'VER,ALTA,MODIFICACION'),
    (N'INSPECCIONES', N'Inspecciones', NULL, NULL, 70, N''),
    (N'INSPECCIONES.EJECUCIONES', N'Inspecciones', N'INSPECCIONES', N'inspecciones', 1, N'VER,ALTA,MODIFICACION'),
    (N'INSPECCIONES.NO_OK', N'Panel NO OK', N'INSPECCIONES', N'inspecciones/hallazgos', 2, N'VER,MODIFICACION'),
    (N'CALIDAD', N'Calidad', NULL, NULL, 80, N''),
    (N'CALIDAD.REGISTRO', N'Registro de defectos', N'CALIDAD', N'calidad', 1, N'VER,ALTA,MODIFICACION'),
    (N'CALIDAD.PARADAS_CAMARA', N'Paradas y producci' + NCHAR(243) + N'n (c' + NCHAR(225) + N'mara)', N'CALIDAD', N'paradas-camara', 2, N'VER'),
    (N'CONFIG', N'Configuraci' + NCHAR(243) + N'n', NULL, NULL, 90, N''),
    (N'CONFIG.TURNOS', N'Turnos', N'CONFIG', N'configuracion/turnos', 1, N'VER,ALTA,MODIFICACION'),
    (N'CONFIG.MAQUINISTAS', N'Maquinistas (consulta)', N'CONFIG', N'configuracion/maquinistas', 2, N'VER'),
    (N'CONFIG.USUARIOS', N'Usuarios', N'CONFIG', N'configuracion/usuarios', 3, N'VER,ALTA,MODIFICACION,BAJA'),
    (N'CONFIG.ROLES', N'Roles y permisos', N'CONFIG', N'configuracion/roles', 4, N'VER,MODIFICACION');

INSERT INTO #Matriz (Rol, Codigo, PuedeVer, PuedeAlta, PuedeModificar, PuedeBaja) VALUES
    (N'ADMINISTRADOR', N'GENERAL.CATALOGOS', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'GENERAL.CATALOGOS', 1, 0, 0, 0),
    (N'SUPERVISOR', N'GENERAL.CATALOGOS', 1, 0, 0, 0),
    (N'OPERARIO', N'GENERAL.CATALOGOS', 1, 0, 0, 0),
    (N'GERENCIA', N'GENERAL.CATALOGOS', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'DASHBOARD.INICIO', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'DASHBOARD.INICIO', 1, 0, 0, 0),
    (N'SUPERVISOR', N'DASHBOARD.INICIO', 1, 0, 0, 0),
    (N'OPERARIO', N'DASHBOARD.INICIO', 1, 0, 0, 0),
    (N'GERENCIA', N'DASHBOARD.INICIO', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'DASHBOARD.REPORTES', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'DASHBOARD.REPORTES', 1, 0, 0, 0),
    (N'SUPERVISOR', N'DASHBOARD.REPORTES', 1, 0, 0, 0),
    (N'OPERARIO', N'DASHBOARD.REPORTES', 1, 0, 0, 0),
    (N'GERENCIA', N'DASHBOARD.REPORTES', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'DASHBOARD.HORAS_PROGRAMADAS', 1, 1, 0, 1),
    (N'SUPERVISOR', N'DASHBOARD.HORAS_PROGRAMADAS', 1, 1, 0, 1),
    (N'ADMINISTRADOR', N'MAESTROS.LINEAS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.EQUIPOS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.TIPOS_FALLA', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.FALLAS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.REGLAS_ANALISIS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.CONTROL_PROCESO', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.INSPECCIONES', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.PRODUCTOS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.PARAMETROS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'MAESTROS.CAMARAS', 1, 1, 1, 1),
    (N'ADMINISTRADOR', N'MAESTROS.CALIDAD', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.LISTADO', 1, 1, 1, 1),
    (N'MANTENIMIENTO', N'DETENCIONES.LISTADO', 1, 1, 1, 0),
    (N'SUPERVISOR', N'DETENCIONES.LISTADO', 1, 1, 1, 0),
    (N'OPERARIO', N'DETENCIONES.LISTADO', 1, 1, 1, 0),
    (N'GERENCIA', N'DETENCIONES.LISTADO', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.PENDIENTES', 1, 0, 1, 0),
    (N'MANTENIMIENTO', N'DETENCIONES.PENDIENTES', 1, 0, 1, 0),
    (N'SUPERVISOR', N'DETENCIONES.PENDIENTES', 1, 0, 1, 0),
    (N'OPERARIO', N'DETENCIONES.PENDIENTES', 1, 0, 1, 0),
    (N'GERENCIA', N'DETENCIONES.PENDIENTES', 1, 0, 1, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.SOLUCIONES', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'DETENCIONES.SOLUCIONES', 1, 0, 0, 0),
    (N'SUPERVISOR', N'DETENCIONES.SOLUCIONES', 1, 0, 0, 0),
    (N'OPERARIO', N'DETENCIONES.SOLUCIONES', 1, 0, 0, 0),
    (N'GERENCIA', N'DETENCIONES.SOLUCIONES', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.DECLARAR_MANUAL', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'DETENCIONES.DECLARAR_MANUAL', 1, 0, 0, 0),
    (N'SUPERVISOR', N'DETENCIONES.DECLARAR_MANUAL', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.SEGUIMIENTO', 0, 0, 1, 0),
    (N'MANTENIMIENTO', N'DETENCIONES.SEGUIMIENTO', 0, 0, 1, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.NOTAS', 0, 1, 0, 0),
    (N'MANTENIMIENTO', N'DETENCIONES.NOTAS', 0, 1, 0, 0),
    (N'SUPERVISOR', N'DETENCIONES.NOTAS', 0, 1, 0, 0),
    (N'OPERARIO', N'DETENCIONES.NOTAS', 0, 1, 0, 0),
    (N'GERENCIA', N'DETENCIONES.NOTAS', 0, 1, 0, 0),
    (N'ADMINISTRADOR', N'DETENCIONES.ANALISIS_CERRADO', 0, 0, 1, 0),
    (N'SUPERVISOR', N'DETENCIONES.ANALISIS_CERRADO', 0, 0, 1, 0),
    (N'OPERARIO', N'PRODUCCION.INICIO_TURNO', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'PRODUCCION.OPERACION_TURNO', 1, 1, 1, 0),
    (N'MANTENIMIENTO', N'PRODUCCION.OPERACION_TURNO', 1, 1, 1, 0),
    (N'SUPERVISOR', N'PRODUCCION.OPERACION_TURNO', 1, 1, 1, 0),
    (N'OPERARIO', N'PRODUCCION.OPERACION_TURNO', 1, 1, 1, 0),
    (N'GERENCIA', N'PRODUCCION.OPERACION_TURNO', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'PRODUCCION.TURNOS', 1, 0, 1, 0),
    (N'MANTENIMIENTO', N'PRODUCCION.TURNOS', 1, 0, 1, 0),
    (N'SUPERVISOR', N'PRODUCCION.TURNOS', 1, 0, 1, 0),
    (N'ADMINISTRADOR', N'PRODUCCION.PRESUPUESTO_REAL', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'PRODUCCION.PRESUPUESTO_REAL', 1, 0, 0, 0),
    (N'SUPERVISOR', N'PRODUCCION.PRESUPUESTO_REAL', 1, 0, 0, 0),
    (N'OPERARIO', N'PRODUCCION.PRESUPUESTO_REAL', 1, 0, 0, 0),
    (N'GERENCIA', N'PRODUCCION.PRESUPUESTO_REAL', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'TAREAS.PENDIENTES', 1, 1, 1, 0),
    (N'MANTENIMIENTO', N'TAREAS.PENDIENTES', 1, 0, 1, 0),
    (N'SUPERVISOR', N'TAREAS.PENDIENTES', 1, 1, 1, 0),
    (N'OPERARIO', N'TAREAS.PENDIENTES', 1, 1, 0, 0),
    (N'GERENCIA', N'TAREAS.PENDIENTES', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'TAREAS.HISTORIAL', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'TAREAS.HISTORIAL', 1, 0, 0, 0),
    (N'SUPERVISOR', N'TAREAS.HISTORIAL', 1, 0, 0, 0),
    (N'OPERARIO', N'TAREAS.HISTORIAL', 1, 0, 0, 0),
    (N'GERENCIA', N'TAREAS.HISTORIAL', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'CONTROL_PROCESO.REGISTROS', 1, 1, 1, 0),
    (N'MANTENIMIENTO', N'CONTROL_PROCESO.REGISTROS', 1, 1, 1, 0),
    (N'SUPERVISOR', N'CONTROL_PROCESO.REGISTROS', 1, 1, 0, 0),
    (N'OPERARIO', N'CONTROL_PROCESO.REGISTROS', 1, 1, 0, 0),
    (N'GERENCIA', N'CONTROL_PROCESO.REGISTROS', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'INSPECCIONES.EJECUCIONES', 1, 1, 1, 0),
    (N'MANTENIMIENTO', N'INSPECCIONES.EJECUCIONES', 1, 1, 1, 0),
    (N'SUPERVISOR', N'INSPECCIONES.EJECUCIONES', 1, 1, 1, 0),
    (N'OPERARIO', N'INSPECCIONES.EJECUCIONES', 1, 1, 1, 0),
    (N'GERENCIA', N'INSPECCIONES.EJECUCIONES', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'INSPECCIONES.NO_OK', 1, 0, 1, 0),
    (N'MANTENIMIENTO', N'INSPECCIONES.NO_OK', 1, 0, 1, 0),
    (N'SUPERVISOR', N'INSPECCIONES.NO_OK', 1, 0, 0, 0),
    (N'OPERARIO', N'INSPECCIONES.NO_OK', 1, 0, 0, 0),
    (N'GERENCIA', N'INSPECCIONES.NO_OK', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'CALIDAD.REGISTRO', 1, 1, 1, 0),
    (N'MANTENIMIENTO', N'CALIDAD.REGISTRO', 1, 0, 1, 0),
    (N'SUPERVISOR', N'CALIDAD.REGISTRO', 1, 1, 1, 0),
    (N'OPERARIO', N'CALIDAD.REGISTRO', 1, 0, 0, 0),
    (N'GERENCIA', N'CALIDAD.REGISTRO', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'CALIDAD.PARADAS_CAMARA', 1, 0, 0, 0),
    (N'MANTENIMIENTO', N'CALIDAD.PARADAS_CAMARA', 1, 0, 0, 0),
    (N'SUPERVISOR', N'CALIDAD.PARADAS_CAMARA', 1, 0, 0, 0),
    (N'OPERARIO', N'CALIDAD.PARADAS_CAMARA', 1, 0, 0, 0),
    (N'GERENCIA', N'CALIDAD.PARADAS_CAMARA', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'CONFIG.TURNOS', 1, 1, 1, 0),
    (N'ADMINISTRADOR', N'CONFIG.MAQUINISTAS', 1, 0, 0, 0),
    (N'ADMINISTRADOR', N'CONFIG.USUARIOS', 1, 1, 1, 1),
    (N'ADMINISTRADOR', N'CONFIG.ROLES', 1, 0, 1, 0);

BEGIN TRANSACTION;

DECLARE @Ahora DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();
DECLARE @Usuario NVARCHAR(450) = N'SCRIPT-39-T802';
DECLARE @OpcionesNuevas INT, @PermisosNuevos INT, @RolesNuevos INT;

-- 1) Opciones: secciones y opciones por código (inserta las que faltan; actualiza los datos del catálogo)
INSERT INTO [mnt].[CFG_Opciones] ([Codigo], [Nombre], [PadreId], [Ruta], [Orden], [AccionesAplicables], [Activo])
SELECT o.Codigo, o.Nombre, NULL, o.Ruta, o.Orden, o.AccionesAplicables, 1
FROM #Opciones o
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CFG_Opciones] x WHERE x.[Codigo] = o.Codigo);
SET @OpcionesNuevas = @@ROWCOUNT;

UPDATE x SET
    x.[Nombre] = o.Nombre,
    x.[PadreId] = p.[OpcionId],
    x.[Ruta] = o.Ruta,
    x.[Orden] = o.Orden,
    x.[AccionesAplicables] = o.AccionesAplicables,
    x.[Activo] = 1
FROM [mnt].[CFG_Opciones] x
JOIN #Opciones o ON o.Codigo = x.[Codigo]
LEFT JOIN [mnt].[CFG_Opciones] p ON p.[Codigo] = o.PadreCodigo;

-- 2) Permisos de la matriz migrada: sólo los pares (rol, opción) que no existen
INSERT INTO [mnt].[CFG_PermisosRol] ([RoleId], [OpcionId], [PuedeVer], [PuedeAlta], [PuedeModificar], [PuedeBaja], [FechaHoraModificacion], [ModificadoPorUsuarioId])
SELECT r.[Id], op.[OpcionId], m.PuedeVer, m.PuedeAlta, m.PuedeModificar, m.PuedeBaja, @Ahora, @Usuario
FROM #Matriz m
JOIN [dbo].[AspNetRoles] r ON r.[NormalizedName] = m.Rol
JOIN [mnt].[CFG_Opciones] op ON op.[Codigo] = m.Codigo
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CFG_PermisosRol] x WHERE x.[RoleId] = r.[Id] AND x.[OpcionId] = op.[OpcionId]);
SET @PermisosNuevos = @@ROWCOUNT;

-- 3) Configuración de cada rol que no la tenga (ObligadoATurno = 1 sólo OPERARIO)
INSERT INTO [mnt].[CFG_RolesConfiguracion] ([RoleId], [ObligadoATurno], [Descripcion], [FechaHoraModificacion], [ModificadoPorUsuarioId])
SELECT r.[Id], CASE WHEN r.[NormalizedName] = N'OPERARIO' THEN 1 ELSE 0 END, NULL, @Ahora, @Usuario
FROM [dbo].[AspNetRoles] r
WHERE NOT EXISTS (SELECT 1 FROM [mnt].[CFG_RolesConfiguracion] x WHERE x.[RoleId] = r.[Id]);
SET @RolesNuevos = @@ROWCOUNT;

COMMIT TRANSACTION;

DECLARE @TotalOpciones INT = (SELECT COUNT(*) FROM [mnt].[CFG_Opciones]);
DECLARE @TotalPermisos INT = (SELECT COUNT(*) FROM [mnt].[CFG_PermisosRol]);
PRINT CONCAT('Opciones nuevas: ', @OpcionesNuevas, ' (total ', @TotalOpciones, ').');
PRINT CONCAT('Permisos nuevos: ', @PermisosNuevos, ' (total ', @TotalPermisos, ').');
PRINT CONCAT('Roles configurados nuevos: ', @RolesNuevos, '.');

-- Roles de la matriz que no existen en AspNetRoles (no deberían faltar: los crea el 08)
SELECT DISTINCT m.Rol AS RolDeLaMatrizSinAspNetRole
FROM #Matriz m
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] r WHERE r.[NormalizedName] = m.Rol);

DROP TABLE #Opciones;
DROP TABLE #Matriz;
GO

SET NOEXEC OFF;
GO
