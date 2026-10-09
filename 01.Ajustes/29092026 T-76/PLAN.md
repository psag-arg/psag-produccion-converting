# T-76 · Ajustes Converting (minuta 29/09/2026)

Nota dueña del trabajo. Fuente: `01. MINUTA_PSAG ... 2026 09 29.md`, transcripción `02.Converting relev ...txt`,
fotos de la planilla "Identificación de fallas", reunión con Emiliano del 05/10 y video de la máquina.
Diseño de pantallas aprobado: `diseno-pantallas.html` (canvas, abrir en el navegador).

Alcance: **tenant PSAG, línea Tango 2** (hoy la única activa). Todo es configurable por maestro, así que sirve para
otras líneas sin cambios de código.

## Hechos y decisiones

| Tema | Decisión | Motivo |
|---|---|---|
| Borrar detenciones (#1) | Borrado lógico: `Eliminada` + quién, cuándo y motivo + registro en Auditoria. Sin DELETE físico ni endpoint HTTP DELETE | CLAUDE.md "Nada se borra" y pedido de Emiliano 05/10 |
| Eliminadas fuera de todo | Filtro global de EF (`HasQueryFilter(d => !d.Eliminada)`). Sólo el admin las ve con `IgnoreQueryFilters` | Un solo lugar; dashboards, exportes y listas quedan bien sin tocarlos uno por uno |
| Estados (#9) | Se conserva el enum. `EN_SEGUIMIENTO` se muestra como **"En análisis"**. "Requiere análisis" NO es un estado: es `RequiereAnalisis = true` con estado `REGISTRADA` | No migrar datos ni romper historial; CLAUDE.md dice REGISTRADA = dato completo |
| Reglas (#6) | Entidad `ReglaAnalisis`. Se evalúan al crear y al editar una detención. No se reevalúa el histórico | Evitar cientos de pendientes viejos de golpe |
| Análisis (#7, #8) | Formulario "Identificación de fallas": Falla (operario, lo ya declarado) + Solución + Causa + Plan de acción (responsable). Solución reutiliza `AccionesRealizadas`; Causa y Plan son campos nuevos | Planilla en papel del cliente |
| Quién completa | Lo declara el operario. Solución/Causa/Plan sólo el responsable de la regla (rol o usuario) o ADMINISTRADOR. **Todos los usuarios lo pueden ver**. Validado en backend | Planilla (USR OPERARIO / USR MANTENIMIENTO) + reunión. "Ver todos" es deducción aceptada por Fran |
| Cerrar | Una detención con `RequiereAnalisis` sólo pasa a CERRADA con Solución y Causa completas. Plan de acción opcional (la planilla dice "falta analizar") | Minuta: "el análisis es obligatorio" |
| Tiempo programado (#14, #15) | Turnos siguen globales. Estándar diario = suma de horas de turnos activos. Excepciones por línea y fecha en `TiempoProgramadoDia`. Histórico Mar–Ago cargado desde la columna HS PROG del Excel | Una sola línea activa; turnos por línea es invasivo (TurnoId materializado) |
| Fines de semana | Sin regla especial: si no trabajan, se carga excepción con 0 h | Supuesto a confirmar con el cliente |
| Colores (#17) | Campo `Color` (hex) en TipoFalla. Si está vacío, se usa el mapa actual del código | Hoy los colores dependen del nombre exacto |
| Excel (#13) | Exportación .xlsx server-side con ClosedXML (NuGet), mismos filtros que la tabla | Sin CDN (red interna) |
| Hora en carga manual (#20) | Hora inicio y fin opcionales → `FechaHoraParada`/`FechaHoraArranque`, `OrigenTiempo = CALCULADO`, minutos calculados en backend. El turno se deriva de la hora de inicio si está | Los campos ya existen en el modelo |
| Fuera de alcance de código | #2 recodificación (espera Excel del cliente), #3 verificar propagación de nombres (se verifica, no se desarrolla), #19 alta de usuarios (dato), #12 KPI (tarjetas en Mis pendientes) | Minuta |

## Forma de los datos

```
Detencion (+)
  Eliminada bool = false, FechaHoraEliminacion DateTimeOffset?, EliminadaPorUsuarioId string?, MotivoEliminacion string?(500)
  RequiereAnalisis bool = false, ReglaAnalisisId int?, ResponsableRol string?(50), ResponsableUsuarioId string?(450)
  CausaFalla string?(2000), PlanAccion string?(2000)          // Solución = AccionesRealizadas (existente)
  AnalisisPorUsuarioId string?(450), FechaHoraAnalisis DateTimeOffset?

ReglaAnalisis : CatalogoBase
  ReglaAnalisisId int, LineaId int?, TipoFallaId int, EquipoId int?, MinutosMinimos int,
  ResponsableRol string?(50), ResponsableUsuarioId string?(450)   // exactamente uno de los dos

Equipo (+)    Critico bool = false
TipoFalla (+) Color string?(7)   // "#RRGGBB"

TiempoProgramadoDia : CatalogoBase
  TiempoProgramadoDiaId int, LineaId int, Fecha DateOnly, Horas decimal(5,2), Motivo string?(200),
  Origen string(20) = "EXCEPCION" | "HISTORICO"; único (LineaId, Fecha)
```

Regla: aplica si `Activo`, `TipoFallaId` coincide, (`LineaId` null o igual), (`EquipoId` null o igual) y
`MinutosEfectivos >= MinutosMinimos`. Si aplican varias, gana la de mayor `MinutosMinimos` y después menor Id.

Indisponibilidad del período = Σ MinutosEfectivos ÷ Σ minutos programados de cada día del período
(día con fila en `TiempoProgramadoDia` → sus horas; sin fila → estándar de turnos activos).

## Unidades

| Unidad | Qué | Archivos dueños |
|---|---|---|
| U0 base | Dominio, EF, script SQL 17, filtro global, evaluación de reglas, DTOs, NavMenu con rutas nuevas, tests | Domain, Infrastructure, Application (DetencionService/DTOs), database/17, NavMenu |
| U1 reportes | Indisponibilidad con tiempo programado + detalle por día, colores, Pareto por mes, script de histórico HS PROG | DashboardService/Controller, Web DashboardService/Models, DashboardReportes.razor, database/18 |
| U2 maestros | Equipos (Crítico), Tipos de falla (Color), Reglas de análisis (página + API), Turnos + calendario de excepciones (API + UI) | Maestros/*.razor, Configuracion/Turnos.razor, CatalogosController, CatalogosService (web y app), páginas nuevas |
| U3 detenciones | Tabla (export Excel, borrar, filtros análisis/eliminadas, hora), modal con hora | Detenciones.razor, DetencionesController (export/eliminar), Web DetencionesService |
| U4 análisis | Detalle con formulario, Mis pendientes (por rol), Base de soluciones | DetencionDetalle.razor, AnalisisController, AnalisisService (app y web), páginas nuevas |

## Registro

Cada unidad escribe `registro-U<n>.md` en esta carpeta (qué hizo, decisiones, cómo lo verificó, pendientes). El coordinador integra el estado acá.

| Unidad | Estado | Commit | Veredicto |
|---|---|---|---|
| U0 | integrada | 82b93ad | VERIFICADO con tests y API local (SQLite); script 17 NO VERIFICADO en SQL Server |
| U1 | integrada | 587fba7 | VERIFICADO en pantalla local (SQLite) con historial real; script 18 NO VERIFICADO en SQL Server |
| U2 | integrada | 816edd9 | VERIFICADO en pantalla local y 403 para OPERARIO |
| U3 | integrada | 40072f5 | VERIFICADO en pantalla local, Excel abierto con openpyxl, 403 para OPERARIO |
| U4 | integrada | 82c00d2 | VERIFICADO en pantalla local con usuarios MANTENIMIENTO y OPERARIO |

| S1/S2 | simplificación en curso | | |
