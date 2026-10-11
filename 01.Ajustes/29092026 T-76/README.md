# T-76 · Ajustes de Converting (minuta del 29/09/2026)

Cambios a la app de detenciones de PSAG pedidos en la reunión del 29/09/2026 con el responsable de Converting. El alcance es la línea Tango 2, pero todo se configura desde los maestros y sirve para cualquier línea sin tocar código.

En esta carpeta:

| Archivo | Qué es |
|---|---|
| `01. MINUTA_PSAG ... 2026 09 29.md` | Minuta de la reunión: el pedido original |
| `02.Converting relev 260929_110319.txt`, `03..txt` | Transcripción de la reunión (texto corrido y con tiempos) |
| `WhatsApp Image ...jpeg` | Planilla en papel "Identificación de fallas", esquema de cuerpos y lista del cliente |
| `video-maquina-transcripcion.txt` | Recorrido de la máquina explicado por Emiliano |
| `GUIA-DE-USO.md` | Guía para la planta: paso a paso por rol y qué hacer si algo no aparece |
| `scripts-verificacion/` | Scripts para repetir las pruebas en local |

## Qué pidió el cliente y cómo quedó

| # | Pedido | Estado |
|---|---|---|
| 1 | Eliminar detenciones (sólo administrador) | Hecho. No se borra: queda marcada con quién, cuándo y motivo, y se puede restaurar. No cuenta en reportes ni exportaciones |
| 2 | Recodificar el histórico | No requiere desarrollo. Se hace por script cuando el cliente mande el Excel con la recodificación |
| 3 | Que un cambio de nombre en el maestro se vea en el historial | Ya funcionaba: el historial muestra el nombre actual del maestro |
| 4 | Detenciones sin equipo | Pendiente del cliente: decidir entre "vacío = máquina completa" (hoy) o crear un equipo "General" |
| 5 | Equipo crítico | Hecho, en Maestros → Equipos |
| 6 | Reglas de análisis | Hecho, en Maestros → Reglas de análisis. Tipo de falla + minutos + línea o equipo opcional → quién analiza (un rol o una persona) |
| 7 | Marca "requiere análisis" | Hecho. Se ve en la tabla, en el detalle y en el aviso al cargar |
| 8 | Formulario "Identificación de fallas" | Hecho, en el detalle de la detención: Falla (operario), Solución y Causa (obligatorias) y Plan de acción (opcional) del responsable, con firma |
| 9 | Estados Registrada → En análisis → Cerrada | Hecho, con filtro por estado y por análisis |
| 10 | Pantalla por rol ("Mis pendientes") | Hecho. Cada rol ve lo que le toca; el administrador ve todo y filtra por responsable |
| 11 | Base de soluciones con buscador | Hecho |
| 12 | Indicador de pendientes por área | Parcial: tarjetas en Mis pendientes. El cliente lo iba a definir más adelante |
| 13 | Exportar a Excel | Hecho, con los filtros aplicados |
| 14 | Corregir la indisponibilidad | Hecho. Antes dividía por días del mes × 3 turnos × 8 h. Ahora divide por las horas programadas reales. Marzo pasó de 11,2% a 12,9% |
| 15 | Tiempo programado por turnos y calendario de excepciones | Hecho. Estándar = suma de turnos activos; el administrador carga sólo los días distintos. Marzo a agosto usan las horas "HS PROG" del Excel histórico |
| 16 | Mostrar las horas programadas y su detalle | Hecho: total en el reporte y detalle día por día al tocar las horas de un mes |
| 17 | Colores por tipo de falla | Hecho, en Maestros → Tipos de falla |
| 18 | Pareto por mes | Hecho |
| 19 | Usuarios y roles del cliente | Dato a cargar por el administrador. La app exige al menos una línea a cada operario |
| 20 | Hora de inicio y fin en la carga manual | Hecho. Calcula minutos y turno; cruza la medianoche |

## Decisiones que conviene conocer

| Tema | Cómo quedó | Por qué |
|---|---|---|
| Quién completa el análisis | El operario describe la falla. Solución, causa y plan los completa sólo el responsable de la regla (o un administrador). Todos lo pueden leer | Planilla en papel del cliente |
| Cuándo se evalúan las reglas | Al crear y al editar una detención. No se reevalúa el histórico | No llenar Mis pendientes con cientos de casos viejos |
| Turnos | Siguen siendo globales, no por línea | Hoy hay una sola línea activa; el turno queda guardado en cada detención |
| Fines de semana | No tienen regla especial. Si no se trabaja, se carga 0 h en el calendario | Supuesto a confirmar con el cliente |
| Hora de la planta | Configurable en `Planta:ZonaHoraria` (por defecto `America/Argentina/Buenos_Aires`) | Que la hora cargada no dependa de la zona del servidor |
| "Todas las líneas" en reportes | Suma sólo las líneas con paradas o tiempo programado en el año | Una línea dada de alta sin uso no diluye el indicador |

## Cómo publicarlo

1. **Backup** de la base de producción.
2. Confirmá que SQL Server es **2017 o más nuevo**. El script 17 usa `STRING_AGG`.
3. Corré los scripts en este orden, en una ventana de poco uso. El 17 reescribe la tabla de detenciones al ampliar la columna de estado. Los dos se pueden volver a correr sin efecto.
   ```
   sqlcmd -S <servidor> -d PSAG_Mantenimiento -E -b -i 45064123_App/database/17_t76_ajustes.sql
   sqlcmd -S <servidor> -d PSAG_Mantenimiento -E -b -i 45064123_App/database/18_t76_tiempo_programado_historico.sql
   ```
   El 18 necesita una línea llamada `Tango 2`. Debe terminar diciendo "173 días insertados" la primera vez y "0 días insertados" las siguientes.
4. Revisá `Planta:ZonaHoraria` en `appsettings.json` de la API.
5. Publicá API y Web como siempre:
   ```
   dotnet publish 45064123_App/src/PSAG.Mantenimiento.Api -c Release -o publish/api
   dotnet publish 45064123_App/src/PSAG.Mantenimiento.Web -c Release -o publish/web
   ```
6. Hacé la configuración inicial que figura en `GUIA-DE-USO.md` → "Para el administrador": turnos reales, usuarios con línea, turno de cada maquinista, equipos críticos, reglas, calendario y colores.

## Cómo se verificó

Todo en local, con la app corriendo sobre SQLite y el historial real de Tango 2 (764 detenciones, marzo a agosto 2026). No se probó contra el SQL Server de producción. Los scripts 17 y 18 se probaron en Azure SQL Edge (motor de SQL Server) sobre una base armada con los scripts 01 a 16 del repo.

| Prueba | Resultado |
|---|---|
| Build | 0 errores, 0 advertencias |
| Tests automáticos | 172 en verde |
| `regresion.py`: lo que ya existía (dashboard, registros, inspecciones, catálogos, usuarios, CSV, carga y edición sin horas, cierre, notas) | 36 de 36 |
| `e2e.py`: flujo nuevo de punta a punta con cada rol | 28 de 28 |
| `fixes_f2.py`: edición sin pisar horas ni análisis, zona horaria, permisos | 31 de 31 |
| `web_f2.py`: edición desde la pantalla | 12 de 12 |
| Auditoría de usabilidad por área y rol | 85 problemas encontrados y corregidos, cada uno comprobado en pantalla |

Para repetirlas (macOS o Linux, con .NET 9 y Python 3 con `openpyxl` y `playwright`):

```
zsh scripts-verificacion/run-local.sh <ruta>/45064123_App 5400 5401 /tmp/t76.db
python3 scripts-verificacion/cargar_historial.py "<Historial detenciones tango 2.xlsx>" /tmp/t76.db
python3 scripts-verificacion/e2e.py http://localhost:5400 /tmp/t76.db
python3 scripts-verificacion/regresion.py http://localhost:5400 /tmp/t76.db
python3 scripts-verificacion/fixes_f2.py http://localhost:5400 /tmp/t76.db
```

`run-local.sh` levanta la API y la web con una base SQLite nueva. `cargar_historial.py` carga el Excel histórico (está en `00.Diseño Fase Inicial/Ajustes_test_25082026/EVentosHistoricosTango2/`). `setup_demo.py` crea usuarios de prueba por rol (`oper1`, `mant1`, `sup1`, clave `Clave123!`), reglas y datos de ejemplo para mirar las pantallas. `capturas.py` saca capturas por ruta y rol. Usuario administrador local: `admin` / `Admin123!`.

## Pendiente

- **Del cliente:** Excel de recodificación del histórico (#2), equipo "General" o vacío (#4), horarios reales de los turnos (la minuta dice 05-13, 13-21 y 21-05; la base trae 06-14, 14-22 y 22-06), supervisor de Tango 2 y usuarios de mantenimiento, si se trabaja los fines de semana.
- **Septiembre en 0%:** los reportes cuentan los meses hasta hoy. Un mes sin paradas cargadas aparece en 0% y baja el promedio. Con la carga diaria es lo correcto; con el histórico incompleto confunde.
- **Del 21 al 31 de agosto** no hay horas históricas y cuentan como días estándar. Si la línea no trabajó, cargarlos como 0 h en el calendario.
- **Fuera de T-76:** `database/09_seed_demo_data.sql` tiene una sección vieja que falla (`mnt.TiposEvento`). Inspecciones y Registros siguen con textos sin tilde.
