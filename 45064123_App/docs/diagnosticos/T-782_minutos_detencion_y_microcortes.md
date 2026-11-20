# T-782 · De dónde salen los minutos de detención del turno y los microcortes

Pedido de Ferretti (16/11, punto 2). Este informe es solo análisis, leyendo el código de `main` (commit `158d535`). No cambia código.
Las referencias son a archivos del repo (`45064123_App/src/...`).

## Resumen

- **Hoy hay dos "minutos de detención del turno" distintos**, y no tienen por qué coincidir:
  1. **Dashboard → Eficiencia por turno (cámara)**: suma las **paradas que vio la cámara** (`mnt.ParadasCamara`), recortadas al horario del turno.
     Incluye los microcortes. No depende de lo que declare el operador.
  2. **Reportes de Detenciones / Pareto**: suma los `MinutosEfectivos` de las **detenciones** (`mnt.Detenciones`) cuyo `TurnoId` es el del turno.
     Una detención creada por la cámara vale **0 minutos hasta que el operador la declara**.
- **Un microcorte** es una parada de la cámara que dura **20 s o más** y **menos que el umbral** de detención de la cámara (hoy 5 min por defecto; en modo prueba, desde 30 s).
  Queda solo en el log de paradas, no se crea una detención.
- **La máquina se da por reanudada con 20 s seguidos en marcha** (2 ventanas de 10 s). Si las microparadas están separadas por 20 s o más de marcha, cada una es una parada aparte.
  La detención toma como hora de parada **el inicio de la última parada**, la que llegó al umbral, y no el de la primera microparada.
  Esa es la explicación más probable de que la consola marcara 11:08 y el sistema 11:14 en la detención 000004. Hay que confirmarlo con los datos del 13/11 (consulta en la sección 5).

## 1. Total de minutos de detención del turno

### 1.a Eficiencia por turno (cámara)

El código es `ConteoCamaraService.ObtenerEficienciaAsync` y `EficienciaCamaraCalculador.MinutosParadaEnVentana`.

- **Qué suma**: la duración de cada parada de `mnt.ParadasCamara` de esa cámara que se superpone con el turno, **recortada a la ventana del turno**.
  Se redondea a minutos **una vez**, sobre el total de segundos.
- **Qué entra**: todas las paradas cerradas de 20 s o más:
  - las del tipo `PARADA` (máquina quieta);
  - las del tipo `SIN_MANGAS` (cám. 10: el rodillo gira pero no salen mangas);
  - las del tipo `ACUMULACION` (cám. 09: algún carril sin cortar);
  - tanto los microcortes como las largas.
- **Desde cuándo**: desde la `HoraInicio` del turno ese día hasta la `HoraInicio` + la duración del turno.
  El turno de noche es del día en que empieza (21:00 del día D a 05:00 del D+1). Las paradas se leen desde el día anterior, para poder recortar las que vienen del turno previo.
- **Una parada que cruza el cambio de turno**: **se reparte**. Cada turno cuenta solo los minutos que caen dentro de su ventana.
  Pero en el **conteo** de paradas (microcortes / largas) la parada cuenta **solo en el turno en que empezó**.
- **Una parada todavía en curso no suma**: recién entra a `ParadasCamara` cuando la máquina arranca, o sea con 20 s de marcha confirmados.
  Durante un turno en curso con la máquina parada, el número queda corto.
- La tabla de minutos (`mnt.ConteosCamaraMinuto`) también guarda los segundos de parada, acumulación y sin señal de cada minuto, **pero no se usa** para este total.

### 1.b Reportes de Detenciones (Dashboard de detenciones, Pareto)

El código es `DashboardService`, que filtra por `d.TurnoId`.

- **Qué suma**: el `MinutosEfectivos` de cada detención con el `TurnoId` del turno. Como dice el CLAUDE.md, es el único campo de minutos que se usa.
- **La detención automática** (cámara) nace así:
  - en `PENDIENTE_JUSTIFICACION`;
  - con `MinutosEfectivos = 0`;
  - con `TurnoId` y `Fecha` de la **hora de inicio** de la parada.
  
  Se cuenta en la lista, pero con 0 minutos, hasta que el operador la declara (`DeclaracionDetencionService`): ahí los minutos pasan a ser arranque declarado − hora de parada.
- **Una detención que cruza el cambio de turno**: **no se reparte**. Todos sus minutos van al turno en que empezó, porque es turno materializado y no se recalcula.
- Los microcortes **no aparecen acá**, porque no generan detención.

> Por eso los dos números difieren: el (a) incluye los microcortes y reparte entre turnos. El (b) no tiene los microcortes, pone la detención entera en el turno de inicio y depende de que la declaren.

## 2. Cómo nace un microcorte

El código está en `ConteoCamara/Analisis/ProcesadorMangas.cs`, `DetectorParadas.cs` y `Domain/Services/DetencionCamaraReglas.cs`.

1. **Señal** (cámara 10, rebobinadora): cada **ventana de 10 s** queda en:
   - `PARADA` si el rodillo estuvo quieto (movimiento < 6) en la mitad de los cuadros o más;
   - `SIN_MANGAS` si el rodillo gira pero no cruzó ninguna manga por la mesa;
   - `MARCHA` si gira y cruzó al menos una manga.
   
   En la cámara 09 es por carriles: `MARCHA` solo si los 4 carriles cortan a la frecuencia normal.
2. **Apertura (histéresis)**: hacen falta **2 ventanas seguidas no-MARCHA (20 s)** para abrir una parada (`VentanasParaConfirmar = 2`).
   La parada se fecha al **inicio de la primera** de esas ventanas. Una sola ventana de 10 s sin marcha no abre nada.
3. **Regla de los 20 s para darla por reanudada**: hacen falta **2 ventanas seguidas en MARCHA (20 s)** para cerrarla.
   El fin se fecha al inicio de la primera ventana en marcha. Un arranque de 10 s (1 ventana) **no corta la parada**: queda adentro.
4. **Duración mínima**: si la parada cerrada dura **menos de 20 s** (`SegundosMinimosParada`), se descarta y no se manda a la API.
5. **Umbral de detención**: lo decide `DetencionCamaraReglas.UmbralSegundos`.
   - Vale `SegundosUmbralDetencion` si está cargado (modo prueba, **de 30 a 14400 s**). El mínimo es 30 porque la cámara recién confirma a los 20 s.
   - Si no, `MinutosUmbralDetencion` × 60 (1 a 240 min, **5 por defecto**).
   - Un valor fuera de rango vuelve a 5 min.
   - Si la parada llega al umbral y el punto tiene `CreaDetenciones`, la API crea la detención **mientras sigue parada**, sin esperar el arranque.
     La parada en curso viaja en cada lote, cada 30 s, así que puede haber hasta ~30 s de demora.
   - **De 20 s hasta el umbral = microcorte**: queda solo en `mnt.ParadasCamara`. Se cuenta en *Microcortes* del Dashboard y se ve en *Paradas y producción (cámara)*.

Resolución: todo es en pasos de 10 s. Inicio y fin se redondean a la ventana.

## 3. Prueba del 13/11: consola 11:08, sistema 11:14 (detención 000004)

La detención automática guarda como `FechaHoraParada` **el inicio de la parada de la cámara que llegó al umbral** (`ConteoCamaraService.CrearDetencionesAsync`, `FechaHoraParada = c.Inicio`).
Con microparadas seguidas entre 11:08 y 11:14, hay dos mecanismos que llevan a 11:14:

- **Arranques de 20 s o más entre microparadas.** Cada vez que la máquina anduvo 20 s seguidos (2 ventanas con rodillo girando y al menos una manga en cada una), la parada se cerró.
  Cada microparada quedó como **parada aparte**. Ninguna llegó al umbral, así que todas quedaron como microcortes.
  Recién la que empezó cerca de las 11:14 duró lo suficiente, y la detención nació con **esa** hora.
- **Con umbral de prueba de 30 s**, cada microparada de 30 s o más crea **su propia** detención.
  Es posible que 000001 a 000003 sean microparadas de esa misma secuencia, si ese día estaba en 30 s.
  Para afirmarlo hay que ver qué umbral tenía cargado el punto ese día.

Simulación con el `DetectorParadas` real (no es un dato de planta: es una secuencia inventada parecida a la del 13/11):

```
A: microparadas de 30-50 s con arranques de 20-30 s desde 11:08, umbral 5 min
  parada 11:08:00-11:08:30   30 s PARADA     -> microcorte (solo log)
  parada 11:09:00-11:09:50   50 s PARADA     -> microcorte (solo log)
  parada 11:10:10-11:10:40   30 s SIN_MANGAS -> microcorte (solo log)
  ...
  parada 11:14:10-11:14:40   30 s PARADA     -> microcorte (solo log)
  [11:20:00] parada en curso desde 11:15:00 llega al umbral -> detención con hora de parada 11:15:00
C: un arranque de 10 s dentro de la parada  -> una sola parada 11:08:00-11:14:20 (no se corta)
D: un arranque de 20 s dentro de la parada  -> microcorte 11:08:00-11:09:00 + detención desde 11:09:20
```

La salida completa está en el adjunto `T-782_simulacion.txt`.

Otras causas posibles (menos probables; se descartan con los mismos datos):

- La consola de la máquina marca la parada por **otro equipo de la línea**, mientras que en la mesa de la rebobinadora siguieron pasando mangas y el rodillo giraba. La cámara 10 solo ve la rebobinadora.
- Un corte de video o una cámara congelada entre 11:08 y 11:14. Si se congela, esos cuadros se descartan y no hay parada. Se ve en `mnt.AlertasCamara`.
- Una diferencia de reloj entre la consola y el servidor. Es poco probable: son 6 minutos.

## 4. Qué se descarta hoy y no queda registrado

| Qué | Dónde queda | Efecto |
|---|---|---|
| Paradas de **menos de 20 s** (1 ventana suelta o una parada que se cierra antes de los 20 s) | Solo en los segundos de cada minuto (`ConteosCamaraMinuto`) | No son parada ni microcorte, y no suman en "Minutos de parada" del turno |
| **Arranques de 10 s** dentro de una parada | Se absorben en la parada | La parada se ve más larga (incluye esos 10 s de marcha) |
| Microparadas separadas por **menos de 20 s** de marcha | Se **unen** en una sola parada | Un solo microcorte o una sola detención, en vez de varios |
| Paradas de 20 s hasta el **umbral** | Solo `ParadasCamara` (log de microcortes) | No hay detención: no entran en Reportes de Detenciones, Pareto ni `MinutosEfectivos` |
| Parada que llega al umbral **sin turno activo** a esa hora | Solo un warning en el log de la API | No se crea la detención (cuenta en `DetencionesSinTurno`) |
| Cuadros de **cámara congelada** (6 s sin cambios) | Alerta `CONGELADA` | No hay parada en ese lapso, aunque la máquina esté parada |
| **Sin video** más de 120 s con una parada abierta | La parada se cierra con `FinIncierto`; al volver arranca otra con `InicioIncierto` | La parada queda partida en dos |
| Lotes que la API rechazó 3 veces | `pendientes\rechazados\` en el servidor | No llegan a la base hasta que alguien los reenvíe |
| Apagado brusco del servicio (no por NSSM) | — | Se pierden hasta ~30 s de datos |
| Detención automática **eliminada** | — | No se vuelve a crear (es idempotente por `IdentificadorParadaCamara`) |
| Parada **en curso** | Solo en memoria y en el lote (`paradasEnCurso`) | No suma en "Minutos de parada" del turno hasta que cierra |
| Cámara 09 | `ParadasCamara` | Solo informa: no crea detenciones (`CreaDetenciones` apagado) |

## 5. Para confirmar el punto 3 con datos reales (requiere OK de Emiliano)

Es una consulta **solo de lectura** sobre la base de producción. No modifica nada.

```sql
-- Umbral y configuración de la cámara
SELECT Codigo, CreaDetenciones, MinutosUmbralDetencion, SegundosUmbralDetencion
FROM mnt.PuntosControlCalidad WHERE Codigo LIKE 'T2-CAM%';

-- Paradas de la cámara entre 11:00 y 11:30 del 13/11 (hora de planta, -03:00)
SELECT FechaHoraInicio, FechaHoraFin, DuracionSegundos, Tipo, InicioIncierto, FinIncierto, IdentificadorExterno
FROM mnt.ParadasCamara
WHERE FechaHoraInicio >= '2026-11-13T11:00:00-03:00' AND FechaHoraInicio < '2026-11-13T11:30:00-03:00'
ORDER BY FechaHoraInicio;

-- Minuto a minuto (segundos en marcha / parada / sin mangas)
SELECT Minuto, Carril, Rollos, SegundosMarcha, SegundosParada, SegundosAcumulacion, SegundosSinSenal, Estado
FROM mnt.ConteosCamaraMinuto
WHERE Minuto >= '2026-11-13T11:00:00-03:00' AND Minuto < '2026-11-13T11:30:00-03:00'
ORDER BY Minuto, Carril;

-- Detenciones automáticas del día y alertas de cámara
SELECT Codigo, FechaHoraParada, FechaHoraArranqueDetectada, IdentificadorParadaCamara, EstadoActual
FROM mnt.Detenciones WHERE Fecha = '2026-11-13' AND IdentificadorParadaCamara IS NOT NULL ORDER BY FechaHoraParada;
SELECT Tipo, FechaHoraInicio, FechaHoraFin, Detalle FROM mnt.AlertasCamara
WHERE FechaHoraInicio >= '2026-11-13T10:30:00-03:00' AND FechaHoraInicio < '2026-11-13T12:00:00-03:00';
```

Además, la línea `Detención automática DET-... creada por la cámara ... (parada desde HH:mm:ss)` del log de la API de ese día.

## 6. Qué queda por definir (para la tarea "Registrar todas las detenciones")

1. **¿Qué número es "el" total del turno?** ¿El de la cámara (con microcortes, repartido entre turnos) o el de las detenciones declaradas? ¿O mostrar los dos con nombre distinto?
2. **¿Las microparadas seguidas se agrupan?** Por ejemplo, unir paradas separadas por menos de N segundos de marcha, para que la detención tome la hora de la primera (11:08).
   Hoy N = 20 s, fijo por la histéresis.
3. **¿Qué se registra por debajo del umbral?** ¿Detenciones para todo, o microcortes en un registro aparte, como hoy?
   ¿Bajar la duración mínima de 20 s? El piso real es 10 s, por la ventana.
4. **¿Una detención que cruza el cambio de turno se parte en dos** (una por turno) o se queda en el turno de inicio, como hoy?
5. **¿El total del turno en curso debe incluir la parada abierta** hasta "ahora"?
