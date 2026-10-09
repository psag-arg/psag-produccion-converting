# Registro F2 · Corrección de defectos de lógica (T-76)

**Estado: PASS con 4 decisiones pendientes** (ver al final). Los 8 puntos del pedido están hechos, con tests y
probados contra la app local corriendo. La pantalla se verificó sólo en el modal de edición; las pestañas de
Mis pendientes se verificaron por API.

| Nivel | Estado |
|---|---|
| Local | Hecho en `t76-fix-logica` (worktree `wt-f2`) |
| Commit | 8 commits de corrección y este registro (ver tabla) |
| PR, merge, deploy | No. Nada pusheado, mergeado ni desplegado |
| Visible para el usuario final | No. Sólo corre en la app local con datos de prueba |

## Commits

| Hash | Punto | Mensaje |
|---|---|---|
| 284128a | 3 | fix(analisis): análisis abierto sólo en REGISTRADA o EN_SEGUIMIENTO |
| 5f21f46 | 1 | fix(analisis): mostrar las pestañas de análisis a todo responsable |
| 75ba4c9 | 2 | perf(analisis): Mis pendientes filtra por asignación en SQL |
| 517e244 | 4 | fix(analisis): re-evaluar reglas no borra un análisis en curso |
| 12dd455 | 6 | fix(detenciones): horas manuales en la zona de la planta, no la del servidor |
| 909dfab | 7 | fix(detenciones): la previsualización de análisis calcula igual que el alta |
| 0268d50 | 5 | fix(detenciones): editar no pisa horas reales ni re-deriva el turno |
| ed1c61f | 8 | refactor(detenciones): una sola copia de los campos de análisis en las vistas |

El orden no es el de los puntos: el 6 y el 7 van antes que el 5, porque el 5 usa el cálculo de tiempo que
introducen esos dos.

## Qué cambió, por punto

1. **Pestañas de análisis.** `AnalisisFallaReglas.MuestraAnalisis` muestra las pestañas al administrador, a quien
   tiene un rol responsable (OPERARIO, SUPERVISOR, MANTENIMIENTO) y a quien tiene un análisis abierto asignado por id.
   La lista de roles responsables está en el dominio y la usa también la validación de reglas.
2. **Mis pendientes en SQL.** Sólo el administrador carga todas las abiertas. El resto recibe las suyas filtradas en la
   base. La lista y el conteo usan la misma consulta (`AnalisisAbiertosAsignadosA`), que arma `AnalisisFallaReglas.AsignadaAExpr`.
3. **Análisis abierto.** `EstaAbiertoExpr` y `EstaAbierto` cambian juntos: REGISTRADA o EN_SEGUIMIENTO con análisis.
   PENDIENTE_JUSTIFICACION queda afuera.
4. **Re-evaluación de reglas.** `AnalisisFallaReglas.PuedeQuitarMarcaAnalisis`: la marca y el responsable sólo se quitan si
   la detención sigue REGISTRADA, sin solución, causa ni plan (un texto de espacios cuenta como vacío) y sin fecha de análisis.
   `DetencionService.AplicarReglaAnalisisAsync` la usa.
5. **Edición sin pisar horas.**
   - Web: el modal marca `horasEditadas` al cambiar hora de inicio o de fin, y sólo envía horas si cambiaron. En una
     detención PLC, horas y minutos son de solo lectura y no se envían. Una detención con horas no puede quedarse sin ellas.
   - API: `TiempoDetencionCalculador.Editar` decide. Horas nuevas recalculan y re-derivan el turno sólo si cambiaron.
     Fechas explícitas se toman tal cual. Sin horas, una CALCULADO o PLC conserva parada, arranque, origen, minutos y turno.
     Una DECLARADO actualiza los minutos.
6. **Zona de la planta.** `TiempoManualCalculador.Calcular` recibe la zona. `Api/Configuration/ZonaHorariaPlanta` lee
   `Planta:ZonaHoraria` (por defecto `America/Argentina/Buenos_Aires`). Si esa zona no existe, usa `Argentina Standard Time`.
   Si tampoco, la app no arranca. La clave está en `appsettings.json` y la zona se registra como singleton en `Program.cs`.
7. **Previsualización.** Alta y previsualización usan `TiempoDetencionCalculador.Calcular`. Antes la previsualización
   ignoraba un CALCULADO con fechas de parada y arranque.
8. **Campos de análisis copiados.** `DatosAnalisisMapper.ConDatosAnalisis` es el único lugar que los copia. Lo usan los
   cuatro mapeos de `DetencionService` y el controlador, que tenía su propia copia privada. Los mapeos del servicio que la API
   no usa siguen públicos. Ahora también traen `AnalisisPendiente` y `EstadoAnalisisDescripcion`.

## Pruebas unitarias

| Proyecto | Base (23dd6b3) | Ahora |
|---|---|---|
| Domain.Tests | 88 | 123 |
| Application.Tests | 7 | 9 |
| Api.IntegrationTests | 5 | 8 |
| **Total** | **100** | **140** |

- `dotnet build PSAG.Mantenimiento.sln`: 0 errores, 0 advertencias.
- `dotnet test PSAG.Mantenimiento.sln`: 140 pasan, 0 fallan.
- Tests nuevos por punto: 1 (7 casos de pestañas), 2 (paridad expresión y función), 3 (matriz de todos los estados),
  4 (ambas ramas, una por campo), 5 (decisión de edición, 7 casos), 6 (zona Tokio y Argentina, 3 casos de resolución),
  7 (cálculo compartido, 6 casos), 8 (mapper, 2 casos).

## Verificación en la app local

Base SQLite limpia con el historial real de Tango 2 (`cargar_historial.py`). API en `http://localhost:5330`, web en
`http://localhost:5331`. Para el punto 6 hay una segunda API en `http://localhost:5332` con `TZ=UTC` sobre la misma base.

| Chequeo | Resultado | Nivel |
|---|---|---|
| `regresion.py http://localhost:5330` | 31 OK, 0 FAIL | en app corriendo |
| `e2e.py http://localhost:5330` | 28 OK, 0 FAIL | en app corriendo |
| `fixes_f2.py` (script nuevo, con la API UTC) | 34 OK, 0 FAIL | en app corriendo |
| `web_f2.py` (Chromium headless, navegador en zona Argentina) | 12 OK, 0 FAIL | en pantalla, sólo modal de edición |

**Antes.** El mismo `fixes_f2.py` contra el commit base (23dd6b3), en un worktree temporal con su base propia:
26 OK, 8 FAIL. Siete son defectos reproducidos y uno es el cambio de comportamiento intencional del punto 1:

- Un OPERARIO responsable no veía Para analizar (punto 1).
- Un gerente sin asignación veía las pestañas. Ahora no las ve (cambio intencional, ver decisiones).
- El conteo del administrador contaba una pendiente de justificación (punto 3).
- Un borrador en curso perdía la marca de análisis al editar con la regla desactivada (punto 4).
- Editar sólo la descripción de una CALCULADO cambiaba sus horas y su turno (punto 5). Una PLC perdía sus horas (en la base quedó `FechaHoraParada` nula).
- Una carga manual en un servidor en UTC guardaba 05:40 como 05:40 UTC, o sea 02:40 en Argentina (punto 6).
- La previsualización de un CALCULADO con fechas no avisaba análisis (punto 7).

## Decisiones y desvíos

- **GERENCIA sin asignación** ya no ve las pestañas de análisis. Antes las veía vacías. ADMINISTRADOR sigue viendo todo.
- **Asignado por id** se toma sobre análisis abiertos. Un usuario que sólo tiene análisis cerrados ya no ve las pestañas.
- **Web, punto 5.** Además de lo pedido, los minutos de un PLC son de solo lectura, porque la API no los toma en ese
  caso. Y no se pueden quitar las horas de una CALCULADO: la pantalla muestra un error en vez de ignorarlo en silencio.
- **API, punto 5.** Si llegan horas iguales a las guardadas, no se re-deriva el turno, aunque el turno vigente haya
  cambiado (trampa 4 de CLAUDE.md). En la rama DECLARADO el origen queda DECLARADO aunque el request diga otra cosa.
- **Punto 6.** Si la zona configurada y su respaldo no existen, la app falla al arrancar en vez de usar la del servidor.
- **Punto 8.** El helper también lo usa el controlador, aunque el pedido lo nombraba sólo para `DetencionService`. Así
  queda una sola copia en todo el código.
- **Punto 4.** Un texto de espacios cuenta como análisis vacío.

## Pendientes para decidir (no tocados)

1. **El PUT de detenciones reemplaza campos que no llegan.** `AccionesRealizadas`, `ResolucionDescripcion`, `EquipoId`,
   `TipoFallaId` y `DetalleFallaId` quedan con lo que venga, o null. La web siempre los envía, así que desde la pantalla no
   pasa. Un cliente de API que mande el PUT sin `accionesRealizadas` borra la solución en curso. Propuesta: que el PUT sólo
   cambie los campos presentes, o documentar que es un reemplazo total.
2. **Cambiar la fecha de una detención con horas no mueve las horas.** La fecha cambia, pero la parada y el arranque se
   quedan en el día anterior. El pedido fue conservar las horas. Decidir si se re-anclan a la nueva fecha.
3. **Trampa nueva para CLAUDE.md (propuesta, no agregada).** La API devuelve las fechas en UTC y la hora de planta la
   define `Planta:ZonaHoraria`, no la zona del servidor. Conviene agregarla cuando se apruebe F2.
4. **GERENCIA sin asignación.** Confirmar con el cliente que deben dejar de ver las pestañas de análisis.

## Sin verificar

- Las pestañas de Mis pendientes en pantalla. Se verificaron por API.
- Scripts 17 y 18 en SQL Server. Esta fase no toca SQL: `database/` no tiene cambios.
- Pantalla con el navegador en Argentina. La Mac de desarrollo está en Europe/Amsterdam, así que `web_f2.py` emula la zona
  Argentina en el contexto del navegador.
- Advertencia de EF al arrancar (filtro global con navegación requerida). Ya existía en el código base.

## Salida de regresion.py y e2e.py

```
RESULTADO: 31 OK, 0 FAIL
RESULTADO: 28 OK, 0 FAIL
```

Salidas completas en `scratchpad/run/f2/regresion-final.txt` y `e2e-final.txt` (fuera del repo).

## Salida de fixes_f2.py

```
Mis pendientes por rol y asignación (puntos 1, 2 y 3)
  OK   60 min con regla OPERARIO queda con responsable por rol
  OK   80 min gana la regla de usuario (mayor minimo)
  OK   OPERARIO responsable ve las pestañas de análisis (punto 1)
  OK   OPERARIO ve sus análisis en Para analizar
  OK   OPERARIO no ve el análisis de otro responsable
  OK   gerente con análisis asignado por usuario ve las pestañas (punto 1)
  OK   gerente asignado ve sólo su análisis
  OK   gerente sin análisis asignado NO ve las pestañas (punto 1)
  OK   gerente sin asignación no recibe análisis ajenos (punto 2)
  OK   administrador ve todos los abiertos
  OK   conteo de OPERARIO = lo que muestran las listas (punto 2)
  OK   conteo de gerente asignado = su lista (punto 2)
Borrador en curso sobrevive a desactivar la regla (punto 4)
  OK   operario responsable guarda borrador
  OK   borrador pasa a En análisis
  OK   edición con la regla desactivada responde 200
  OK   con borrador en curso la marca y el responsable se conservan
  OK   el texto del borrador no se pierde
  OK   sin análisis iniciado, la regla que ya no aplica quita la marca
Análisis abierto excluye PENDIENTE_JUSTIFICACION (punto 3)
  OK   PENDIENTE_JUSTIFICACION no figura en Para analizar del administrador
  OK   conteo del administrador no cuenta la pendiente de justificación
  OK   la pendiente de justificación aparece en Para declarar del operario
Horas de una detención existente (punto 5, API)
  OK   alta con horas: CALCULADO y 145 min
  OK   editar sólo la descripción responde 200
  OK   sólo la descripción cambió: origen, horas, minutos y turno iguales
  OK   mismas horas no re-derivan el turno
  OK   horas nuevas recalculan y re-derivan el turno
  OK   PLC sin horas nuevas conserva origen, horas, minutos y turno
Horas en la zona de la planta (punto 6)
  OK   API con TZ=UTC acepta la carga manual
  OK   hora de inicio 05:40 es 05:40 en Argentina (el servidor corre en UTC)
  OK   minutos de la carga en zona UTC del servidor siguen en 145
Previsualización igual que el alta (punto 7)
  OK   previsualización con CALCULADO y fechas (60 min) avisa análisis
  OK   previsualización con 30 min no avisa
  OK   el alta con las mismas fechas responde 201
  OK   el alta coincide con la previsualización (60 min y análisis)
Limpieza de reglas de prueba
  reglas de prueba desactivadas

RESULTADO: 34 OK, 0 FAIL
```

## Salida de web_f2.py

```
Edición de una detención con horas reales (punto 5, pantalla)
  OK   el modal muestra la hora de inicio cargada (15:00)
  OK   el modal muestra la hora de fin cargada (15:30)
  OK   guardar sólo la descripción cierra el modal
  OK   tras guardar la descripción, horas, minutos y turno quedan iguales
  OK   la descripción sí se guardó
Cambiar la hora desde la pantalla recalcula y re-deriva el turno
  OK   horas cambiadas: 45 minutos y turno de la nueva hora de inicio (06:00)
No se pueden quitar las horas de una detención cargada con horas
  OK   muestra el error y el modal sigue abierto
  OK   la detención no cambió
Detención del PLC: horas y minutos de solo lectura
  OK   hora de inicio del PLC deshabilitada
  OK   hora de fin del PLC deshabilitada
  OK   minutos del PLC se muestran como dato del PLC
  OK   guardar el PLC conserva origen, horas y minutos

RESULTADO: 12 OK, 0 FAIL
```
