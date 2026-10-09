# Registro U1 · Reportes con tiempo programado real

**Estado: ISSUES.** Indisponibilidad, fallos mayores y Pareto funcionan con datos reales en la app local (SQLite).
Quedan decisiones de negocio sobre agosto y sobre "Todas las líneas", y el script 18 no se ejecutó en un SQL Server real.
Nada está mergeado, desplegado ni publicado. Solo commits locales en `t76-u1`.

## Commits (rama t76-u1)

| Hash | Mensaje |
|---|---|
| 2ceb344 | feat(app): tiempo programado real en indisponibilidad y fallos mayores |
| 149b48f | feat(web): reportes con tiempo programado, colores del maestro y Pareto por mes |
| 71cb208 | feat(db): script 18 con tiempo programado histórico de Tango 2 |
| (este) | docs(t76): registro de la unidad U1 y capturas |

## Antes y después (Tango 2, 2026)

El denominador anterior era días del mes × 3 turnos × 8 h. Ahora es el tiempo programado real de cada día. La columna "Antes" es la fórmula anterior aplicada a los mismos minutos (marzo: 5.005 / 44.640 min = 11,2 %). Coincide con los valores del brief; no se midió con el build anterior.

| Mes | Antes | Ahora | Minutos de detención | Horas programadas |
|---|---|---|---|---|
| Mar | 11,2 % | 12,9 % | 5.005 | 647,5 |
| Abr | 8,8 % | 10,3 % | 3.805 | 616 |
| May | 6,4 % | 7,6 % | 2.844 | 624 |
| Jun | 9,4 % | 10,5 % | 4.040 | 640 |
| Jul | 9,6 % | 10,7 % | 4.280 | 664 |
| Ago | 5,9 % | 6,7 % | 2.620 | 648 (ver decisión 1) |

Marzo coincide con el criterio del brief: 5.005 min / (647,5 h × 60) = 12,9 %.

## Qué se hizo

- **Indisponibilidad (#14).** `CalcularTurnosTrabajados` y la constante de 480 min se eliminaron. El denominador sale de `TiempoProgramadoReporte` (función pura en Application) sobre `TiempoProgramadoCalculador` de U0: excepción activa si existe, si no, suma de turnos activos. Con línea, se usa esa línea. Con "Todas las líneas", se suman las líneas activas. Fallos mayores usa el mismo denominador en su indicador.
- **Tiempo programado (#16).** Los DTOs mensuales tienen `MinutosProgramados` y `HorasProgramadas`. La tabla tiene las filas "Minutos de detención" y "Tiempo programado (h)". Las horas son botones que abren el modal "Tiempo programado · Mes · Línea" con día, horas y origen (estándar, excepción con motivo, histórico). Endpoint nuevo `GET /api/dashboard/tiempo-programado?lineaId=&anio=&mes=`.
- **Fallos mayores (#17).** `FalloMayorTipoItem` e `IndisponibilidadTipoFallaItem` llevan `Color` del maestro. En la página: color del maestro → mapa por nombre (el existente) → color fijo de una paleta de 8 según `TipoFallaId`. Así los tipos sin color no quedan todos iguales.
- **Pareto (#18).** Selector "Mes" (Todos + 12) junto a Línea y Año, visible solo en la pestaña Pareto. Se traduce a rango de fechas del endpoint, que no cambió. El período también se aplica a los clics de detalle.
- **Script 18.** `database/18_t76_tiempo_programado_historico.sql`, generado por `database/tools/generar_18_tiempo_programado.py`. 173 días (2026-03-01 a 2026-08-20), 3.575,5 h, 159 días con HS PROG. Idempotente. Ver verificación.

## Decisiones tomadas y por qué

- **Fallos mayores: se quitaron el marcador de % y el eje derecho.** Los marcadores tenían la posición fija para valores de 1 a 4 % y con los datos reales quedaban fuera del gráfico. El diseño aprobado no tiene esa línea. El % sigue en la etiqueta de cada barra y en la fila "Indicador %".
- **Indisponibilidad: se quitó la leyenda "Indisponibilidad (%)".** Apuntaba a una línea que el gráfico no dibuja. Se agregó la fórmula en un aviso, como en el diseño.
- **Filas de tipo de falla por `TipoFallaId`, no por nombre.** El DTO ya trae el color y el id es su clave, así cada fila toma el color de su propio tipo.
- **Modal sin columna "Turnos tomados".** El cálculo no sabe qué turnos aplican en un día con excepción. El modal muestra día, horas, origen y, cuando se ven todas las líneas, la línea.

## Decisiones pendientes (para el usuario)

1. **Agosto.** El histórico termina el 20/08. Los días 21 a 31 caen en el estándar (24 h cada uno, 264 h) y ese es el denominador de agosto (648 h). Si la línea no trabajó esos días, agosto da **11,4 %** (2.620 / 23.040 min), no 6,7 %. Hay que cargar esos días como excepción con 0 h o decidir que no cuenten.
2. **"Todas las líneas".** Suma las líneas activas. La base de demo tiene 7 líneas activas; las que no tienen histórico cuentan 24 h por día. Decidir si el reporte total debe contar solo las líneas que operan o si una línea sin datos queda fuera hasta configurarla.
3. **Días sin HS PROG dentro del Excel** (15 días) quedan en 0 h, como dice el PLAN para fines de semana sin producción.

## Pendientes que no entran en esta unidad

- Botón "Exportar Excel" del modal y el doble clic para abrir el calendario de excepciones (diseño). El export es de U3 y el calendario de U2.
- `database/README.md` no tiene la entrada del script 18. Ese archivo lo edita U0; lo agrega el coordinador.
- Pantalla de Tipos de Falla (color): la prueba de colores se hizo cargando `TipoFalla.Color` a mano en la base local.

## Verificación

| Qué | Cómo | Resultado | Nivel |
|---|---|---|---|
| Build | `dotnet build PSAG.Mantenimiento.sln` | 0 Error(s), 0 Warning(s) | Compila |
| Tests | `dotnet test PSAG.Mantenimiento.sln --no-build` | Domain 73, Application 7 (6 nuevas en `TiempoProgramadoReporteTests`), Integration 1. 81 en total, 0 fallidas | Tests |
| Indisponibilidad | `GET /api/dashboard/indisponibilidad?anio=2026&lineaId=7` con la base cargada | Marzo 12,9 % (5.005 / 38.850 min). Resto de meses en la tabla de arriba | API real (SQLite) |
| Modal por día | `GET /api/dashboard/tiempo-programado?lineaId=7&anio=2026&mes=m` para m = 3 a 8 | Suma de horas igual a la fila del reporte en los seis meses (647,5 / 616 / 624 / 640 / 664 / 648) | API real (SQLite) |
| Mes inválido | `mes=13` | 400 | API real |
| Pantalla | Playwright headless: Tango 2, modal de julio, Fallos Mayores, Pareto con agosto | 4 capturas, sin errores de consola | Pantalla real |
| Errores de API | `grep "responded 500" api-5301.log` | 0 | API real |
| Datos del script 18 | Valores del SQL contra el Excel y contra la base local | 173 días, 0 diferencias con la base local. Totales por mes iguales al Excel (mar 647,5) | Datos |
| Script 18 en motor | No se ejecutó en SQL Server | **NO VERIFICADO** a nivel motor. U0 no pudo levantar SQL Server en Docker | Sin motor |

Comandos del script: `database/tools/generar_18_tiempo_programado.py "<Historial detenciones tango 2.xlsx>"` (venv con openpyxl).

## Hallazgos fuera de alcance

- **Cargador de historial (scratchpad del coordinador).** `cargar_historial.py` toma la fecha 1900-01-25 (typo del Excel) como inicio del rango de HS PROG y escribe unas 46.000 filas de HISTORICO desde 1900. No afecta a 2026, pero ensucia la base. Para este registro se borraron las filas anteriores a 2026 y se verificó que queden exactamente las 173 del script. Corregir: ignorar fechas anteriores a 2026 también en `horas`.
- **Promedio del gráfico de fallos mayores.** La barra PROM muestra un hueco arriba: el total promedio se redondea (12) y la suma de los promedios por tipo, también redondeados, da 10. Ya pasaba antes; no se cambió el tipo de los DTO.

## Archivos cambiados

- `45064123_App/src/PSAG.Mantenimiento.Application/Services/Implementations/TiempoProgramadoReporte.cs` (nuevo)
- `45064123_App/src/PSAG.Mantenimiento.Application/Services/Implementations/DashboardService.cs`
- `45064123_App/src/PSAG.Mantenimiento.Application/Services/Interfaces/IDashboardService.cs`
- `45064123_App/src/PSAG.Mantenimiento.Application/DTOs/Dashboard/DashboardDtos.cs`
- `45064123_App/src/PSAG.Mantenimiento.Api/Controllers/DashboardController.cs`
- `45064123_App/tests/PSAG.Mantenimiento.Application.Tests/TiempoProgramadoReporteTests.cs` (nuevo)
- `45064123_App/src/PSAG.Mantenimiento.Web/Models/DashboardModels.cs`
- `45064123_App/src/PSAG.Mantenimiento.Web/Services/DashboardService.cs`
- `45064123_App/src/PSAG.Mantenimiento.Web/Pages/DashboardReportes.razor`
- `45064123_App/database/18_t76_tiempo_programado_historico.sql` (nuevo)
- `45064123_App/database/tools/generar_18_tiempo_programado.py` (nuevo)
- `01.Ajustes/29092026 T-76/registro-U1.md` (este archivo)
- `01.Ajustes/29092026 T-76/capturas/U1/*.png` (4 capturas)

## Capturas

- `capturas/U1/01-indisponibilidad.png`: Tango 2, 2026, con la fila de tiempo programado y la fórmula.
- `capturas/U1/02-modal-tiempo-programado.png`: modal de julio, 664 h.
- `capturas/U1/03-fallos-mayores.png`: colores del maestro (en la prueba) y paleta de respaldo.
- `capturas/U1/04-pareto-agosto.png`: Pareto con Mes = Agosto, total 2.620 min.
