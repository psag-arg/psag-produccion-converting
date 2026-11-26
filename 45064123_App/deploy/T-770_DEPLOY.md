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
| 2 | `database/42_t770_tablero_gerencial.sql` | Opción `PRODUCCION.TABLERO_GERENCIAL` (sin menú), VER para ADMINISTRADOR, SUPERVISOR y GERENCIA | `42_…_rollback.sql` |

- **Operario (obligado a turno)**: después de tomar el turno cae en **Producción → Tablero del turno** (`/produccion/tablero`),
  pensado para la **tablet**: presupuesto vs. real de su turno, detenciones reales vs. microcortes, pendientes de declarar
  (se declaran en un modal), último dato de cámara. Se actualiza solo (SignalR) o cada 10 s.
- **Supervisor**: el mismo tablero eligiendo la línea, en sólo lectura.
- **Iteración 2 (láminas de Ferretti)**: arriba del tablero, los 4 indicadores en gráficos propios (SVG, sin librerías ni
  Internet): ver "Los 4 indicadores" abajo.
- **Tablero gerencial** (`/produccion/tablero/gerencial`, administrador, supervisor y gerencia): fecha + línea + turno, cualquier
  turno en curso o cerrado, sólo lectura. Todavía **no está en el menú** (lo ordena T-803): se entra por la URL o, el
  supervisor, con "Otros turnos (gerencial)" desde su tablero.
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
con la API andando.) Después el 42:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 42_t770_tablero_gerencial.sql -o "$log\42.txt"
Get-Content "$log\42.txt"
```

Tiene que decir "Opcion PRODUCCION.TABLERO_GERENCIAL creada" y listar ADMINISTRADOR, GERENCIA y SUPERVISOR con `PuedeVer` = 1.

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
5. Arriba, los 4 gráficos: eficiencia (aguja), producción (mangas y bolsones), velocidad (línea por minuto) y detenciones por
   tipo de falla con el total y los minutos pendientes (tocarlos abre las pendientes para declarar).
6. Como gerencia o administrador: abrir `/produccion/tablero/gerencial`, elegir fecha, línea y turno (● = en curso).
7. Si dice "Cada 10 s" en lugar de "En línea": la consola del navegador (F12) muestra por qué no conectó el WebSocket
   (casi siempre CORS: falta el origen en `Cors:AllowedOrigins`).

## 4. Volver atrás

Volver a publicar la API y la Web anteriores (las de `C:\PSAG\Mantenimiento\backups`) y, si se quiere sacar la opción:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 42_t770_tablero_gerencial_rollback.sql -o "$log\rollback42.txt"
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 41_t770_tablero_operador_rollback.sql -o "$log\rollback41.txt"
```

(Si queda la opción con la API vieja no pasa nada: aparece en Roles y permisos pero la Web vieja no tiene la pantalla.)

## Los 4 indicadores (láminas de Ferretti) y decisiones

1. **Eficiencia** (medidor semicircular con aguja): mangas producidas ÷ mangas objetivo, **acumulada del turno**. La aguja
   usa el objetivo **a la hora** (prorrateado por producto hasta ahora: es comparable desde el primer minuto; contra el
   objetivo del turno completo la aguja arrancaría siempre en rojo). Debajo, el objetivo del **turno completo** (11.200 del
   SH 30; con cambio de producto se prorratea por tramo) y el avance contra él. Colores del texto de Ferretti: **rojo < 65 %,
   amarillo 65–85 %, verde > 85 %** (el dibujo de la lámina tenía otros cortes). Los mismos cortes pasan a los demás colores
   del tablero (mangas, bolsones, mangas por hora); Presupuesto vs. real sigue con los suyos. Escala 0–100 % (más de 100 deja
   la aguja al tope; el número muestra el valor).
2. **Producción** (barras horizontales): mangas y bolsones acumulados contra el objetivo del turno completo ("Obj: 11.200
   mangas"), con una raya en el objetivo a la hora y el producto. Sin dato de bolsones: "Sin datos" (nunca 0 %).
3. **Velocidad** (línea): m/min de cada minuto = mangas del minuto de la cámara oficial × metros por manga del producto
   vigente en ese minuto. Paradas = 0; minutos sin datos de la cámara cortan la línea. Raya verde: 670 m/min (constante
   `VelocidadReferenciaMetrosMinuto`; si cambia por producto, pasarla al parámetro línea + producto).
4. **Detenciones** (barras por tipo de falla del maestro, todos los activos aunque estén en 0): minutos acumulados del turno,
   **total** y **minutos pendientes de declaración** (tocarlo abre las pendientes). Una pendiente tiene `MinutosEfectivos` = 0,
   así que sus minutos salen de la parada de cámara: `FechaHoraParada` → `FechaHoraArranqueDetectada` (o ahora si sigue
   parada; el fin del turno si ya cerró) y van en la barra "Sin declarar". El total suma lo que cuenta para indicadores
   (`CuentaParaIndicadores`, más las pendientes); las paradas programadas se muestran en gris y aparte ("+ N min
   programadas"). Ojo: este total es más que "Indisponibilidad" (que cuenta las pendientes en 0 hasta declararlas).
