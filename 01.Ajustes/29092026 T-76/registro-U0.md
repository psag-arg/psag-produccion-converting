# Registro U0 · Fundación T-76

**Estado: ISSUES.** Dominio, EF, script 17, funciones de reglas y tiempo programado, DTOs y menú están hechos y probados.
La regla de análisis **no se dispara desde la API real**: `DetencionesController` no usa `DetencionService` (ver ISSUE 1).
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

## Qué se hizo

- **Dominio**: campos de análisis y borrado lógico en `Detencion`; `Equipo.Critico`; `TipoFalla.Color`; entidades `ReglaAnalisis` y `TiempoProgramadoDia`.
- **Funciones puras**: `ReglaAnalisisEvaluador.Evaluar` (gana mayor `MinutosMinimos`, luego menor Id; ignora inactivas; `null` en línea o equipo = cualquiera) y `TiempoProgramadoCalculador.MinutosProgramadosPorDia` (excepción activa = horas × 60; si no, suma de turnos activos; turno nocturno cuenta su duración real).
- **EF**: configuraciones, filtro global `!Eliminada`, repositorios y `UnitOfWork`. `GenerarCodigoDetencionAsync` ignora el filtro para no reutilizar un código de una eliminada (el código es único).
- **Aplicación**: `DetencionService` evalúa reglas en crear y actualizar. Sin regla y sin análisis previo, limpia la marca y el responsable. DTOs de lista y detalle con los campos pedidos. `EN_SEGUIMIENTO` se describe como "En análisis" sin renombrar el enum.
- **Web**: modelos reflejan los DTOs. `NavMenu` tiene "Reglas de análisis", "Mis pendientes" y "Base de soluciones". No se crearon páginas.
- **SQL**: `database/17_t76_ajustes.sql` (idempotente) y entrada en `database/README.md`.
- **Pedido extra del coordinador**: en SQLite, `DateTimeOffset` se guarda como `DateTime` UTC (solo con proveedor SQLite). SQL Server no cambia.

## Decisiones y desvíos del PLAN

| Tema | Decisión | Motivo |
|---|---|---|
| Nombres de tabla | `ReglasAnalisis`, `TiemposProgramadosDia` | El PLAN no los fija |
| Filas inactivas en tiempo programado | Se ignoran, como si no existieran | `TiempoProgramadoDia` hereda `Activo`; desactivar una excepción debe devolver el estándar |
| Checks extra | `CK_ReglasAnalisis_MinutosMinimos`, `CK_ReglasAnalisis_Responsable` (exactamente uno de rol o usuario), `CK_TiemposProgramadosDia_Horas` (0 a 24), `CK_TiemposProgramadosDia_Origen` | Integridad del modelo del PLAN |
| Check de horas con `CAST` | `CAST([Horas] AS FLOAT)` | SQLite guarda `decimal` como texto y comparaba mal (7.5 fallaba). Verificado |
| Auditoría en tablas nuevas | Sin `DEFAULT SYSDATETIMEOFFSET()` | EF no modela ese default y la app siempre completa los campos. Las tablas viejas sí lo tienen (diferencia previa) |
| Nombres de FK e índice | FK con nombre por defecto de EF (`FK_Detenciones_ReglasAnalisis_ReglaAnalisisId`); único `UX_TiemposProgramadosDia_LineaId_Fecha` | Para que el script coincida con EF |
| Regla al editar | Usa `MinutosEfectivos` actual | `ActualizarDetencionAsync` no cambia minutos |
| `ReglaAnalisisId` en DTOs | No expuesto | No estaba en la lista pedida |
| `EquipoDto.Critico`, `TipoFallaDto.Color` | No expuestos | Son de U2 (catálogos) |
| Cierre con análisis completo | No implementado | No estaba en el alcance de U0 |
| Payload de login | `{"usuario":"admin","password":"Admin123!"}` | El pedido decía `userName`; el controller usa `usuario` |

## Verificación

| Qué | Comando o método | Resultado | Nivel |
|---|---|---|---|
| Build | `dotnet build PSAG.Mantenimiento.sln` | `0 Error(s)`, `0 Warning(s)` (la base también 0) | Compila |
| Tests | `dotnet test PSAG.Mantenimiento.sln` | Domain 73 passed (48 previas + 25 nuevas), Application 1, Integration 1. Total 75, 0 failed (base: 50) | Tests |
| Boot SQLite | `dotnet run ... --urls http://localhost:5290`, esquema con `sqlite3 .schema` | Columnas nuevas en `Detenciones`, tablas `ReglasAnalisis` y `TiemposProgramadosDia`, `Critico` y `Color`, con sus checks | Schema real |
| Boot SQLite, API | `GET /api/detenciones`, `/api/dashboard`, `/api/catalogos/turnos` | 200, 200, 200. Log sin errores | API real |
| Orden (pedido extra) | Dos filas con distinta `FechaHoraCreacion` | Orden descendente correcto. Antes: 500 con "SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY" | API real |
| Filtro de eliminadas | Fila con `Eliminada=1` | Fuera de la lista (total 1). `GET /api/detenciones/2` 404. `PUT` sobre ella 404 y no cambia la fila | API real |
| Check de horas en SQLite | `INSERT` con 7.5 y con 24.5 | 7.5 aceptado. 24.5 rechazado por `CK_TiemposProgramadosDia_Horas` | Schema real |
| Script 17 contra EF | DDL de SQL Server generado desde `MantenimientoDbContext` (`GenerateCreateScript`, proyecto temporal fuera del repo) y comparador | `DIFERENCIAS: 0` en 12+11 columnas nuevas, 14 columnas agregadas, 4 checks, 5 FKs, 5 índices. Prueba de control: el comparador detectó 4 de 5 mutaciones (la quinta era un reemplazo sin efecto) | Comparación de DDL |
| Script 17 en motor | No hay `sqlcmd` ni imagen de SQL Server local | No ejecutado | **NO VERIFICADO** a nivel motor |
| Regla en la API | `PUT /api/detenciones/1` con `tipoFallaId` 1, 45 min, regla activa (min 30) | 200, pero `RequiereAnalisis` queda en 0. La regla no corre (ISSUE 1) | **NO VERIFICADO** en la API |

Veredicto de la regla: lógica cubierta por tests unitarios; cableado en la API no verificado y roto por ISSUE 1.

## ISSUES

1. **Bloqueante para U3 y U4.** `DetencionesController` no llama a `IDetencionService` (está registrado pero sin uso) y no mapea los campos nuevos (cero referencias a `RequiereAnalisis`, `EquipoCritico` ni `CausaFalla`). Efecto: la regla nunca se dispara por la API y el GET devuelve valores por defecto. Propuesta: que el controller llame la evaluación en POST y PUT y mapee los campos en sus cuatro mapeos. Toca un archivo de U3, por eso no lo toqué (fuera de alcance). Hace falta decisión.
2. **Pre-existente, SQLite.** `POST /api/detenciones` responde 500: `NOT NULL constraint failed: Detenciones.RowVersion`. `IsRowVersion()` no genera el valor en SQLite (la línea ya estaba en `d0f03c4`). Impide crear detenciones en modo desarrollo. No corregido.
3. **Pre-existente.** `CK_Detenciones_EstadoActual` (script 05) no incluye `PENDIENTE_JUSTIFICACION`, que el enum sí permite.
4. **Pre-existente.** Los nombres de FK del script 05 (`FK_Detenciones_Lineas`) no coinciden con los de EF (`..._LineaId`).
5. **Para U1 y U3.** Las vistas de `06_create_views.sql` leen `Detenciones` sin filtrar `Eliminada`. Si se usan para reportes, mostrarán eliminadas.
6. **Para U3 y U4.** `Detenciones.razor` línea 64 muestra "En Seguimiento" como texto fijo. No lo cambié (Razor fuera de alcance).

## Pendientes

- Decidir ISSUE 1 (quién cablea el controller).
- Ejecutar `17_t76_ajustes.sql` en un SQL Server de desarrollo y comprobar que corre dos veces sin error.
- U2: exponer `Critico` en `EquipoDto` y `Color` en `TipoFallaDto`.
- Cierre con análisis completo (Solución y Causa obligatorias para reglas) queda para U4.

## Archivos cambiados (desde 30a9450)

32 archivos, 1216 inserciones, 1 eliminación. Principales: `Domain/Entities/{Detencion,Equipo,TipoFalla,ReglaAnalisis,TiempoProgramadoDia}.cs`, `Domain/Services/{ReglaAnalisisEvaluador,TiempoProgramadoCalculador}.cs`, `Infrastructure/Data/MantenimientoDbContext.cs`, `Infrastructure/Data/Configurations/*` (cuatro tocados y dos nuevos), `Infrastructure/Data/Conversions/DateTimeOffsetUtcConverter.cs`, `Infrastructure/Repositories/{UnitOfWork,DetencionRepository,ReglaAnalisisRepository,TiempoProgramadoDiaRepository}.cs`, `Application/Services/Implementations/DetencionService.cs`, `Application/DTOs/Detenciones/{DetencionDto,DetencionDetalleDto}.cs`, `Web/Models/DetencionModels.cs`, `Web/Layout/NavMenu.razor`, `database/17_t76_ajustes.sql`, `database/README.md`, tests de Domain.
