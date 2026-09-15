# Modelo de Datos - PSAG.Mantenimiento

Esquema: `[mnt]`

## Diagrama de Entidades

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│     Lineas      │────<│     Equipos     │     │   TiposFalla    │
│─────────────────│     │─────────────────│     │─────────────────│
│ LineaId (PK)    │     │ EquipoId (PK)   │     │ TipoFallaId (PK)│
│ Codigo          │     │ LineaId (FK)    │     │ Codigo          │
│ Nombre          │     │ Codigo          │     │ Nombre          │
│ Activo          │     │ Nombre          │     │ Orden           │
└─────────────────┘     │ Orden           │     │ Activo          │
                        │ Activo          │     └─────────────────┘
                        └─────────────────┘              │
                               │                         │
                               ▼                         ▼
┌─────────────────┐     ┌─────────────────────────────────────────┐
│     Turnos      │     │              DetallesFalla              │
│─────────────────│     │─────────────────────────────────────────│
│ TurnoId (PK)    │     │ DetalleFallaId (PK)                     │
│ Codigo          │     │ EquipoId (FK) NULL -- genérico si NULL  │
│ Nombre          │     │ TipoFallaId (FK) NULL                   │
│ HoraInicio      │     │ Nombre                                  │
│ HoraFin         │     │ Orden                                   │
│ Orden           │     │ Activo                                  │
│ Activo          │     └─────────────────────────────────────────┘
└─────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────────┐
│                          Detenciones                            │
│─────────────────────────────────────────────────────────────────│
│ DetencionId (PK)                                                │
│ Codigo (UNIQUE) -- DET-YYYYMMDD-NNNNNN                          │
│ LineaId (FK)                                                    │
│ EquipoId (FK)                                                   │
│ TipoFallaId (FK)                                                │
│ DetalleFallaId (FK)                                             │
│ TurnoId (FK) -- materializado, no recalcular                    │
│ UsuarioId (FK)                                                  │
│ Fecha                                                           │
│ MinutosDeclarados (NULL)                                        │
│ FechaHoraParada (NULL)                                          │
│ FechaHoraArranque (NULL)                                        │
│ OrigenTiempo -- DECLARADO | CALCULADO | PLC                     │
│ MinutosEfectivos -- *** ÚNICO CAMPO QUE CONSUME EL SISTEMA ***  │
│ Descripcion                                                     │
│ EstadoActual -- REGISTRADA | EN_SEGUIMIENTO | CERRADA           │
│ CausaId (FK) NULL -- solo al cerrar                             │
│ Resolucion (NULL)                                               │
│ FechaHoraCierre (NULL)                                          │
│ RowVersion                                                      │
└─────────────────────────────────────────────────────────────────┘
        │
        ├──────────────────────┐
        ▼                      ▼
┌─────────────────┐     ┌─────────────────────┐
│DetencionHistorial│    │   DetencionNotas    │
│─────────────────│     │─────────────────────│
│ Id (PK)         │     │ NotaId (PK)         │
│ DetencionId (FK)│     │ DetencionId (FK)    │
│ EstadoAnterior  │     │ Texto               │
│ EstadoNuevo     │     │ UsuarioId           │
│ FechaHora       │     │ FechaHora           │
│ UsuarioId       │     │ TipoNota            │
│ Observacion     │     │ EsNotaDeCierre      │
│ CorrelationId   │     └─────────────────────┘
└─────────────────┘
```

## Reglas de Integridad

### MinutosEfectivos
```sql
CHECK (MinutosEfectivos > 0)
CHECK (FechaHoraArranque IS NULL OR FechaHoraArranque > FechaHoraParada)
-- Sin fechas futuras
```

### Coherencia Equipo-Línea
```sql
-- FK con validación: Equipo debe pertenecer a la Línea de la Detención
```

### Cascada TipoFalla → Equipo → Detalle
El frontend filtra en cascada, pero la API valida la coherencia antes de persistir.

## Vistas Principales

| Vista | Propósito |
|-------|-----------|
| `VW_DetencionesDetalle` | Detenciones con nombres resueltos |
| `VW_DetencionesIndicadores` | Agregados para dashboard |
| `VW_DetencionesPorTurno` | Acumulado del turno actual |
| `VW_ProcesoRegistrosDetalle` | Registros de proceso expandidos |
| `VW_ProcesoDesvios` | Parámetros fuera de estándar |
| `VW_InspeccionesNoOk` | Panel de NO OK |

## Índices Críticos

- `IX_Detenciones_Fecha_Linea_Turno`
- `IX_Detenciones_Estado`
- `IX_Detenciones_Equipo`
- `IX_Detenciones_TipoFalla`
- `IX_RegistrosProceso_Linea_Producto_Fecha`
- `IX_EjecucionesInspeccion_Plan_Fecha`
