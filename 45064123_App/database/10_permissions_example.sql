-- ==============================================================================
-- Script: 10_permissions_example.sql
-- Descripción: Ejemplo de configuración de permisos SQL Server
-- Nota: Ajustar según las políticas de seguridad de la organización
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

/*
================================================================================
IMPORTANTE: Este script es un EJEMPLO de configuración de permisos.
Revisar y ajustar según las políticas de seguridad de PSAG antes de ejecutar.
================================================================================

Estrategia de permisos recomendada:
1. La aplicación usa una única cuenta de servicio con permisos mínimos
2. Los permisos de usuario se controlan a nivel de aplicación (roles de Identity)
3. La cuenta de servicio NO debe ser sysadmin ni db_owner en producción
*/

-- ============================================================================
-- CREAR LOGIN Y USUARIO DE SERVICIO
-- ============================================================================
PRINT 'Creando login y usuario de servicio...'
GO

-- Crear login para la aplicación (ajustar contraseña)
/*
USE [master]
GO

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'PSAG_Mantenimiento_App')
BEGIN
    CREATE LOGIN [PSAG_Mantenimiento_App]
    WITH PASSWORD = N'CAMBIAR_ESTA_PASSWORD_EN_PRODUCCION!'
        , DEFAULT_DATABASE = [PSAG_Mantenimiento]
        , CHECK_POLICY = ON
        , CHECK_EXPIRATION = OFF
    PRINT 'Login PSAG_Mantenimiento_App creado.'
END
GO
*/

-- Crear usuario en la base de datos
/*
USE [PSAG_Mantenimiento]
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'PSAG_Mantenimiento_App')
BEGIN
    CREATE USER [PSAG_Mantenimiento_App] FOR LOGIN [PSAG_Mantenimiento_App]
    PRINT 'Usuario PSAG_Mantenimiento_App creado.'
END
GO
*/

-- ============================================================================
-- ASIGNAR PERMISOS AL USUARIO DE SERVICIO
-- ============================================================================
PRINT 'Configurando permisos para usuario de servicio...'
GO

/*
-- Permisos sobre esquema dbo (Identity tables)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [PSAG_Mantenimiento_App]

-- Permisos sobre esquema mnt (tablas de negocio)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::mnt TO [PSAG_Mantenimiento_App]

-- Permisos para ejecutar procedimientos almacenados (si se crean)
-- GRANT EXECUTE ON SCHEMA::mnt TO [PSAG_Mantenimiento_App]

-- NO dar permisos de:
-- - ALTER (modificar estructura)
-- - DROP (eliminar objetos)
-- - CREATE (crear objetos)
-- - db_owner role
-- - sysadmin role

PRINT 'Permisos asignados a PSAG_Mantenimiento_App.'
GO
*/

-- ============================================================================
-- CREAR ROL DE BASE DE DATOS PERSONALIZADO (Opcional)
-- ============================================================================
/*
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'AppServiceRole' AND type = 'R')
BEGIN
    CREATE ROLE [AppServiceRole]
    PRINT 'Rol AppServiceRole creado.'
END
GO

-- Asignar permisos al rol
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [AppServiceRole]
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::mnt TO [AppServiceRole]

-- Agregar usuario al rol
ALTER ROLE [AppServiceRole] ADD MEMBER [PSAG_Mantenimiento_App]
GO
*/

-- ============================================================================
-- PERMISOS DE SOLO LECTURA PARA REPORTING (Opcional)
-- ============================================================================
/*
-- Para usuarios que solo necesitan consultar reportes
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'ReportingRole' AND type = 'R')
BEGIN
    CREATE ROLE [ReportingRole]
    PRINT 'Rol ReportingRole creado.'
END
GO

GRANT SELECT ON SCHEMA::mnt TO [ReportingRole]
GRANT SELECT ON [mnt].[VW_EventosDetalle] TO [ReportingRole]
GRANT SELECT ON [mnt].[VW_EventosAbiertos] TO [ReportingRole]
GRANT SELECT ON [mnt].[VW_EventosIndicadores] TO [ReportingRole]
GO
*/

-- ============================================================================
-- CONFIGURACIÓN PARA WINDOWS AUTHENTICATION (Recomendado)
-- ============================================================================
/*
-- Si se usa autenticación de Windows (recomendado en intranet):

USE [master]
GO

-- Crear login para cuenta de servicio de Windows
CREATE LOGIN [DOMINIO\SvcPSAGMantenimiento] FROM WINDOWS
WITH DEFAULT_DATABASE = [PSAG_Mantenimiento]
GO

USE [PSAG_Mantenimiento]
GO

CREATE USER [DOMINIO\SvcPSAGMantenimiento] FOR LOGIN [DOMINIO\SvcPSAGMantenimiento]
GO

-- Asignar permisos
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [DOMINIO\SvcPSAGMantenimiento]
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::mnt TO [DOMINIO\SvcPSAGMantenimiento]
GO
*/

-- ============================================================================
-- AUDITORÍA DE PERMISOS
-- ============================================================================
PRINT 'Consultas para verificar permisos configurados:'
GO

-- Ver permisos de un usuario
/*
SELECT
    dp.name AS [Usuario],
    dp.type_desc AS [Tipo],
    OBJECT_NAME(major_id) AS [Objeto],
    permission_name AS [Permiso],
    state_desc AS [Estado]
FROM sys.database_permissions perm
JOIN sys.database_principals dp ON perm.grantee_principal_id = dp.principal_id
WHERE dp.name = 'PSAG_Mantenimiento_App'
ORDER BY OBJECT_NAME(major_id), permission_name
*/

-- Ver miembros de roles
/*
SELECT
    r.name AS [Rol],
    m.name AS [Miembro]
FROM sys.database_role_members rm
JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
ORDER BY r.name, m.name
*/

PRINT 'Script de permisos completado (revisar comentarios para activar).'
GO
