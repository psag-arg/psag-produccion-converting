# Operación - PSAG.Mantenimiento

## Monitoreo

### Health Check
```
GET /health
```
Verifica conexión a base de datos.

### Logs
- Ubicación: `logs/` en carpeta de la aplicación
- Formato: Serilog JSON
- Rotación: diaria
- Incluye: CorrelationId, UserId, Path, tiempos de respuesta

### Métricas clave
- Tiempo de respuesta de endpoints
- Errores 4xx/5xx
- Conexiones activas a BD

## Tareas de Mantenimiento

### Base de Datos

#### Índices
Revisar fragmentación mensualmente:
```sql
SELECT
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 30
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

#### Estadísticas
```sql
EXEC sp_updatestats;
```

#### Backups
- Full: diario
- Log: cada hora
- Retención: según política PSAG

### Logs

#### Limpieza
```powershell
# Eliminar logs > 30 días
Get-ChildItem -Path "C:\inetpub\wwwroot\PSAGMantenimiento\api\logs" -Recurse |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item
```

### Catálogos

#### Horarios de turno (T-798)
Los turnos son un ABM global (`mnt.ABM_Turnos`, Configuración → Turnos): T1 05–13, T2 13–21, T3 21–05.
Si cambian los horarios:
1. Cambiarlos desde la Web (no a mano en la base).
2. **NO recalcular** nada histórico: cada detención, conteo, parada y registro cuelga de su turno de producción
   (`mnt.PRD_TurnosProduccion`), que guarda el inicio, el fin y los minutos programados de ese día (snapshot).
3. Los turnos que se abran después usan los horarios nuevos.

#### Turnos de producción (T-798)
- El operario abre el turno al entrar (con el producto); el supervisor lo corrige en **Turnos de producción**.
- Cierre automático a `FinProgramado` + `TurnosProduccion:ToleranciaCierreAutomaticoMinutos` (30) y `SIN_APERTURA`
  si un turno programado no se abrió en `ToleranciaSinAperturaMinutos` (30): lo hace el job de la API
  (`TurnosProduccion:JobHabilitado`, cada `JobIntervaloSegundos`).
- Qué turnos están programados sale de **Horas programadas** (programación de cada línea: día de la semana × turno). Sin programación cargada, la API
  toma todos los turnos activos como programados (y marca `SIN_APERTURA` en todos los que nadie abra).
- La producción oficial de cada tramo de producto la valida, corrige o carga a mano el supervisor
  (`PENDIENTE` / `VALIDADA` / `SIN_DATOS`). Sin datos no es producción cero.
- Los turnos que vienen de la conversión del deploy de T-798 son `CERRADO` por `SISTEMA`, sin operador ni producto
  ("a completar"): su eficiencia dice "sin objetivo".

#### Nuevos productos/estándares
1. ABM desde la UI (rol ADMINISTRADOR)
2. O ejecutar scripts de seed para datos masivos

## Procedimientos de Emergencia

### Rollback de despliegue
> Para el deploy de T-798 (cambio de esquema): seguir `deploy/T-798_DEPLOY.md` (scripts `*_rollback.sql` en orden
> inverso y, como último recurso, restore del backup). Lo de abajo vale sólo para volver binarios sin cambio de base.

```powershell
# Mantener carpeta backup del despliegue anterior
# Para rollback:
Stop-WebSite -Name "PSAGMantenimientoApi"
robocopy C:\backup\api C:\inetpub\wwwroot\PSAGMantenimiento\api /MIR
Start-WebSite -Name "PSAGMantenimientoApi"
```

### Conexión perdida a BD
1. Verificar conectividad de red
2. Verificar estado de SQL Server
3. Revisar logs de la aplicación
4. Health check retornará error

### Usuario bloqueado
```sql
-- Desbloquear usuario
UPDATE AspNetUsers
SET LockoutEnd = NULL, AccessFailedCount = 0
WHERE UserName = 'usuario';
```

### Reset de password (ADMINISTRADOR)
Desde la UI o:
```sql
-- Generar hash de password temporal
-- (mejor usar la API o UI)
```

## Contactos

- **IT PSAG**: Leonardo
- **Desarrollo**: Pillow
- **Operaciones**: Sebastián Ferretti (líneas de Converting)

## Checklist de Operación Diaria

- [ ] Verificar health check
- [ ] Revisar logs de errores
- [ ] Verificar backups completados
- [ ] Revisar panel de NO OK (si hay pendientes críticos)

## Declarar una detención manual (corte de energía) — T-786

Ante un corte de energía las cámaras y el sistema se reinician y la detención no queda registrada. El supervisor
(también mantenimiento o administrador) la declara en **Detenciones → Declarar detención manual**
(`/detenciones/declarar-manual`):

1. Máquina, fecha, hora **desde** y **hasta** (si hasta es menor, termina al día siguiente). El turno sale de la hora
   desde y se muestra antes de guardar.
2. Tipo de falla (obligatorio), detalle y equipo (opcionales), maquinista (opcional: si no se elige, el del turno) y
   **motivo** (obligatorio; botones rápidos "Corte de energía", "Reinicio del sistema", "Falla de cámara").
3. Si ya hay otra detención de esa máquina en ese horario (por ejemplo, la que creó la cámara al volver) se avisa con
   el código: conviene completar esa en lugar de cargar otra. El aviso no bloquea.

La detención queda **REGISTRADA**, con `OrigenRegistro = MANUAL` (las de la cámara son `CAMARA`), minutos calculados
con desde/hasta, y cuenta en los reportes como cualquier otra. Quién la cargó y cuándo se ven en el detalle
("Registrado": fecha y "Cargada por …"). El listado de detenciones muestra la insignia **Manual** / **Cámara** y filtra
por **Origen**; el Excel exportado trae la columna *Origen*.
