# Roles y permisos configurables (T-802)

Hasta T-798 lo que cada rol veía y podía hacer estaba fijo en el código (políticas de `Api/Program.cs`,
`[Authorize(Roles=)]` en controllers y páginas, `AuthorizeView Roles=` en el menú y chequeos por rol en los servicios).
Desde T-802 sale de una **matriz rol × opción × VER / ALTA / MODIFICACIÓN / BAJA** que el administrador edita en
**Configuración → Roles y permisos** (`/configuracion/roles`).

La matriz que se carga al migrar (**script 39** y la semilla de la API en DEV/tests) **reproduce lo que hacía el código**:
nada cambia de comportamiento hasta que se edite. Está abajo para revisarla.

## Modelo

| Tabla (esquema `mnt`) | Qué guarda |
|---|---|
| `CFG_Opciones` | Catálogo de opciones: `Codigo` único, `Nombre`, `PadreId` (secciones del menú), `Ruta` de la Web, `Orden`, `AccionesAplicables` (qué de VER/ALTA/MODIFICACION/BAJA tiene sentido), `Activo`. No se edita desde la app: lo cargan el script 39 y la semilla desde `Domain/Permisos/CatalogoPermisos.cs` (única fuente). |
| `CFG_PermisosRol` | `RoleId` (AspNetRoles) + `OpcionId` + `PuedeVer`, `PuedeAlta`, `PuedeModificar`, `PuedeBaja`. Único (rol, opción). El configurador guarda **todas** las filas del rol (también las que quedan en 0). |
| `CFG_RolesConfiguracion` | `RoleId` (PK/FK) + `ObligadoATurno` + `Descripcion`. `ObligadoATurno` = 1 sólo OPERARIO (matriz migrada). Se aplica en la API y en la Web: ver **Obligado a turno**. |

Los cambios del configurador se auditan en `mnt.Auditoria` (`Accion` = CambioRol, `Entidad` = `CFG_PermisosRol`,
valores anterior y nuevo de cada opción que cambió). Las tablas de Identity no se tocan.

Reglas:

- Un usuario con varios roles tiene la **unión** de sus permisos.
- Si la opción tiene VER y no está tildado, no puede tener ALTA/MODIFICACIÓN/BAJA (el configurador y la API lo normalizan).
  Las "funciones" (opciones sin pantalla, p. ej. *Seguimiento y cierre*) sólo tienen la acción que aplica.
- El rol **ADMINISTRADOR no puede quedarse sin VER + MODIFICACIÓN de "Roles y permisos"** (la API rechaza el guardado).
- Rol nuevo creado a mano en `AspNetRoles`: sin filas no tiene ningún permiso hasta que se lo configure.

## Cómo se aplica

**API.** Cada acción de cada controller lleva `[Permiso("OPCION", AccionPermiso.X)]` (o
`[Permiso(AccionPermiso.Ver, "OPCION_1", "OPCION_2", …)]` = alcanza con una: lecturas que usan varias pantallas). Es un
`[Authorize]` (exige sesión: sin token, 401) con un requisito que resuelve `PermisoHandler`: permisos de los roles del token,
leídos de la base con **caché de 60 s que se invalida al guardar** desde el configurador (el cambio vale en la siguiente
request). Sin permiso: **403 con ProblemDetails** (`Title` = "Sin permiso", `Detail` en castellano con la opción y la acción).
Las políticas viejas se borraron. Los servicios que además chequeaban por rol (maestros de cámaras, calidad y producción,
carga/revisión de calidad, tareas, análisis cerrado, corrección de turnos) ahora preguntan el permiso
(`IPermisosUsuarioActual`); fuera de una request (jobs, tests de servicio) usan la regla por rol de antes, que es igual a la
matriz migrada.

**Web.** El menú (`NavMenu.razor`) se arma con `GET api/permisos/mios` (opciones que ve el usuario con sus acciones +
`ObligadoATurno`), cacheado en la sesión de la Web y vuelto a pedir al loguear/desloguear y al guardar en el configurador.
Cada página exige VER de su opción (`Shared/ControlPermiso.razor` + `Shared/RutasPermiso`; las páginas sin menú —alta,
detalle, impresión— usan la opción de su pantalla); sin VER muestra "No tenés permiso". Los botones de alta / editar /
dar de baja se muestran según el permiso (`<SiPermiso>` en maestros y configuración; detenciones, detalle, tareas, calidad
y presupuesto vs. real leen los permisos).

### Lo que queda en el código a propósito (alcance, no permiso de menú)

- **Alcance por línea**: el que es sólo operario carga y ve el turno sólo en sus líneas; la corrección de turnos de producción
  (T-793) es el permiso MODIFICACIÓN de "Turnos de producción", y el alcance sigue igual: ADMINISTRADOR y MANTENIMIENTO todas
  las líneas, el resto sus líneas (sin líneas asignadas, todas); horas programadas: ADMINISTRADOR todas, el resto sus líneas.
- **Responsable del análisis de falla** (`AnalisisFallaReglas`: lo completa el rol/usuario responsable o un administrador),
  quién ve la declaración de detenciones de cámara y el aviso de fin de turno (operario, supervisor, administrador), la
  propuesta de "Inicio de turno" (sólo para quien es operario), elegir operador al cargar (administrador y supervisor).
- `api/auth/logout`, `change-password`, `me` y `api/permisos/mios`: cualquier usuario logueado (sin opción).
- Sin sesión (como siempre): `api/auth/login`, `refresh`, `seed-passwords`, `/health`, ingesta de calidad y de conteo de
  cámara y `api/camaras/configuracion` (con clave de ingesta).

### Dependencias entre opciones (para tener en cuenta al editar)

- Las listas para combos (líneas, equipos, fallas, turnos, productos, operadores de línea) son **"Consultas comunes"**
  (`GENERAL.CATALOGOS`, sin menú): si se le saca a un rol, se le rompen casi todas las pantallas.
- "Declarar detención manual" guarda con ALTA de "Ver detenciones"; "Inicio de turno" opera con
  "Operar el turno"; las pantallas de maestros guardan con ALTA/MODIFICACIÓN de su opción pero leen las listas comunes.
- `GET api/usuarios` lo usan Usuarios, Maquinistas y Reglas de análisis (alcanza con VER de cualquiera de las tres).

### Cambios deliberados respecto de antes (sin efecto para el usuario)

Lecturas que antes eran de cualquier usuario logueado y ahora piden el VER de la única pantalla que las usa:
`GET api/calendario-lineas/*` y `GET api/catalogos/tiempos-programados` (Horas programadas: ADMINISTRADOR y SUPERVISOR),
`GET api/catalogos/reglas-analisis*` (Reglas de análisis: ADMINISTRADOR) y `GET api/parametros-linea-producto`
(Parámetros línea + producto: ADMINISTRADOR). `PUT api/analisis/{id}/cerrado` dejaba pasar a todos en el endpoint pero el
servicio sólo aceptaba ADMINISTRADOR y SUPERVISOR: ahora lo corta el permiso. Los accesos "Nueva tarea", "Nuevo control" y
"Nueva inspección" del menú se muestran sólo a quien tiene ALTA (antes los veía también Gerencia, que no podía guardar).

## Obligado a turno (T-802 parte 2)

Un usuario con **algún rol con `ObligadoATurno`** (tilde "Obligado a declarar turno" en Configuración → Roles y permisos;
en la matriz migrada, sólo OPERARIO) trabaja siempre dentro de un turno de producción:

1. **Después del login no puede hacer nada hasta abrir o tomar (relevo) un turno.** La Web le muestra sólo
   *Producción → Inicio de turno* (sin menú, sin barra y **sin "seguir sin tomar"**; sólo "Cerrar sesión"). El turno es el
   **en curso por hora**: lo declara él al tomarlo (ya no hay turno habitual) y sólo puede elegir **sus líneas habilitadas**.
2. **En la API** (no sólo en la Web), mientras no sea el operador vigente de un turno ABIERTO, toda acción de negocio responde
   **409** (`Title` "Turno requerido", `Detail` "Abrí o tomá un turno para empezar…", extensión `codigo` = `TURNO_REQUERIDO`).
   Quedan permitidas (`[SinTurnoObligatorio]`): `api/auth/*`, `GET api/permisos/mios`, `api/turnos-produccion/propuesta`,
   `abrir`, `{id}/relevar`, `actual` y `mio`, y las listas que usa esa pantalla (`api/catalogos/lineas`, `turnos`,
   `turnos/actual`, `maquinistas/mis-asignaciones`). Sin permiso sigue siendo 403 "Sin permiso" (la autorización va antes).
3. **Con el turno abierto, todo se ajusta a ese turno** (filtro `TurnoObligatorioFilter`, `Api/Autorizacion/TurnoObligatorio.cs`):
   - lecturas (GET): `lineaId`, `turnoId`, `turnoProduccionId` y los rangos `fechaDesde`/`fechaHasta` (`desde`/`hasta` de tipo
     fecha) —sueltos o dentro del objeto de filtro— se fijan a la línea, el turno y la fecha de producción del turno: listado
     de detenciones, dashboard/acumulado, control de proceso, inspecciones, calidad, tareas, cámaras…;
   - escrituras: una `lineaId` o `turnoProduccionId` distinta de la del turno → **403 "Fuera de tu turno"**;
   - alta de detención: además la hora (o ahora, sin horas) tiene que caer en su turno (línea, turno y fecha fijos);
   - recursos por id (`[DelTurno]`): detención / análisis / declaración de otro turno, y cerrar / cambiar producto / indicadores
     de otro turno de producción → 403;
   - *Mis pendientes* (y su contador) muestra sólo las detenciones de su turno; `api/catalogos/lineas` y `turnos` devuelven
     sólo la línea y el turno del turno (los combos de la Web quedan fijos).
4. **Al cerrar el turno** (o si lo relevan, o vence y lo cierra el sistema) vuelve a quedar bloqueado: la Web vuelve a
   *Inicio de turno* (al cerrar desde la barra, o con el 409 de la siguiente llamada).

Reglas de turno (para todos, no sólo obligados; en `TurnoProduccionService`):

- **Un solo operador vigente por turno**: el relevo cierra el tramo del anterior a la misma hora en que empieza el nuevo
  (sin superposición) y el índice único filtrado `UX_PRD_TurnosProduccionOperadores_Vigente` (script 40) lo garantiza en la
  base aunque dos relevos lleguen a la vez.
- **Un operario no tiene dos turnos abiertos**: si es el operador vigente del turno abierto de la línea A, no puede abrir ni
  tomar (ni el supervisor dejarlo vigente en) el de la línea B hasta cerrar A o ser relevado (409 "Ya tenés abierto…").
  Abrir el turno en curso de la **misma** línea sí puede: cierra el anterior de esa línea.

Configuración:

- Un rol obligado a turno necesita *Operar el turno* con VER, ALTA y MODIFICACIÓN (el configurador lo exige al guardar).
- **Alta de usuario en un solo circuito** (Configuración → Usuarios): roles + **líneas habilitadas**. Si algún rol elegido es
  obligado a turno, hace falta al menos una línea (la API lo valida; `GET api/usuarios/roles-obligados-turno` le dice a la
  pantalla cuáles son). La habilitación (`ABM_OperadoresLinea`, sin turno desde el script 40) también restringe al que es
  sólo operario a cargar detenciones en sus líneas.
- *Configuración → Maquinistas* queda **sólo como consulta** (quién está habilitado en qué línea): se sacaron el alta, la
  edición y la baja (API y pantalla) porque permitían cargar cosas imposibles (un operario con turno fijo distinto por línea,
  o líneas que no salen del usuario). Se dejó en el menú porque es la única vista "por línea" de las habilitaciones.
- Si a un usuario logueado le cambian los permisos, la Web vuelve a pedir `api/permisos/mios` al recibir un 403 y cada 5
  minutos: el menú se alinea sin volver a entrar.

## Matriz migrada

V = Ver, A = Alta, M = Modificación, B = Baja; — = nada. "(sin menú)" = función sin pantalla propia.

| Opción | Código | Ruta | ADMINISTRADOR | MANTENIMIENTO | SUPERVISOR | OPERARIO | GERENCIA |
|---|---|---|---|---|---|---|---|
| **General (sin menú)** | `GENERAL` | | | | | | |
| Consultas comunes (listas de líneas, equipos, fallas, turnos, productos y operadores) <br><sub>aplica: VER</sub> | `GENERAL.CATALOGOS` | (sin menú) | V | V | V | V | V |
| **Dashboard** | `DASHBOARD` | | | | | | |
| Inicio <br><sub>aplica: VER</sub> | `DASHBOARD.INICIO` | `/` | V | V | V | V | V |
| Reportes <br><sub>aplica: VER</sub> | `DASHBOARD.REPORTES` | `/dashboard/reportes` | V | V | V | V | V |
| Horas programadas <br><sub>aplica: VER,ALTA,BAJA</sub> | `DASHBOARD.HORAS_PROGRAMADAS` | `/configuracion/horas-programadas` | V A B | — | V A B | — | — |
| **Maestros** | `MAESTROS` | | | | | | |
| Líneas <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.LINEAS` | `/maestros/lineas` | V A M | — | — | — | — |
| Equipos <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.EQUIPOS` | `/maestros/equipos` | V A M | — | — | — | — |
| Tipos de falla <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.TIPOS_FALLA` | `/maestros/tipos-falla` | V A M | — | — | — | — |
| Fallas <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.FALLAS` | `/maestros/fallas` | V A M | — | — | — | — |
| Reglas de análisis <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.REGLAS_ANALISIS` | `/maestros/reglas-analisis` | V A M | — | — | — | — |
| Puntos de control de proceso <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.CONTROL_PROCESO` | `/maestros/control-proceso` | V A M | — | — | — | — |
| Planes de inspección <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.INSPECCIONES` | `/maestros/inspecciones` | V A M | — | — | — | — |
| Productos <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.PRODUCTOS` | `/maestros/productos` | V A M | — | — | — | — |
| Parámetros línea + producto <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.PARAMETROS` | `/maestros/parametros-linea-producto` | V A M | — | — | — | — |
| Cámaras <br><sub>aplica: VER,ALTA,MOD,BAJA</sub> | `MAESTROS.CAMARAS` | `/maestros/camaras` | V A M B | — | — | — | — |
| Calidad (puntos y defectos) <br><sub>aplica: VER,ALTA,MOD</sub> | `MAESTROS.CALIDAD` | `/maestros/calidad` | V A M | — | — | — | — |
| **Detenciones** | `DETENCIONES` | | | | | | |
| Ver detenciones <br><sub>aplica: VER,ALTA,MOD,BAJA</sub> | `DETENCIONES.LISTADO` | `/detenciones` | V A M B | V A M | V A M | V A M | V |
| Mis pendientes (análisis de falla) <br><sub>aplica: VER,MOD</sub> | `DETENCIONES.PENDIENTES` | `/detenciones/pendientes` | V M | V M | V M | V M | V M |
| Base de soluciones <br><sub>aplica: VER</sub> | `DETENCIONES.SOLUCIONES` | `/detenciones/soluciones` | V | V | V | V | V |
| Declarar detención manual <br><sub>aplica: VER</sub> | `DETENCIONES.DECLARAR_MANUAL` | `/detenciones/declarar-manual` | V | V | V | — | — |
| Seguimiento y cierre de detenciones <br><sub>aplica: MOD</sub> | `DETENCIONES.SEGUIMIENTO` | (sin menú) | M | M | — | — | — |
| Notas en detenciones <br><sub>aplica: ALTA</sub> | `DETENCIONES.NOTAS` | (sin menú) | A | A | A | A | A |
| Corregir análisis cerrado <br><sub>aplica: MOD</sub> | `DETENCIONES.ANALISIS_CERRADO` | (sin menú) | M | — | M | — | — |
| **Producción** | `PRODUCCION` | | | | | | |
| Inicio de turno <br><sub>aplica: VER</sub> | `PRODUCCION.INICIO_TURNO` | `/produccion/inicio-turno` | — | — | — | V | — |
| Operar el turno (tomar, cambiar producto, relevar, cerrar) <br><sub>aplica: VER,ALTA,MOD</sub> | `PRODUCCION.OPERACION_TURNO` | (sin menú) | V A M | V A M | V A M | V A M | V |
| Turnos de producción (corregir turno cerrado y producción) <br><sub>aplica: VER,MOD</sub> | `PRODUCCION.TURNOS` | `/produccion/turnos` | V M | V M | V M | — | — |
| Presupuesto vs. real <br><sub>aplica: VER</sub> | `PRODUCCION.PRESUPUESTO_REAL` | `/dashboard/produccion` | V | V | V | V | V |
| Tablero del turno (operador) <br><sub>aplica: VER · T-770, script 41</sub> | `PRODUCCION.TABLERO` | `/produccion/tablero` | — | — | V | V | — |
| **Tareas de mantenimiento** | `TAREAS` | | | | | | |
| Pendientes y programadas <br><sub>aplica: VER,ALTA,MOD</sub> | `TAREAS.PENDIENTES` | `/tareas-mantenimiento` | V A M | V M | V A M | V A | V |
| Historial <br><sub>aplica: VER</sub> | `TAREAS.HISTORIAL` | `/tareas-mantenimiento/historial` | V | V | V | V | V |
| **Control de Proceso** | `CONTROL_PROCESO` | | | | | | |
| Controles de proceso <br><sub>aplica: VER,ALTA,MOD</sub> | `CONTROL_PROCESO.REGISTROS` | `/control-proceso` | V A M | V A M | V A | V A | V |
| **Inspecciones** | `INSPECCIONES` | | | | | | |
| Inspecciones <br><sub>aplica: VER,ALTA,MOD</sub> | `INSPECCIONES.EJECUCIONES` | `/inspecciones` | V A M | V A M | V A M | V A M | V |
| Panel NO OK <br><sub>aplica: VER,MOD</sub> | `INSPECCIONES.NO_OK` | `/inspecciones/hallazgos` | V M | V M | V | V | V |
| **Calidad** | `CALIDAD` | | | | | | |
| Registro de defectos <br><sub>aplica: VER,ALTA,MOD</sub> | `CALIDAD.REGISTRO` | `/calidad` | V A M | V M | V A M | V | V |
| Paradas y producción (cámara) <br><sub>aplica: VER</sub> | `CALIDAD.PARADAS_CAMARA` | `/paradas-camara` | V | V | V | V | V |
| **Configuración** | `CONFIG` | | | | | | |
| Turnos <br><sub>aplica: VER,ALTA,MOD</sub> | `CONFIG.TURNOS` | `/configuracion/turnos` | V A M | — | — | — | — |
| Maquinistas (consulta) <br><sub>aplica: VER</sub> | `CONFIG.MAQUINISTAS` | `/configuracion/maquinistas` | V | — | — | — | — |
| Usuarios <br><sub>aplica: VER,ALTA,MOD,BAJA</sub> | `CONFIG.USUARIOS` | `/configuracion/usuarios` | V A M B | — | — | — | — |
| Roles y permisos <br><sub>aplica: VER,MOD</sub> | `CONFIG.ROLES` | `/configuracion/roles` | V M | — | — | — | — |

## Mapa endpoint → (opción, acción)

Todas las acciones de los controllers (161 con permiso; 10 sin opción a propósito). Lo verifica
`PermisosCatalogoTests` (ninguna acción sin `[Permiso]` salvo esas, y los roles que habilita la matriz migrada son los de
antes, con los cambios deliberados de arriba).

| Verbo | Ruta | Opción · acción |
|---|---|---|
| GET | `api/analisis/{id:long}` | `DETENCIONES.LISTADO` o `DETENCIONES.PENDIENTES` · VER |
| PUT | `api/analisis/{id:long}` | `DETENCIONES.PENDIENTES` · MODIFICACION |
| POST | `api/analisis/{id:long}/cerrar` | `DETENCIONES.PENDIENTES` · MODIFICACION |
| PUT | `api/analisis/{id:long}/cerrado` | `DETENCIONES.ANALISIS_CERRADO` · MODIFICACION |
| POST | `api/analisis/{id:long}/adjuntos` | `DETENCIONES.PENDIENTES` · MODIFICACION |
| GET | `api/analisis/{id:long}/adjuntos/{adjuntoId:long}` | `DETENCIONES.LISTADO` o `DETENCIONES.PENDIENTES` · VER |
| GET | `api/analisis/pendientes` | `DETENCIONES.PENDIENTES` · VER |
| GET | `api/analisis/pendientes/conteo` | `DETENCIONES.PENDIENTES` · VER |
| GET | `api/analisis/soluciones` | `DETENCIONES.SOLUCIONES` · VER |
| POST | `api/auth/login` | sin opción (anónimo, con clave o login) |
| POST | `api/auth/refresh` | sin opción (anónimo, con clave o login) |
| POST | `api/auth/logout` | sin opción (sesión propia / menú) |
| POST | `api/auth/change-password` | sin opción (sesión propia / menú) |
| GET | `api/auth/me` | sin opción (sesión propia / menú) |
| POST | `api/auth/seed-passwords` | sin opción (anónimo, con clave o login) |
| GET | `api/calendario-lineas/lineas` | `DASHBOARD.HORAS_PROGRAMADAS` · VER |
| GET | `api/calendario-lineas/{lineaId:int}` | `DASHBOARD.HORAS_PROGRAMADAS` · VER |
| POST | `api/calendario-lineas/{lineaId:int}` | `DASHBOARD.HORAS_PROGRAMADAS` · ALTA |
| POST | `api/calendario-lineas/versiones/{id:int}/desactivar` | `DASHBOARD.HORAS_PROGRAMADAS` · BAJA |
| GET | `api/calidad/registros` | `CALIDAD.REGISTRO` · VER |
| GET | `api/calidad/registros/{id:long}` | `CALIDAD.REGISTRO` · VER |
| GET | `api/calidad/registros/{id:long}/imagen` | `CALIDAD.REGISTRO` · VER |
| POST | `api/calidad/registros` | `CALIDAD.REGISTRO` · ALTA |
| POST | `api/calidad/registros/{id:long}/revision` | `CALIDAD.REGISTRO` · MODIFICACION |
| POST | `api/calidad/registros/{id:long}/detencion` | `CALIDAD.REGISTRO` · MODIFICACION |
| GET | `api/calidad/puntos-control` | `CALIDAD.REGISTRO` o `MAESTROS.CALIDAD` · VER |
| POST | `api/calidad/puntos-control` | `MAESTROS.CALIDAD` · ALTA |
| PUT | `api/calidad/puntos-control/{id:int}` | `MAESTROS.CALIDAD` · MODIFICACION |
| GET | `api/calidad/tipos-defecto` | `CALIDAD.REGISTRO` o `MAESTROS.CALIDAD` · VER |
| POST | `api/calidad/tipos-defecto` | `MAESTROS.CALIDAD` · ALTA |
| PUT | `api/calidad/tipos-defecto/{id:int}` | `MAESTROS.CALIDAD` · MODIFICACION |
| POST | `api/calidad/ingesta` | sin opción (anónimo, con clave o login) |
| GET | `api/camaras` | `MAESTROS.CAMARAS` o `CALIDAD.PARADAS_CAMARA` o `MAESTROS.CALIDAD` · VER |
| GET | `api/camaras/{id:int}` | `MAESTROS.CAMARAS` o `CALIDAD.PARADAS_CAMARA` o `MAESTROS.CALIDAD` · VER |
| POST | `api/camaras` | `MAESTROS.CAMARAS` · ALTA |
| PUT | `api/camaras/{id:int}` | `MAESTROS.CAMARAS` · MODIFICACION |
| POST | `api/camaras/{id:int}/desactivar` | `MAESTROS.CAMARAS` · BAJA |
| GET | `api/camaras/configuracion` | sin opción (anónimo, con clave o login) |
| GET | `api/catalogos/lineas` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/lineas/{id:int}` | `GENERAL.CATALOGOS` · VER |
| POST | `api/catalogos/lineas` | `MAESTROS.LINEAS` · ALTA |
| PUT | `api/catalogos/lineas/{id:int}` | `MAESTROS.LINEAS` · MODIFICACION |
| GET | `api/catalogos/equipos` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/equipos/{id:int}` | `GENERAL.CATALOGOS` · VER |
| POST | `api/catalogos/equipos` | `MAESTROS.EQUIPOS` · ALTA |
| PUT | `api/catalogos/equipos/{id:int}` | `MAESTROS.EQUIPOS` · MODIFICACION |
| GET | `api/catalogos/tipos-falla` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/tipos-falla/{id:int}` | `GENERAL.CATALOGOS` · VER |
| POST | `api/catalogos/tipos-falla` | `MAESTROS.TIPOS_FALLA` · ALTA |
| PUT | `api/catalogos/tipos-falla/{id:int}` | `MAESTROS.TIPOS_FALLA` · MODIFICACION |
| GET | `api/catalogos/detalles-falla` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/detalles-falla/{id:int}` | `GENERAL.CATALOGOS` · VER |
| POST | `api/catalogos/detalles-falla` | `MAESTROS.FALLAS` · ALTA |
| PUT | `api/catalogos/detalles-falla/{id:int}` | `MAESTROS.FALLAS` · MODIFICACION |
| GET | `api/catalogos/turnos` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/turnos/{id:int}` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/turnos/actual` | `GENERAL.CATALOGOS` · VER |
| POST | `api/catalogos/turnos` | `CONFIG.TURNOS` · ALTA |
| PUT | `api/catalogos/turnos/{id:int}` | `CONFIG.TURNOS` · MODIFICACION |
| GET | `api/catalogos/reglas-analisis` | `MAESTROS.REGLAS_ANALISIS` · VER |
| GET | `api/catalogos/reglas-analisis/{id:int}` | `MAESTROS.REGLAS_ANALISIS` · VER |
| POST | `api/catalogos/reglas-analisis` | `MAESTROS.REGLAS_ANALISIS` · ALTA |
| PUT | `api/catalogos/reglas-analisis/{id:int}` | `MAESTROS.REGLAS_ANALISIS` · MODIFICACION |
| GET | `api/catalogos/tiempos-programados` | `DASHBOARD.HORAS_PROGRAMADAS` · VER |
| POST | `api/catalogos/tiempos-programados` | `DASHBOARD.HORAS_PROGRAMADAS` · ALTA |
| POST | `api/catalogos/tiempos-programados/{id:int}/desactivar` | `DASHBOARD.HORAS_PROGRAMADAS` · BAJA |
| GET | `api/catalogos/maquinistas` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/maquinistas/{id:int}` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/maquinistas/por-usuario/{usuarioId}` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/maquinistas/mis-asignaciones` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/productos` | `GENERAL.CATALOGOS` · VER |
| GET | `api/catalogos/productos/{id:int}` | `GENERAL.CATALOGOS` · VER |
| GET | `api/conteo-camara/paradas` | `CALIDAD.PARADAS_CAMARA` o `PRODUCCION.TURNOS` o `PRODUCCION.PRESUPUESTO_REAL` o `DETENCIONES.LISTADO` · VER |
| GET | `api/conteo-camara/produccion` | `CALIDAD.PARADAS_CAMARA` o `PRODUCCION.TURNOS` o `PRODUCCION.PRESUPUESTO_REAL` o `DETENCIONES.LISTADO` · VER |
| GET | `api/conteo-camara/alertas` | `CALIDAD.PARADAS_CAMARA` o `PRODUCCION.TURNOS` o `PRODUCCION.PRESUPUESTO_REAL` o `DETENCIONES.LISTADO` · VER |
| POST | `api/conteo-camara/ingesta` | sin opción (anónimo, con clave o login) |
| GET | `api/control-proceso/plantilla/{lineaId:int}` | `MAESTROS.CONTROL_PROCESO` · VER |
| POST | `api/control-proceso/grupos` | `MAESTROS.CONTROL_PROCESO` · ALTA |
| PUT | `api/control-proceso/grupos/{id:int}` | `MAESTROS.CONTROL_PROCESO` · MODIFICACION |
| POST | `api/control-proceso/puntos` | `MAESTROS.CONTROL_PROCESO` · ALTA |
| PUT | `api/control-proceso/puntos/{id:int}` | `MAESTROS.CONTROL_PROCESO` · MODIFICACION |
| GET | `api/dashboard` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/resumen` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/detenciones-por-linea` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/detenciones-por-tipo-falla` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/top-detalles-falla` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/tendencia` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/indisponibilidad` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/fallos-mayores` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/pareto` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/dashboard/tiempo-programado` | `DASHBOARD.INICIO` o `DASHBOARD.REPORTES` · VER |
| GET | `api/detenciones/{id:long}/declaracion` | `DETENCIONES.LISTADO` · VER |
| POST | `api/detenciones/{id:long}/declarar` | `DETENCIONES.LISTADO` · ALTA |
| GET | `api/detenciones/aviso-fin-turno` | `DETENCIONES.LISTADO` · VER |
| GET | `api/detenciones` | `DETENCIONES.LISTADO` · VER |
| GET | `api/detenciones/{id:long}` | `DETENCIONES.LISTADO` · VER |
| POST | `api/detenciones` | `DETENCIONES.LISTADO` · ALTA |
| POST | `api/detenciones/{id:long}/cerrar` | `DETENCIONES.SEGUIMIENTO` · MODIFICACION |
| POST | `api/detenciones/{id:long}/notas` | `DETENCIONES.NOTAS` · ALTA |
| GET | `api/detenciones/abiertas` | `DETENCIONES.LISTADO` · VER |
| PUT | `api/detenciones/{id:long}` | `DETENCIONES.LISTADO` · MODIFICACION |
| POST | `api/detenciones/{id:long}/iniciar-seguimiento` | `DETENCIONES.SEGUIMIENTO` · MODIFICACION |
| GET | `api/detenciones/resumen-turno` | `DETENCIONES.LISTADO` · VER |
| GET | `api/detenciones/export/xlsx` | `DETENCIONES.LISTADO` · VER |
| POST | `api/detenciones/previsualizar-analisis` | `DETENCIONES.LISTADO` · ALTA |
| POST | `api/detenciones/{id:long}/eliminar` | `DETENCIONES.LISTADO` · BAJA |
| POST | `api/detenciones/{id:long}/restaurar` | `DETENCIONES.LISTADO` · BAJA |
| GET | `api/detenciones/export/csv` | `DETENCIONES.LISTADO` · VER |
| GET | `api/detenciones/superpuestas` | `DETENCIONES.LISTADO` · ALTA |
| GET | `api/indicadores/turno/{turnoProduccionId:long}` | `PRODUCCION.PRESUPUESTO_REAL` o `PRODUCCION.TURNOS` o `PRODUCCION.OPERACION_TURNO` · VER |
| GET | `api/indicadores/produccion` | `PRODUCCION.PRESUPUESTO_REAL` · VER |
| GET | `api/inspecciones/maestro/plantilla/{lineaId:int}` | `MAESTROS.INSPECCIONES` · VER |
| POST | `api/inspecciones/maestro/planes` | `MAESTROS.INSPECCIONES` · ALTA |
| PUT | `api/inspecciones/maestro/planes/{id:int}` | `MAESTROS.INSPECCIONES` · MODIFICACION |
| POST | `api/inspecciones/maestro/puntos` | `MAESTROS.INSPECCIONES` · ALTA |
| PUT | `api/inspecciones/maestro/puntos/{id:int}` | `MAESTROS.INSPECCIONES` · MODIFICACION |
| GET | `api/inspecciones/planes-linea/{lineaId:int}` | `INSPECCIONES.EJECUCIONES` · VER |
| POST | `api/inspecciones/ejecuciones/{id:long}/cerrar` | `INSPECCIONES.EJECUCIONES` · MODIFICACION |
| GET | `api/inspecciones/planes` | `INSPECCIONES.EJECUCIONES` · VER |
| GET | `api/inspecciones/pendientes` | `INSPECCIONES.EJECUCIONES` · VER |
| GET | `api/inspecciones/planes/{planId:int}` | `INSPECCIONES.EJECUCIONES` · VER |
| POST | `api/inspecciones/ejecuciones` | `INSPECCIONES.EJECUCIONES` · ALTA |
| GET | `api/inspecciones/ejecuciones/{id:long}` | `INSPECCIONES.EJECUCIONES` · VER |
| POST | `api/inspecciones/ejecuciones/guardar` | `INSPECCIONES.EJECUCIONES` · ALTA |
| GET | `api/inspecciones/ejecuciones` | `INSPECCIONES.EJECUCIONES` · VER |
| GET | `api/inspecciones/no-ok` | `INSPECCIONES.NO_OK` · VER |
| GET | `api/inspecciones/no-ok/paginado` | `INSPECCIONES.NO_OK` · VER |
| POST | `api/inspecciones/no-ok/{id:long}/resolver` | `INSPECCIONES.NO_OK` · MODIFICACION |
| GET | `api/productos-maestro` | `GENERAL.CATALOGOS` · VER |
| GET | `api/productos-maestro/{id:int}` | `GENERAL.CATALOGOS` · VER |
| POST | `api/productos-maestro` | `MAESTROS.PRODUCTOS` · ALTA |
| PUT | `api/productos-maestro/{id:int}` | `MAESTROS.PRODUCTOS` · MODIFICACION |
| POST | `api/productos-maestro/skus` | `MAESTROS.PRODUCTOS` · ALTA |
| PUT | `api/productos-maestro/skus/{skuId:int}` | `MAESTROS.PRODUCTOS` · MODIFICACION |
| GET | `api/parametros-linea-producto` | `MAESTROS.PARAMETROS` · VER |
| POST | `api/parametros-linea-producto` | `MAESTROS.PARAMETROS` · ALTA |
| PUT | `api/parametros-linea-producto/{id:int}` | `MAESTROS.PARAMETROS` · MODIFICACION |
| GET | `api/permisos/mios` | sin opción (sesión propia / menú) |
| GET | `api/permisos/roles` | `CONFIG.ROLES` · VER |
| PUT | `api/permisos/roles/{roleId}` | `CONFIG.ROLES` · MODIFICACION |
| POST | `api/registrosproceso/iniciar` | `CONTROL_PROCESO.REGISTROS` · ALTA |
| GET | `api/registrosproceso/{id:long}` | `CONTROL_PROCESO.REGISTROS` · VER |
| POST | `api/registrosproceso/guardar-valores` | `CONTROL_PROCESO.REGISTROS` · ALTA |
| POST | `api/registrosproceso/{id:long}/cerrar` | `CONTROL_PROCESO.REGISTROS` · MODIFICACION |
| GET | `api/registrosproceso` | `CONTROL_PROCESO.REGISTROS` · VER |
| GET | `api/registrosproceso/grupos/{lineaId:int}/{productoId:int}` | `CONTROL_PROCESO.REGISTROS` · VER |
| GET | `api/tablero/turno` | `PRODUCCION.TABLERO` · VER (obligado a turno: su turno; resto: `lineaId`, sólo lectura) |
| GET | `api/tareas-mantenimiento` | `TAREAS.PENDIENTES` · VER |
| GET | `api/tareas-mantenimiento/historial` | `TAREAS.HISTORIAL` · VER |
| POST | `api/tareas-mantenimiento` | `TAREAS.PENDIENTES` · ALTA |
| POST | `api/tareas-mantenimiento/programar` | `TAREAS.PENDIENTES` · MODIFICACION |
| POST | `api/tareas-mantenimiento/{id:long}/cerrar` | `TAREAS.PENDIENTES` · MODIFICACION |
| GET | `api/turnos-produccion/propuesta` | `PRODUCCION.OPERACION_TURNO` · VER |
| POST | `api/turnos-produccion/abrir` | `PRODUCCION.OPERACION_TURNO` · ALTA |
| POST | `api/turnos-produccion/{id:long}/cambiar-producto` | `PRODUCCION.OPERACION_TURNO` · MODIFICACION |
| POST | `api/turnos-produccion/{id:long}/relevar` | `PRODUCCION.OPERACION_TURNO` · MODIFICACION |
| POST | `api/turnos-produccion/{id:long}/cerrar` | `PRODUCCION.OPERACION_TURNO` · MODIFICACION |
| GET | `api/turnos-produccion/actual` | `PRODUCCION.OPERACION_TURNO` · VER |
| GET | `api/turnos-produccion/mio` | `PRODUCCION.OPERACION_TURNO` · VER |
| GET | `api/turnos-produccion` | `PRODUCCION.TURNOS` · VER |
| POST | `api/turnos-produccion/{id:long}/corregir-operador` | `PRODUCCION.TURNOS` · MODIFICACION |
| POST | `api/turnos-produccion/{id:long}/corregir-producto` | `PRODUCCION.TURNOS` · MODIFICACION |
| POST | `api/turnos-produccion/{id:long}/produccion` | `PRODUCCION.TURNOS` · MODIFICACION |
| GET | `api/usuarios` | `CONFIG.USUARIOS` o `CONFIG.MAQUINISTAS` o `MAESTROS.REGLAS_ANALISIS` · VER |
| GET | `api/usuarios/{id}` | `CONFIG.USUARIOS` · VER |
| POST | `api/usuarios` | `CONFIG.USUARIOS` · ALTA |
| PUT | `api/usuarios/{id}` | `CONFIG.USUARIOS` · MODIFICACION |
| GET | `api/usuarios/roles` | `CONFIG.USUARIOS` · VER |
| GET | `api/usuarios/roles-obligados-turno` | `CONFIG.USUARIOS` · VER |
| DELETE | `api/usuarios/{id}` | `CONFIG.USUARIOS` · BAJA |
| POST | `api/usuarios/{id}/reset-password` | `CONFIG.USUARIOS` · MODIFICACION |

## Tablero del operador (T-770)

- `PRODUCCION.TABLERO` (script **41**, también en `CatalogoPermisos.cs` y la semilla): VER para OPERARIO y SUPERVISOR.
  Administrador, mantenimiento y gerencia no lo tienen en la matriz: se les puede dar desde Roles y permisos.
- `GET api/tablero/turno`: el obligado a turno ve **su** turno (el filtro de turno obligatorio fija la línea; sin turno, 409);
  el resto pasa `lineaId` y lo ve en **sólo lectura** (el que es sólo operario, únicamente en sus líneas).
- Hub SignalR `/hubs/tablero` (sin opción en el mapa de controllers): exige sesión; `Seguir(turnoProduccionId)` exige VER del
  tablero y, al obligado a turno, que sea su turno. Sólo avisa "cambió el turno N"; los datos se piden a la API.

## Tests

- `PermisosCatalogoTests`: todas las acciones mapeadas, códigos y acciones válidos, catálogo consistente y, para cada
  una de las 159 acciones que tenían autorización, los roles de la matriz migrada = los de antes.
- `PermisosApiTests` (API entera con WebApplicationFactory, SQLite y SQL Server): 2xx con permiso, 403 sin VER en GET,
  sin ALTA en POST, sin MODIFICACIÓN en PUT/corregir y sin BAJA en eliminar/desactivar (detenciones, turnos de producción,
  maestros, cámaras, configuración); el cambio desde el configurador vale en la siguiente request; el ADMINISTRADOR no puede
  quitarse "Roles y permisos"; la semilla es la matriz migrada y no pisa lo editado.
- `Script39PermisosTests` (SQL Server): el script carga exactamente el catálogo y la matriz, es idempotente, no pisa lo
  editado, y el rollback se puede correr dos veces y volver a aplicar.
- `PermisosWebTests`: los códigos de la Web son los del catálogo, cada ruta exige su opción, menú y botones por permiso.
