# Registro S1 · Simplificación backend (T-76)

**Estado: PASS con desviaciones menores.** Los diez puntos del brief están hechos, salvo lo que aparece en "Desvíos".
Build sin errores ni advertencias, 100 pruebas en verde, API local probada con curl en una base SQLite nueva.
Nada está mergeado, pusheado ni desplegado. Solo commits locales en `t76-simplificar`.

No hay cambios de interfaz en esta unidad, así que no hay capturas. No se creó `capturas/S/`.

## Commits (desde cc2946f)

| Hash | Mensaje |
|---|---|
| eda28d9 | refactor(dominio): roles del sistema en RolesSistema compartido |
| 92d37ce | refactor(api): fechas de auditoría las completa el DbContext |
| a1a0586 | refactor(dominio): duración del turno como método de Turno |
| b04d803 | refactor(api): un solo mapeo de campos de análisis en detenciones |
| 27c0a3d | refactor(api): estado y responsable de análisis calculados en el servidor |
| ca73168 | refactor(dominio): Detencion.CambiarEstado valida la transición y registra el historial |
| 060145e | refactor(api): reglas de análisis con una sola ruta de alta y edición |
| 3e3f383 | refactor(api): lista de tiempo programado por día usa TiempoProgramadoReporte |
| fce469a | refactor(application): reportes de indisponibilidad cargan el tiempo programado una vez |
| 3340727 | refactor(api): conteo de pendientes con COUNT en la base |
| 60dc706 | refactor(backend): quitar comentarios que repetían el código de T-76 |

## Qué se hizo, por punto del brief

1. **Mapeo de análisis.** Los cuatro mapeos de `DetencionesController` pasan por `ConDatosAnalisis`. Los campos viven en la base `DatosAnalisisDetencion`, que heredan `DetencionDto`, `DetencionListaDto` y `DetencionDetalleDto`. El historial del detalle devuelve `estadoNuevoDescripcion`.
2. **Campos nuevos.**
   - `analisisPendiente` y `estadoAnalisisDescripcion` en las tres vistas de detención. La condición es `AnalisisFallaReglas.EstaAbiertoExpr`, una sola expresión que usan `GetAnalisisAbiertosAsync` y `GetPaginadoAsync`. El filtro negado se arma con `Expression.Not` sobre la misma expresión.
   - `EstadoAnalisisDescripcion` (extensión `ObtenerDescripcionAnalisis` en `EstadoDetencion.cs`). La usan `PendienteAnalisisDto` y `ExportacionService`.
   - `ResponsableNombre` en `AnalisisDetencionDto` y `PendienteAnalisisDto`. Para rol, lo arma `AnalisisService` ("Rol Mantenimiento"). Para usuario, `AnalisisController` pone el nombre completo o "Usuario asignado".
   - `Turno.DuracionMinutos()` reemplaza a `TiempoProgramadoCalculador.DuracionMinutos`. `TurnoDto.DuracionMinutos` se completa en los cinco lugares donde se arma.
3. **CambiarEstado.** `Detencion.CambiarEstado` valida `PuedeTransicionarA`, agrega el historial y cambia el estado. `AnalisisService` lo usa y se borró su copia privada. Los tests de dominio nuevos cubren el caso permitido y el rechazo.
4. **Fechas de auditoría.** Se quitaron las asignaciones manuales de T-76: cinco en `CatalogosController` y una en `AnalisisService`. Las de negocio (`FechaHoraAnalisis`, `FechaHoraCierre`) se mantienen.
5. **Reglas de análisis.** `NormalizarRegla` corre una vez y `ValidarReglaAnalisisAsync`, `AplicarRegla` y `ArmarReglaDto` reciben esos datos. Create y Update comparten `AplicarRegla`. Una regla se mapea con `MapearReglaAnalisisAsync`, que pide solo su línea, tipo, equipo y usuario. La lista sigue usando `MapearReglasAnalisisAsync` con los catálogos completos.
6. **Tiempo programado por día.** El catálogo `GET tiempos-programados` usa `TiempoProgramadoReporte.DiasDeLinea`. Se quitó el literal "ESTANDAR" del controlador. `TiempoProgramadoDiaDto` (dashboard) suma `tiempoProgramadoDiaId`, que la edición necesita.
7. **Dashboard.** `CargarTiempoProgramadoAsync(lineas, turnos, anio, lineaId)` recibe lo ya cargado. Las excepciones del año salen en una consulta: `ITiempoProgramadoDiaRepository.GetPorRangoAsync(desde, hasta, lineaId?)`, que reemplaza a `GetPorLineaYRangoAsync`. Se agregó `Porcentaje(parte, total)` y se usa en los indicadores de T-76.
8. **Conteo de pendientes.** Admin: `CountAsync(EstaAbiertoExpr)`. Resto: `CountAnalisisAbiertosParaAsync`, un COUNT con rol o usuario. Operario: COUNT de `PENDIENTE_JUSTIFICACION` en sus líneas. Ver "Desvíos".
9. **Roles.** `Domain/Common/RolesSistema.cs` tiene los nombres. `Infrastructure.Identity.Roles` los reexpone. `AnalisisService` y `AnalisisFallaReglas` usan `RolesSistema`.
10. **Comentarios.** Se quitaron cinco que repetían el código: en `AnalisisService` (dos), `AnalisisFallaReglas` (uno), `CatalogosController` (uno, propio de esta unidad) y `DetencionConfiguration` (uno). Se mantienen los que explican una regla de negocio o un porqué (por ejemplo, permisos, índices, por qué `IgnoreQueryFilters`, por qué `DiasEsperando` usa la expresión). Los encabezados de sección se dejaron por consistencia con el resto de las entidades.

## Desvíos

| Punto | Qué pidió el brief | Qué hice | Por qué |
|---|---|---|---|
| 8 | Saltear la consulta de análisis si el usuario no tiene rol que pueda ser responsable | No se saltea. El conteo siempre corre (un COUNT) | Un usuario activo también puede figurar como responsable por usuario (la validación solo exige que esté activo). Saltear cambiaría el número para usuarios sin rol responsable asignados por nombre |
| 3 | Usar `CambiarEstado` en los caminos de T-76 de `DetencionesController` | No se cambió el controlador | Ningún camino de T-76 cambia el estado: `CerrarDetencion` e `IniciarSeguimiento` son anteriores a T-76. `CerrarDetencion` acepta hoy `PENDIENTE_JUSTIFICACION` → `CERRADA`, y `CambiarEstado` lo rechazaría. Cambiarlo sería un cambio de comportamiento |
| 2 | Export usa la nueva descripción | Cambia el texto de la columna "Análisis" del Excel: "Analizada" pasa a "Cerrada", y "EN_SEGUIMIENTO" pasa a "En análisis" (antes decía "Requiere análisis"). "Sin análisis" no cambia | Lo pidió el brief. Es visible para quien lee el Excel. Confirmar con Fran |
| 6 | Redondeo de horas del día | `horas` del catálogo ahora tiene dos decimales (antes podía tener muchos). Con los datos actuales, la respuesta es idéntica al baseline | Lo pidió el brief |
| 2 | No aplica | El JSON de los DTO ahora emite primero las propiedades de la base. No cambian los nombres | Consecuencia de la base compartida. Los clientes leen por nombre |

## Verificación

Comandos (PATH de dotnet exportado):

```
dotnet build 45064123_App/PSAG.Mantenimiento.sln
dotnet test  45064123_App/PSAG.Mantenimiento.sln --no-build
```

| Corrida | Resultado |
|---|---|
| Build antes de tocar nada | 0 Warning(s), 0 Error(s) |
| Build final (commit 60dc706) | 0 Warning(s), 0 Error(s) |
| Tests antes | Domain 86, Application 7, Integration 5. Todas verdes (98) |
| Tests final | Domain 88, Application 7, Integration 5. Todas verdes (100): dos pruebas nuevas de `CambiarEstado` |

Logs en `scratchpad/build-*.log` y `scratchpad/test-*.log` (fuera del repo).

### API local (SQLite nueva)

`zsh run-local.sh .../45064123_App 5320 5321 .../scratchpad/run/post/psag.db`, luego el cargador del histórico Tango 2. El cargador reporta 764 detenciones: el baseline dio lo mismo, así que el "766" del brief no coincide con el archivo.

Resultados (medido con curl, login `admin`):

- `GET /api/dashboard/indisponibilidad?lineaId=7&anio=2026`: mar-26 12.9, abr-26 10.3, may-26 7.6, jun-26 10.5, jul-26 10.7, ago-26 6.7. Igual al baseline mes por mes. Los indicadores de fallos mayores y el tiempo programado de marzo también coinciden. La única diferencia es la adición de `tiempoProgramadoDiaId` en el tiempo programado.
- `GET /api/catalogos/turnos`: `duracionMinutos` 480, 480, 480.
- `GET /api/catalogos/tiempos-programados?lineaId=7&desde=2026-03-01&hasta=2026-03-31`: igual al baseline (31 días).
- `GET /api/detenciones?page=1&pageSize=2`: items con `analisisPendiente: true`, `estadoAnalisisDescripcion: "Requiere análisis"`, `responsableRol: "MANTENIMIENTO"` (la detención 768). Las no pendientes no traen los campos null (el serializador omite nulos).
- `GET /api/detenciones/765` historial: `estadoNuevoDescripcion` = "Registrada", "En análisis", "Cerrada". Antes el valor era vacío.
- `GET /api/analisis/pendientes/conteo` (admin): `{"total":2}` con una regla de rol y una de usuario abiertas. Con la base de baseline daba 1.
- Usuario MANTENIMIENTO: conteo 1 (rol). Usuario OPERARIO con una `PENDIENTE_JUSTIFICACION` insertada en la base: conteo 1 (declaración).
- Responsable por usuario: `responsableNombre` = "Administrador del Sistema" (nombre completo). Por rol: "Rol Mantenimiento".
- Reglas: POST 201, GET 200, PUT 200 con rol en minúscula normalizado a MAYÚSCULAS, rol no permitido (GERENCIA) 400, usuario inexistente 400. La respuesta de POST es idéntica a la del baseline.
- Análisis: PUT borrador (REGISTRADA → EN_SEGUIMIENTO) 200, cerrar 200 (→ CERRADA), cerrar de nuevo 400. Historial equivalente al baseline más las descripciones.
- Filtro `analisisPendiente=true` (1) y `=false` (765) funcionan con la expresión negada.
- Export `GET /api/detenciones/export/xlsx`: 200, con "Sin análisis" y "Cerrada".
- `api-5320.log`: sin excepciones ni 500. Solo la advertencia de redirección HTTPS que genera el sondeo de `run-local.sh`.

Nivel: **pruebas y API real (SQLite)**. Sin SQL Server.

## Pendiente o para S2 / coordinador

- **Web (S2)** puede borrar sus copias: `NombreResponsable` y `Capitalizar` en `DetencionDetalle.razor`, la función `ResponsableDe` y `EstadoAnalisis` en `MisPendientes.razor`. Usar `responsableNombre`, `estadoAnalisisDescripcion` y `analisisPendiente` que ya llegan en la respuesta. Para usuario sin nombre, el fallback de la API es "Usuario asignado" (el web usaba textos distintos).
- `DetencionesController`, `User.IsInRole("ADMINISTRADOR")` (filtro de eliminadas): sigue con literal. Fuera del alcance del punto 9.
- `Program.cs` tiene políticas con nombres de rol literales. Son anteriores a T-76.
- Script 17 sigue sin verificarse en SQL Server (ya estaba en el registro de U0).
