# Reglas de Negocio - PSAG.Mantenimiento

## Detenciones

### R1: Registro post-resolución
El operador registra la detención **después** de resolver el problema, no durante.
La detención nace completa con minutos, clasificación y descripción.

### R2: Modelo de tiempo dual
```
SI hay FechaHoraParada Y FechaHoraArranque:
    MinutosEfectivos = ROUND(FechaHoraArranque - FechaHoraParada, minutos)
    OrigenTiempo = CALCULADO (o PLC si vino de integración)
SI NO:
    MinutosEfectivos = MinutosDeclarados
    OrigenTiempo = DECLARADO
```
**MinutosEfectivos es la única columna que consume el resto del sistema.**

### R3: Derivación de turno
El turno se deriva automáticamente de la fecha/hora del registro.
Se guarda materializado (`TurnoId`), no se recalcula.
Contempla turnos que cruzan medianoche (`HoraFin < HoraInicio`).

### R4: Umbral mínimo configurable
Parámetro `Detenciones__MinutosMinimos` (default: 5).
Por debajo, la pantalla advierte pero **no bloquea**.

### R5: Clasificación en cascada
```
TipoFalla → Equipo → DetalleFalla
```
El frontend filtra desplegables. La API valida coherencia:
- Equipo debe pertenecer a la Línea del registro
- DetalleFalla debe aplicar al Equipo y/o TipoFalla seleccionados

### R6: Estados opcionales
| Estado | Significado |
|--------|-------------|
| REGISTRADA | Dato completo y válido. Cuenta en dashboard. |
| EN_SEGUIMIENTO | Mantenimiento decide hacerle seguimiento |
| CERRADA | Se documentó causa y resolución |
| PENDIENTE_JUSTIFICACION | (Fase 2) Creada por PLC, esperando justificación |

**REGISTRADA no es un pendiente.** El flujo a CERRADA es opcional.

### R7: Cierre requiere campos
Al pasar a CERRADA: CausaId, CausaDetalle, Resolucion son obligatorios.

## Variables de Proceso

### R8: Producto define estándares
El mismo parámetro en la misma línea tiene distintos estándares según el producto que esté corriendo.

### R9: Snapshot de estándar
`ValorEstandarSnapshot` guarda el estándar vigente **al momento del registro**.
Cambios futuros al estándar no afectan registros históricos.

### R10: Desvío requiere observación
Si `ValorReal` difiere del estándar (o cae fuera del rango), la observación es obligatoria.
La API rechaza el guardado si falta.

### R11: Comparación de rangos
```
Estándar: "3,8 - 4,1"
ValorReal: 4.0 → NO es desvío (está dentro)
ValorReal: 4.5 → SÍ es desvío (está fuera)
```

### R12: Registro único por contexto
Un registro por: Línea + Producto + Turno + Fecha.
Si ya existe, se retoma, no se duplica.

### R13: Orden libre entre grupos
El operador puede completar los grupos en cualquier orden.
Guardado parcial permitido dentro del mismo turno.

## Inspecciones

### R14: Plan por puesto
El operador ve solo los planes correspondientes a su puesto operativo y línea.

### R15: Frecuencia
El sistema muestra qué ítems tocan hoy según frecuencia y última ejecución.
Frecuencia en días (1 = diaria, 10 = cada 10 días).

### R16: NO_OK requiere observación
Si el estado es NO_OK, la observación es obligatoria.

### R17: Resolución de NO_OK
Un NO_OK se marca como resuelto con usuario, fecha y observación.
No se borra, se resuelve.

## Transversales

### R18: Nada se borra
Detenciones, historial, notas, registros de proceso, ejecuciones de inspección: **INSERT-ONLY**.

### R19: Restricción por línea
Un operario de Línea 1 no puede registrar ni consultar nada de Línea 4.
Se valida **en backend**, no solo en UI.

### R20: Auditoría automática
Todo cambio de estado genera historial de solo inserción.
Cambios significativos se registran en la tabla de auditoría.
