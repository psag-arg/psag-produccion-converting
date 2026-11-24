# Turno automático · Deploy en producción (turno de máquina vs. sesión del operario)

Para Emiliano. Servidor de la app **2k22recimgProd (172.16.18.52)**; base **SQL Server** `PSAG_Mantenimiento`.
Rama `agente-psag-conv/turno-automatico` (va **después** de T-806 → T-805 → T-803 → T-770 → T-802 → T-798: necesita los
scripts 31 a 43 corridos).

> Probado en SQL Server de TEST (`PSAG_Mantenimiento_TEST`, armada con los mismos scripts hasta el **44**): suite de
> integración completa en verde, el 44 corrido dos veces, rollback dos veces y vuelta a aplicar. Diseño:
> `docs/diseno/T-798_modelo_produccion.md`, sección 2.6 bis. Permisos: `docs/PERMISOS.md` (Obligado a turno, punto 5).

## 0. Qué cambia

| Orden | Archivo | Qué hace | Deshace |
|---|---|---|---|
| 1 | `database/44_turno_automatico.sql` | `ABM_Turnos.ToleranciaCierreOperadorMinutos` (30), confirmación del producto heredado (`PRD_TurnosProduccionProductos.ConfirmadoPorUsuarioId`, `FechaHoraConfirmacion`, origen `HEREDADO`) y `DET_Detenciones.DetencionAnteriorId` (paradas que cruzan el cambio de turno). | `44_…_rollback.sql` |

- **El job de la API** (`TurnosProduccionJob`, cada minuto; no hay SQL Agent) ahora **abre** a la hora de inicio el turno de
  cada línea programada (sin operador, con el producto del turno anterior como supuesto) y lo **cierra a la hora exacta** de fin.
  Ya no aparecen turnos "Sin apertura".
- El operario **entra** al turno ya abierto eligiendo o confirmando el producto (obligatorio). "Cerrar turno" pasa a
  **"Terminar mi turno"**. Después del fin tiene **30 min** (Configuración → Turnos, por turno) para declarar lo suyo.
- Una parada que sigue al cambio de turno queda en **dos detenciones** (una por turno, cada una se declara aparte).
- Pendientes de cámara: el detalle, el listado y *Mis pendientes* muestran la **duración detectada** (antes "0 min").
- `appsettings`: `TurnosProduccion:ToleranciaSinAperturaMinutos` ya no se usa (se puede dejar). `ToleranciaCierreAutomaticoMinutos`
  queda sólo como margen para corregir tramos. Nuevo opcional `TurnosProduccion:DiasHerenciaProducto` (3).

## 1. Backup y script 44 (con la API detenida)

```powershell
$app = "C:\PSAG_Converting\45064123_App"   # ajustar
$srv = "localhost\SQLEXPRESS"              # la instancia real (PSAG-SQL 172.16.12.189 cuando se pase al SQL central)
$f   = Get-Date -Format yyyyMMdd_HHmm
$log = "C:\Backups\TurnoAuto_$f"; New-Item -ItemType Directory $log | Out-Null
Stop-Service PSAG_Mantenimiento_API
sqlcmd -S $srv -E -b -Q "BACKUP DATABASE [PSAG_Mantenimiento] TO DISK = N'C:\Backups\PSAG_Mantenimiento_antes_TurnoAuto_$f.bak' WITH INIT, CHECKSUM, COPY_ONLY, STATS = 10"
cd $app\database
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 44_turno_automatico.sql -o "$log\44.txt"
Get-Content "$log\44.txt"
```

Tiene que terminar con "44_turno_automatico.sql terminado." (la primera vez, con "Columna … agregada" en las 4 columnas, el
CHECK con HEREDADO y el índice; si se vuelve a correr dice "ya existía").

Verificación:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -Q "SELECT Codigo, HoraInicio, HoraFin, ToleranciaCierreOperadorMinutos FROM mnt.ABM_Turnos WHERE Activo = 1 ORDER BY Orden; SELECT COUNT(*) AS IndiceDetencionAnterior FROM sys.indexes WHERE name = N'UX_DET_Detenciones_DetencionAnteriorId'; SELECT definition FROM sys.check_constraints WHERE name = N'CK_PRD_TurnosProduccionProductos_Origen'"
```

Los turnos activos con 30, el índice = 1 y el CHECK con `HEREDADO`.

## 2. Publicar API y Web

```powershell
cd $app
.\deploy\02-Publicar-Aplicacion.ps1 -OutputPath "C:\PSAG\Mantenimiento"
```

Como siempre, el 02 regenera `Api\appsettings.Production.json`: volver a poner lo propio del servidor (y que
`TurnosProduccion:JobHabilitado` siga en `true`). `Start-Service PSAG_Mantenimiento_API` y `/health` → `Healthy`.

En el primer minuto el job abre el turno vigente de cada línea programada (y, si había un turno abierto vencido, lo cierra
a su hora). En el log de la API: "Job de turnos de producción: … abiertos automáticamente …".

## 3. Probar

1. *Producción → Turnos de producción* (supervisor): las líneas programadas tienen su turno en curso **Abierto sin operador**
   (o con el operario que ya entró), con el producto heredado marcado **supuesto** y las alertas ("Producto supuesto sin
   confirmar", "Turno sin producto", "TANGO 1 no tiene objetivos cargados" si falta cargar parámetros). Las líneas sin
   programación en ese turno no tienen turno.
2. Operario: al entrar, *Inicio de turno* le sugiere el producto (confirmar o cambiar); no puede seguir sin elegirlo (salvo
   línea sin objetivos: entra con el aviso). En la barra, **Terminar mi turno**: el turno sigue abierto sin operador.
3. A la hora de fin: el turno cierra solo (fin = hora exacta) y abre el siguiente. El operario del turno que terminó ve
   "hasta las HH:mm podés declarar sus detenciones" y sólo puede ir a *Mis pendientes* / declarar; pasada la tolerancia
   vuelve a *Inicio de turno*.
4. Si la máquina estaba parada en el cambio de turno: la detención del turno anterior termina a la hora de fin y hay otra en
   el turno nuevo ("viene del turno anterior"), que el tablero del turno nuevo muestra como parada.
5. *Configuración → Turnos*: columna y campo **Tolerancia operario** (0 a 240 min).
6. Una detención pendiente de cámara (p. ej. `/detenciones/9`): "Duración detectada por cámara: 30 min (18:30 → 19:00)";
   en el listado y en *Mis pendientes*, los minutos con "(detectados, sin declarar)".

## 4. Volver atrás

Volver a publicar la API y la Web anteriores (T-806) y después, con la API detenida:

```powershell
sqlcmd -S $srv -d PSAG_Mantenimiento -I -b -f 65001 -i 44_turno_automatico_rollback.sql -o "$log\rollback44.txt"
```

El rollback respalda los vínculos de las partes (`mnt.T44_RespaldoDetencionesPartes`) y los tramos heredados
(`mnt.T44_RespaldoTramosHeredados`), pasa los tramos `HEREDADO` a `LOGIN` (el CHECK viejo no los admite) y saca las columnas.
Las detenciones partidas quedan como detenciones sueltas (no se borra nada). La API anterior vuelve a generar SIN_APERTURA y
a cerrar con tolerancia. Último recurso: restore del backup del paso 1.
