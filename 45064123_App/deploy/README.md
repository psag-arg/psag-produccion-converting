# PSAG.Mantenimiento - Guía de Instalación en Producción

## Resumen

Sistema de control de paradas y variables de proceso para líneas de Converting.

### Arquitectura de Deploy

```
[Browser] → [IIS :80] → Blazor WASM (static files)
                ↓
[API Service :5188] → [SQL Server Express]
```

### Componentes

| Componente | Tecnología | Puerto | Ubicación |
|------------|------------|--------|-----------|
| Web Frontend | Blazor WASM | 80 (IIS) | C:\PSAG\Mantenimiento\Web |
| API Backend | .NET 9 | 5188 | C:\PSAG\Mantenimiento\Api |
| Base de Datos | SQL Express | 1433 | localhost\SQLEXPRESS |

---

## Requisitos del Servidor

- **OS**: Windows Server 2022 (verificado en 2K22RECIMG)
- **RAM**: 2 GB libres mínimo (recomendado 4 GB)
- **Disco**: 1 GB para aplicación + espacio para logs
- **Base de Datos**: SQLite (incluida) o SQL Server Express 2019+

---

## Instalación Paso a Paso

### PASO 1: En la máquina de DESARROLLO

```powershell
# 1. Abrir PowerShell como Administrador en la carpeta del proyecto

# 2. Publicar la aplicación
.\deploy\02-Publicar-Aplicacion.ps1

# 3. Se crea la carpeta C:\PSAG\Mantenimiento con:
#    - Api\     (API compilada)
#    - Web\     (Blazor WASM static files)
#    - logs\    (carpeta para logs)
```

### PASO 2: Copiar al servidor

```powershell
# Copiar toda la carpeta C:\PSAG al servidor 2K22RECIMG
# Puede ser via red, USB, o comprimiendo como ZIP

# También copiar la carpeta deploy\ con los scripts
```

### PASO 3: En el SERVIDOR (2K22RECIMG)

```powershell
# Abrir PowerShell como Administrador

# 1. Instalar prerrequisitos (IIS, .NET 9 Runtime)
.\01-Instalar-Prerrequisitos.ps1

# 2. Configurar IIS (sitio web para Blazor)
.\03-Configurar-IIS.ps1 -InstallPath "C:\PSAG_Mantenimiento"

# 3. Crear Windows Service para API
.\04-Crear-Servicio-API.ps1 -InstallPath "C:\PSAG_Mantenimiento"

# 4. Configurar base de datos (SQLite por defecto)
.\05-Configurar-BaseDatos.ps1 -InstallPath "C:\PSAG_Mantenimiento"

# O para SQL Server:
# .\05-Configurar-BaseDatos.ps1 -SqlInstance "localhost\SQLEXPRESS" -UseSqlServer
```

**Opcion Rapida (todo en uno con SQLite):**
```powershell
.\00-Instalacion-Completa.ps1 -InstallPath "C:\PSAG_Mantenimiento"
```

---

## Configuración Post-Instalación

### 1. Ajustar appsettings.Production.json (API)

Editar `C:\PSAG\Mantenimiento\Api\appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=PSAG_Mantenimiento;..."
  },
  "JwtSettings": {
    "SecretKey": "CAMBIAR_ESTA_CLAVE_EN_PRODUCCION_MIN_32_CARACTERES!"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://2K22RECIMG",
      "http://2K22RECIMG.psag.local",
      "http://IP_DEL_SERVIDOR"
    ]
  }
}
```

### 2. Ajustar appsettings.json (Web)

Editar `C:\PSAG\Mantenimiento\Web\appsettings.json`:

```json
{
  "ApiBaseUrl": "http://2K22RECIMG:5188/"
}
```

### 3. Reiniciar servicios

```powershell
Restart-Service PSAG_Mantenimiento_API
iisreset
```

---

## Verificación

### Verificar API

```powershell
# Estado del servicio
Get-Service PSAG_Mantenimiento_API

# Verificar respuesta
Invoke-WebRequest http://localhost:5188/swagger -UseBasicParsing
```

### Verificar Web

Abrir en navegador: `http://2K22RECIMG/`

### Verificar Logs

```powershell
# Logs del API
Get-Content C:\PSAG\Mantenimiento\logs\api-stdout.log -Tail 50

# Logs de aplicación
Get-Content C:\PSAG\Mantenimiento\logs\api-*.log -Tail 50
```

---

## Usuarios Iniciales

El script de base de datos crea estos usuarios de prueba:

| Usuario | Contraseña | Rol |
|---------|------------|-----|
| admin@psag.local | Admin123! | ADMINISTRADOR |
| supervisor@psag.local | Super123! | SUPERVISOR |
| operario@psag.local | Oper123! | OPERARIO |

**IMPORTANTE**: Cambiar contraseñas en producción.

---

## Comandos Útiles

```powershell
# === SERVICIO API ===
Start-Service PSAG_Mantenimiento_API      # Iniciar
Stop-Service PSAG_Mantenimiento_API       # Detener
Restart-Service PSAG_Mantenimiento_API    # Reiniciar
Get-Service PSAG_Mantenimiento_API        # Estado

# === IIS ===
iisreset                                   # Reiniciar IIS
Get-Website PSAG_Mantenimiento            # Estado sitio
Start-Website PSAG_Mantenimiento          # Iniciar sitio
Stop-Website PSAG_Mantenimiento           # Detener sitio

# === LOGS ===
Get-Content C:\PSAG\Mantenimiento\logs\api-stdout.log -Tail 100 -Wait

# === BASE DE DATOS ===
sqlcmd -S localhost\SQLEXPRESS -d PSAG_Mantenimiento -Q "SELECT COUNT(*) FROM mnt.Detenciones"
```

---

## Troubleshooting

### El API no inicia

1. Verificar logs: `C:\PSAG\Mantenimiento\logs\api-stderr.log`
2. Verificar conexión SQL:
   ```powershell
   sqlcmd -S localhost\SQLEXPRESS -Q "SELECT 1"
   ```
3. Verificar puerto disponible:
   ```powershell
   netstat -an | findstr 5188
   ```

### Error de CORS

Agregar la URL del cliente en `appsettings.Production.json`:
```json
"Cors": {
  "AllowedOrigins": ["http://IP_O_NOMBRE_SERVIDOR"]
}
```

### La Web no carga (Blazor)

1. Verificar IIS corriendo: `Get-Service W3SVC`
2. Verificar sitio: `Get-Website PSAG_Mantenimiento`
3. Verificar ApiBaseUrl en `Web\appsettings.json`
4. Limpiar cache del navegador (Ctrl+Shift+R)

### Error de conexión a SQL

1. Verificar servicio SQL:
   ```powershell
   Get-Service 'MSSQL$SQLEXPRESS'
   ```
2. Verificar connection string en `appsettings.Production.json`
3. Verificar que la base de datos existe:
   ```powershell
   sqlcmd -S localhost\SQLEXPRESS -Q "SELECT name FROM sys.databases"
   ```

---

## Estructura de Archivos

```
C:\PSAG\Mantenimiento\
├── Api\
│   ├── PSAG.Mantenimiento.Api.exe
│   ├── PSAG.Mantenimiento.Api.dll
│   ├── appsettings.json
│   ├── appsettings.Production.json    ← CONFIGURAR
│   └── ...
├── Web\
│   ├── index.html
│   ├── appsettings.json               ← CONFIGURAR
│   ├── web.config
│   ├── _framework\
│   └── ...
├── logs\
│   ├── api-stdout.log
│   ├── api-stderr.log
│   └── api-YYYYMMDD.log
├── tools\
│   └── nssm.exe
└── backups\
    └── YYYYMMDD_HHMMSS\
```

---

## Actualizaciones

Para actualizar la aplicación:

```powershell
# 1. En desarrollo, publicar nueva versión
.\deploy\02-Publicar-Aplicacion.ps1

# 2. Copiar C:\PSAG\Mantenimiento\Api y Web al servidor
#    (el script hace backup automático)

# 3. En el servidor, reiniciar
Restart-Service PSAG_Mantenimiento_API
iisreset
```

---

## Contacto

Sistema desarrollado para PSAG (Papelera San Andrés de Giles).
