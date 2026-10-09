# Registro U3 · Detenciones (tabla, exportación, eliminar, análisis, hora)

**Estado: PASS con pendientes.** Funciona en la app real (API y web en SQLite local). Nada está mergeado, desplegado ni publicado. Commits locales en `t76-u3`.

## Commits

| Hash | Mensaje |
|---|---|
| 18dea58 | feat(dominio): calcular minutos de carga manual por hora de inicio y fin |
| 267fc95 | feat(infra): filtro de análisis pendiente y vista de eliminadas en detenciones |
| 19a1459 | feat(aplicacion): exportar detenciones a Excel con ClosedXML |
| f8fd0e1 | feat(api): exportar xlsx, eliminar y restaurar detenciones y horas en carga manual |
| b6ac11c | feat(web): exportar Excel, eliminar con motivo y horas en la carga de detenciones |
| f6acfaa | fix(api): cerrar detención con análisis requerido devuelve 400 |

Este registro y las capturas van en un commit aparte (ver último commit de la rama).

## Qué se hizo

1. **Exportar Excel (#13).** Botón "Exportar Excel" junto a "Nueva Detencion". `GET /api/detenciones/export/xlsx` con los mismos filtros de la lista (búsqueda, fechas, línea, estado, análisis, ver eliminadas). Genera .xlsx en `ExportacionService.GenerarDetencionesXlsx` (ClosedXML 0.105.1, en el proyecto Application). Columnas: Código, Fecha, Hora, Línea, Equipo, Tipo de falla, Detalle de falla, Minutos, Estado, Análisis, Turno, Descripción, Solución, Causa, Eliminada. La descarga usa un módulo JS nuevo `wwwroot/js/descargas.js` (importado con `JS.InvokeAsync<IJSObjectReference>("import", ...)`, sin tocar `index.html`). El CSV sigue igual.
2. **Eliminar (#1).** Botón de papelera en Acciones, sólo para ADMINISTRADOR. Modal con "Motivo" obligatorio (máx. 500). `POST /api/detenciones/{id}/eliminar` (policy `RequireAdministrador`): marca `Eliminada`, `FechaHoraEliminacion`, `EliminadaPorUsuarioId`, `MotivoEliminacion` y escribe en Auditoria (`CorreccionAdministrativa`, entidad `Detencion`). `POST /api/detenciones/{id}/restaurar` (admin): limpia esos campos y audita. No hay DELETE HTTP. Toggle "Ver eliminadas" sólo para admin: el backend lo honra sólo si `User.IsInRole("ADMINISTRADOR")` y usa `IgnoreQueryFilters`. Las eliminadas se ven atenuadas, con badge "Eliminada" y botón Restaurar.
3. **Estado y análisis (#7, #9).** Filtro Estado: "Registrada / En análisis / Cerrada" (el literal "En Seguimiento" ya no está; el valor `EN_SEGUIMIENTO` no cambió). Filtro nuevo "Análisis": Todos / Requiere análisis / Sin análisis. Filas con `RequiereAnalisis` y no cerradas muestran badge naranja "Requiere análisis" en lugar de "Registrada", y botón naranja de herramientas que va a `detenciones/{id}`.
4. **Hora en carga manual (#20).** Campos opcionales "Hora inicio" y "Hora fin" (`type=time`). Si van las dos, el backend calcula `FechaHoraParada`/`FechaHoraArranque` desde Fecha + horas (fin anterior al inicio = día siguiente), `OrigenTiempo = CALCULADO`, `MinutosEfectivos` y `TurnoId` a partir de la hora de inicio (`GetTurnoPorHoraAsync`). Los minutos del cliente se ignoran. El modal muestra los minutos calculados en solo lectura. Columna "Fecha y hora".
5. **Edición por PUT.** El botón "Actualizar" del modal de edición hacía un POST (creaba una detención nueva). Ahora llama `PUT /api/detenciones/{id}` con RowVersion y conserva acciones y resolución. Esto era necesario para que las horas se guarden al editar.
6. **Alerta de análisis en el modal.** Se eligió la opción más simple: `POST /api/detenciones/previsualizar-analisis` (usa el mismo DTO de creación y el mismo `AplicarReglaAnalisisAsync`, no guarda nada). El modal la consulta al cambiar línea, tipo de falla, equipo, minutos u horas, y muestra "Esta detención va a requerir análisis." cuando corresponde.
7. **Cerrar con análisis (pedido del coordinador).** `POST /api/detenciones/{id}/cerrar` devuelve 400 si `RequiereAnalisis = true`, con el mensaje indicado. Commit separado (f6acfaa).

## Decisiones y por qué

| Decisión | Motivo |
|---|---|
| "Requiere análisis" (filtro) = `RequiereAnalisis && Estado != CERRADA`. "Sin análisis" = el resto, incluidas las cerradas | Coincide con el badge naranja. Una cerrada ya tiene análisis hecho. **Para confirmar con Fran**: si "Sin análisis" debe significar sólo `!RequiereAnalisis`, es un cambio de una línea en `DetencionRepository` |
| Hora sólo se muestra en la lista y en el Excel si `OrigenTiempo != DECLARADO` | El loader de histórico guarda `FechaHoraParada` = 08:00 como placeholder en filas DECLARADO. Mostrarlo daría una hora falsa en 764 filas |
| Hora mostrada en hora local (`ToLocalTime()`) | En SQLite de desarrollo el conversor guarda `DateTimeOffset` como UTC, y al leer vuelve con offset +00:00 (una carga de 05:40 se veía como 03:40). En SQL Server el offset se conserva. Supone que el servidor y los operadores están en la misma zona horaria (Argentina) |
| Turno en el modal: "Según hora de inicio" (sin nombre) | El cliente no tiene endpoint para calcular el turno por hora. El backend sí lo calcula al guardar |
| Eliminar/restaurar auditan con `CorreccionAdministrativa` | No agrego valores al enum (la tabla de auditoría no se tocó). CLAUDE.md pide "corrección administrativa = nuevo registro de auditoría" |
| Exportar Excel visible para todos los roles (sólo `[Authorize]`) | Igual que el CSV existente y con los mismos datos que ve cada rol en la tabla. Si Fran quiere limitarlo a `CanExport`, es un atributo |
| `Hora inicio` y `Hora fin` opcionales, sin asterisco | Lo pide el brief. El diseño muestra asterisco; no lo seguí |

## Desvíos del alcance declarado

- **Controller fuera de "sólo export/eliminar/filtros":** create y update necesitan la lógica de horas (la lista del brief lo pide en el punto 4). Los cambios son acotados: validación, cálculo y turno.
- **Archivos de U0 tocados (mínimos):** `Domain/Interfaces/IDetencionRepository.cs` y `Infrastructure/Repositories/DetencionRepository.cs` (dos parámetros opcionales en `GetPaginadoAsync` y un método `GetByIdIncluyendoEliminadasAsync`). Sin esto no hay filtro de análisis ni restaurar. `DetencionService.ObtenerPaginadoAsync` pasa ahora el filtro con argumentos con nombre. Ese método no tiene llamadas en el código (ver pendientes).
- **`NuevaDetencion.razor` no se tocó.** Se accede sólo por la URL `/detenciones/nueva`, no hay ningún enlace. No tiene campos de hora, pero el backend acepta create sin horas, así que no rompe. Recomiendo borrarla (requiere OK de Fran).
- **`DetencionDetalle.razor` no se tocó (U4).** Una detención eliminada devuelve 404 en `GET /api/detenciones/{id}` para todos, incluido admin. U4 debe contemplarlo si el botón de herramientas llega a una eliminada (hoy no llega: la fila eliminada no tiene ese botón).

## Verificación

| Qué | Cómo | Resultado | Nivel |
|---|---|---|---|
| Build | `dotnet build PSAG.Mantenimiento.sln` | `Build succeeded`, 0 Warning(s), 0 Error(s) | Compila |
| Tests | `dotnet test PSAG.Mantenimiento.sln` | Domain 76 passed (3 nuevas de `TiempoManualCalculadorTests`), Application 1 passed, Integration 5 passed. 0 failed | Tests |
| Carga manual 05:40–08:05 | `POST /api/detenciones` | 201, `minutosEfectivos` 145, `OrigenTiempo` CALCULADO, `MinutosDeclarados` null. **Turno "Turno 3 (Noche)", no mañana** (ver pendientes) | API real (SQLite) |
| Carga 07:00–09:00 | Igual | 120 min, Turno 1 (Mañana), requiere análisis = 1 | API real (SQLite) |
| Cruce de medianoche 22:00–06:00 | Igual | 480 min, arranque al día siguiente 06:00 local, Turno 3 (Noche) | API real (SQLite) |
| Validaciones | Sólo inicio / inicio = fin | 400 con mensaje ("Horas incompletas", "Horas no válidas") | API real |
| Preview de análisis | Regla de prueba (FALLO EQUIPO, 60 min) | 30 y 45 min → false; 145 min → true | API real |
| Permisos | OPERARIO: eliminar, restaurar | 403 y 403. Export 200. En la web, OPERARIO no ve Eliminar, Ver eliminadas ni Restaurar | API y navegador |
| Eliminar (admin) | `POST .../eliminar` con motivo | 204. Sin motivo: 400. Segunda vez: 404. Fila con `Eliminada=1`, motivo y usuario. Auditoria con `Detencion` | API real |
| Dashboard antes/después | `GET /api/dashboard/resumen` (filtro agosto) | antes: `totalDetenciones` 92, `minutosTotalesParada` 3365. Después de eliminar: 91 y 3245. Después de restaurar: 92 y 3365 (igual que antes) | API real |
| Ver eliminadas | Admin y OPERARIO con `verEliminadas=true` | admin: incluye la eliminada. OPERARIO: no la ve | API real |
| Restaurar | Admin y OPERARIO | 204 y 403. Segunda vez: 400. Auditoria registra ambas acciones | API real |
| Exportar (API) | `GET /api/detenciones/export/xlsx?fechaDesde=2026-08-01&fechaHasta=2026-08-31` | openpyxl: 92 filas de datos = total de la lista con el mismo filtro. Encabezados correctos | API + openpyxl |
| Exportar (navegador) | Clic en "Exportar Excel" con descarga real | Archivo `detenciones_20261009_*.xlsx`, 767 filas = total sin filtros (767) | Navegador + openpyxl |
| Editar con horas (PUT) | Modal de edición, 10:00–10:15 sobre la detención 767 (era 0 min DECLARADO) | Guarda. Queda CALCULADO, 15 min, Turno 1 (Mañana), sin alertas en el modal | Navegador + DB |
| Cerrar con análisis | `POST /api/detenciones/766/cerrar` (RequiereAnalisis = 1) | 400 con el mensaje pedido. La detención sigue REGISTRADA | API real |
| Cerrar sin análisis | `POST /api/detenciones/764/cerrar` | 204 (el endpoint existente devuelve 204, no 200). Pasa a CERRADA | API real |
| Consola del navegador | Playwright sobre lista, modales, filtros, descarga, edición | 0 errores de consola | Navegador |

Comandos usados: `zsh run-local.sh <app> 5303 5313 <db>` (levantar), `venv/bin/python -I cargar_historial.py <xlsx> <db>` (carga; 764 detenciones Tango 2, 0 fechas de 1900), scripts de API y de Playwright en la carpeta de scratchpad de la sesión.

**Capturas** en `01.Ajustes/29092026 T-76/capturas/U3/`:
- `lista_admin.png`: lista admin con badges "Requiere análisis" y botón de herramientas
- `eliminar_modal.png`: modal de eliminar con motivo
- `ver_eliminadas.png`: admin con "Ver eliminadas" activo (fila atenuada, badge Eliminada, Restaurar)
- `modal_hora.png`: carga con 05:40–08:05 (145 min) y aviso de análisis
- `filtro_requiere_analisis.png`: filtro Análisis = Requiere análisis
- `editar_con_horas_lista.png`: lista después de editar con horas
- `operario_lista.png`: vista de OPERARIO (sin acciones de admin)

## Pendientes y riesgos

1. **Turnos de la semilla vs. diseño.** La base de desarrollo tiene Mañana 06:00–14:00, Tarde 14:00–22:00 y Noche 22:00–06:00. El diseño dice Mañana 05:00–13:00. Por eso 05:40 cae en Noche y el criterio "mañana" del brief no se cumple con estos datos. La lógica del turno es la correcta (`GetTurnoPorHoraAsync`). Hay que confirmar los horarios reales (pendiente ya listado en CLAUDE.md).
2. **Loader de histórico (de U1/coordinador).** Pone `FechaHoraParada` = 08:00 como placeholder y `OrigenTiempo` DECLARADO en 764 filas. Hoy la UI lo oculta, pero conviene que no lo guarde. Decisión del coordinador.
3. **SQL Server no verificado.** U3 no agrega esquema (los campos vienen de script 17 de U0). Las consultas con `IgnoreQueryFilters` y la columna de hora no se probaron en SQL Server.
4. **No probado:** modo oscuro, ancho de teléfono.
5. **`DetencionService.ObtenerPaginadoAsync` sin llamadas.** Se conservó (quizá lo use U4 en "Mis pendientes"). Si nadie lo usa, borrarlo.
6. **Pre-existente:** `POST /api/detenciones` acepta 0 minutos en DECLARADO (la web lo valida, la API no). No se cambió.
7. **Pre-existente:** `POST /api/detenciones/{id}/cerrar` responde 204 y no 200.
