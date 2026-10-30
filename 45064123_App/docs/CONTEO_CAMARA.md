# Conteo de producción y paradas por cámara (T-610)

Cuenta los rollos que salen de la cortadora de la **Tango 2 / Futura 2** y detecta las paradas mirando la
**cámara 09** del grabador Hikvision (NVR `172.16.9.67`). No requiere hardware nuevo ni toca el NVR: sólo lee el
video en vivo (una conexión RTSP de lectura).

La cámara **sólo informa**. No crea detenciones: las paradas detectadas se cruzan con las detenciones cargadas y la
pantalla *Calidad → Paradas y producción (cámara)* muestra las que nadie cargó.

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
API (PSAG_Mantenimiento_API) → mnt.ConteosCamaraMinuto, mnt.ParadasCamara (script 25)
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
| `PuntoControl` | `T2-CAM09` | código del punto de control (Maestros → Calidad) que define la línea |
| `Fuente:RutaRtsp` | `/Streaming/Channels/901` | canal 09 en vivo |
| `Fuente:AnchoDecodificado` × `AltoDecodificado` @ `Fps` | 640 × 480 @ 10 | lo justo para el método |
| `Analisis:FrecuenciaNominalHz` | 2.78 | velocidad normal; cambiarla si cambia el formato/velocidad |
| `Analisis:VentanasParaConfirmar` | 2 | histéresis (20 s) |
| `Analisis:SegundosMinimosParada` | 20 | paradas más cortas no se informan (quedan en los minutos) |
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
3. Web → Maestros → Calidad → Cámaras: dar de alta **`T2-CAM09`** en la línea Tango 2 (o el bloque comentado del script 25).
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
  "puntoControl": "T2-CAM09",
  "minutos": [
    { "minuto": "2026-10-30T08:16:00-03:00", "carril": 1, "rollos": 166.7, "segundosMarcha": 60,
      "segundosParada": 0, "segundosAcumulacion": 0, "segundosSinSenal": 0, "estado": "MARCHA" }
  ],
  "paradas": [
    { "identificadorExterno": "T2-CAM09-20261030T105334Z", "inicio": "2026-10-30T07:53:34-03:00",
      "fin": "2026-10-30T07:54:54-03:00", "tipo": "PARADA", "carriles": [1,2,3,4],
      "inicioIncierto": false, "finIncierto": false }
  ]
}
```

- Todo o nada: si un elemento es inválido el lote entero vuelve 400 y el servicio lo reintenta (queda en `pendientes\`).
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
- Resolución de 10 s: paradas de menos de 20 s no se informan como parada (sí quedan en los segundos de cada minuto).
- Si cambia la velocidad nominal (otro formato) ajustar `FrecuenciaNominalHz`; si no, las ventanas lentas se ven como acumulación.
- Si se apaga el servicio de golpe (no por NSSM) se pierden hasta ~30 s de datos en memoria; un minuto partido por un
  reinicio queda con lo del primer envío (el segundo se toma como repetido).
