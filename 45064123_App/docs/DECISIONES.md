# Decisiones Técnicas - PSAG.Mantenimiento

Este documento registra las decisiones técnicas tomadas durante el desarrollo del sistema.

## Arquitectura

### DEC-001: Arquitectura de Capas
**Decisión**: Implementar arquitectura de monolito modular con capas bien definidas.
**Justificación**: Siguiendo los requisitos del proyecto, se evitan microservicios innecesarios en esta etapa inicial.
**Capas**:
- Domain: Entidades, enumeraciones, interfaces de repositorio
- Application: DTOs, servicios de aplicación, validaciones con FluentValidation
- Infrastructure: Implementación de repositorios, DbContext, configuración de EF Core
- Api: Controllers, middleware, autenticación
- Web: Frontend Blazor WebAssembly

### DEC-002: Gestión de Estados como Enum
**Decisión**: Implementar los estados de eventos como enumeración en el dominio, no como tabla de catálogo.
**Justificación**: Los estados tienen lógica de transición definida y no deben ser modificables por usuarios. La lógica de dominio controla las transiciones válidas.

### DEC-003: Código de Evento con Secuencia SQL
**Decisión**: Generar códigos de evento usando SEQUENCE de SQL Server.
**Justificación**: Garantiza unicidad sin bloqueos de tabla y permite el formato `EVT-YYYYMMDD-NNNNNN`.

## Autenticación y Seguridad

### DEC-004: JWT para Autenticación de API
**Decisión**: Usar JWT Bearer tokens para autenticación de la API.
**Justificación**: Permite que el frontend Blazor WebAssembly almacene el token y lo envíe en cada solicitud. Facilita futura integración con otros sistemas.

### DEC-005: Refresh Token
**Decisión**: Implementar refresh tokens para sesiones de larga duración.
**Justificación**: Mejora la experiencia de usuario sin comprometer la seguridad. Los tokens de acceso expiran en 30 minutos, los refresh tokens en 7 días.

## Base de Datos

### DEC-006: Esquema [mnt]
**Decisión**: Usar el esquema `mnt` para todas las tablas de negocio.
**Justificación**: Separación clara de las tablas de Identity y permite mejor organización.

### DEC-007: Soft Delete para Catálogos
**Decisión**: Implementar campo `Activo` en lugar de eliminación física.
**Justificación**: Mantiene integridad referencial y cumple requisito de no eliminar registros utilizados.

### DEC-008: DateTimeOffset para Timestamps
**Decisión**: Usar `datetimeoffset` para todas las fechas/horas.
**Justificación**: Permite correcto manejo de zona horaria Argentina sin ambigüedades.

### DEC-009: RowVersion para Concurrencia
**Decisión**: Usar `rowversion` en tabla Eventos para control de concurrencia optimista.
**Justificación**: Previene actualizaciones concurrentes conflictivas sin necesidad de locks explícitos.

## API

### DEC-010: Versionado de API
**Decisión**: Versionar API con prefijo `/api/v1/`.
**Justificación**: Permite evolución futura sin romper clientes existentes.

### DEC-011: ProblemDetails para Errores
**Decisión**: Usar RFC 7807 (ProblemDetails) para respuestas de error.
**Justificación**: Estándar de la industria que proporciona estructura consistente para errores.

### DEC-012: Paginación por Defecto
**Decisión**: Límite máximo de 100 registros por página, default 20.
**Justificación**: Evita sobrecarga del servidor y mejora rendimiento.

## Frontend

### DEC-013: Blazor WebAssembly Standalone
**Decisión**: Usar Blazor WebAssembly en modo standalone (no hosted).
**Justificación**: El frontend se despliega como archivos estáticos, la API es independiente. Facilita despliegue y escalado independiente.

### DEC-014: LocalStorage para Token
**Decisión**: Almacenar JWT en LocalStorage usando Blazored.LocalStorage.
**Justificación**: Persiste entre sesiones del navegador. Se mitigan riesgos XSS mediante sanitización y políticas CSP.

### DEC-015: Interfaz en Español
**Decisión**: Toda la interfaz en español, incluyendo mensajes de validación.
**Justificación**: Requisito explícito del cliente para operación en planta.

## Logging y Observabilidad

### DEC-016: Serilog con Archivos Rotativos
**Decisión**: Usar Serilog con sink de archivo rotativo diario.
**Justificación**: No depende de servicios externos, logs accesibles localmente.

### DEC-017: Correlation ID
**Decisión**: Generar Correlation ID en cada request y propagarlo a logs y auditoría.
**Justificación**: Permite trazar una solicitud completa a través de todos los componentes.

## Convenciones de Código

### DEC-018: Nombres de Negocio en Español
**Decisión**: Entidades, propiedades de negocio y DTOs usan nombres en español.
**Justificación**: Facilita comprensión del dominio por parte del equipo y usuarios.

### DEC-019: Código Técnico en Inglés
**Decisión**: Nombres de métodos, interfaces, servicios y conceptos técnicos en inglés.
**Justificación**: Convención estándar de la industria, facilita mantenimiento.

---

## Decisiones - 08/08/2026 (Rediseño según reunión con Sebastián Ferretti)

### DEROGACIONES

#### DEC-003 (PARCIALMENTE DEROGADA)
**Original**: Prefijo de código `EVT-YYYYMMDD-NNNNNN`
**Nuevo**: Prefijo `DET-YYYYMMDD-NNNNNN` (Detención en lugar de Evento)
**Motivo**: Renombre de Evento a Detención para alinear con nomenclatura de planta.

#### REGLA IMPLÍCITA DEROGADA: "El tiempo de parada nunca se ingresa manualmente"
**Nuevo modelo**: El operador puede declarar minutos manualmente (Fase 1). El sistema soporta tres orígenes de tiempo:
- `DECLARADO`: minutos tipeados por el operador
- `CALCULADO`: diferencia entre FechaHoraParada y FechaHoraArranque
- `PLC`: horas informadas automáticamente (Fase 2)

`MinutosEfectivos` es el único campo que consume el sistema. Se calcula en backend según las reglas de precedencia.

### NUEVAS DECISIONES

### DEC-020: Renombre Maquina → Equipo
**Decisión**: Renombrar la entidad `Maquina` a `Equipo` en todo el sistema.
**Justificación**: En planta, "la máquina" es la línea completa; "el equipo" es una parte de la línea. El código actual lo tenía al revés.

### DEC-021: Renombre Evento → Detencion
**Decisión**: Renombrar la entidad `Evento` a `Detencion` en todo el sistema.
**Justificación**: El término "evento" es genérico. El sistema registra detenciones de máquina.

### DEC-022: Clasificación en Cascada
**Decisión**: Reemplazar `TipoEvento` + `Causa` por árbol `TipoFalla` → `Equipo` → `DetalleFalla`.
**Justificación**: El modelo actual (plano e independiente) no refleja cómo la planta clasifica las fallas. El árbol en cascada permite filtrar y validar coherencia.

### DEC-023: Turno Materializado
**Decisión**: Guardar `TurnoId` materializado en cada detención, no derivarlo al consultar.
**Justificación**: Si cambian los horarios de turno, los registros históricos deben mantener el turno al que pertenecieron originalmente.

### DEC-024: Estado REGISTRADA es Completo
**Decisión**: Una detención en estado `REGISTRADA` es un dato válido y completo. El flujo a `CERRADA` es opcional.
**Justificación**: El operador registra después de resolver. El seguimiento de Mantenimiento es una herramienta opcional, no un requisito.

### DEC-025: Snapshot de Estándares
**Decisión**: Guardar `ValorEstandarSnapshot` en registros de proceso, no hacer JOIN al estándar vigente.
**Justificación**: La consulta histórica debe mostrar contra qué estándar se comparó ESE DÍA, no el vigente hoy.

### DEC-026: Implementar Servicios en Application
**Decisión**: Implementar los servicios de Application (opción a de sección 9.2), no eliminar las interfaces.
**Justificación**: La lógica de cálculo de tiempo, derivación de turno, validación de cascada y comparación contra estándar necesita tests unitarios. Con servicios implementados, la lógica es testeable de forma aislada.

### DEC-027: Puestos Operativos
**Decisión**: Crear catálogo de puestos (`MAQUINISTA`, `AYUDANTE`, `PACKAGING`) separado de roles de Identity.
**Justificación**: Las inspecciones se asignan a un puesto, no a un rol. Un operario puede ser maquinista o de packaging, con checklists distintos.

---

## Decisiones - 19/08/2026 (Cierre Fase 1)

### DEC-028: Dashboard Consolidado
**Decisión**: El dashboard integra indicadores de los 3 ejes en una sola vista.
**Justificación**: Permite a supervisores y gerencia ver el estado general sin navegar múltiples pantallas.

### DEC-029: Patrón Result<T> en Application
**Decisión**: Los servicios de Application retornan `Result<T>` en lugar de lanzar excepciones.
**Justificación**: Hace explícitos los casos de error, facilita testing y evita try-catch en controllers.

### DEC-030: Tests de Dominio con xUnit
**Decisión**: Tests unitarios de entidades de dominio usando xUnit + Theory/InlineData.
**Justificación**: Validan comportamiento de propiedades calculadas, transiciones de estado y valores por defecto sin depender de infraestructura.

---

## Decisiones - 22/08/2026 (UI/UX Fase 1)

### DEC-031: Dark Mode con Bootstrap 5.3 Nativo
**Decisión**: Usar `data-bs-theme` en `<html>` en lugar de CSS custom.
**Justificación**: Bootstrap 5.3 incluye soporte nativo de dark mode. Evita duplicación de estilos.

### DEC-032: Sidebar Colapsable con Navegación por Íconos
**Decisión**: Sidebar se colapsa a 60px mostrando solo íconos. Click en sección colapsada navega directo.
**Justificación**: Permite más espacio de trabajo sin perder acceso a navegación.

### DEC-033: DateOnly para Fechas sin Hora
**Decisión**: Usar `DateOnly` en DTOs de API para campos de fecha (no fecha/hora).
**Justificación**: .NET 6+ soporta DateOnly. Evita ambigüedades de zona horaria en fechas puras.

### DEC-034: AutocompleteSelect Genérico
**Decisión**: Componente `AutocompleteSelect<T>` para dropdowns con búsqueda.
**Justificación**: Reusable en todas las pantallas con catálogos largos. Mejora UX de operario.

---

## Registro de Cambios

| Fecha | Decisión | Autor |
|-------|----------|-------|
| 2026-07-21 | DEC-001 a DEC-019 | Claude Code |
| 2026-08-08 | Derogaciones + DEC-020 a DEC-027 | Claude Code |
| 2026-08-19 | DEC-028 a DEC-030 (Cierre Fase 1) | Claude Code |
| 2026-08-22 | DEC-031 a DEC-034 (UI/UX Fase 1) | Claude Code |
