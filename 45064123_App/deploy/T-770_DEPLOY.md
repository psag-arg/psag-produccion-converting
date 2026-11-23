# T-770 · Deploy en producción (tablero del operador en la tablet)

Para Emiliano. Servidor de la app **2k22recimgProd (172.16.18.52)**; base **SQL Server** `PSAG_Mantenimiento`.
Rama `agente-psag-conv/T-770` (va **después** de T-802: necesita las tablas `CFG_` del script 39).

> Probado en SQL Server de TEST (`PSAG_Mantenimiento_TEST`, armada con los mismos scripts: 02-08, 17, 18, 11, 12, 19-30,
> 31-38, 39, 40 y **41**): suite de integración completa en verde; el 41 corrido dos veces, rollback dos veces y vuelta a
> aplicar.

## 0. Qué cambia

| Orden | Archivo | Qué hace | Deshace |
|---|---|---|---|
| 1 | `database/41_t770_tablero_operador.sql` | Opción `PRODUCCION.TABLERO` en el catálogo de permisos, VER para OPERARIO y SUPERVISOR | `41_…_rollback.sql` |

- **Operario (obligado a turno)**: después de tomar el turno cae en **Producción → Tablero del turno** (`/produccion/tablero`),
  pensado para la **tablet**: presupuesto vs. real de su turno, detenciones reales vs. microcortes, pendientes de declarar
  (se declaran en un modal), último dato de cámara. Se actualiza solo (SignalR) o cada 10 s.
- **Supervisor**: el mismo tablero eligiendo la línea, en sólo lectura.
- La API agrega el hub **SignalR** `/hubs/tablero` (mismo puerto; no hay que tocar IIS: ver `docs/DESPLIEGUE_IIS.md`,
  "Tablero en línea").

## 1. Backup y script 41

```powershell
$app = "C:\PSAG_Converting\45064123_App"   # ajustar
$srv = "localhost\SQLEXPRESS"              # la instancia real
$f   = Get-Date -Format yyyyMMdd_HHmm
$log = "C:\Backups\T770_$f"; New-Item -ItemType Directory $log | Out-Null
sqlcmd -S $srv -E -b -Q "BACKUP DATABASE [PSAG_Mantenimiento] TO DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T770_$f.bak' WITH INIT, CHECKSUM, COPY_ONLY, STATS = 10"
cd $app\database
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 41_t770_tablero_operador.sql -o "$log\41.txt"
Get-Content "$log\41.txt"
```

Tiene que decir "Opcion PRODUCCION.TABLERO creada" y listar OPERARIO y SUPERVISOR con `PuedeVer` = 1. (El 41 se puede correr
con la API andando.)

## 2. Publicar API y Web

```powershell
Stop-Service PSAG_Mantenimiento_API
cd $app
.\deploy\02-Publicar-Aplicacion.ps1 -OutputPath "C:\PSAG\Mantenimiento"
```

Como siempre, el 02 regenera `Api\appsettings.Production.json`: volver a poner lo propio del servidor. Revisar que
`Cors:AllowedOrigins` tenga la URL con la que se abre la Web (si no, el tablero anda igual pero sólo "Cada 10 s").
`Start-Service PSAG_Mantenimiento_API` y `/health` → `Healthy`.

## 3. Probar

1. Entrar como operario en la tablet: tomar el turno → cae en el tablero. Arriba: "En línea" (verde) y
   "Último dato de cámara: HH:mm:ss" (avanza solo cada ~30 s si la cámara manda).
2. Si hay detenciones de cámara sin declarar: aviso rojo "Tenés N detenciones para declarar" → **Declarar ahora** abre el
   modal; al guardar, el bloque "Pendientes de declarar" baja solo.
3. "Ver detalle" de cada bloque abre un modal (no cambia de pantalla).
4. Entrar como supervisor: Producción → Tablero del turno, elegir la línea: sólo lectura.
5. Si dice "Cada 10 s" en lugar de "En línea": la consola del navegador (F12) muestra por qué no conectó el WebSocket
   (casi siempre CORS: falta el origen en `Cors:AllowedOrigins`).

## 4. Volver atrás

Volver a publicar la API y la Web anteriores (las de `C:\PSAG\Mantenimiento\backups`) y, si se quiere sacar la opción:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 41_t770_tablero_operador_rollback.sql -o "$log\rollback41.txt"
```

(Si queda la opción con la API vieja no pasa nada: aparece en Roles y permisos pero la Web vieja no tiene la pantalla.)

## Pendiente

- **Bloques de Ferretti (18/11)**: el adjunto no llegó; el tablero está preparado para sumar bloques (una `TarjetaKpi` más).
