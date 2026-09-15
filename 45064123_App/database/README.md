# Scripts de Base de Datos - PSAG Mantenimiento

Scripts SQL para crear y configurar la base de datos del sistema de gestión de paradas de máquina.

## Orden de Ejecución

Ejecutar los scripts en orden numérico:

| # | Script | Descripción |
|---|--------|-------------|
| 01 | `01_create_database.sql` | Crea la base de datos PSAG_Mantenimiento |
| 02 | `02_create_schema.sql` | Crea el esquema [mnt] para tablas de negocio |
| 03 | `03_create_identity.sql` | Crea tablas de ASP.NET Core Identity |
| 04 | `04_create_tables.sql` | Crea tablas de negocio |
| 05 | `05_create_constraints_indexes.sql` | Crea constraints, FKs e índices |
| 06 | `06_create_views.sql` | Crea vistas para consultas comunes |
| 07 | `07_seed_catalogs.sql` | Datos iniciales de catálogos |
| 08 | `08_seed_roles.sql` | Roles del sistema y usuario admin |
| 09 | `09_seed_demo_data.sql` | **SOLO DESARROLLO** - Datos de prueba |
| 10 | `10_permissions_example.sql` | Ejemplo de configuración de permisos |
| 99 | `99_drop_objects_development_only.sql` | **SOLO DESARROLLO** - Limpieza total |

## Requisitos

- SQL Server 2019 o superior
- Permisos de administrador para crear la base de datos

## Ejecución Rápida

```powershell
# Desde SQL Server Management Studio o sqlcmd
sqlcmd -S localhost -E -i 01_create_database.sql
sqlcmd -S localhost -E -i 02_create_schema.sql
# ... continuar con cada script
```

## Notas Importantes

1. **Script 09**: Solo para desarrollo/pruebas. NO ejecutar en producción.
2. **Script 99**: Elimina TODOS los objetos. Solo para reiniciar en desarrollo.
3. **Usuario admin**: La contraseña debe establecerse desde la aplicación.
4. Los scripts son idempotentes (pueden re-ejecutarse sin error).

## Estructura de la Base de Datos

### Esquema [dbo] - ASP.NET Identity
- `AspNetUsers` - Usuarios (con campos personalizados)
- `AspNetRoles` - Roles del sistema
- `AspNetUserRoles` - Asignación usuario-rol
- `AspNetUserClaims` - Claims de usuario
- `AspNetRoleClaims` - Claims de rol (permisos)
- `AspNetUserLogins` - Logins externos
- `AspNetUserTokens` - Tokens de usuario

### Esquema [mnt] - Negocio
- `Lineas` - Líneas de producción (6 en Converting)
- `Maquinas` - Máquinas por línea
- `TiposEvento` - Catálogo de tipos de parada
- `Causas` - Catálogo de causas
- `Eventos` - Eventos de parada (tabla principal)
- `EventosEstadoHistorial` - Historial de cambios de estado
- `EventosNotas` - Notas de seguimiento
- `UsuariosLineas` - Asignación de usuarios a líneas
- `Auditoria` - Registro de auditoría

### Vistas
- `VW_EventosDetalle` - Vista completa de eventos
- `VW_EventosAbiertos` - Eventos no cerrados
- `VW_EventosIndicadores` - Indicadores agregados
- `VW_LineasConMaquinas` - Líneas con sus máquinas
- `VW_UsuariosConLineas` - Usuarios con líneas asignadas
