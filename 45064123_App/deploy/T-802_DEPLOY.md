# T-802 · Deploy en producción (roles y permisos configurables + obligado a turno)

Para Emiliano. Servidor de la app **2k22recimgProd (172.16.18.52)**; base **SQL Server** `PSAG_Mantenimiento`.
Rama `agente-psag-conv/T-802` (parte 1: roles y permisos; parte 2: obligado a turno y alta de usuario en un solo circuito).

> **Va DESPUÉS del deploy de T-798** (`deploy/T-798_DEPLOY.md`): necesita las tablas con prefijo (31), el turno de
> producción (34) y la API de T-798 funcionando. Si T-798 todavía no se instaló, hacer primero ese procedimiento completo.

> Probado en SQL Server de TEST (`PSAG_Mantenimiento_TEST`, armada con los mismos scripts que producción: 02-08, 17, 18,
> 11, 12, 19-30, 31-38, 39 y 40): suite de integración completa en verde, 39 y 40 corridos dos veces, rollback del 40 dos
> veces y vuelta a aplicar. Igual: **correrlos primero en una copia** (`PSAG_Mantenimiento_PRUEBA`, restaurando el backup
> del paso 2) y recién después en la real.

Duración estimada: 30 minutos con la verificación. Hacerlo sin producción o avisando: mientras dure no hay carga.

---

## 0. Qué se instala y qué cambia para la gente

| Orden | Archivo | Qué hace | Deshace |
|---|---|---|---|
| 1 | `database/39_t802_roles_permisos.sql` | Tablas `CFG_Opciones`, `CFG_PermisosRol`, `CFG_RolesConfiguracion` y la **matriz migrada** (lo mismo que hacía el código) | `39_…_rollback.sql` |
| 2 | `database/40_t802_obligado_turno.sql` | Saca `ABM_OperadoresLinea.TurnoHabitualId` (la respalda en `mnt.T802_RespaldoTurnoHabitual`), índice único "un operador vigente por turno", Maquinistas sólo consulta | `40_…_rollback.sql` |

Cambios visibles (detalle en `docs/PERMISOS.md`):

- **Configuración → Roles y permisos**: matriz rol × opción × Ver / Alta / Modificación / Baja y el tilde "Obligado a
  declarar turno" (OPERARIO).
- **Operarios (obligados a turno)**: al entrar **tienen que abrir o tomar el turno** de una de sus líneas (no hay "seguir
  sin tomar"); mientras lo tienen abierto ven y cargan sólo lo de ese turno (línea, turno y fecha). Al cerrar el turno
  vuelven a la pantalla de inicio de turno. Un operario no puede tener dos turnos abiertos a la vez.
- **Configuración → Usuarios**: el alta lleva rol + **líneas habilitadas** (obligatorias si el rol es obligado a turno).
  Ya no hay "turno habitual".
- **Configuración → Maquinistas**: queda **sólo como consulta** (las líneas se cargan desde Usuarios).

## 1. Preparar

```powershell
$app = "C:\PSAG_Converting\45064123_App"   # ajustar a donde esté el repo en el servidor (rama agente-psag-conv/T-802 o main)
$srv = "localhost\SQLEXPRESS"              # la instancia real (ver la nota de T-798: ¿o PSAG-SQL 172.16.12.189?)
$f   = Get-Date -Format yyyyMMdd_HHmm
$log = "C:\Backups\T802_$f"; New-Item -ItemType Directory $log | Out-Null
```

Hacer todo en **la misma ventana de PowerShell** (como Administrador).

## 2. Backup

```powershell
sqlcmd -S $srv -E -b -Q "BACKUP DATABASE [PSAG_Mantenimiento] TO DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T802_$f.bak' WITH INIT, CHECKSUM, COPY_ONLY, STATS = 10"
sqlcmd -S $srv -E -b -Q "RESTORE VERIFYONLY FROM DISK = N'C:\Backups\PSAG_Mantenimiento_antes_T802_$f.bak' WITH CHECKSUM"
Copy-Item C:\PSAG\Mantenimiento\Api\appsettings.Production.json $log
```

Antes de tocar nada, **anotar las habilitaciones actuales** (lo que hoy se ve en Maquinistas), para revisarlas después en
Usuarios:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -W -s "|" -o "$log\habilitaciones_antes.txt" -Q "SELECT u.UserName, l.Nombre AS Linea, t.Codigo AS TurnoHabitual FROM mnt.ABM_OperadoresLinea ol JOIN dbo.AspNetUsers u ON u.Id = ol.UsuarioId JOIN mnt.ABM_Lineas l ON l.LineaId = ol.LineaId LEFT JOIN mnt.ABM_Turnos t ON t.TurnoId = ol.TurnoHabitualId ORDER BY u.UserName, l.Nombre"
```

## 3. Detener la API

```powershell
Stop-Service PSAG_Mantenimiento_API
```

(El servicio de cámaras puede seguir: si la API está caída reintenta. Si se prefiere, `Stop-Service PSAG_ConteoCamara`
antes y `Start-Service` al final.)

## 4. Scripts 39 y 40 (con la API detenida)

```powershell
cd $app\database
foreach ($s in "39_t802_roles_permisos", "40_t802_obligado_turno") {
    Write-Host "== $s"
    sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i "$s.sql" -o "$log\$s.txt"
    Get-Content "$log\$s.txt" -Tail 15
    if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en $s: NO seguir. Revisar $log\$s.txt" -ForegroundColor Red; break }
}
```

Qué mirar en la salida:
- **39**: tablas creadas y opciones / permisos cargados.
- **40**: "Turno habitual respaldado", "Columna … TurnoHabitualId eliminada", "Indice UX_PRD_TurnosProduccionOperadores_Vigente
  creado". Si dice **AVISO: … más de un operador vigente**, el índice NO se creó: anotar los turnos de la verificación 4
  de abajo, corregir el operador desde Producción → Turnos de producción (o pasarle la salida a Emiliano) y volver a correr
  el 40. El resto del 40 se aplica igual.

## 5. Verificación de la base (antes de publicar)

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -W -s "|" -o "$log\verificacion.txt" -Q "
SELECT '1. tablas CFG' AS chequeo, COUNT(*) AS valor FROM sys.tables WHERE name LIKE 'CFG[_]%';                 -- 3
SELECT '2. opciones' AS chequeo, COUNT(*) AS valor FROM mnt.CFG_Opciones;                                     -- > 40
SELECT '3. obligados a turno' AS chequeo, r.Name FROM mnt.CFG_RolesConfiguracion c JOIN dbo.AspNetRoles r ON r.Id = c.RoleId WHERE c.ObligadoATurno = 1;  -- OPERARIO
SELECT '4. turnos con 2 operadores vigentes' AS chequeo, TurnoProduccionId, COUNT(*) AS n FROM mnt.PRD_TurnosProduccionOperadores WHERE Hasta IS NULL AND AnuladoPorId IS NULL GROUP BY TurnoProduccionId HAVING COUNT(*) > 1;  -- ninguno
SELECT '5. columna TurnoHabitualId' AS chequeo, COL_LENGTH('mnt.ABM_OperadoresLinea', 'TurnoHabitualId') AS valor;  -- NULL
SELECT '6. indice vigente' AS chequeo, COUNT(*) AS valor FROM sys.indexes WHERE name = 'UX_PRD_TurnosProduccionOperadores_Vigente';  -- 1
SELECT '7. respaldo turno habitual' AS chequeo, COUNT(*) AS valor FROM mnt.T802_RespaldoTurnoHabitual;      -- = filas de habilitaciones_antes.txt
SELECT '8. maquinistas' AS chequeo, Nombre, AccionesAplicables FROM mnt.CFG_Opciones WHERE Codigo = 'CONFIG.MAQUINISTAS';  -- Maquinistas (consulta) | VER
SELECT '9. operarios sin lineas' AS chequeo, u.UserName FROM dbo.AspNetUsers u JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
  WHERE r.Name = 'OPERARIO' AND u.Activo = 1 AND NOT EXISTS (SELECT 1 FROM mnt.ABM_OperadoresLinea ol WHERE ol.UsuarioId = u.Id);  -- ninguno (si hay: no van a poder abrir turno)
"
notepad "$log\verificacion.txt"
```

Si algo no da: **no publicar**; pasar la salida a Emiliano (o volver atrás, punto 8).

## 6. Publicar API y Web

```powershell
cd $app
.\deploy\02-Publicar-Aplicacion.ps1 -OutputPath "C:\PSAG\Mantenimiento"
```

El 02 **regenera** `Api\appsettings.Production.json`: volver a poner lo que el servidor tenía distinto (comparar con la
copia del paso 2). Después `Start-Service PSAG_Mantenimiento_API` (si el 02 no la levantó) y `Invoke-WebRequest
http://localhost:<puerto>/health` → `Healthy`.

## 7. Revisión en la aplicación (como admin)

1. **Configuración → Roles y permisos**: revisar la matriz migrada rol por rol (tiene que ser lo que cada uno hacía antes;
   la tabla está en `docs/PERMISOS.md`). OPERARIO con "Obligado a declarar turno" tildado.
2. **Configuración → Usuarios**: para cada operario, revisar las **líneas habilitadas** (antes se veían en Maquinistas;
   comparar con `habilitaciones_antes.txt`). El turno habitual ya no existe: no hay nada que pasar.
3. **Configuración → Maquinistas**: sólo consulta (sin botones de alta / editar / borrar).
4. Entrar como un operario de prueba (o con uno real en su turno):
   - después del login aparece **Inicio de turno** sin menú ni "seguir sin tomar"; tomar el turno de su línea;
   - Detenciones, Mis pendientes y la barra muestran sólo su línea / turno / fecha; la carga tiene la línea fija;
   - cerrar el turno desde la barra → vuelve a Inicio de turno.
5. Entrar como supervisor: no pide turno y ve todo como antes.

## 8. Volver atrás

Con la API detenida, **primero** volver a publicar la API y la Web anteriores (las que dejó el 02 en
`C:\PSAG\Mantenimiento\backups`), después:

```powershell
cd $app\database
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 40_t802_obligado_turno_rollback.sql -o "$log\rollback40.txt"
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 39_t802_roles_permisos_rollback.sql -o "$log\rollback39.txt"
```

- El **40 rollback** saca el índice, vuelve a crear `TurnoHabitualId` con su FK y le devuelve los valores respaldados
  (habilitaciones creadas después quedan sin turno habitual), y Maquinistas vuelve a alta / edición / baja.
- El **39 rollback** borra las tablas `CFG_` (lo configurado en Roles y permisos se pierde; queda en `mnt.Auditoria`).
  Sólo si se vuelve a la API de T-798; con la de T-802 parte 1, correr sólo el rollback del 40.
- Si algo salió mal en serio: restaurar el backup del paso 2.
