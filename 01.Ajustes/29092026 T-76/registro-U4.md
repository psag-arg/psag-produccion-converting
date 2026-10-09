# Registro U4 · Identificación de fallas

**Estado: ISSUES.** Funciona de punta a punta en la API y en la pantalla (SQLite con el historial de Tango 2). Hay una brecha que no puede cerrar esta unidad: el endpoint viejo de cierre de U3 todavía deja cerrar un análisis sin solución ni causa (ver Pendientes).
Nada está mergeado, desplegado ni publicado. Solo commits locales en `t76-u4`.

## Commits

| Hash | Mensaje |
|---|---|
| e974433 | feat(dominio): funciones de permiso y cierre del análisis de falla |
| 55a8abc | feat(infra): consultas de análisis abiertos, cerrados y base de soluciones |
| f329c26 | feat(aplicacion): servicio de análisis de falla y pendientes |
| 7d0ca74 | feat(api): endpoints de análisis, pendientes y base de soluciones |
| be9581b | feat(web): bloque de identificación de fallas en el detalle |
| 59824dd | feat(web): pantallas Mis pendientes y Base de soluciones |
| (este) | docs(t76): registro de la unidad U4 |

## Qué se hizo

- **Detalle de detención**: stepper de estados arriba (Registrada, Requiere análisis solo si aplica, En análisis, Cerrada), bloque "Identificación de fallas" (Falla de solo lectura, Solución, Causa, Plan de acción, firma), "Por qué está acá" y "Fallas parecidas" con enlace a la base. El botón "Cerrar Detención" se oculta solo en detenciones con análisis; las demás siguen igual.
- **Permisos en backend**: `AnalisisFallaReglas` (Domain, función pura) decide quién edita (responsable por rol o por usuario, o ADMINISTRADOR). Guardar y cerrar devuelven 403 a cualquier otro. Todos pueden leer.
- **Endpoints** (`AnalisisController`, `/api/analisis`):
  - `GET {id}`, `PUT {id}` (borrador), `POST {id}/cerrar`.
  - `GET pendientes` (con `responsableRol` o `responsableUsuarioId` para el administrador).
  - `GET pendientes/conteo`, `GET soluciones` (máximo 50, búsqueda por palabras).
- **Mis pendientes** (`detenciones/pendientes`): KPIs, pestañas por rol, "Esperando hace N días" en rojo desde 4 días, filtro de responsable para el administrador. OPERARIO ve "Para declarar" con el texto de estado vacío del diseño.
- **Base de soluciones** (`detenciones/soluciones`): búsqueda libre, filtros de línea, equipo y tipo de falla, tarjetas con falla, solución y causa. Acepta filtros por query string (los usa el enlace del detalle).
- **Contador** rojo en el menú "Mis pendientes" (cantidad de análisis abiertos visibles, más "Para declarar" si corresponde).

## Decisiones

| Tema | Decisión | Motivo |
|---|---|---|
| Permiso de edición | Responsable de la regla (rol o usuario) o ADMINISTRADOR, validado en el servicio | CLAUDE.md: validar en backend |
| Cierre | Exige solución y causa (no vacías). El plan es opcional | Planilla y minuta |
| Primer guardado | REGISTRADA pasa a EN_SEGUIMIENTO con entrada en el historial "Análisis iniciado" | Brief |
| Días de espera | Desde `Fecha` de la detención, no desde `FechaHoraCreacion` | `FechaHoraCreacion` queda en 0001-01-01 en las altas por POST (ISSUE 1 de U0) |
| "Cerradas este mes" | Cuenta las que cerró el usuario actual (para el administrador, las suyas) | Supuesto. Confirmar con Fran si el KPI debe ser del equipo |
| Fallas parecidas | Mismo equipo; si no tiene equipo, mismo tipo de falla. Máximo 3, excluye la detención actual | Brief |
| Nombres de usuario | La API los completa (el servicio de aplicación devuelve IDs) | Mismo patrón que `CatalogosController` |
| Errores de cierre | 400 con el motivo en `detail` (la UI lo muestra) | Mismo patrón que el resto de la API |

## Desvíos y cambios fuera de la lista de archivos

1. **Repositorio de detenciones (U0)**: agregué tres métodos a `IDetencionRepository` y `DetencionRepository` (`GetAnalisisAbiertosAsync`, `GetAnalisisCerradosPorUsuarioAsync`, `GetSolucionesAsync`). Hacen falta `Include` y `Take` que el repositorio genérico no tiene. Aditivos. Si U3 toca esa interfaz, puede haber conflicto de merge.
2. **NavMenu**: además de la línea del enlace, el badge necesita inyectar `IAnalisisService` y refrescar el conteo al navegar (`LocationChanged`). Son unas 30 líneas.
3. **Stepper**: el brief lo pedía dentro del bloque. Lo puse arriba del detalle para todas las detenciones, con "Requiere análisis" solo si aplica.
4. **Falla duplicada**: la Información General y el bloque Falla muestran los mismos datos (línea, fecha, equipo, tipo, minutos, descripción). Es lo que pidió el brief. Fran decide si colapsar.
5. **Acciones Realizadas**: se oculta en Información General cuando la detención requiere análisis, porque ahí se muestra como Solución.

## Pendientes y para decidir

- **Brecha de cierre (bloqueante para el criterio "cerrar sin causa da 400" en la UI completa)**: `POST /api/detenciones/{id}/cerrar` (DetencionesController, U3) todavía cierra detenciones con análisis sin solución ni causa. El detalle ya no muestra ese botón para ellas, pero el endpoint sigue abierto. Hace falta que U3 lo rechace si `RequiereAnalisis` o que el coordinador decida.
- **ISSUE 1 de U0, sin tocar**: "Registrado" y el historial de altas por POST muestran 01/01/0001. Se ve en el detalle (captura incluida).
- **Botón "Declarar"** de la pestaña Para declarar no está. La lista está vacía hasta la señal del PLC y el brief no lo pedía.
- **Bloque "Qué ve cada rol"** del diseño no está implementado. Es explicación para la revisión del diseño, no pantalla de uso.
- **Test de servicio con mocks**: no hay librería de mocks en `Application.Tests`. Las reglas de cierre y permiso están en funciones puras con tests en Domain, y el flujo completo se probó contra la API real.

## Verificación

### Build y tests

| Comando | Resultado |
|---|---|
| `dotnet build 45064123_App/PSAG.Mantenimiento.sln` | `0 Error(s)`, `0 Warning(s)` (línea base y final) |
| `dotnet test 45064123_App/PSAG.Mantenimiento.sln` | Domain 83 (73 de base + 10 nuevos), Application 1, Integration 1. Total 85, 0 fallidos |

Tests nuevos: `AnalisisFallaReglasTests` (permiso de responsable por rol, por usuario, admin, operario sin asignación, otro usuario asignado, cierre sin solución, cierre sin causa, cierre válido, `EstaAbierto`).

### API real (SQLite, puerto 5304)

Preparación: regla por SQL (Tango 2, FALLO EQUIPO, 60 min, MANTENIMIENTO). Usuarios `cperez` (MANTENIMIENTO) y `lrojas` (OPERARIO, asignado a Tango 2) creados con `POST /api/usuarios`. Detenciones 767 a 771 creadas por `POST /api/detenciones` como `lrojas`. Las cinco quedaron con `RequiereAnalisis = 1` y la regla 1.

| Caso | Resultado |
|---|---|
| MANTENIMIENTO, `GET pendientes` | `paraAnalizar` 2, `enAnalisis` 1, `demoraPromedioDias` 5,33; conteo 5 antes de cerrar y 3 después |
| MANTENIMIENTO, `PUT 767` borrador | 200, pasa a `EN_SEGUIMIENTO`, entrada de historial "Análisis iniciado" con fecha real |
| MANTENIMIENTO, cerrar sin causa | 400, "Para cerrar el análisis falta la causa de la falla." |
| MANTENIMIENTO, cerrar sin solución | 400 |
| MANTENIMIENTO, cerrar 768 con solución y causa | 200, `CERRADA`, firma "Carlos Pérez", `fechaHoraAnalisis` cargada |
| MANTENIMIENTO, cerrar 768 otra vez | 400 |
| `PUT` con `rowVersion` vencido | 409 |
| OPERARIO, `GET 767` | 200, `puedeEditar` false, responsable "Mantenimiento" |
| OPERARIO, `PUT 767` y `POST 767/cerrar` | 403 y 403 |
| `GET soluciones?busqueda=carro limpieza rodillo` | 1 resultado (768), con firma |
| `GET 767` fallas parecidas | 768 y 771 (mismo equipo REBOBINADORA) |
| ADMINISTRADOR, filtro `responsableRol=MANTENIMIENTO` | 3 abiertos; opciones de responsable con rol |
| OPERARIO, "Para declarar" con 769 en `PENDIENTE_JUSTIFICACION` (temporal) | Lista 1 con línea "Tango 2", conteo 1. Revertido a REGISTRADA |
| Detención 771 eliminada (temporal) | `GET 771` 404, sale de la base de soluciones. Restaurada |
| Sin token / id inexistente | 401 / 404 |
| Errores 500 en `run/u4/api-5304.log` | 0 |

Nota: en la base local escribí por error un valor numérico en `EstadoActual` de la 769. Lo corregí a `REGISTRADA` y verifiqué `GET 769` 200. No afecta al código.

### Pantalla real (Web puerto 5314, capturas headless)

Login y capturas con `capturas.py` (MANTENIMIENTO, OPERARIO y ADMINISTRADOR). Sin errores de consola propios en las rutas capturadas. Las capturas de página completa muestran la barra lateral fija en mitad de página: es un artefacto de captura de la app, no de este cambio.

### Datos del historial

El cargador original creó dos detenciones y 46.021 filas de tiempo programado con fecha 1900 (error de dos fechas del Excel). Según la indicación del coordinador, las eliminé de mi base con el `delete` indicado. Quedaron 769 detenciones (764 del historial más 5 de prueba).

## Capturas

Carpeta: `01.Ajustes/29092026 T-76/capturas/U4/`

- `detalle-mantenimiento-borrador.png`: detalle como MANTENIMIENTO, editable, en análisis con borrador.
- `detalle-mantenimiento-cerrar-sin-causa.png`: aviso al intentar cerrar sin causa.
- `detalle-mantenimiento-cerrada.png`: detención 768 cerrada, con firma.
- `detalle-operario-lectura.png`: detalle como OPERARIO, solo lectura.
- `detalle-sin-analisis-flujo-existente.png`: detención sin análisis, flujo de cierre existente.
- `mis-pendientes-mantenimiento.png`, `mis-pendientes-mantenimiento-cerradas.png`, `mis-pendientes-administrador.png`, `mis-pendientes-operario.png`.
- `menu-badge-pendientes.png`: contador rojo en el menú.
- `base-soluciones.png`, `base-soluciones-busqueda.png`.
