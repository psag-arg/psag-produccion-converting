# Registro S2 · Simplificación web (T-76)

**Estado: PASS con desvíos menores.** Los 11 puntos del brief están hechos. Build sin errores ni advertencias,
100 pruebas en verde y las pantallas revisadas en la app local (SQLite) con capturas. Una pantalla quedó sin
verificar en pantalla (registro de proceso, ver Pendientes).

| Nivel | Estado |
|---|---|
| Local | Hecho en `t76-simplificar` (worktree `wt-s`) |
| Commit | 6 commits locales + este registro (ver tabla) |
| PR, merge, deploy | No. Nada pusheado, mergeado ni desplegado |
| Visible para el usuario final | No. Solo corre en la app local con datos de prueba |

## Commits

| Hash | Mensaje |
|---|---|
| fbd258c | refactor(api): análisis sin los datos de la falla que la pantalla ya no muestra |
| 9d0d94f | refactor(web): helpers compartidos de estado y de errores de la API |
| bcebc6c | refactor(web): detalle y pendientes con el estado y el responsable del servidor |
| bb30727 | feat(web): contador de pendientes con 30 s de espera y refresco al guardar |
| a30fa01 | refactor(web): cultura es-AR en toda la app, sin formato propio en reportes |
| 95b4ddc | refactor(web): turnos con duración del servidor y cargas en paralelo |

## Qué se hizo, por punto del brief

1. **Cultura.** `Program.cs` fija `es-AR` en `DefaultThreadCurrentCulture` y `DefaultThreadCurrentUICulture`.
   Se quitaron `Castellano` (reportes), `GetCultureInfo("es-AR")` (Mis pendientes y Turnos). No hizo falta
   `BlazorWebAssemblyLoadAllGlobalizationData`: el formato es-AR se ve bien en pantalla ("30.335,5 h").
   Las fechas explícitas `dd/MM/yyyy` no cambiaron.
2. **Badge de estado.** `Shared/EstadoUi.cs` con `BadgeClass`, `BadgeStyle` (naranja `#fd7e14` en un solo lugar)
   y `Texto`. Lo usan Detenciones, DetencionDetalle y MisPendientes. Se borraron las copias locales.
3. **Detalle sin duplicados.** Un solo bloque de falla en "Información General": Fecha, Hora de parada (si
   `OrigenTiempo != DECLARADO`), Turno, Línea, Equipo con "Crítico", Tipo, Detalle, Minutos efectivos,
   Maquinista, Registrado y Descripción. "Identificación de fallas" queda con Solución, Causa, Plan y firma.
   El historial muestra `EstadoNuevoDescripcion`.
4. **Responsable.** Se usa `ResponsableNombre` del servidor. Se borraron `Capitalizar`, `NombreResponsable`,
   `ResponsableAsignado` y `ResponsableDe` en detalle y pendientes.
5. **Turnos.** Columna "Horas" con `DuracionMinutos` del API. Se borró el cálculo local de cruce de medianoche.
6. **Errores de la API.** `Services/ApiErrores.cs` con `AsegurarExitoAsync` (la copia de CatalogosService y
   UsuariosService) y `ResultadoAsync` (movido desde AnalisisService).
7. **Paralelo.** Turnos (lineas + turnos + calendario), Reglas de análisis (catálogos + reglas), Base de
   soluciones (lineas + tipos + equipos) y detalle (detención + análisis) usan `Task.WhenAll` o llamadas juntas.
8. **Mis pendientes.** KPI y pestañas con `@foreach`. Pestañas con `enum Pestania`.
9. **Reportes.** `UnicosPorTipo<T>` reemplaza a los dos `GetX...Unicos`. `RangoMes` lo usan `PeriodoPareto` y los
   dos handlers de detalle por mes.
10. **Contador de pendientes.** `NavMenu` vuelve a consultar al navegar, salvo que hayan pasado menos de 30 s.
    `AnalisisService.PendientesCambiaron` dispara la consulta al guardar o cerrar un análisis.
11. **Comentarios.** Se quitaron los que repetían el código de T-76 (encabezados de tarjeta, "Modal ..." y
    resúmenes de lo que el código ya dice). Se dejaron los que explican un porqué.

## Fuera de la lista (y por qué)

- **Registro de proceso.** `RegistroProceso.razor` parseaba el input numérico con la cultura de la app. Con
  es-AR, `decimal.TryParse("7.5")` da **75** (medido con una consola C# en `scratchpad/s2/culturecheck`). El
  cambio de cultura hubiera corrompido valores de proceso. Ahora parsea y muestra con `CultureInfo.InvariantCulture`.
  El input `number` siempre envía punto decimal.
- **Turnos, input de horas.** Blazor ya lee y escribe ese input en formato invariante. Verificado en pantalla:
  se guardó `7.5` y se mostró `7,5 h`. No hizo falta cambiarlo.
- **Backend (análisis).** Se quitaron del `AnalisisDetencionDto` (Application, mapeo y controller) los campos que
  solo mostraba la tarjeta "Falla": `LineaNombre`, `Fecha`, `FechaHoraParada`, `TurnoNombre`, `MinutosEfectivos`,
  `EquipoNombre`, `EquipoCritico`, `DetalleFallaNombre`, `Descripcion`, `CreadoPorUsuarioId` y `CreadoPorUsuarioNombre`.
  El brief lo autoriza ("si quedan sin uso, quitarlos en Application y Web").
- **Dashboard.** Los dos handlers de detalle por mes (no los agregó T-76) usan `RangoMes`. Mismo comportamiento.

## Desvíos respecto del brief y del diseño

| Tema | Brief o diseño | Qué hice | Por qué |
|---|---|---|---|
| Información General | Lista del brief sin "Minutos Declarados" y con "Maquinista" | Igual al brief | Pedido explícito |
| Bloque Falla | El diseño lo pone dentro de Identificación | Se movió a Información General | Brief, punto 3 |
| Stepper y "Por qué está acá" | El brief las nombra dentro de la tarjeta Identificación | Quedan donde las pone el diseño (fuera de la tarjeta) | Diseño aprobado. La tarjeta solo tiene Solución, Causa, Plan y firma |
| Texto del responsable | Diseño: "Completa Mantenimiento" | "Completa Rol Mantenimiento" (texto del servidor) | Brief, punto 4 |
| Badge en Mis pendientes | `AnalisisPendiente` | `PendienteAnalisisDto` no trae ese campo. Se usa "pestaña distinta de Cerradas" | Las pestañas ya separan abiertas de cerradas. No agregué un campo de backend para esto |
| `Capitalizar` en Mis pendientes | Borrar | Queda uno, para el subtítulo "Rol: Mantenimiento" | No es copia del responsable |
| `ResultadoAsync` | Borrar si es equivalente | No es equivalente (devuelve tupla y texto genérico). Se movió a `ApiErrores` | Un solo lugar, mismo comportamiento |
| Análisis en paralelo | Detalle y análisis juntos | El análisis se pide también para detenciones sin análisis (el endpoint responde 200 a cualquiera). La pantalla lo ignora | Costo: una consulta extra por detalle. Secuencial hubiera sido más lento en el caso más común |
| Ruta de capturas | Brief común: `capturas/S/` | `capturas/final/`, como pide el brief de S2 | No se creó `capturas/S/` |
| Hora de parada | `OrigenTiempo != DECLARADO` | Igual, además exige que la hora exista | Evita un renglón vacío |

## Verificación

Entorno: API en `localhost:5320` y Web en `localhost:5321` con `run-local.sh`, base SQLite propia en
`scratchpad/run/s2/psag.db`. Historial cargado con `cargar_historial.py`: **764 detenciones** (el brief dice 766;
S1 midió lo mismo). Datos de prueba: regla FALLO EQUIPO ≥ 60 min para MANTENIMIENTO; usuarios `mantenimiento1` y
`operario1` (línea Tango 2); detención 765 de 145 min (cerrada durante la prueba de contador) y 766 de 145 min
(pendiente). Logs en `scratchpad/run/s2/`.

| Corrida | Resultado |
|---|---|
| `dotnet build 45064123_App/PSAG.Mantenimiento.sln` (corrida final) | `0 Warning(s)`, `0 Error(s)` |
| `dotnet test 45064123_App/PSAG.Mantenimiento.sln --no-build` | Domain 88, Application 7, Integration 5. Todas verdes (100) |
| Decimal en Turnos (`pruebas.py decimal`) | Guardó `7.5` como `7,5 h`. Al reabrir, el input muestra `7.5`. Sin errores de consola. Excepción de prueba quitada |
| Contador (`pruebas.py nav`) | Login: 1 consulta. Navegar a pendientes y volver con atrás (dentro de 30 s): 0 consultas nuevas. Cerrar análisis sin navegar: badge 2 → 1 y 1 consulta nueva. Estado "Cerrada". Sin errores de consola |
| Consola en capturas (`capturas.py`) | Ninguna corrida reportó `console errors` |
| Log de la API | Sin respuestas 500 de los endpoints usados. Una excepción de conexión a SQLite en un login cancelado (15:38:28), el reintento anduvo. No es de estas pantallas |

Capturas revisadas con `Read` (carpeta `01.Ajustes/29092026 T-76/capturas/final/`):

| Archivo | Qué muestra |
|---|---|
| 01-detenciones-lista.png | Listado admin: badge naranja "Requiere análisis", 765 "Cerrada" |
| 02-detalle-admin-requiere-analisis.png | Detalle admin de 766: falla una sola vez, fallas parecidas (765) |
| 03-detalle-admin-cerrada.png | Detalle de 765 cerrada: solución, causa y firma; historial "Cerrada" |
| 04-detalle-mantenimiento-editable.png | Detalle de 766 como mantenimiento (editable) |
| 05-detalle-operario-solo-lectura.png | Detalle de 766 como operario (solo lectura) |
| 06-mis-pendientes-mantenimiento.png | KPI, pestañas y tabla con badge naranja |
| 07-mis-pendientes-operario.png | Pestaña "Para declarar" vacía |
| 08-reportes-indisponibilidad.png | Números en es-AR ("22.739", "30.335,5 h", "1,6%") |
| 09-reportes-modal-tiempo-programado.png | Modal con "5.111,5 h (306.690 min)". Captura de viewport, sin artefacto |
| 10-turnos-calendario.png | Columna Horas con `DuracionMinutos`, calendario en "Octubre 2026" |
| 11-reglas-analisis.png | Lista de reglas |
| 12-base-soluciones.png | Un análisis cerrado con solución y causa |

## Pendientes y para decidir

- **Registro de proceso en pantalla: NO VERIFICADO.** La base local no tiene productos ni estándares, así que no
  se puede cargar un registro. El parseo está verificado en consola C#, no en la pantalla.
- **Maquinista vacío.** Si el usuario no tiene nombre completo, la API devuelve `""` y la pantalla muestra
  "No especificado". Decidir si la API cae al nombre de usuario (cambio de backend).
- **Campos del análisis que siguen sin uso en la web:** `Codigo`, `EstadoActual`, `EstadoActualDescripcion` y
  `RequiereAnalisis`. Se dejaron como identificación del registro. Se pueden quitar en otra pasada.
- **Layout del listado (no tocado, ya existía):** "Tango 2" y "Turno 1 (Manana)" se parten en dos renglones.
- **Color del botón "Completar análisis"** en el listado usa `var(--bs-orange)`. Es el mismo naranja que
  `EstadoUi`, pero no sale del helper.
- **Script 17 en SQL Server:** sigue sin verificar en un motor real (venía de U0). No es parte de S2.

## Archivos cambiados

Backend (commit fbd258c): `Api/Controllers/AnalisisController.cs`,
`Application/DTOs/Analisis/AnalisisDtos.cs`, `Application/Services/Implementations/AnalisisService.cs`.

Web: `Shared/EstadoUi.cs` (nuevo), `Services/ApiErrores.cs` (nuevo), `Services/AnalisisService.cs`,
`Services/CatalogosService.cs`, `Services/UsuariosService.cs`, `Layout/NavMenu.razor`, `Models/AnalisisModels.cs`,
`Models/CatalogoModels.cs`, `Models/DetencionModels.cs`, `Pages/DetencionDetalle.razor`,
`Pages/MisPendientes.razor`, `Pages/Detenciones.razor`, `Pages/DashboardReportes.razor`,
`Pages/RegistroProceso.razor`, `Pages/Configuracion/Turnos.razor`, `Pages/Maestros/ReglasAnalisis.razor`,
`Pages/BaseSoluciones.razor`, `Program.cs`.

Docs: `01.Ajustes/29092026 T-76/registro-S2.md`, `01.Ajustes/29092026 T-76/capturas/final/*.png` (12 archivos).
