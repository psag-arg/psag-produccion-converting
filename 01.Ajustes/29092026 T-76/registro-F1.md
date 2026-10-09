# Registro F1 · Scripts 17 y 18 sobre SQL Server (T-76)

**Veredicto: VERIFICADO en Azure SQL Edge (motor SQL Server 2019, arm64). No verificado en SQL Server real.**

| Nivel | Estado |
|---|---|
| Local | Hecho en la rama `t76-fix-sql` (worktree `wt-f1`) |
| Commit | 2 commits de código y este registro (ver tabla al final) |
| PR, merge, deploy | No. Nada pusheado ni mergeado |
| Visible para el usuario final | No. Son scripts de base de datos |

## Resultado

Los scripts 17 y 18 corren sin error sobre una base armada con los scripts 01 a 16. Correrlos de nuevo no cambia nada. Antes del fix, el 17 fallaba con `Msg 5074`.

## Qué falló antes del fix

Corrida del 17 original (el de HEAD) sobre una base limpia con 01 a 16:

```
Msg 5074, Level 16, State 1, Server 983a1946b6c9, Line 5
The object 'DF__Detencion__Estad__7F2BE32F' is dependent on column 'EstadoActual'.
```

Ese es el primer bloqueo. Probé aparte, dentro de una transacción con ROLLBACK, qué pasa si se quita sólo el DEFAULT. Falla el siguiente objeto:

```
Msg 5074, Level 16, State 1, Server 983a1946b6c9, Line 4
The index 'IX_Detenciones_EstadoActual' is dependent on column 'EstadoActual'.
```

Además, el 17 original no era atómico. Quitaba los CHECK `CK_Detenciones_EstadoActual` y `CK_DetencionesEstadoHistorial_EstadoNuevo` antes del ALTER fallido. Cuando el ALTER fallaba, esos dos CHECK quedaban eliminados y nada los restauraba.

Dependencias reales de las columnas, leídas de `sys.*`:

| Columna | DEFAULT | Índices que la usan | CHECK |
|---|---|---|---|
| `Detenciones.EstadoActual` | `DF__Detencion__Estad__7F2BE32F` ('REGISTRADA'), nombre generado por SQL Server | `IX_Detenciones_EstadoActual` (clave), `IX_Detenciones_LineaId_EstadoActual` (clave), `IX_Detenciones_Fecha`, `IX_Detenciones_LineaId`, `IX_Detenciones_Linea_Turno_Fecha` (INCLUDE) | `CK_Detenciones_EstadoActual` |
| `DetencionesEstadoHistorial.EstadoNuevo` | no | `IX_DetencionesEstadoHistorial_DetencionId` (INCLUDE) | `CK_DetencionesEstadoHistorial_EstadoNuevo` |
| `DetencionesEstadoHistorial.EstadoAnterior` | no | no | no |

## Qué se cambió

- **`17_t76_ajustes.sql`**: los tres ALTER se reemplazaron por un bloque que, para cada columna, en una transacción:
  1. Lee de `sys.indexes` y `sys.index_columns` los índices (no PK ni UNIQUE) que usan la columna, y arma su `CREATE` con claves, orden, INCLUDE y filtro.
  2. Lee de `sys.default_constraints` el DEFAULT, con su nombre y definición, y de `sys.check_constraints` los CHECK que mencionan la columna.
  3. Elimina esos objetos, hace `ALTER COLUMN ... NVARCHAR(30)` con la misma nulabilidad y recrea índices y DEFAULT con el mismo nombre.
  - Si la columna ya tiene 60 bytes, no hace nada. Por eso es idempotente.
  - El bloque que quita el CHECK viejo ahora va después del ensanchado. Si el ensanchado falla, la transacción revierte y el CHECK sigue en su lugar.
  - Los CHECK se recrean en los bloques que ya existían, con `PENDIENTE_JUSTIFICACION`.
  - Los demás cambios del 17 se revisaron. Son ADD COLUMN, ADD CONSTRAINT y FK nuevos, sin dependencias sobre columnas existentes, y el DROP de un CHECK viejo que se recrea después. No hay columnas calculadas ni vistas WITH SCHEMABINDING.
- **`18_t76_tiempo_programado_historico.sql`**: se agregó `USE [PSAG_Mantenimiento]` seguido de `GO`. Sin eso, `sqlcmd -i` sin `-d` corre en `master` y falla con `Msg 208, Line 14, Invalid object name 'mnt.Lineas'`.
- **`tools/generar_18_tiempo_programado.py`**: el mismo `USE` fue a la plantilla, porque el 18 lo genera este script. No pude correrlo, porque necesita el Excel que no está en el repo. Sí comprobé que, alimentado con los VALUES del archivo, regenera el 18 byte a byte.
- Sin cambios en los scripts 01 a 16 ni en el código C#.

## Verificación

Entorno: Azure SQL Edge `15.0.2000.1574 (ARM64)` en Docker (colima), con cliente go-sqlcmd.

Comando de ejecución:

```
sqlcmd -S localhost,14333 -U sa -C -I -b -i <archivo.sql>
```

`-b` es necesario. Sin él, sqlcmd termina con código 0 aunque un script falle, y eso lo comprobé.

Secuencia de la corrida final, desde cero:

| Paso | Script | Resultado | Última línea de salida |
|---|---|---|---|
| 1 | 01 a 08 | rc 0 | `Roles y usuario administrador creados exitosamente.` |
| 1 | 09 (sólo usuarios, ver "Fuera de alcance") | rc 0 | `Usuarios de prueba creados.` |
| 1 | 10, 11, 12 | rc 0 | `Tablas de Eje 3 - Inspecciones creadas correctamente.` |
| 1 | 15, 16 | rc 0 | `Datos de demostración de Inspecciones insertados correctamente.` |
| 1 | datos previos a 17 (línea Tango 2, 3 detenciones, 3 historiales) | rc 0 | `pre17  3` |
| 2 | 17 | rc 0 | `Script 17_t76_ajustes.sql completado.` |
| 3 | 18 | rc 0 | `Tiempo programado histórico de Tango 2: 173 días insertados.` |
| 4 | datos posteriores a 17 (PENDIENTE_JUSTIFICACION y una eliminada) | rc 0 | fila `DET-TEST-0005 ... 1` |
| 5 | 17 otra vez | rc 0 | `Script 17_t76_ajustes.sql completado.` (sin creaciones ni ampliaciones) |
| 6 | 18 otra vez | rc 0 | `Tiempo programado histórico de Tango 2: 0 días insertados.` |

Verificación después de 17:

```
Detenciones EstadoActual nvarchar 60 0
DetencionesEstadoHistorial EstadoAnterior nvarchar 60 1
DetencionesEstadoHistorial EstadoNuevo nvarchar 60 0
DF__Detencion__Estad__7F2BE32F ('REGISTRADA')
CK_Detenciones_EstadoActual ([EstadoActual]='PENDIENTE_JUSTIFICACION' OR ...)
CK_DetencionesEstadoHistorial_EstadoNuevo ([EstadoNuevo]='PENDIENTE_JUSTIFICACION' OR ...)
mnt.ReglasAnalisis y mnt.TiemposProgramadosDia: existen
Equipos.Critico, TiposFalla.Color, Detenciones.Eliminada, Detenciones.ReglaAnalisisId: existen
```

Vistas, con datos de prueba posteriores a 17:

```
VW_DetencionesDetalle          4 filas de prueba, 0 con Eliminada visible
VW_DetencionesAbiertas         3 filas (sin la CERRADA ni la eliminada)
VW_DetencionesIndicadores      4 detenciones de Tango 2
VW_ResumenTurnoActual          4 detenciones de Tango 2
Vistas con filtro Eliminada    4 de 4
```

CHECK: el insert con `PENDIENTE_JUSTIFICACION` funciona. Un insert con `ESTADO_INVENTADO` falla con `Msg 547 ... conflicted with the CHECK constraint "CK_DetencionesEstadoHistorial_EstadoNuevo"`.

Tango 2 (script 18):

```
Dias  TotalHoras  HorasMarzo  DiasConHoras  Desde       Hasta       Historicos
173   3575.50     647.50      159           2026-03-01  2026-08-20  173
```

Coincide con lo esperado: 173 días, 3575.5 h, marzo 647.5 h. Después de la segunda corrida sigue igual.

Comparación del esquema. Se volcaron índices, columnas de índice, DEFAULT, CHECK, FK, columnas de `mnt` y vistas, antes y después del 17:

- 5 líneas eliminadas. Son los 2 CHECK y las 3 columnas de estado, que cambian de `varchar 20` a `nvarchar 60`. Es lo esperado.
- 64 líneas agregadas. Son los objetos nuevos del 17.
- Los 6 índices existentes y el DEFAULT de `EstadoActual` son idénticos, línea por línea.
- Después de correr 17 y 18 otra vez, el volcado es idéntico al de antes de la segunda corrida.

## Lo que Edge no prueba

- Edge es el motor SQL Server 2019. No probé SQL Server 2022 ni Azure SQL.
- No medí tiempos ni bloqueos sobre una tabla grande. El ALTER reescribe la tabla y toma un bloqueo de esquema. En producción conviene correrlo en una ventana de mantenimiento con backup previo. Supuesto: el tiempo depende del volumen de `Detenciones`, que no conozco.
- No hay replicación, CDC, tablas temporales ni particiones en el entorno de prueba. Si existen en producción, el ALTER puede comportarse distinto.
- `STRING_AGG` requiere SQL Server 2017 o superior y nivel de compatibilidad 140 o más. El README dice 2019 o superior, pero no probé con un nivel de compatibilidad menor.
- El entorno usa collation `SQL_Latin1_General_CP1_CI_AS`. No probé otra.
- La recreación de índices copia claves, INCLUDE, filtro y unicidad. No copia opciones como fill factor. En esta base todas son las de fábrica, y el volcado las compara.

## Fuera de alcance (no corregido)

**`09_seed_demo_data.sql` falla en la sección "EVENTOS DE DEMOSTRACIÓN"** (líneas 95 a 259):

```
Msg 208, Level 16, State 1, Server 983a1946b6c9, Line 20
Invalid object name 'mnt.TiposEvento'.
```

Esa sección usa el modelo viejo: `Eventos`, `TiposEvento`, `Maquinas`, `Causas`. El script 04 ya los reemplazó por `Detenciones`, `Equipos` y `TiposFalla`. No es un límite de Edge.

Para esta prueba corrí 09 sólo hasta la línea 94, que crea los usuarios. Lo hice con una copia en el scratchpad. Como la regla era no editar 01 a 16, no toqué el archivo del repo.

Opciones: borrar la sección legacy o reescribirla con el modelo actual. Queda pendiente de decisión.

## Datos de prueba (sólo Edge, no están en el repo)

Los seeds crean "Linea 1" a "Linea 6", pero no "Tango 2". Para la prueba inserté "Tango 2" con código `TGO2-TEST` en `mnt.Lineas`. Insertar detenciones y historiales de prueba no forma parte de los scripts.

## Commits

| Hash | Mensaje |
|---|---|
| fda4b11 | fix(db): ampliar columnas de estado en 17 sin fallar por DEFAULT, CHECK ni índices |
| 605cf9b | fix(db): script 18 se ejecuta sobre PSAG_Mantenimiento y el generador lo emite |
| (este commit) | docs(t76): registro F1 de scripts SQL |

## Qué queda para decidir

1. La sección legacy de eventos en 09. Borrarla o reescribirla con el modelo actual.
2. Cuándo y cómo correr 17 en producción, dada la tabla real y su volumen.
