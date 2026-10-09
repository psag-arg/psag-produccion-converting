# Registro U2 · Maestros T-76

**Estado: PASS con pendientes menores.** Equipos (crítico), tipos de falla (color), reglas de análisis y turnos con calendario de excepciones funcionan en la app real (API + web, SQLite local). Nada está mergeado, desplegado ni publicado. Commits locales en `t76-u2`.

## Commits

| Hash | Mensaje |
|---|---|
| c0865a3 | feat(api): criticidad de equipos, color de tipos de falla, reglas de análisis y tiempo programado por día |
| 0041a24 | feat(web): modelos y servicios para reglas de análisis, calendario y color |
| 093615c | feat(web): equipos críticos y color en tipos de falla |
| 63aa2c0 | feat(web): página de reglas de análisis |
| d7848f6 | feat(web): horas de turno y calendario de excepciones |
| (este commit) | docs(t76): registro U2 y capturas |

## Qué se hizo

- **Equipos (#5)**: columna "Crítico" con badge rojo ("No" si no lo es) y checkbox en el modal con la aclaración del diseño. Persiste por el endpoint de equipos existente.
- **Tipos de falla (#17)**: columna "Color en gráficos" (cuadro de color + hex, o "Automático") y control de color en el modal. El color es opcional: un checkbox "Color propio en gráficos" activa el `<input type="color">`; sin color queda `null` (mapa automático del código). Backend valida `#RRGGBB` (400 si no cumple) y normaliza a mayúsculas.
- **Reglas de análisis (#6)**: página nueva `maestros/reglas-analisis` (solo ADMINISTRADOR en la UI) con listado, alta/edición en modal, activar/desactivar (`Activo`, sin DELETE) y el callout del diseño. Endpoints en `CatalogosController`:
  - `GET api/catalogos/reglas-analisis` (cualquier autenticado), `GET {id}`.
  - `POST` y `PUT {id}` solo con `CanManageCatalogs`. `PUT` también activa o desactiva.
  - Validación en backend: tipo de falla existente; minutos > 0; línea y equipo opcionales, y si hay ambos el equipo debe pertenecer a la línea; exactamente un responsable (rol o usuario); rol solo entre OPERARIO, SUPERVISOR o MANTENIMIENTO; usuario existente y activo.
- **Turnos (#15)**: columna "Horas" (duración, cruza medianoche). Debajo, "Calendario de excepciones": selector de línea, navegación de mes, grilla de 7 columnas con horas por día y estilos distintos para estándar, excepción (amarillo) e histórico HS PROG (celeste), día seleccionado con borde, formulario lateral Fecha / Horas / Motivo, "Guardar excepción" y "Quitar excepción".
  - Endpoints: `GET api/catalogos/tiempos-programados?lineaId&desde&hasta` (una fila por día, usa `TiempoProgramadoCalculador`; máximo 366 días), `POST` upsert por línea y fecha (solo CanManageCatalogs), `POST {id}/desactivar` (204, sin DELETE).
  - Editar un día histórico actualiza esa fila: `Origen` pasa a EXCEPCION (decisión pedida en el brief).
- **Corrección de pérdida de datos en edición**: el modal de equipos y el de tipos de falla no copiaban `Orden` (equipos) ni `Codigo` y `Orden` (tipos). Editar un registro los dejaba en 0 o vacío. Ahora se copian.

## Decisiones

| Tema | Decisión | Motivo |
|---|---|---|
| Validación de catálogos | Controller con `ProblemDetails` 400, no FluentValidation | `AddValidatorsFromAssembly` existe pero ningún controller lo invoca. Mantener el patrón del archivo sin tocar `CatalogoValidators.cs`, que no es de U2 |
| Roles responsables | OPERARIO, SUPERVISOR, MANTENIMIENTO | ADMINISTRADOR ya puede siempre (redundante). GERENCIA es solo lectura |
| Preview "vista previa" | **No implementada** | El brief lo permite si no es barato; requiere endpoint y lógica nueva. Queda pendiente |
| Calendario, línea por defecto | Primera línea activa (orden de `Orden`) | Hoy es Línea 1, no Tango 2. Ver pendientes |
| Auditoría de altas | Solo las filas nuevas de regla y de día se crean con `FechaHoraCreacion` | El resto de los catálogos no lo asigna (ISSUE 1 de U0). No lo toqué en las altas existentes |
| Horas del turno | Se calculan en el web (3 líneas, misma regla que `TiempoProgramadoCalculador.DuracionMinutos`) | El web no referencia Domain. Duplicación chica, señalada |
| `ICatalogoService` | Sin cambios | Existe la interfaz en Application pero no tiene implementación ni usos. El web usa `ICatalogosService` |

## Desviaciones del diseño

- **Equipos**: no hay columna "Cuerpo" (el modelo no tiene ese campo). Se mantuvo "Descripcion".
- **Tipos de falla**: se mantuvo la columna "Descripcion" (el diseño no la muestra) para no ocultar datos existentes.
- **Turnos (formulario de excepción)**: sin checkboxes "Turnos que trabajan", por el brief (Fecha, Horas, Motivo).
- **Reglas**: sin vista previa (ver decisiones).

## Verificación

| Qué | Cómo | Resultado | Nivel |
|---|---|---|---|
| Build | `dotnet build PSAG.Mantenimiento.sln` | `0 Error(s)`, `0 Warning(s)` | Compila |
| Tests | `dotnet test PSAG.Mantenimiento.sln` | Domain 73, Application 1, Integration 1: **75 passed, 0 failed** | Tests |
| Criticidad (API) | PUT REBOBINADORA `critico: true`, GET y lista | 200, `critico` true persistido y listado | API real (SQLite) |
| Color (API) | PUT `#5b9bd5`, `#GG0000`, mayúsculas, `null` | `#5B9BD5` normalizado; `#GG0000` → 400 "Color inválido"; `null` → automático; `codigo` y `orden` se conservan | API real (SQLite) |
| Reglas (API) | Alta válida y 9 casos inválidos | Válida 201. Ambos responsables, ninguno, minutos 0, rol GERENCIA, tipo inexistente, usuario inexistente, equipo de otra línea → 400 con mensaje | API real (SQLite) |
| **403 OPERARIO** | Usuario `operario.u2` (rol OPERARIO) con token propio | **POST reglas: 403. PUT reglas: 403. POST tiempos-programados: 403.** GET reglas: 200 | API real (SQLite) |
| Calendario (API) | GET agosto; upsert; validaciones; desactivar | Días con `HISTORICO` y `ESTANDAR`; upsert 6 h → `EXCEPCION`; 25 h → 400; motivo de 201 → 400; rango invertido → 400; desactivar → 204; id inexistente → 404 | API real (SQLite) |
| UI reglas, equipos, tipos | Playwright headless (Chromium), login admin, recarga completa tras cada guardado | Equipo REBOBINADORA muestra badge Crítico; FALLA PROCESO muestra #5B9BD5; regla Tango 2 / FALLA PROCESO / ≥ 60 min / Rol Supervisor aparece tras recargar. 3/3 | Pantalla real |
| UI calendario | Playwright, línea Tango 2, agosto 2026 | Historial del 9/08 = 8 h antes; tras guardar 6 h "Paro parcial" tras recargar; 10/08 sigue 24 h; 2/08 = 0 h. **6/6** | Pantalla real |
| Consola | Errores de consola y `pageerror` en ambos flujos | Ninguno | Pantalla real |

Nota sobre la primera pasada del calendario: 5 de 6 checks. El que falló era un error de mi script: el calendario abre en la primera línea activa (Línea 1) y el script no eligió Tango 2. El cambio quedó guardado en Línea 1 por error. Lo desactivé con el endpoint (204) y repetí la prueba en Tango 2 con 6/6. No es un defecto de la app.

Datos de prueba: la base local es `scratchpad/run/u2/psag.db`. Usé la historia de Tango 2 con el loader. Según la nota del coordinador, borré las filas anteriores a 2026-01-01 en `Detenciones` y `TiemposProgramadosDia`. También borré las filas HISTORICO de enero y febrero que dejó el bug del loader, que eran residuo del mismo ciclo. Quedan 173 días HISTORICO (2026-03-01 a 2026-08-20) y 764 detenciones.

## Capturas

Carpeta: `01.Ajustes/29092026 T-76/capturas/U2/`

- `equipos.png`, `equipo_modal.png`
- `tipos_falla.png`, `tipo_falla_modal.png`
- `reglas.png`, `regla_modal.png`
- `turnos_tabla.png`, `turnos_calendario.png` (con excepción seleccionada), `turnos_calendario_guardado.png`

Antes de los cambios: `scratchpad/run/u2/antes/` (equipos, tipos de falla, turnos).

## Pendientes

1. **Línea por defecto del calendario**: hoy es la primera línea activa (Línea 1), no Tango 2. ¿Se prefiere otra regla (última usada, o la línea del usuario)? Es una decisión de producto.
2. **Vista previa de la regla** (cuántas detenciones de 30 días cumplirían): no hecha. Si la quieren, es un endpoint de conteo que reutiliza `ReglaAnalisisEvaluador`.
3. **ISSUE 1 de U0** (`FechaHoraCreacion` en altas de catálogos): sigue igual para equipos, tipos, líneas y turnos.
4. **Bug del loader de historia** (`cargar_historial.py`, scratchpad del coordinador): creaba una fila por día desde 1900 por dos fechas erróneas en el Excel. Según el coordinador ya está corregido. Mi base local queda limpia, pero no volví a correr el loader corregido.
5. **Observación fuera de alcance**: `CatalogosController` tiene `DELETE api/catalogos/maquinistas/{id}`. Viola la regla "sin HTTP DELETE para registros de negocio". Es anterior a T-76 y no lo toqué.

## Archivos cambiados

API
- `src/PSAG.Mantenimiento.Api/Controllers/CatalogosController.cs`
- `src/PSAG.Mantenimiento.Application/DTOs/Catalogos/EquipoDto.cs`
- `src/PSAG.Mantenimiento.Application/DTOs/Catalogos/TipoFallaDto.cs`
- `src/PSAG.Mantenimiento.Application/DTOs/Catalogos/ReglaAnalisisDto.cs` (nuevo)
- `src/PSAG.Mantenimiento.Application/DTOs/Catalogos/TiempoProgramadoDiaDto.cs` (nuevo)

Web
- `src/PSAG.Mantenimiento.Web/Models/CatalogoModels.cs`
- `src/PSAG.Mantenimiento.Web/Services/CatalogosService.cs`
- `src/PSAG.Mantenimiento.Web/Pages/Maestros/Equipos.razor`
- `src/PSAG.Mantenimiento.Web/Pages/Maestros/TiposFalla.razor`
- `src/PSAG.Mantenimiento.Web/Pages/Maestros/ReglasAnalisis.razor` (nuevo)
- `src/PSAG.Mantenimiento.Web/Pages/Configuracion/Turnos.razor`
- `src/PSAG.Mantenimiento.Web/Pages/Configuracion/Turnos.razor.css` (nuevo)

Docs
- `01.Ajustes/29092026 T-76/registro-U2.md`
- `01.Ajustes/29092026 T-76/capturas/U2/*.png`
