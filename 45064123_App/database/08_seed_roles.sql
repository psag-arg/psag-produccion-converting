-- ==============================================================================
-- Script: 08_seed_roles.sql
-- Descripción: Crea los roles del sistema y usuario administrador inicial
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

-- ============================================================================
-- ROLES DEL SISTEMA
-- ============================================================================
PRINT 'Insertando roles del sistema...'

-- ADMINISTRADOR: Acceso total al sistema
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'ADMINISTRADOR')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), N'ADMINISTRADOR', N'ADMINISTRADOR', NEWID())
    PRINT '  - Rol ADMINISTRADOR creado'
END

-- GERENCIA: Consulta total, dashboards, exportación
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'GERENCIA')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), N'GERENCIA', N'GERENCIA', NEWID())
    PRINT '  - Rol GERENCIA creado'
END

-- MANTENIMIENTO: Gestión de eventos, asignar causas, cerrar eventos
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'MANTENIMIENTO')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), N'MANTENIMIENTO', N'MANTENIMIENTO', NEWID())
    PRINT '  - Rol MANTENIMIENTO creado'
END

-- SUPERVISOR: Supervisión de líneas asignadas, puede tomar eventos
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'SUPERVISOR')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), N'SUPERVISOR', N'SUPERVISOR', NEWID())
    PRINT '  - Rol SUPERVISOR creado'
END

-- OPERARIO: Registro de eventos en líneas asignadas
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'OPERARIO')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), N'OPERARIO', N'OPERARIO', NEWID())
    PRINT '  - Rol OPERARIO creado'
END

PRINT 'Roles del sistema creados.'
GO

-- ============================================================================
-- USUARIO ADMINISTRADOR INICIAL
-- Nota: La contraseña está hasheada con ASP.NET Core Identity
-- Contraseña por defecto: Admin123!
-- El usuario DEBE cambiar la contraseña en el primer inicio de sesión
-- ============================================================================
PRINT 'Creando usuario administrador inicial...'

DECLARE @AdminId NVARCHAR(450) = 'b5a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
DECLARE @AdminRoleId NVARCHAR(450)

SELECT @AdminRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'ADMINISTRADOR'

IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = N'ADMIN')
BEGIN
    -- Insertar usuario admin
    -- NOTA: El PasswordHash debe generarse con ASP.NET Core Identity
    -- Este es un placeholder, la aplicación debe establecer la contraseña real
    INSERT INTO [dbo].[AspNetUsers] (
        [Id],
        [UserName],
        [NormalizedUserName],
        [Email],
        [NormalizedEmail],
        [EmailConfirmed],
        [PasswordHash],
        [SecurityStamp],
        [ConcurrencyStamp],
        [PhoneNumber],
        [PhoneNumberConfirmed],
        [TwoFactorEnabled],
        [LockoutEnd],
        [LockoutEnabled],
        [AccessFailedCount],
        [NombreCompleto],
        [Activo],
        [RequiereCambioPassword],
        [FechaCreacion]
    )
    VALUES (
        @AdminId,
        N'admin',
        N'ADMIN',
        N'admin@psag.local',
        N'ADMIN@PSAG.LOCAL',
        1,  -- EmailConfirmed
        NULL, -- PasswordHash - será establecido por la aplicación
        NEWID(), -- SecurityStamp
        NEWID(), -- ConcurrencyStamp
        NULL, -- PhoneNumber
        0, -- PhoneNumberConfirmed
        0, -- TwoFactorEnabled
        NULL, -- LockoutEnd
        1, -- LockoutEnabled
        0, -- AccessFailedCount
        N'Administrador del Sistema',
        1, -- Activo
        1, -- RequiereCambioPassword (debe cambiar en primer login)
        SYSDATETIMEOFFSET()
    )
    PRINT '  - Usuario admin creado'

    -- Asignar rol de administrador
    IF @AdminRoleId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId])
        VALUES (@AdminId, @AdminRoleId)
        PRINT '  - Rol ADMINISTRADOR asignado al usuario admin'
    END
END
ELSE
BEGIN
    PRINT '  - Usuario admin ya existe'
END
GO

-- ============================================================================
-- CLAIMS DE PERMISOS (opcional, para control granular)
-- ============================================================================
PRINT 'Creando claims de permisos por rol...'

-- Obtener IDs de roles
DECLARE @AdminRoleId NVARCHAR(450)
DECLARE @GerenciaRoleId NVARCHAR(450)
DECLARE @MantenimientoRoleId NVARCHAR(450)
DECLARE @SupervisorRoleId NVARCHAR(450)
DECLARE @OperarioRoleId NVARCHAR(450)

SELECT @AdminRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'ADMINISTRADOR'
SELECT @GerenciaRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'GERENCIA'
SELECT @MantenimientoRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'MANTENIMIENTO'
SELECT @SupervisorRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'SUPERVISOR'
SELECT @OperarioRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'OPERARIO'

-- Permisos ADMINISTRADOR (todos los permisos)
IF @AdminRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoleClaims] WHERE [RoleId] = @AdminRoleId AND [ClaimType] = 'permission')
BEGIN
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'eventos.crear')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'eventos.ver')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'eventos.editar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'eventos.cerrar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'eventos.tomar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'catalogos.gestionar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'usuarios.gestionar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'reportes.ver')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'reportes.exportar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@AdminRoleId, 'permission', 'auditoria.ver')
    PRINT '  - Permisos ADMINISTRADOR creados'
END

-- Permisos GERENCIA
IF @GerenciaRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoleClaims] WHERE [RoleId] = @GerenciaRoleId AND [ClaimType] = 'permission')
BEGIN
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@GerenciaRoleId, 'permission', 'eventos.ver')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@GerenciaRoleId, 'permission', 'reportes.ver')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@GerenciaRoleId, 'permission', 'reportes.exportar')
    PRINT '  - Permisos GERENCIA creados'
END

-- Permisos MANTENIMIENTO
IF @MantenimientoRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoleClaims] WHERE [RoleId] = @MantenimientoRoleId AND [ClaimType] = 'permission')
BEGIN
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@MantenimientoRoleId, 'permission', 'eventos.crear')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@MantenimientoRoleId, 'permission', 'eventos.ver')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@MantenimientoRoleId, 'permission', 'eventos.editar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@MantenimientoRoleId, 'permission', 'eventos.cerrar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@MantenimientoRoleId, 'permission', 'eventos.tomar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@MantenimientoRoleId, 'permission', 'reportes.ver')
    PRINT '  - Permisos MANTENIMIENTO creados'
END

-- Permisos SUPERVISOR
IF @SupervisorRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoleClaims] WHERE [RoleId] = @SupervisorRoleId AND [ClaimType] = 'permission')
BEGIN
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@SupervisorRoleId, 'permission', 'eventos.crear')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@SupervisorRoleId, 'permission', 'eventos.ver')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@SupervisorRoleId, 'permission', 'eventos.tomar')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@SupervisorRoleId, 'permission', 'reportes.ver')
    PRINT '  - Permisos SUPERVISOR creados'
END

-- Permisos OPERARIO
IF @OperarioRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoleClaims] WHERE [RoleId] = @OperarioRoleId AND [ClaimType] = 'permission')
BEGIN
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@OperarioRoleId, 'permission', 'eventos.crear')
    INSERT INTO [dbo].[AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue]) VALUES (@OperarioRoleId, 'permission', 'eventos.ver')
    PRINT '  - Permisos OPERARIO creados'
END

PRINT 'Claims de permisos creados.'
GO

PRINT 'Roles y usuario administrador creados exitosamente.'
GO
