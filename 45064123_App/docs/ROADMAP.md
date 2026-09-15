# Roadmap - PSAG.Mantenimiento

Puntos de integración y extensión futuros. **NO implementar ahora.**

## Fase 2 - Integración PLC

### Endpoint de Ingesta Automática
```
POST /api/v1/detenciones/ingesta-plc
Authorization: ApiKey (para sistema PLC)

Request:
{
  "lineaId": 1,
  "equipoId": 2,
  "fechaHoraParada": "2026-08-08T10:30:00-03:00",
  "fechaHoraArranque": "2026-08-08T10:45:00-03:00",
  "origenExterno": "PLC_LINEA_1"
}

Response 201:
{
  "detencionId": 123,
  "codigo": "DET-20260808-000001",
  "estadoActual": "PENDIENTE_JUSTIFICACION",
  "minutosEfectivos": 15,
  "origenTiempo": "PLC"
}
```

### Flujo
1. PLC/cámara detecta parada
2. Envía datos al endpoint
3. Sistema crea detención en `PENDIENTE_JUSTIFICACION`
4. Operador ve la detención y agrega:
   - TipoFalla, DetalleFalla, Descripción
5. Al completar → pasa a `REGISTRADA`

### Consideraciones
- Autenticación por API Key (no JWT)
- Rate limiting
- Validación de origen
- Log específico de ingesta

## Integración con Control de Turnos

### Bloqueo de Cierre de Turno
El sistema de Control de Turnos existente en el servidor de planta podría consultar:

```
GET /api/v1/detenciones/pendientes-turno?lineaId=1&turnoId=2
```

Si hay detenciones sin declarar (venidas del PLC en `PENDIENTE_JUSTIFICACION`), bloquear el cierre del turno.

### Consideraciones
- Endpoint de consulta read-only
- Autenticación service-to-service
- No modificar el sistema de Control de Turnos desde este sistema

## Generación de Órdenes de Trabajo

### Desde NO OK de Inspección
```
POST /api/v1/inspecciones/no-ok/{id}/generar-ot

Response:
{
  "ordenTrabajoId": 456,
  "tipo": "CORRECTIVO",
  "origen": "INSPECCION_NO_OK",
  "referenciaId": 123
}
```

### Desde Detención Recurrente
Cuando un equipo acumula X detenciones en Y días:
- Alerta al supervisor
- Opción de generar OT preventiva

### Consideraciones
- Módulo de Órdenes de Trabajo completo (fuera de alcance actual)
- Integración con sistema de repuestos/inventario
- Asignación de recursos

## Modelos Predictivos

### Fuera de Alcance Actual
- MTBF requiere horas reales de funcionamiento (no disponibles)
- Análisis de tendencias requiere volumen de datos históricos
- Machine learning para predicción de fallas

### Preparación
- Guardar datos con granularidad suficiente
- Timestamps precisos
- No agregar/resumir prematuramente

## Integración BASE/ERP

### Explícitamente Diferido
- BASE no es un ERP productivo
- IT corporativa queda del otro lado de la frontera
- Este sistema es independiente

### Si se requiere en el futuro
- Exportación de datos a CSV/Excel
- API de consulta read-only
- No sincronización bidireccional

## Cruce de Datos

### Diferido por el Cliente
- Desvíos de proceso vs. detenciones posteriores
- NO OK de inspección vs. roturas
- Correlación causa-efecto

### Preparación
- Modelo de datos permite el cruce
- Timestamps y claves foráneas correctos
- Queries de análisis se pueden construir después
