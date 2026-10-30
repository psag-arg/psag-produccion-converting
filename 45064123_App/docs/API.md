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

## Calidad (T-611)

Detalle completo, reglas y contrato del detector en [CALIDAD.md](CALIDAD.md).

### GET /calidad/registros
Filtros: `fechaDesde`, `fechaHasta` (días de planta), `turnoId`, `lineaId`, `puntoControlCalidadId`, `tipoDefectoCalidadId`, `estadoRevision` (PENDIENTE | CONFIRMADO | FALSO_POSITIVO), `origen` (AUTOMATICO | MANUAL), `page`, `pageSize`.
### GET /calidad/registros/{id}
Con historial de revisión.
### GET /calidad/registros/{id}/imagen
Solo con sesión, `nosniff`.
### POST /calidad/registros
Carga manual (multipart, imagen opcional en `archivo`). SUPERVISOR, ADMINISTRADOR.
### POST /calidad/registros/{id}/revision
`{ "estado": "CONFIRMADO" | "FALSO_POSITIVO", "comentario": "...", "rowVersion": "..." }`. SUPERVISOR, MANTENIMIENTO, ADMINISTRADOR.
### POST /calidad/registros/{id}/detencion
`{ "codigoDetencion": "DET-..." }` (vacío = quitar). Mismos roles que la revisión.
### GET / POST / PUT /calidad/puntos-control, /calidad/tipos-defecto
Maestros. Alta y edición solo ADMINISTRADOR.
### POST /calidad/ingesta
Detector. Sin JWT: header `X-Clave-Ingesta` (`Calidad:ClaveIngesta`). 201 / 200 (duplicado) / 400 / 401 / 503.

### GET /conteo-camara/paradas
T-610. Paradas detectadas por la cámara con el cruce con detenciones (`CON_DETENCION` / `POSIBLE` / `SIN_DETENCION`).
Query: `fechaDesde`, `fechaHasta` (días de planta, máx. 3 meses), `lineaId`, `soloSinDetencion`, `segundosMinimos`.
### GET /conteo-camara/produccion?fecha=YYYY-MM-DD&lineaId=
T-610. Rollos por hora y carril de un día de planta.
### POST /conteo-camara/ingesta
Servicio de conteo. Sin JWT: misma clave que la ingesta de calidad. JSON con `puntoControl`, `minutos[]` y `paradas[]`;
idempotente (minuto+carril, identificadorExterno), todo o nada. 200 / 400 / 401 / 503. Contrato en `docs/CONTEO_CAMARA.md`.

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
