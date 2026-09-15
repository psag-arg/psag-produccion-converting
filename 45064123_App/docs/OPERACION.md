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

#### Sincronización de turnos
Si cambian los horarios de turno:
1. Actualizar registros en `mnt.Turnos`
2. **NO recalcular** detenciones históricas
3. Los nuevos registros usarán los nuevos horarios

#### Nuevos productos/estándares
1. ABM desde la UI (rol ADMINISTRADOR)
2. O ejecutar scripts de seed para datos masivos

## Procedimientos de Emergencia

### Rollback de despliegue
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
