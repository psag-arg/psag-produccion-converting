# T-798 · Deploy en producción (modelo de producción: el turno)

Para Francisco / Emiliano. Servidor de la app **2k22recimgProd (172.16.18.52)**; base **SQL Server**
`PSAG_Mantenimiento`. Rama `agente-psag-conv/T-798` (etapas 1 a 7).

> **¿En qué instancia está la base? (a definir con IT antes del deploy).** Los comandos usan `-S $srv`. Los scripts de
> instalación asumían `localhost\SQLEXPRESS` (SQL en el mismo servidor de la app), pero el plan es tener los datos en el
> SQL central **PSAG-SQL 172.16.12.189**. Definir `$srv` en el paso 2 con la instancia real. Si es el 189:
> - `-E` (Windows) sólo sirve si el usuario que corre los comandos tiene permisos ahí; si no, login SQL con la clave en
>   `$env:SQLCMDPASSWORD` (nunca `-P` en la línea de comandos);
> - las rutas de `BACKUP`/`RESTORE` (`C:\Backups\...`) son del servidor SQL, no del de la app.
>
> Versión exacta de SQL Server de producción: **no está en el repo** (sólo dice "2019 o superior"). Pregunta abierta.

> **Probado en SQL Server DEV (23/11/2026)**: SQL Server 2022 (16.0, compatibilidad 160), base `PSAG_Mantenimiento_DEV`,
> intercalación `Latin1_General_CI_AS` con la del servidor distinta (`SQL_Latin1_General_CP1_CI_AS`). Base armada con los
> mismos scripts que producción (02-08, 17, 18, 11, 12, 19-30) más datos de prueba (`database/dev/`), y sobre ella:
> 31→38, verificación, rollback 38→31, de nuevo 31→38 corriendo cada script dos veces, y 31→38 otra vez completo. Todo sin
> errores después de las correcciones (intercalación de tempdb en 36 y 38, re-ejecución del 31 después del 35/36,
> índices que el rollback de 35/36 no dejaba como estaban, turno original de la excepción 05-06 en el rollback del 36).
> Igual: **correrlos primero en una copia de la base de producción** (restaurar el backup del paso 2 como
> `PSAG_Mantenimiento_PRUEBA`, cambiar el `-d` de los comandos y revisar la salida) y recién después en la real.
> En DEV los scripts se corren con un filtro que cambia `USE [PSAG_Mantenimiento]` por el nombre de la base de DEV; en
> producción van tal cual.

Duración estimada: 1 hora con la verificación. Hacerlo en un horario sin producción (o avisar: mientras dure, no
hay carga de detenciones ni conteo de cámaras).

---

## 0. Qué se instala

| Orden | Archivo | Qué hace | Deshace |
|---|---|---|---|
| 1 | `database/31_t798_renombre_por_modulo.sql` | Renombra las tablas con prefijo de módulo (`ABM_`, `PRD_`, `CAM_`, `DET_`…) | `31_…_rollback.sql` |
| 2 | `database/32_t798_maestros_productos.sql` | Productos maestro, SKU, parámetros línea + producto, `CuentaParaIndicadores`, turnos 05/13/21 | `32_…_rollback.sql` |
| 3 | `database/33_t798_datos_tango2.sql` | Datos de Tango 2: SH 30 / SH 50 y sus objetivos | `33_…_rollback.sql` |
| 4 | `database/34_t798_turnos_produccion.sql` | Tablas del turno de producción (vacías) | `34_…_rollback.sql` |
| 5 | `database/35_t798_detenciones_turno.sql` | Cuelga las detenciones del turno de producción (convierte las existentes) | `35_…_rollback.sql` |
| 6 | `database/36_t798_camaras.sql` | Cámaras unificadas (`CAM_Camaras`); conteos y paradas al turno | `36_…_rollback.sql` |
| 7 | `database/37_t798_indicadores.sql` | `PRD_ProduccionesTurno.Motivo` | `37_…_rollback.sql` |
| 8 | `database/38_t798_conversion_turnos.sql` | Cierra la conversión: registros de proceso y calidad al turno, minutos programados desde la programación | `38_…_rollback.sql` |
| — | `deploy/T-798_conteos_antes.sql` | Sólo lectura: conteos antes | — |
| — | `deploy/T-798_verificacion.sql` | Sólo lectura: verificación después | — |

Conversión de los datos que ya hay (opción B, diseño 2.11): por cada línea + turno + fecha con datos se crea un turno de
producción **CERRADO por SISTEMA, sin operador ni producto** ("a completar"); la fecha de producción es el día en que
empieza el turno (el T3 de 21 a 05 es del día de las 21). **Excepción documentada**: lo de 05:00 a 06:00 que había
quedado en el T3 por el horario viejo 21–06 pasa al T1 de ese día. Los turnos convertidos no tienen producción oficial
(la eficiencia histórica dice "sin objetivo").

---

## 1. Preparar

1. Tener en el servidor el código de la rama `agente-psag-conv/T-798` (o `main` cuando se mergee), con el mismo
   procedimiento del último deploy (`git pull` en la copia del repo, o copiar la carpeta). En los comandos de abajo,
   `$app` es la carpeta `45064123_App` de esa copia:

   ```powershell
   $app = "C:\PSAG_Converting\45064123_App"   # ajustar a donde esté el repo en el servidor
   ```

2. Paquete del servicio de cámaras (en la PC de desarrollo, como siempre):
   `.\deploy\Empaquetar-ConteoCamara.ps1 -Ffmpeg <ruta a ffmpeg.exe>`; copiar el ZIP al servidor y descomprimirlo
   (por ejemplo en `C:\Deploy\T-798\ConteoCamara`).
   (Si se usa `Empaquetar-Deploy.ps1`, el ZIP trae `deploy\` con este procedimiento y `deploy\database\` con los scripts.)

## 2. Backup (en el servidor, como Administrador)

Hacer los pasos 2 a 9 en **la misma ventana de PowerShell** (usan las variables `$app`, `$srv`, `$f` y `$log`).

1. Backup completo de la base (SSMS o `sqlcmd`):

   ```powershell
   $srv = "localhost\SQLEXPRESS"   # <-- la instancia donde está la base (ver nota arriba: ¿o PSAG-SQL 172.16.12.189?)
   $f = Get-Date -Format yyyyMMdd_HHmm
   sqlcmd -S $srv -E -b -Q "BACKUP DATABASE [PSAG_Mantenimiento] TO DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T798_$f.bak' WITH INIT, CHECKSUM, COPY_ONLY, STATS = 10"
   sqlcmd -S $srv -E -b -Q "RESTORE VERIFYONLY FROM DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T798_$f.bak' WITH CHECKSUM"
   ```

   (La carpeta `C:\Backups` tiene que existir y el usuario del servicio de SQL tiene que poder escribir ahí.)
2. Copiar las carpetas de archivos:
   - adjuntos de análisis: `C:\PSAG_Mantenimiento\Adjuntos` (o la que diga `Adjuntos:Carpeta` en
     `C:\PSAG\Mantenimiento\Api\appsettings.Production.json`; el script 02 la pone en `C:\PSAG\Mantenimiento\Adjuntos`);
   - imágenes de calidad: la de `Calidad:CarpetaImagenes`;
   - el servicio de cámaras completo `C:\PSAG\ConteoCamara`, **con** su `appsettings.local.json` y `zonas-camara.json`;
   - `C:\PSAG\Mantenimiento\Api\appsettings.Production.json` (el script 02 lo regenera).

   ```powershell
   $d = "C:\Backups\T798_$f"; New-Item -ItemType Directory $d | Out-Null
   robocopy C:\PSAG_Mantenimiento\Adjuntos "$d\Adjuntos" /E
   robocopy C:\PSAG\ConteoCamara "$d\ConteoCamara" /E /XD logs
   Copy-Item C:\PSAG\Mantenimiento\Api\appsettings.Production.json $d
   ```

## 3. Detener los servicios y sacar los conteos "antes"

```powershell
$log = "C:\Backups\T798_$f"
```

1. `Stop-Service PSAG_ConteoCamara` (primero: así no manda datos a medias).
2. `Stop-Service PSAG_Mantenimiento_API`.
3. Conteos antes (sólo lectura):

   ```powershell
   cd $app\deploy
   sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -W -i T-798_conteos_antes.sql -o C:\Backups\T798_$f\conteos_antes.txt
   ```

## 4. Scripts SQL, en orden

Desde `$app\database`. Correr **de a uno**, leer la salida y seguir sólo si terminó sin error
(`-b` corta ante el primer error; todos son idempotentes: si uno se corta, se corrige la causa y se vuelve a correr
el mismo). Guardar cada salida (`-o`) para el informe.

```powershell
cd $app\database
$log = "C:\Backups\T798_$f"
foreach ($s in "31_t798_renombre_por_modulo", "32_t798_maestros_productos", "33_t798_datos_tango2",
               "34_t798_turnos_produccion", "35_t798_detenciones_turno", "36_t798_camaras",
               "37_t798_indicadores", "38_t798_conversion_turnos") {
    Write-Host "== $s"
    sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i "$s.sql" -o "$log\$s.txt"
    Get-Content "$log\$s.txt" -Tail 15
    if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en $s: NO seguir. Revisar $log\$s.txt" -ForegroundColor Red; break }
}
```

(Equivalente a mano: `sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i <script>`.)

Qué mirar en la salida:
- **32**: imprime el horario anterior de cada turno que cambió (queda en el log).
- **35**: turnos de producción creados, detenciones colgadas y cuántas de 05:00 a 06:00 pasaron del T3 al T1.
- **36**: cámaras creadas desde los puntos de control y una tabla final con cada cámara (habilitada / oficial).
- **38**: registros de proceso y calidad colgados, turnos recalculados y cuántos quedaron fuera de programación (0 min).

## 5. Verificación de la base (antes de publicar)

```powershell
cd $app\deploy
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -W -s "|" -i T-798_verificacion.sql -o "$log\verificacion.txt"
notepad "$log\verificacion.txt"
```

Esperado (cada bloque lo dice):
1. Todas las tablas nuevas existen; ninguna vieja sigue.
2. Conteos: detenciones, conteos, paradas, alertas, registros de proceso y calidad **iguales** a `conteos_antes.txt`.
3. Detenciones / conteos / paradas / registros sin turno de producción = **0**.
4. Líneas con dos turnos ABIERTO = **ninguna**; turnos duplicados = ninguno.
5. Horarios: T1 05:00–13:00, T2 13:00–21:00, T3 21:00–05:00 (480 min cada uno).
6. Turnos convertidos: ninguno "SIN RECALCULAR".
7. Cámaras: revisar con Francisco cuáles quedan habilitadas y cuál es la oficial (una por línea y magnitud).

Si algo no da: **no publicar**; pasar la salida a Emiliano. Si hay que volver atrás, ver el punto 9.

## 6. Publicar API, Web y servicio de cámaras

1. API y Web con los scripts existentes (hacen backup de la versión anterior en `C:\PSAG\Mantenimiento\backups`):

   ```powershell
   cd $app
   .\deploy\02-Publicar-Aplicacion.ps1 -OutputPath "C:\PSAG\Mantenimiento"
   ```

   El 02 **regenera** `Api\appsettings.Production.json`: volver a poner lo que el servidor tenía distinto (comparar con
   la copia del paso 2: cadena de conexión, CORS, carpetas, etc.).
2. El servicio de cámaras se actualiza **al final** (paso 8.7): el `06-Instalar-ConteoCamara.ps1` lo reinicia solo y
   antes hay que dejar bien las cámaras habilitadas en Maestros.

## 7. Configuración

### 7.1 API: sección `TurnosProduccion`

Viene en el `appsettings.json` publicado con estos valores; sólo hace falta tocar algo si se quieren otros, y en ese
caso se agrega la sección en `appsettings.Production.json`:

```json
"TurnosProduccion": {
  "ToleranciaCierreAutomaticoMinutos": 30,
  "ToleranciaSinAperturaMinutos": 30,
  "JobHabilitado": true,
  "JobIntervaloSegundos": 60,
  "MinutosSinDatosCamara": 5,
  "CoberturaMinimaProduccion": 0.90
}
```

- Cierre automático a fin de turno + 30 min; `SIN_APERTURA` si un turno programado no se abre en 30 min.
- `CoberturaMinimaProduccion`: con menos del 90 % de minutos con datos de la cámara oficial, la producción queda
  `SIN_DATOS` (la carga el supervisor a mano).

### 7.2 Servicio de cámaras: `C:\PSAG\ConteoCamara\appsettings.local.json`

| Antes | Ahora |
|---|---|
| `"PuntoControl": "T2-CAM10"` | `"Camara": "T2-CAM10"` (se sigue aceptando `PuntoControl`) |
| `"Activa": true/false` | **se borra**: la habilitación está en Maestros → Cámaras (si queda, se ignora y se loguea) |

Ejemplo: `{ "ConteoCamara": { "Camaras": [ { "Camara": "T2-CAM09" }, { "Camara": "T2-CAM10" }, { "Camara": "T2-CAM08", "Mangas": { "Mesa": { ... } } } ] } }`
(se combinan por posición con el `appsettings.json`: conservar la calibración que ya tenía). Detalle en
`docs/CONTEO_CAMARA.md`, sección 11.

## 8. Arrancar y revisar en la Web

1. `Start-Service PSAG_Mantenimiento_API` y esperar `http://localhost:5188/health` = Healthy.
2. `iisreset` (o reciclar el sitio de la Web) y entrar con un administrador (Ctrl+F5).
3. **Maestros → Cámaras**: dejar *Habilitada* sólo las que tienen que correr (las que antes tenían `"Activa": true`; el
   36 copió el `Activo` del punto viejo, que puede no coincidir) y una sola *Producción oficial* por línea y magnitud
   (la `T2-CAM10` para MANGAS; BOLSONES sólo si la 8 ya cuenta bien).
4. **Horas programadas** → Tango 2: cargar la programación (qué turnos trabaja cada día). **Si no está cargada, todas las
   líneas toman todos los turnos como programados y aparecen `SIN_APERTURA`** cuando nadie los abre.
5. **Maestros → Productos** y **Parámetros línea + producto**: SH 30 (30 m, 28 rollos por manga, 4 por paquete) y SH 50
   (50 m, 4 por paquete) con 11.200 / 6.500 y 6.720 / 4.700 desde 01/11/2026 (los cargó el 33; revisar).
6. **Maestros → Tipos de falla**: destildar *Cuenta para indicadores* en los tipos que el cliente diga (preventivo,
   limpieza, cambio de formato…).
7. Servicio de cámaras: con el `appsettings.local.json` ya cambiado (7.2), instalar la versión nueva (conserva
   `appsettings.local.json` y las zonas, y reinicia el servicio):

   ```powershell
   cd C:\Deploy\T-798\ConteoCamara
   .\06-Instalar-ConteoCamara.ps1 -SoloActualizar
   ```

   Mirar su log: tiene que leer la configuración de la API y correr sólo las habilitadas. En Maestros → Cámaras, *Último dato* en verde a los pocos minutos.
8. Prueba con un operario: entrar, **Inicio de turno** con producto, cargar una detención, ver la barra de turno; con
   el supervisor, **Turnos de producción** del día.

## 9. Rollback

Primero volver a la versión anterior de API, Web y servicio de cámaras (la carpeta `C:\PSAG\Mantenimiento\backups\<fecha>`
que dejó el 02, y la copia de `C:\PSAG\ConteoCamara` del paso 2), con los servicios detenidos. Después, los scripts en
**orden inverso** (cada uno verifica que se corrió el anterior):

```powershell
cd $app\database
foreach ($s in "38_t798_conversion_turnos_rollback", "37_t798_indicadores_rollback", "36_t798_camaras_rollback",
               "35_t798_detenciones_turno_rollback", "34_t798_turnos_produccion_rollback", "33_t798_datos_tango2_rollback",
               "32_t798_maestros_productos_rollback", "31_t798_renombre_por_modulo_rollback") {
    sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i "$s.sql" -o "$log\$s.txt"
    if ($LASTEXITCODE -ne 0) { Write-Host "Se detuvo en $s (ver $log\$s.txt)" -ForegroundColor Red; break }
}
```

- El 34 rollback **se detiene si ya hay turnos tomados por operarios** (datos de planta): no borra nada. En ese caso,
  o se exportan y vacían a mano con el OK de Emiliano, o se va al restore.
- El 32 rollback no vuelve los horarios de turno salvo `@RestaurarTurnosAnteriores = 1`.
- **Último recurso**: restore del backup del paso 2 (se pierde todo lo cargado después del backup):

  ```powershell
  sqlcmd -S $srv -E -b -Q "ALTER DATABASE [PSAG_Mantenimiento] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [PSAG_Mantenimiento] FROM DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T798_<fecha>.bak' WITH REPLACE; ALTER DATABASE [PSAG_Mantenimiento] SET MULTI_USER;"
  ```

## 10. Al terminar

Mandar a Emiliano la carpeta `C:\Backups\T798_<fecha>` sin el `.bak` (logs de los scripts, `conteos_antes.txt` y
`verificacion.txt`).
