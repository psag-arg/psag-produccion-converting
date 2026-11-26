# T-803 · Deploy en producción (menú de dashboards + presupuesto vs. real unificado)

Para Emiliano. Servidor de la app **2k22recimgProd (172.16.18.52)**; base **SQL Server** `PSAG_Mantenimiento`.
Rama `agente-psag-conv/T-803` (va **después** de T-770: necesita las opciones de los scripts 39, 41 y 42).

> Probado en SQL Server de TEST (`PSAG_Mantenimiento_TEST`, armada con los mismos scripts hasta el **43**): suite de
> integración completa en verde; el 43 corrido varias veces, rollback dos veces y vuelta a aplicar.

## 0. Qué cambia

| Orden | Archivo | Qué hace | Deshace |
|---|---|---|---|
| 1 | `database/43_t803_menu_dashboards.sql` | Sección *Dashboard* = Dashboard Admin, Presupuesto vs. real, Tablero del turno, Tablero de producción (gerencial), Horas programadas. "Inicio" sale del menú. Dashboard Admin deja de ser del operario (si nadie lo editó). | `43_…_rollback.sql` |

- No toca datos de negocio ni crea tablas: sólo `CFG_Opciones` (nombre, padre, ruta y orden de 6 opciones) y una fila de
  `CFG_PermisosRol` (operario × Dashboard Admin, queda en 0).
- **"Dashboard de Control" ya no existe**: `/` lleva al operario a su tablero y al resto a **Dashboard Admin**.
- **Presupuesto vs. real**: una sola tabla con la columna **Microcortes**; "Detalle" abre el turno en un modal.

## 1. Backup y script 43

```powershell
$app = "C:\PSAG_Converting\45064123_App"   # ajustar
$srv = "localhost\SQLEXPRESS"              # la instancia real
$f   = Get-Date -Format yyyyMMdd_HHmm
$log = "C:\Backups\T803_$f"; New-Item -ItemType Directory $log | Out-Null
sqlcmd -S $srv -E -b -Q "BACKUP DATABASE [PSAG_Mantenimiento] TO DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T803_$f.bak' WITH INIT, CHECKSUM, COPY_ONLY, STATS = 10"
cd $app\database
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 43_t803_menu_dashboards.sql -o "$log\43.txt"
Get-Content "$log\43.txt"
```

Tiene que decir "Opciones del menú actualizadas: 6", "Permiso Dashboard Admin quitado al OPERARIO: 1 fila(s)" (0 si alguien
lo había editado desde Roles y permisos) y listar las opciones de DASHBOARD en orden 1, 2, 3, 4, 5 y 9 (Inicio, sin ruta).
Se puede correr con la API andando.

## 2. Publicar API y Web

```powershell
Stop-Service PSAG_Mantenimiento_API
cd $app
.\deploy\02-Publicar-Aplicacion.ps1 -OutputPath "C:\PSAG\Mantenimiento"
```

Como siempre, el 02 regenera `Api\appsettings.Production.json`: volver a poner lo propio del servidor.
`Start-Service PSAG_Mantenimiento_API` y `/health` → `Healthy`.

## 3. Probar

1. Como administrador: entrar a `/` → abre **Dashboard Admin**. En el menú, *Dashboard*: Dashboard Admin, Presupuesto vs.
   real, Tablero de producción (gerencial), Horas programadas. No aparece "Inicio" ni "Dashboard de Control".
2. Como supervisor: lo mismo más *Tablero del turno*. Como gerencia: Dashboard Admin, Presupuesto vs. real y el gerencial.
3. Como operario: después de tomar el turno cae en su tablero; en *Dashboard* ve Presupuesto vs. real y Tablero del turno
   (no Dashboard Admin).
4. *Presupuesto vs. real*: una sola tabla; columna **Microcortes** (cantidad y minutos). Agrupando por turno, **Detalle**
   abre un modal con indicadores, producto, producción y microcortes por cámara (sin salir de la pantalla).

## 4. Volver atrás

Volver a publicar la API y la Web anteriores y después:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 43_t803_menu_dashboards_rollback.sql -o "$log\rollback43.txt"
```

(La API de T-803 vuelve a aplicar el menú nuevo al arrancar: por eso primero se vuelve a la API anterior.)
