# PSAG.Mantenimiento - Contexto para Claude Code

Sistema de control de paradas y variables de proceso para líneas de Converting de PSAG (Papelera San Andrés de Giles).

## Comandos

```bash
# Build y test
dotnet restore
dotnet build
dotnet test

# Publicar para IIS
dotnet publish src/PSAG.Mantenimiento.Api -c Release -o publish/api
dotnet publish src/PSAG.Mantenimiento.Web -c Release -o publish/web

# Base de datos (ejecutar scripts en orden)
# database/01_create_database.sql hasta 99_drop_objects_development_only.sql
```

## Trampas críticas - LEER ANTES DE TOCAR

### 1. Nomenclatura de planta vs código

| En la planta dicen | Significa | En el código es |
|-------------------|-----------|-----------------|
| "La máquina" | La línea completa (Futura 2, Tango 1) | `Linea` |
| "El equipo" | Una parte de la línea (empaquetadora, gofrador) | `Equipo` (ex `Maquina`) |

**Si confundís esto, cada conversación con el cliente arrastra la ambigüedad.**

### 2. Modelo de tiempo - MinutosEfectivos es la única verdad

```
MinutosDeclarados  → lo que tipea el operador (Fase 1)
FechaHoraParada    → hora de parada (opcional Fase 1, PLC en Fase 2)
FechaHoraArranque  → hora de arranque (opcional Fase 1, PLC en Fase 2)
MinutosEfectivos   → EL ÚNICO CAMPO QUE CONSUME TODO EL SISTEMA
OrigenTiempo       → DECLARADO | CALCULADO | PLC
```

**NUNCA consultes MinutosDeclarados directamente.** Dashboard, vistas, exportaciones: todo usa MinutosEfectivos.

### 3. Snapshot de estándares - No optimices con JOIN

`ValorEstandarSnapshot` en registros de proceso guarda el estándar vigente AL MOMENTO del registro.
Si mañana cambian el estándar, el registro histórico sigue mostrando contra qué se comparó ESE DÍA.

**NO hagas JOIN al estándar vigente para "optimizar". Rompe la consulta histórica.**

### 4. Turno materializado

El turno se deriva automáticamente de la fecha/hora y se guarda como `TurnoId` en el registro.
**NO lo recalcules al consultar.** Si cambian los horarios de turno, los registros históricos mantienen el turno al que pertenecieron.

### 5. Estado REGISTRADA = dato completo

Una detención en estado `REGISTRADA` ya tiene minutos, clasificación y descripción.
**NO es un pendiente.** Cuenta en dashboard, acumulados y exportaciones.
El flujo a `CERRADA` es opcional, solo para seguimiento de Mantenimiento.

### 6. Nada se borra

Detenciones, historial, notas, registros de proceso, ejecuciones de inspección: **INSERT-ONLY**.
Corrección administrativa = nuevo registro de auditoría, no UPDATE/DELETE.

### 7. Sintaxis Blazor @bind:after

En Blazor, cuando usás `@bind:after` con un método async, NO uses method group:

```razor
<!-- MAL - error de compilación -->
@bind:after="CargarDatos"

<!-- BIEN -->
@bind:after="@(async () => await CargarDatos())"
```

Lo mismo aplica a `@onclick` con métodos async.

### 8. DateOnly en API, DateTime en Blazor

El modelo `CrearDetencionRequest` usa `DateOnly` para `Fecha`.
Al convertir entre backend y frontend, usar `DateOnly.FromDateTime()`.

**Si usás DateTime directo, el API devuelve 400 Bad Request.**

### 9. Rebuild completo ante errores de integridad SRI

Si la app se queda en 99% cargando con errores de `integrity` en consola:
1. Cerrar procesos dotnet
2. Borrar bin/ y obj/ de todos los proyectos
3. `dotnet restore && dotnet build`

**NO es un error de código, es cache de SRI desincronizado.**

### 10. Dark Mode con Bootstrap 5.3 nativo

Usar `data-bs-theme="dark|light"` en el `<html>` (Bootstrap 5.3 nativo).
El toggle está en MainLayout. **No usar CSS custom para colores de tema.**

## Lo que NUNCA hacer

- Endpoints de DELETE para registros de negocio
- Dependencias de CDN (red interna sin Internet)
- Secretos en código versionado
- Confiar en cálculos del cliente (validar siempre en backend)
- Usar `MinutosDeclarados` directamente
- JOIN a estándares vigentes en consultas históricas
- Recalcular turno en cada consulta

## Convenciones

- Esquema SQL: `[mnt]`
- Prefijo de código de detención: `DET-YYYYMMDD-NNNNNN`
- Español en entidades y DTOs de negocio
- Inglés en código técnico (interfaces, métodos)
- Estados como enums en dominio, no tablas
- Soft delete con campo `Activo`, nunca DELETE físico
- Patrón `Result<T>` para servicios de Application que pueden fallar
- Frontend consume servicios vía interfaces `I*Service` en `Services/`

## Roles

| Rol | Qué puede |
|-----|-----------|
| OPERARIO | Registrar detenciones, cargar variables e inspecciones de sus líneas |
| SUPERVISOR | Lo de operario + revisar + panel NO OK |
| MANTENIMIENTO | Todo + seguimiento + cerrar |
| GERENCIA | Solo lectura + dashboards |
| ADMINISTRADOR | Todo + ABM catálogos + usuarios |

La restricción por línea asignada se valida **en backend**, no solo en UI.

## Pendientes bloqueados (esperan datos del cliente)

- Árbol completo de equipos y detalles de falla por línea
- Horarios reales de los turnos
- Productos por línea y sus estándares de parámetros
- Planes de inspección de las demás líneas
- Nombres definitivos de líneas (Futura 2, Tango 1, etc.)

## Mantenimiento de este archivo

Cuando descubras una trampa, una convención implícita o una decisión de
diseño que no estaba escrita, proponé agregarla acá antes de terminar la
tarea. No la agregues sin avisar. Si algo de este archivo quedó viejo,
decímelo.
