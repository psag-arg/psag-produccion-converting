# API - PSAG.Mantenimiento

Base URL: `/api/v1`

## Autenticación

### POST /auth/login
```json
Request:
{
  "usuario": "string",
  "password": "string"
}

Response 200:
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiration": "datetime",
  "usuario": {
    "id": "string",
    "userName": "string",
    "nombreCompleto": "string",
    "roles": ["string"]
  }
}
```

### POST /auth/refresh
### POST /auth/logout
### POST /auth/cambiar-password

## Detenciones

### GET /detenciones
Filtros: `page`, `pageSize`, `lineaId`, `equipoId`, `tipoFallaId`, `estado`, `fechaDesde`, `fechaHasta`, `busqueda`

### POST /detenciones
```json
Request:
{
  "lineaId": 1,
  "equipoId": 2,
  "tipoFallaId": 1,
  "detalleFallaId": 3,
  "minutosDeclarados": 80,
  "descripcion": "alarma servo de correas rápidas"
}

Response 201:
{
  "detencionId": 1,
  "codigo": "DET-20260808-000001",
  "turnoId": 1,
  "minutosEfectivos": 80,
  "origenTiempo": "DECLARADO",
  "estadoActual": "REGISTRADA"
}
```

### GET /detenciones/{id}
### PUT /detenciones/{id}
### POST /detenciones/{id}/estado
### GET /detenciones/{id}/notas
### POST /detenciones/{id}/notas
### GET /detenciones/turno-actual
Devuelve acumulado del turno para la línea del usuario.

## Variables de Proceso

### GET /proceso/registros
### POST /proceso/registros
### GET /proceso/registros/{id}
### PUT /proceso/registros/{id}
### GET /proceso/estandares
Filtros: `lineaId`, `productoId`
### GET /proceso/historico
Filtros: `lineaId`, `productoId`, `fechaDesde`, `fechaHasta`

## Inspecciones

### GET /inspecciones/planes
### GET /inspecciones/pendientes
Según puesto, línea y frecuencia.
### GET /inspecciones/ejecuciones
### POST /inspecciones/ejecuciones
### GET /inspecciones/no-ok
### POST /inspecciones/no-ok/{id}/resolver

## Catálogos

### GET /catalogos/lineas
### GET /catalogos/equipos
Filtros: `lineaId`
### GET /catalogos/tipos-falla
### GET /catalogos/detalles-falla
Filtros: `equipoId`, `tipoFallaId`
### GET /catalogos/turnos
### GET /catalogos/productos
Filtros: `lineaId`
### GET /catalogos/grupos-parametro
### GET /catalogos/parametros
### GET /catalogos/puestos

## Dashboard

### GET /dashboard
Filtros: `periodo`, `lineaId`, `equipoId`, `tipoFallaId`, `turnoId`

## Exportaciones

### GET /exportaciones
Filtros: varios, devuelve CSV.

## Auditoría

### GET /auditoria
Filtros: `usuarioId`, `accion`, `entidad`, `fechaDesde`, `fechaHasta`

## Respuestas de Error

Formato RFC 7807 (ProblemDetails):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Solicitud inválida",
  "status": 400,
  "detail": "Mensaje de error",
  "instance": "/api/v1/detenciones",
  "correlationId": "guid",
  "errors": ["error1", "error2"]
}
```

## Notas

- **No hay endpoints de DELETE** para registros de negocio
- Paginación con tope de 100 registros
- Concurrencia optimista con RowVersion → 409 Conflict
- Correlation ID en headers y respuestas
