# Seguridad - PSAG.Mantenimiento

## Autenticación

### JWT Bearer Tokens
- Access Token: 60 minutos (configurable)
- Refresh Token: 7 días (configurable)
- Algoritmo: HMAC-SHA256
- Clave mínima: 32 caracteres

### ASP.NET Core Identity
- Lockout: 5 intentos fallidos → bloqueo 15 minutos
- Password: mínimo 8 caracteres, mayúscula, minúscula, número, símbolo
- Usuarios únicos por email

## Autorización

### Roles
| Rol | Descripción |
|-----|-------------|
| OPERARIO | Registrar en sus líneas |
| SUPERVISOR | Revisar + panel NO OK |
| MANTENIMIENTO | Todo + cerrar |
| GERENCIA | Solo lectura |
| ADMINISTRADOR | ABM completo |

### Políticas
```csharp
RequireAdministrador    → ADMINISTRADOR
RequireGerencia         → ADMINISTRADOR, GERENCIA
RequireMantenimiento    → ADMINISTRADOR, MANTENIMIENTO
RequireSupervisor       → ADMINISTRADOR, MANTENIMIENTO, SUPERVISOR
RequireOperario         → ADMINISTRADOR, MANTENIMIENTO, SUPERVISOR, OPERARIO
CanCreateDetenciones    → ADMINISTRADOR, MANTENIMIENTO, SUPERVISOR, OPERARIO
CanEditDetenciones      → ADMINISTRADOR, MANTENIMIENTO
CanCloseDetenciones     → ADMINISTRADOR, MANTENIMIENTO
CanManageCatalogs       → ADMINISTRADOR
CanManageUsers          → ADMINISTRADOR
CanExport               → ADMINISTRADOR, GERENCIA, MANTENIMIENTO
```

### Restricción por Línea
- Tabla `UsuarioLinea` define qué líneas puede operar cada usuario
- Se valida **en backend** en cada operación
- No alcanza con ocultar en UI

```csharp
// En cada service/controller
if (!await _lineaService.UsuarioPuedeAccederAsync(userId, lineaId))
    throw new AccesoLineaNoAutorizadoException(lineaId);
```

## Validaciones

### Backend (obligatorias)
- Coherencia Equipo ↔ Línea
- Cascada TipoFalla → Equipo → DetalleFalla
- MinutosEfectivos > 0
- FechaHoraArranque > FechaHoraParada
- Sin fechas futuras
- Observación obligatoria en desvío y NO_OK

### Frontend (UX)
- Filtrado de desplegables
- Validaciones de formulario
- **No son suficientes** - el backend siempre revalida

## Auditoría

### Campos automáticos
- `CreadoPorUsuarioId`, `FechaHoraCreacion`
- `ModificadoPorUsuarioId`, `FechaHoraModificacion`
- `CorrelationId` en cada request

### Tabla de Auditoría
Registro de acciones significativas:
- InicioSesion, CierreSesion
- Crear, Modificar (cambios de estado)
- Exportar

### Historial de Estados
Tabla `DetencionEstadoHistorial` - INSERT-ONLY.
Cada cambio de estado queda registrado con usuario, fecha y observación.

## Concurrencia

### Optimista con RowVersion
- Campos `RowVersion` en tablas editables
- Si el registro cambió → 409 Conflict
- Cliente debe recargar y reintentar

## Headers de Seguridad

Configurar en IIS/reverse proxy:
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
```

## Secretos

### NO versionar
- `appsettings.Production.json`
- Claves JWT
- `Calidad:ClaveIngesta` (clave de servicio del detector de calidad, T-611): en producción va en la variable de entorno `Calidad__ClaveIngesta` del servicio. Ver `CALIDAD.md`.
- Connection strings de producción

### Usar
- Variables de entorno
- Azure Key Vault (si aplica)
- User Secrets en desarrollo
