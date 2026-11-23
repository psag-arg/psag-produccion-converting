# Conteo de producción y paradas por cámara (T-610)

Cuenta los rollos que salen de la cortadora de la **Tango 2 / Futura 2** y detecta las paradas mirando la
**cámara 09** del grabador Hikvision (NVR `172.16.9.67`). No requiere hardware nuevo ni toca el NVR: sólo lee el
video en vivo (una conexión RTSP de lectura).

Desde el 03/11/2026 el servicio corre **varias cámaras** (`ConteoCamara:Camaras`). La **cámara 09** sigue igual: sólo
informa, y las paradas detectadas se cruzan con las detenciones cargadas en *Calidad → Paradas y producción (cámara)*.
La **cámara 10** (rebobinadora) cuenta las **mangas** y **crea sola la detención** cuando la máquina queda parada 5
minutos o más (umbral configurable en Maestros). Ver la sección 8.

## 1. Cómo funciona

```
NVR 172.16.9.67 ──RTSP (canal 901, H.265 2560x1920 20 fps)──> ffmpeg.exe (proceso hijo)
      gris 640x480, 10 fps por stdout
                                   │
PSAG.Mantenimiento.ConteoCamara (servicio NSSM "PSAG_ConteoCamara")
   brillo medio de un cuadrado de 32 px por carril (zonas-camara.json)
   ventana de 10 s → espectro → frecuencia de corte común → estado por carril
   minutos por carril + paradas con histéresis
   lotes cada 30 s → carpeta pendientes\ → POST api/conteo-camara/ingesta (X-Clave-Ingesta)
                                   │
API (PSAG_Mantenimiento_API) → mnt.CAM_ConteosMinuto, mnt.CAM_Paradas (scripts 25 y 36; ex ConteosCamaraMinuto / ParadasCamara)
```

**Método** (validado en la prueba de factibilidad, informe `T-610_pruebas_camara_cortadora.md`): cada rollo que pasa
por el punto de un carril genera un ciclo de brillo. En cada ventana de 10 s se calcula el espectro de cada carril y
se busca la frecuencia común (la cortadora corta los 4 carriles a la vez, ~2,78 rollos/s = 167/min por carril).

| Estado del carril en la ventana | Regla | Rollos |
|---|---|---|
| `MARCHA` | se mueve (desvío del brillo > 8) y tiene pico en la frecuencia común, ≥ 80 % de la nominal | frecuencia × 10 s |
| `ACUMULACION` | se mueve pero sin la frecuencia de corte (pegados, a los tirones), o a menos del 80 % de la nominal | 0, o frecuencia × 10 s si el paso es regular pero lento |
| `PARADA` | el brillo no cambia | 0 |
| `SIN_SENAL` | no hubo video | 0 |

**Paradas** (histéresis): la línea está *en marcha* si los 4 carriles están en marcha y *detenida* si no. Para cambiar
hacen falta **2 ventanas seguidas** (20 s); el cambio se fecha al inicio de la primera, así no se corre la hora. Una
ventana suelta de un carril ruidoso no abre una parada y un arranque de 10 s no la corta. Tipo `PARADA` si en algún
momento estuvieron quietos los 4 carriles; `ACUMULACION` si no. `Carriles` = los que no estuvieron en marcha.
`InicioIncierto` / `FinIncierto`: el servicio arrancó con la línea parada o se cortó el video en medio de la parada.

**Cruce con detenciones** (se calcula al consultar): `CON_DETENCION` si hay una detención de la misma línea con
horario que se superpone (±5 min); `POSIBLE` si hay una sin horario del mismo día y turno; `SIN_DETENCION` si no.

## 2. Decisión de diseño: .NET + ffmpeg portable (no Python)

| | .NET 9 en el repo (elegida) | Python empaquetado |
|---|---|---|
| Runtime en el servidor | ya está (lo usa la API) | intérprete embebido + venv/wheels (numpy, opencv) |
| Paquetes | ninguno (framework compartido de ASP.NET Core) | ~150 MB de wheels a mano, sin pip online |
| Decodificación | `ffmpeg.exe` estático, un archivo | ídem |
| Servicio | NSSM, igual que la API | NSSM |
| Base de datos | por la API (Clean Architecture, EF Core, mismas reglas) | habría que duplicar validaciones y conexión |
| Mantenimiento | un solo stack, tests en `dotnet test` | dos stacks |

El cálculo es chico (DFT de 100 muestras × 4 carriles cada 10 s): no hace falta numpy ni OpenCV.

## 3. Archivos y configuración

Carpeta del servicio en el servidor: `C:\PSAG\ConteoCamara\`

| Archivo | Qué es | ¿Versionado? |
|---|---|---|
| `PSAG.Mantenimiento.ConteoCamara.exe` + dlls | el servicio | se publica |
| `ffmpeg\ffmpeg.exe` | ffmpeg estático con HEVC (usado en la prueba: 7.1 *essentials* de gyan.dev) | **no** (va en el paquete) |
| `appsettings.json` | parámetros (sección `ConteoCamara`) | sí, sin secretos |
| `zonas-camara.json` | coordenadas de los carriles | sí (valores calibrados); el instalador conserva el del servidor |
| `appsettings.local.json` | overrides del servidor (opcional) | no |
| `pendientes\lote-*.json` | lotes que la API todavía no aceptó | no |
| `pendientes\rechazados\` | lotes que la API rechazó 3 veces por su contenido (4xx), apartados para no trabar la cola | no |
| `pendientes\minuto-en-curso.json` | minuto incompleto al detener el servicio; se suma si se reinicia dentro de ese minuto | no |
| `logs\conteo.log` | salida del servicio (NSSM, rota a 10 MB) | no |
| `C:\PSAG\secretos\nvr.env` | `NVR=172.16.9.67`, `NVR_USER=...`, `NVR_PASS=...` | **nunca** |

**Secretos**: las credenciales del NVR se leen del `.env` (ruta en `ConteoCamara:Fuente:ArchivoCredenciales`), nunca se
loguean (el stderr de ffmpeg se filtra). La clave de ingesta es la misma que `Calidad:ClaveIngesta` de la API y va como
variable de entorno del servicio (`ConteoCamara__Api__ClaveIngesta`, la carga el instalador en NSSM).
Nota: la URL RTSP con usuario y clave queda en la línea de comandos del proceso ffmpeg (visible para administradores
del servidor). Conviene un usuario del NVR **sólo de lectura/visualización** para este servicio.

Parámetros principales (`appsettings.json` → `ConteoCamara`):

| Clave | Valor | Para qué |
|---|---|---|
| `Camara` | `T2-CAM09` | código de la cámara (Maestros → Cámaras) que define la línea. T-798 etapa 4: antes `PuntoControl` (se sigue leyendo) |
| `Fuente:RutaRtsp` | `/Streaming/Channels/901` | canal 09 en vivo |
| `Fuente:Canal` | (vacío) | T-785: número de cámara del grabador; si está, la ruta es `/Streaming/Channels/{Canal}01` y manda sobre `RutaRtsp` |
| `Fuente:AnchoDecodificado` × `AltoDecodificado` @ `Fps` | 640 × 480 @ 10 | lo justo para el método |
| `Analisis:FrecuenciaNominalHz` | 2.78 | velocidad normal; cambiarla si cambia el formato/velocidad |
| `Analisis:VentanasParaConfirmar` | 2 | histéresis (20 s) |
| `Analisis:SegundosMinimosParada` | 20 | cámara 09: paradas más cortas no se informan (quedan en los minutos). La 10 no lo usa (T-784) |
| `Api:UrlBase` | `http://localhost:5188` | la API en el mismo servidor |

## 4. Instalación offline en el servidor de Producción (2k22recimgProd, 172.16.18.52)

Requisitos que ya cumple el servidor: Windows Server 2022, runtime .NET 9 (hosting bundle de la API), NSSM en
`C:\PSAG\Mantenimiento\tools\nssm.exe`. Uso medido: ~0,3 núcleos y ~110 MB de RAM.
El servidor tiene que llegar al NVR `172.16.9.67:554` (RTSP/TCP). Verificar antes:
`Test-NetConnection 172.16.9.67 -Port 554`.

**En la PC de desarrollo**

1. `.\deploy\Empaquetar-ConteoCamara.ps1 -Ffmpeg <ruta a ffmpeg.exe estático con HEVC>` → `publish\ConteoCamara-AAAAMMDD.zip`.
2. Copiar el ZIP al servidor (carpeta compartida o USB). El paquete no lleva credenciales.

**En el servidor (PowerShell como Administrador)**

1. Base: correr `database\25_t610_conteo_camara.sql` (requiere el 23). `sqlcmd -S localhost\SQLEXPRESS -I -b -f 65001 -i 25_t610_conteo_camara.sql`.
2. API: publicar la versión de esta rama (`02-Publicar-Aplicacion.ps1`) y reiniciar `PSAG_Mantenimiento_API` y IIS.
   Si todavía no tiene `Calidad__ClaveIngesta`, configurarla (ver `docs/CALIDAD.md`).
3. Web → Maestros → Cámaras: dar de alta **`T2-CAM09`** en la línea Tango 2 y dejarla **Habilitada** (T-798 etapa 4; ver la sección 11).
4. Crear `C:\PSAG\secretos\nvr.env` con las tres líneas `NVR=172.16.9.67`, `NVR_USER=...`, `NVR_PASS=...`.
5. Descomprimir el ZIP en una carpeta temporal y, desde ahí: `.\06-Instalar-ConteoCamara.ps1`
   - copia a `C:\PSAG\ConteoCamara` (conserva `zonas-camara.json` y `appsettings.local.json` si ya existían),
   - restringe `C:\PSAG\secretos` a Administradores y SYSTEM,
   - pide la clave de ingesta (no se muestra) y registra `PSAG_ConteoCamara` con NSSM (inicio automático, reinicio ante fallas, logs rotados).
6. Verificar: `Get-Content C:\PSAG\ConteoCamara\logs\conteo.log -Tail 20` → "Video recibido del NVR"; a los 2 minutos,
   la pantalla *Paradas y producción (cámara)* muestra la hora en curso.

Actualizar: mismo paquete nuevo y `.\06-Instalar-ConteoCamara.ps1 -SoloActualizar`.
Detener: `C:\PSAG\Mantenimiento\tools\nssm.exe stop PSAG_ConteoCamara` (cierra prolijo y manda lo pendiente).

Probar sin instalar (desde la carpeta del servicio):

```powershell
.\PSAG.Mantenimiento.ConteoCamara.exe vivo --segundos 120          # en vivo, imprime cada ventana, no manda nada
.\PSAG.Mantenimiento.ConteoCamara.exe procesar --archivo c09.mp4 --inicio 2026-10-28T10:15:00-03:00 --eco
```

## 5. Recalibrar las zonas (si se mueve la cámara)

1. Foto a resolución completa: `.\PSAG.Mantenimiento.ConteoCamara.exe foto --salida C:\temp\cam09.jpg`
2. `.\PSAG.Mantenimiento.ConteoCamara.exe calibrar --foto C:\temp\cam09.jpg` → abre `C:\temp\calibracion.html` en el
   navegador del servidor (funciona sin internet), elegí el carril, hacé clic sobre el centro del carril en el tramo
   recto de salida, descargá `zonas-camara.json` y reemplazá el de `C:\PSAG\ConteoCamara`. Reiniciar el servicio.
3. Opcional, ajuste fino numérico: `.\PSAG.Mantenimiento.ConteoCamara.exe sugerir --segundos 300` (con la línea en
   marcha). Prueba puntos alrededor de cada zona y muestra los que tienen la señal más limpia **y siguen siendo del
   mismo carril** (columna *pertenencia*: la parte irregular de la señal se parece a la del punto actual del carril).
   Cambiar sólo si el puntaje mejora claro y la amplitud queda por encima de ~12.

Las fotos y videos quedan en el servidor: no se mandan a ningún servicio externo.

## 6. Contrato de ingesta

`POST api/conteo-camara/ingesta`, header `X-Clave-Ingesta`, JSON:

```json
{
  "camara": "T2-CAM09",
  "minutos": [
    { "minuto": "2026-10-30T08:16:00-03:00", "carril": 1, "cantidad": 166.7, "segundosMarcha": 60,
      "segundosParada": 0, "segundosAcumulacion": 0, "segundosSinSenal": 0, "estado": "MARCHA" }
  ],
  "paradas": [
    { "identificadorExterno": "T2-CAM09-20261030T105334Z", "inicio": "2026-10-30T07:53:34-03:00",
      "fin": "2026-10-30T07:54:54-03:00", "tipo": "PARADA", "carriles": [1,2,3,4],
      "inicioIncierto": false, "finIncierto": false }
  ]
}
```

- Todo o nada: si un elemento es inválido el lote entero vuelve 400. El servicio sigue con los lotes siguientes y, después de 3 rechazos, lo aparta a `pendientes\rechazados\` (sólo si en ese ciclo la API aceptó otro lote). Si todos vuelven con el mismo motivo (por ejemplo, falta el punto de control en Maestros) es configuración: corta y no aparta nada. 5xx, red y timeout se reintentan en orden.
- Idempotente: minuto+carril por punto y `identificadorExterno` de la parada. Respuesta 200 con nuevos y repetidos.
- Los segundos de cada minuto suman 60; minutos con segundos en cero; fechas no futuras (tolerancia 10 min).
- 401 sin clave o clave incorrecta, 503 si la API no tiene `Calidad:ClaveIngesta`.

Consultas (con sesión): `GET api/conteo-camara/paradas?fechaDesde&fechaHasta&lineaId&soloSinDetencion&segundosMinimos`
y `GET api/conteo-camara/produccion?fecha&lineaId`.

## 7. Limitaciones conocidas

- **Validar contra el contador de la cortadora** antes de usar los rollos como dato oficial (el método mide frecuencia,
  no cuenta objetos: con paso regular es exacto, en acumulación es aproximado).
- En **acumulación** (curva llena, rollos a los tirones) la frecuencia es ambigua: esas ventanas se marcan
  `ACUMULACION` y el conteo de esos minutos es aproximado (en la prueba del 25/10, ±7 % entre métodos).
- Cámara 09: resolución de 10 s; paradas de menos de 20 s no se informan como parada (sí quedan en los segundos de cada
  minuto). La cámara 10 detecta las paradas cuadro a cuadro (sección 8.1) y no descarta ninguna.
- Si cambia la velocidad nominal (otro formato) ajustar `FrecuenciaNominalHz`; si no, las ventanas lentas se ven como acumulación.
- Si se apaga el servicio de golpe (no por NSSM) se pierden hasta ~30 s de datos en memoria; un minuto partido por un
  reinicio queda con lo del primer envío (el segundo se toma como repetido).

## 8. Cámara 10: mangas, rodillo y detención automática (03/11/2026)

### 8.1 Método `MANGAS`

Medido en grabaciones reales del 02 y 03/11 (día y noche), decodificando el stream principal a 320x180 y 5 fps. Se
usa el **movimiento medio** de una zona (diferencia absoluta entre dos cuadros seguidos):

| Zona (px de 2560x1440) | En marcha | Parada |
|---|---|---|
| **Mesa** `X 1120 Y 160 960x560`: cruzan las mangas | un pulso de 15-45 por manga, una cada ~2,7 s; 0-2 entre mangas | 0-1 |
| **Rodillo** `X 240 Y 80 320x480` (arriba a la izquierda) | ~20 constante | 0-1 con el rodillo quieto |

Cada ventana de 10 s queda en:

| Estado | Regla |
|---|---|
| `PARADA` | el rodillo estuvo quieto (< 6) la mitad de los cuadros o más |
| `ACUMULACION` → tipo de parada **`SIN_MANGAS`** | el rodillo gira pero no pasó ninguna manga en la ventana |
| `MARCHA` | el rodillo gira y pasó al menos una manga |

**Paradas (T-784, `DetectorParadasMangas`)**: ya no salen de las ventanas sino de las mangas, cuadro a cuadro, para que
cada microparada quede con su inicio y fin reales (la histéresis de 2 ventanas juntaba microparadas seguidas y descartaba
las de menos de 20 s: caso del 13/11, 11:08 a 11:14).

| | Regla | Parámetro (`Mangas:`) |
|---|---|---|
| Para | 10 s o más sin mangas, o con el rodillo quieto 10 s seguidos | `SegundosSinMangasParaParada` = 10 |
| Inicio | la última manga que salió (o el primer cuadro con el rodillo quieto) | |
| Arranca | 2 mangas seguidas a menos de 10 s una de otra; el **fin** es la primera. Una manga suelta no corta la parada | `MangasParaReanudar` = 2 |
| Tipo | `PARADA` si el rodillo estuvo quieto 5 s seguidos durante la parada; si no, `SIN_MANGAS` | `SegundosRodilloQuietoParaParada` = 5 |
| Mínimo | no hay: todo lo que cumple la definición se informa; el umbral de detención lo aplica la API | |

Corte de video o imagen congelada con la parada abierta: queda suspendida y, si el video vuelve antes de 120 s con la
máquina todavía parada, sigue la misma (mismo identificador). Si al volver ya estaba produciendo, o el corte fue más
largo, se cierra donde se perdió el video con `FinIncierto`. Las ventanas de 10 s siguen dando el estado de cada minuto.

Las **mangas** se cuentan siempre
(histéresis 12/4 y 1,2 s mínimo entre mangas) y van a `mnt.CAM_ConteosMinuto` (columna `Cantidad`, ex `Rollos`) en el carril 1; la API
las cuelga del turno de producción y la producción oficial sale de los indicadores (T-798, sección 11).

**Por qué las dos señales (OR):** en el 02-03/11 se vieron paradas con el rodillo girando y sin mangas (09:55, 05:20,
22:10, 23:15: todo quieto menos el rodillo) y paradas con el rodillo quieto (02:28 a 02:36, 23:10). Mirar sólo el
rodillo pierde las primeras; mirar sólo las mangas no distingue el tipo. **Empalme**: el rodillo sigue y las mangas
salen más espaciadas (cada ~7 s) pero siguen saliendo, así que no se abre parada; si alguna vez dejan de salir 10 s o
más queda como microparada (registrada, sin detención). Recién al umbral (5 minutos) se crea la detención.

### 8.2 Detención automática

1. Mientras la máquina sigue parada, cada lote lleva la parada **en curso** (`paradasEnCurso`: inicio y última hora
   vista). Cuando llega al umbral del punto de control (`MinutosUmbralDetencion`, 5 por defecto) y el punto tiene
   `CreaDetenciones`, la API da de alta la detención **sin esperar el arranque**. Para probar en planta el umbral se
   puede cargar en segundos (T-798: un solo campo `CAM_Camaras.UmbralDetencionSegundos`, 30 a 14400, Maestros → Cámaras; el script 36 migró
   segundos o minutos × 60). El umbral efectivo lo decide `DetencionCamaraReglas.UmbralSegundos`.
2. La detención nace en `PENDIENTE_JUSTIFICACION` ("Pendiente de declaración"), `OrigenRegistro = CAMARA`, con
   línea (la del punto), fecha y turno de la hora de parada, `FechaHoraParada` y **maquinista del turno**. Quedan
   vacíos el equipo, el tipo y detalle de falla, la descripción, la hora de arranque y los minutos (0).
3. **Idempotente**: `IdentificadorParadaCamara` (punto + inicio UTC) es único en `mnt.DET_Detenciones`. Un reenvío o la
   misma parada cerrada no la duplican; una detención eliminada no se vuelve a crear.
4. Cuando la máquina arranca, la parada cerrada guarda el arranque visto en `FechaHoraArranqueDetectada`. Es **sólo
   una sugerencia**: la hora de arranque la declara el operador (botón "Usar la de la cámara").
5. Todas las paradas quedan en `mnt.CAM_Paradas`, micro y grandes, con el umbral efectivo de ese momento
   (`UmbralSegundos`) y la marca `SuperaUmbral` (T-784; columnas creadas por el script 31 de T-798). Es un snapshot: si después cambia el umbral, la
   marca no cambia. Las que no llegan al umbral son **microparadas**: no crean detención ni se le piden declarar.

**Maquinista** (`DetencionCamaraReglas.ElegirMaquinista`): la asignación de la línea para el turno de la parada; si
hay dos (error de carga), la asignada más recientemente y se deja escrito en el historial; si no hay, la única
asignación de la línea sin turno fijo; si no, vacío, y al declarar queda el operador que declara si está asignado.

**Declaración** (`POST api/detenciones/{id}/declarar`): equipo, tipo y detalle de falla, descripción (obligatoria) y
hora de arranque HH:mm (o minutos). Pasa a `REGISTRADA`, se calcula `MinutosEfectivos` y se aplican las reglas de
análisis como en cualquier detención. Valida la línea asignada del operario en backend.

**Pantallas**: *Mis pendientes* → solapa **Pendiente de declaración** (primera), filtrada por las líneas del usuario
(administrador o supervisor sin líneas: todas). *Declarar detención* (`/detenciones/{id}/declarar`). **Aviso de fin
de turno**: banner en todas las pantallas cuando faltan 30 min o menos y hay pendientes (no bloquea nada).

### 8.3 Cámara congelada o sin video

- **Congelada**: si durante 6 s ningún píxel cambia más de 4 entre cuadros (una imagen viva siempre cambia: el reloj de
  la cámara avanza cada segundo y hay ruido; con la máquina y el rodillo quietos la diferencia máxima medida fue 15),
  esos cuadros se descartan antes de analizarlos: **no hay parada ni detención falsa**. Se abre una alerta `CONGELADA`
  y se cierra cuando la imagen cambia 1 s seguido (un cuadro clave del códec no la cierra).
- **Sin video**: 60 s sin cuadros del NVR abren una alerta `SIN_VIDEO` ("pérdida de conexión de cámara"); se cierra
  con el primer cuadro. Las alertas se guardan en `mnt.CAM_Alertas` y se ven en Mis pendientes y en Paradas (cámara).

### 8.4 Configuración (`appsettings.json` → `ConteoCamara:Camaras`)

La sección común (`Fuente`, `Analisis`, `Vigilancia`, `Api`) vale para todas; cada cámara pisa lo suyo. La 09 sigue
leyendo sus carriles de `zonas-camara.json` y conserva la carpeta `pendientes\`; las demás usan `pendientes\<código>`.
> **T-798 etapa 4**: `Activa` ya no existe (si viene, se ignora y se loguea): la habilitación está en Maestros →
> Cámaras. Los ejemplos de abajo con `PuntoControl`/`Activa` son históricos; ver la sección 11.

La 10 venía **apagada** (`"Activa": false`) hasta que el usuario del servicio tenga permiso sobre el canal 10. Para
encenderla, en `appsettings.local.json` del servidor:

```json
{ "ConteoCamara": { "Camaras": [ { "PuntoControl": "T2-CAM09" }, { "PuntoControl": "T2-CAM10", "Activa": true } ] } }
```

Probar sin instalar: `.\PSAG.Mantenimiento.ConteoCamara.exe vivo --camara T2-CAM10 --segundos 120` o
`procesar --camara T2-CAM10 --archivo c10.mp4 --inicio 2026-11-03T02:25:00-03:00 --eco`.

**Umbral, cuerpo y número de cámara**: Web → Maestros → Cámaras → editar `T2-CAM10` (T-798 etapa 4).

## 9. Eficiencia por turno en el Dashboard (reunión 03/11, script 28)

> **T-798 etapa 4**: `ObjetivoPorHora` se quitó de las cámaras. La tarjeta del Dashboard pasa a "Producción por cámara"
> y muestra contadas, minutos con datos, paradas y microcortes **sin objetivo ni %**; un turno sin minutos dice
> "Sin datos" (nunca 0). El objetivo y la eficiencia vuelven en la etapa 5 desde `PRD_ParametrosLineaProducto`. Lo que
> sigue describe el cálculo anterior.

- **Fórmula** [8:31]: eficiencia = contadas en el turno / objetivo del turno; objetivo = `ObjetivoPorHora` (Maestros →
  Calidad → cámara) x horas programadas del turno.
- **Horas programadas del turno** (`EficienciaCamaraCalculador.MinutosProgramadosTurno`): misma regla que las horas del
  día (T-541/T-542). Duración del turno si el calendario de la línea lo incluye ese día (sin calendario, todos los
  turnos activos); 0 si no. Con excepción del día (EXCEPCION/HISTORICO), las horas del día se reparten entre los turnos
  en proporción a su duración, sin pasar la duración del turno; 0 h = no programado.
- **Turno de noche**: es del día en que empieza (21:00 del día a 05:00 del siguiente). Se cuentan los minutos con ese
  `TurnoId` materializado dentro de esa ventana (no se recalcula el turno).
- **Sin objetivo** (`ObjetivoPorHora` vacío): estado `SIN_OBJETIVO`, el % viene vacío y el Dashboard dice "Falta cargar
  el objetivo" (nunca 0 %). Objetivo 0 (turno no programado): `NO_PROGRAMADO`, sin dividir por cero.
- **Paradas y microcortes**: minutos de todas las paradas de la cámara recortadas al turno (`MinutosParada`), y de
  ellos los de microparadas (`MinutosMicroparadas`, T-784); microcortes = paradas que empezaron en el turno y no
  llegaron al umbral **guardado al registrarlas** (`SuperaUmbral`; las anteriores al script 31 de T-798 se comparan con el umbral
  actual). Los reportes deciden si toman las microparadas. *Paradas y producción (cámara)* filtra por umbral.
- **Unidad**: `MetodoConteo` del punto (`CARRILES` = rollos, `MANGAS` = mangas). Vacío: 1 carril con datos = mangas,
  más = rollos.
- **API** (histórica): `GET api/conteo-camara/eficiencia`. **T-798 etapa 5: se quitó.** La eficiencia sale de los
  indicadores (`GET api/indicadores/turno/{id}` y `GET api/indicadores/produccion`): objetivo del parámetro línea +
  producto (snapshot en el tramo, prorrateado por minutos) vs. la producción oficial del turno (`PRD_ProduccionesTurno`,
  que se llena con la cámara `CuentaProduccionOficial` al cerrar cada tramo y la valida el supervisor). Las paradas y
  microparadas por cámara siguen en *Paradas y producción (cámara)* (`GET api/conteo-camara/paradas`).
- **T-803 · Microcortes en el presupuesto vs. real**: `GET api/indicadores/produccion` trae en cada fila (y en el total)
  `Microcortes`, `SegundosMicrocortes` y `MinutosMicrocortes`: paradas de cámara del `TurnoProduccionId` que no llegaron al
  umbral (`DetencionCamaraReglas.SuperaUmbral`). Por turno, día, línea y total: todos los del turno. **Por producto** (o
  filtrando un producto): los que **empezaron dentro de un tramo** de ese producto (las paradas tienen hora); "Sin producto
  (a completar)" lleva los de los turnos sin tramos. **Null (no 0)** si ninguna línea de la fila tiene cámara de conteo.
  *Dashboard → Presupuesto vs. real* muestra una sola tabla con esa columna; el detalle por cámara está en el modal
  "Detalle" del turno.

## 10. Cámara 8: bolsones para la eficiencia (T-785)

Ferretti (16/11, punto 1): la eficiencia cuenta **mangas** (cámara 10) y **bolsones** (cámara 8). La cámara 8 reemplaza a
la 09 en la eficiencia. IT habilitó la cámara 8 en el grabador el 10/11.

- **Método `BOLSONES`**: el mismo contador de pasos que las mangas (movimiento medio en la zona `Mangas:Mesa`, con
  histéresis `UmbralPasoAlto`/`UmbralPasoBajo` y `SegundosMinimosEntrePasos`), sin rodillo y **sin paradas**: las paradas
  y las detenciones las da la cámara 10. Los bolsones van a `mnt.CAM_ConteosMinuto` de la cámara `T2-CAM08` (carril 1).
- **Canal sin fijarlo en el código**: `Fuente:Canal = 8` en la entrada de la cámara (`appsettings.json`, o
  `appsettings.local.json` en el servidor). El repo la trae **apagada** (`"Activa": false`), como la 10 al principio.
- **API y Dashboard**: con el punto `T2-CAM08` (método *Bolsones*, objetivo por hora de bolsones), la eficiencia por turno
  muestra la cámara 10 en mangas y la 8 en bolsones, cada una con su objetivo. La producción del turno
  (`GET api/conteo-camara/produccion`) también sale por cámara, con su unidad.
- **Reemplazo de la 09**: el tilde *Cuenta para la eficiencia* (`IncluirEnEficiencia`) se quitó en T-798 (etapa 1).
  Lo reemplaza `CAM_Camaras.CuentaProduccionOficial` en la etapa 4 (cámaras unificadas). Hasta entonces, para sacar
  la 09 de la eficiencia se le borran el objetivo y el método (o se la desactiva).

### 10.1 Para probar con la cámara real (Francisco)

Desde `C:\PSAG\ConteoCamara`, con la versión nueva instalada y el usuario del NVR con permiso sobre el canal 8:

1. Foto: `.\PSAG.Mantenimiento.ConteoCamara.exe foto --camara T2-CAM08 --salida C:\temp\cam08.jpg`.
   - Anotar la resolución de la foto. Si no es 2560x1440, poner `Mangas:AnchoImagen` y `AltoImagen` de la 8.
2. En la foto, elegir un rectángulo por donde cruza **cada bolsón una sola vez**: una franja de la cinta, sin gente ni
   máquinas que se muevan. Cargarlo en `Mangas:Mesa` (X, Y, Ancho, Alto, en píxeles de la foto) en `appsettings.local.json`:

   ```json
   { "ConteoCamara": { "Camaras": [ { "PuntoControl": "T2-CAM09" }, { "PuntoControl": "T2-CAM10" },
     { "PuntoControl": "T2-CAM08", "Activa": true, "Mangas": { "Mesa": { "X": 0, "Y": 0, "Ancho": 0, "Alto": 0 } } } ] } }
   ```

   Las cámaras se combinan por posición (09, 10, 8): si el `appsettings.local.json` del servidor ya tiene algo (por
   ejemplo, la 10 con `"Activa": true`), agregar la tercera entrada y conservar lo demás.

3. Probar sin mandar nada: `.\PSAG.Mantenimiento.ConteoCamara.exe vivo --camara T2-CAM08 --segundos 300`.
   - Contar a mano los bolsones de esos 5 minutos y comparar con el total que imprime.
4. Ajustar si no coincide:
   - si cuenta de más: subir `UmbralPasoAlto` o `SegundosMinimosEntrePasos` (hoy 2 s: un bolsón no puede pasar más seguido que eso);
   - si cuenta de menos: bajar `UmbralPasoAlto`.
5. Si hay video grabado de la 8: `procesar --camara T2-CAM08 --archivo c08.mp4 --inicio <hora> --eco` repite la prueba sin la cámara en vivo.
6. Con el conteo bien:
   - (T-798) en Maestros → Cámaras, dar de alta o editar la `T2-CAM08`: línea Tango 2, canal 8, función CONTEO,
     magnitud BOLSONES, método PASOS, sin paradas, *Habilitada*; producción oficial de BOLSONES si corresponde;
   - sacarle a la 09 la producción oficial (o deshabilitarla);
   - reiniciar `PSAG_ConteoCamara`.
   (El script 33 viejo de T-785 y el alta en Maestros → Calidad ya no aplican: ver la sección 11.)


## 11. Cámaras unificadas (T-798 etapa 4, script 36)

**Maestro único `CAM_Camaras`** (Web → Maestros → Cámaras): cada cámara es un dispositivo con código, línea, equipo que
mira, canal del grabador y función (CONTEO o CALIDAD). Las de conteo dicen qué cuentan (`Magnitud`: MANGAS, ROLLOS,
PAQUETES, BOLSONES, PALLETS), cómo (`CARRILES` o `PASOS`), carriles, si detectan paradas, si crean detenciones y desde
qué umbral (un solo campo en segundos, 30 a 14400; vacío = 5 min) y si son la **producción oficial** de su magnitud en
la línea (una sola por línea y magnitud). Los puntos de control de calidad ya no tienen datos de cámara: eligen qué
cámara usan (opcional). La pantalla muestra el **último dato recibido** (verde ≤ 5 min, amarillo ≤ 30 min, rojo).

**`Habilitada` es la única fuente de verdad** de qué corre el servicio:

1. Al arrancar y cada `ConteoCamara:Api:MinutosRefrescoConfiguracion` (5) el servicio pide
   `GET api/camaras/configuracion` con `X-Clave-Ingesta` (la misma clave de la ingesta). La API devuelve las cámaras
   activas y habilitadas: `codigo`, `nombre`, `lineaId`, `lineaNombre`, `canal`, `funcion`, `magnitud`,
   `metodoDeteccion`, `cantidadCarriles`, `detectaParadas`, `creaDetenciones`, `umbralDetencionSegundos` (efectivo).
2. Corre **sólo** las de CONTEO que además tienen calibración local en `ConteoCamara:Camaras` (por `Camara`, sin
   distinguir mayúsculas). Si se deshabilita una, la detiene (manda lo pendiente); si se habilita, la arranca.
3. Una habilitada **sin calibración local** se loguea y se manda la alerta `SIN_CONFIGURACION` (una vez por arranque; la
   API no la duplica mientras siga abierta y la cierra cuando llegan minutos de esa cámara).
4. El canal sale de la API si la entrada local de la cámara no define `Fuente:RutaRtsp` ni `Fuente:Canal`.
5. Si la API no responde, sigue con la **última lista conocida** (`pendientes\camaras-configuracion.json`). Sin API ni
   lista guardada (primer arranque con la API caída) no corre ninguna y reintenta cada 30 s.

**Ingesta** (`POST api/conteo-camara/ingesta`): acepta `camara` (nuevo) o `puntoControl` (viejo: la cámara se busca por
ese código, la migración lo conservó) y en cada minuto `cantidad` (nuevo) o `rollos` (viejo). Rechaza (400) si la
cámara no existe, está inactiva, **no está habilitada** o es de calidad. Cada minuto y cada parada cuelgan del turno de
producción de su hora (`AsegurarTurno`: si no había, lo abre el SISTEMA). Actualiza `UltimoDatoRecibido`. Las
detenciones automáticas guardan `CamaraId` y usan el umbral de la cámara. Respuesta: además `minutosSinTurno`.

**Alerta `SIN_DATOS`**: el job de la API (el de turnos de producción, cada minuto) abre la alerta si la línea tiene un
turno ABIERTO y su cámara de producción oficial (habilitada) no manda minutos hace más de
`TurnosProduccion:MinutosSinDatosCamara` (5). Se cierra cuando llega un minuto. **Sin datos no es producción cero**: un
minuto con cantidad 0 es un dato y no abre la alerta.

### 11.1 Cambios en el appsettings del servidor (`C:\PSAG\ConteoCamara`)

| Antes | Ahora |
|---|---|
| `"PuntoControl": "T2-CAM10"` | `"Camara": "T2-CAM10"` (se sigue aceptando `PuntoControl`; si vienen las dos, gana `Camara`) |
| `"Activa": true/false` por cámara | **se elimina**: se habilita en Maestros → Cámaras (si queda, se ignora y se loguea) |
| — | `Api:MinutosRefrescoConfiguracion` (opcional, 5) |

El `appsettings.json` del repo trae la calibración de la 09, la 10 y la 8 sin `Activa`. En el
`appsettings.local.json` del servidor conviene reemplazar `PuntoControl` por `Camara` y borrar `Activa` (no es
obligatorio el mismo día: se leen igual). **Antes de arrancar el servicio nuevo**, dejar habilitadas en Maestros →
Cámaras sólo las cámaras que tienen que correr (las que tenían `"Activa": true`): el script 36 pone
`Habilitada` = `Activo` del punto viejo, que puede no coincidir.

### 11.2 Script 36

Ver `database/README.md`. Migra los puntos que eran cámaras a `CAM_Camaras` conservando el código, repunta conteos,
paradas, alertas y detenciones, cuelga conteos y paradas del turno de producción y recién al final borra las columnas
viejas. Rollback: `36_t798_camaras_rollback.sql`.
