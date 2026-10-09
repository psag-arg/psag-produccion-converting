# Registro U0 · Fundación T-76

**Estado: ISSUES (menores).** Los cuatro puntos del follow-up del coordinador están hechos y verificados en SQLite.
La regla de análisis dispara desde la API real. Las fechas de auditoría se completan al guardar (ver "Auditoría").
Queda una cosa: el script 17 no se pudo ejecutar en un SQL Server real.
Nada está mergeado, desplegado ni publicado. Solo commits locales en `t76-u0`.

## Commits

| Hash | Mensaje |
|---|---|
| 9518e15 | feat(dominio): reglas de análisis y tiempo programado por día |
| b762293 | feat(infra): configuración EF, repositorios y filtro de eliminadas |
| 850e648 | feat(aplicacion): evaluar reglas de análisis al crear y editar detenciones |
| 7214ded | feat(web): modelos de detención y accesos de menú para análisis |
| 1c20857 | fix(sqlite): ordenar por fechas en modo desarrollo |
| c165996 | fix(sqlite): check de horas compatible con decimal |
| 46f5e07 | feat(db): script 17 de ajustes T-76 (idempotente) |
| d817160 | test(dominio): EN_SEGUIMIENTO se describe como En análisis |
| 1b15949 | docs(t76): registro de la unidad U0 |
| 31e7ab0 | fix(sqlite): asignar RowVersion al insertar y actualizar |
| d78812e | feat(api): aplicar reglas de análisis al crear y editar detenciones |
| f0bdc35 | feat(api): devolver campos de análisis en lista, detalle y respuesta de edición |
| 9d28e2f | fix(db): PENDIENTE_JUSTIFICACION en estados de detención |
| 30931d0 | feat(db): vistas de detenciones sin eliminadas en script 17 |
| 6df6417 | docs(t76): registro U0 tras el follow-up |
| 07959a3 | fix(auditoria): fechas de creación y modificación automáticas |

## Qué se hizo

- **Dominio**: campos de análisis y borrado lógico en `Detencion`; `Equipo.Critico`; `TipoFalla.Color`; entidades `ReglaAnalisis` y `TiempoProgramadoDia`.
- **Funciones puras**: `ReglaAnalisisEvaluador.Evaluar` (gana mayor `MinutosMinimos`, luego menor Id; ignora inactivas; `null` en línea o equipo = cualquiera) y `TiempoProgramadoCalculador.MinutosProgramadosPorDia` (excepción activa = horas × 60; si no, suma de turnos activos; turno nocturno cuenta su duración real).
- **EF**: configuraciones, filtro global `!Eliminada`, repositorios y `UnitOfWork`. `GenerarCodigoDetencionAsync` ignora el filtro para no reutilizar el código de una eliminada.
- **Aplicación**: `IDetencionService.AplicarReglaAnalisisAsync` (público). `DetencionService` lo usa al crear y editar. `DetencionesController` lo llama en POST y PUT antes de guardar. Sin regla y sin análisis previo, limpia la marca y el responsable. `EN_SEGUIMIENTO` se describe como "En análisis" sin renombrar el enum.
- **API**: el controller devuelve los campos de análisis en lista, abiertas, detalle y respuesta de PUT. Sin export, borrado ni filtros (eso es U3).
- **Web**: modelos reflejan los DTOs. `NavMenu` tiene "Reglas de análisis", "Mis pendientes" y "Base de soluciones". No se crearon páginas.
- **SQL**: `database/17_t76_ajustes.sql` (idempotente) y entrada en `database/README.md`.

## Follow-up del coordinador

1. **Regla en la API real (antes ISSUE 1).** Evaluación pública en `IDetencionService`. El controller la llama en POST y PUT: una inyección, dos llamadas, cuatro mapeos de campos. No se duplicó la lógica.
2. **RowVersion en SQLite (antes ISSUE 2).** En SQLite la propiedad `RowVersion` de todas las entidades se marca `ValueGeneratedNever`, para que EF la envíe en el insert. `SaveChanges` le asigna 8 bytes aleatorios en insert y update. No sirve un generador de valores: EF solo genera cuando el valor es el default del CLR, y `RowVersion` arranca como arreglo vacío. SQL Server no cambia.
3. **Vistas (decisión: se actualizan).** Ningún código de la aplicación lee las vistas de `06_create_views.sql`. Igual se recrean en script 17 con `CREATE OR ALTER`: `VW_DetencionesDetalle`, `VW_DetencionesAbiertas`, `VW_DetencionesIndicadores` y `VW_ResumenTurnoActual`. Son copias de 06 con sólo el filtro `Eliminada = 0`. Verificado con diff: solo se agregan líneas de filtro y el encabezado. `VW_LineasConEquipos` y `VW_UsuariosConLineas` no leen detenciones y no se tocaron.
4. **Estados (antes punto 4).** `PENDIENTE_JUSTIFICACION` sí sería rechazado por SQL Server: el check de script 05 no lo incluye y, además, las columnas eran `VARCHAR(20)` (el valor tiene 23 caracteres). Por eso se hacen tres cosas en script 17, todas idempotentes:
   - DROP y ADD de `CK_Detenciones_EstadoActual`.
   - DROP y ADD de `CK_DetencionesEstadoHistorial_EstadoNuevo`. Es la misma regla en el historial, decidida por mí.
   - `EstadoActual`, `EstadoAnterior` y `EstadoNuevo` pasan a `NVARCHAR(30)`, igual que EF.

## Auditoría (fechas automáticas)

`MantenimientoDbContext` completa las fechas en cada `SaveChanges` / `SaveChangesAsync`, con una sola fecha de corte por guardado:

- Alta (`Added`): `FechaHoraCreacion` y `FechaHoraModificacion` de toda `EntityBase` que lleguen por defecto.
- Edición (`Modified`): `FechaHoraModificacion` = ahora.
- `DetencionEstadoHistorial.FechaHora`, `DetencionNota.FechaHora` y `Auditoria.FechaHora`: solo si llegan por defecto.
- No se tocan las fechas de negocio (`FechaHoraParada`, `FechaHoraHallazgo`, etc.) ni `UsuarioLinea.FechaAsignacion`.
- `ApplicationUser.FechaCreacion` no se incluyó: `UsuariosController` y el inicializador ya la asignan.

Verificación:
- Pruebas de integración (SQLite en memoria, `AuditoriaFechasTests`): 4 nuevas, todas verdes. Prueba de control: con la llamada desactivada, 4 de 5 fallan (las 4 nuevas). Se restauró el código y se volvió a verde.
- API real (SQLite, base nueva): `POST /api/detenciones` → 201. `GET /api/detenciones/1` devuelve `fechaHoraCreacion` `2026-10-09T12:29:26.731888+00:00` y `historialEstados[0].fechaHora` con la misma fecha. La base guarda lo mismo. Log sin errores.

## E2E en SQLite (build final)

Base nueva. Regla insertada por SQL: tipo 1, 60 min, rol MANTENIMIENTO, línea NULL (cualquiera). Salida completa en `scratchpad/run/u0/e2e_final.txt`.

```
== 1. Regla insertada por SQL
1||1||60|MANTENIMIENTO|1
== 2. POST 145 min, tipo 1
HTTP 201
{"detencionId":1,"codigo":"DET-20261009-000001","estado":"REGISTRADA","minutosEfectivos":145,"turnoNombre":"Turno 2 (Tarde)","fechaHoraRegistro":"0001-01-01T00:00:00+00:00"}
== 3. GET detalle tras POST
HTTP 200
    "detencionId": 1,
    "minutosEfectivos": 145,
    "eliminada": false,
    "requiereAnalisis": true,
    "responsableRol": "MANTENIMIENTO",
    "equipoCritico": false,
    "rowVersion": "/y6rKdcf2U4=",
== 4. GET lista tras POST
HTTP 200
{'detencionId': 1, 'requiereAnalisis': True, 'responsableRol': 'MANTENIMIENTO', 'responsableUsuarioId': None, 'eliminada': False, 'equipoCritico': False}
== 5. PUT a 30 min con el rowVersion de GET
HTTP 200
    "detencionId": 1,
    "minutosEfectivos": 30,
    "requiereAnalisis": false,
    "rowVersion": "Q/vHOxCq52s="
== 6. GET detalle tras PUT
HTTP 200
    "detencionId": 1,
    "minutosEfectivos": 30,
    "requiereAnalisis": false,
    "rowVersion": "Q/vHOxCq52s="
== 7. Estado en BD
1|30|0||
== errores en log del API: 0
```

Además: PUT con el `rowVersion` vigente da 200 y devuelve un `rowVersion` nuevo. Un PUT con el valor anterior da 409.

## Verificación

| Qué | Cómo | Resultado | Nivel |
|---|---|---|---|
| Build | `dotnet build PSAG.Mantenimiento.sln` | `0 Error(s)`, `0 Warning(s)` | Compila |
| Tests | `dotnet test PSAG.Mantenimiento.sln` | Domain 73 passed, Application 1, Integration 5 (4 nuevas de auditoría). Total 79, 0 failed (base: 50) | Tests |
| Regla en la API | E2E arriba: POST 145 min, PUT 30 min | `requiereAnalisis` true → false, `responsableRol` MANTENIMIENTO | API real (SQLite) |
| RowVersion | POST, GET, PUT con token vigente, PUT con token viejo | 201, 200, 200 con token nuevo, 409 | API real (SQLite) |
| Filtro de eliminadas | Fila con `Eliminada=1` (verificado antes del follow-up, sin cambios desde) | Fuera de la lista. GET y PUT por id: 404 | API real (SQLite) |
| Fechas de auditoría | Pruebas de integración + POST real (ver sección Auditoría) | 4 nuevas verdes. Con la llamada desactivada fallan. POST: fechas de hoy | Tests y API real (SQLite) |
| Script 17 vs EF | DDL de SQL Server generado desde `MantenimientoDbContext` y comparador (proyecto temporal fuera del repo) | `DIFERENCIAS: 0`. Cubre columnas nuevas, `ALTER COLUMN` de estados, FKs, índices y checks | Comparación de DDL |
| Vistas | `diff` de cada vista contra 06 | Solo se agregan líneas de filtro y `CREATE OR ALTER` | Revisión de texto |
| Script 17 en motor | SQL Server 2022 en Docker (imagen amd64 bajo emulación) | El proceso aborta al arrancar, en dos intentos ("Invalid mapping of address"). No ejecutado | **NO VERIFICADO** a nivel motor |
| Parseo T-SQL | `sqlglot` (dialecto tsql) | Sin errores, pero el parser cae a comando genérico en cada IF y PRINT. Casi no prueba nada | Débil |

Nota de costos: para el intento de SQL Server bajé una imagen de 2,3 GB sin pedirlo antes. Ya eliminé el contenedor y la imagen.

## ISSUES

1. **Resuelto (commit 07959a3).** Fechas de auditoría en `0001-01-01` en las altas: se completan en `SaveChanges` (ver sección Auditoría). Quedan vacíos `creadoPorUsuarioNombre` y `estadoNuevoDescripcion` en el detalle. Son mapeos del controller, no fechas; no se tocaron porque el coordinador pidió no tocar controllers.
2. **Pre-existente.** `DEFAULT 'REGISTRADA'` en `EstadoActual` (script 04) no lo modela EF. No tocado.
3. **Pre-existente, DB-only.** Los checks de estado no existen en el modelo EF, así que el comparador los trata aparte. Se mantienen solo en la base.
4. **Para decidir.** `tools/MigrarDatos` copia `Detenciones` con la tabla cruda, así que también migra las eliminadas. Probablemente es lo correcto para el histórico, pero conviene confirmarlo.
5. **Para U3 y U4.** `Detenciones.razor` línea 64 muestra "En Seguimiento" como texto fijo.
6. **Ya resuelto.** Las vistas, que antes figuraban como pendientes, están cubiertas en script 17.

## Pendientes

- Ejecutar `17_t76_ajustes.sql` en un SQL Server de desarrollo y comprobar que corre dos veces sin error.
- U2: exponer `Critico` en `EquipoDto` y `Color` en `TipoFallaDto`.
- U3: reemplazar el texto "En Seguimiento" en `Detenciones.razor`.
- U4: cierre con análisis completo (Solución y Causa obligatorias para reglas).

## Archivos cambiados (desde 30a9450)

Plan y U0 completos: 32 archivos del primer alcance, más el follow-up: `Api/Controllers/DetencionesController.cs`, `Application/Services/Interfaces/IDetencionService.cs`, `Application/Services/Implementations/DetencionService.cs`, `Infrastructure/Data/MantenimientoDbContext.cs`, `database/17_t76_ajustes.sql`, `database/README.md`, `tests/PSAG.Mantenimiento.Api.IntegrationTests/AuditoriaFechasTests.cs` (auditoría).
