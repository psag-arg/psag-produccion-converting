<#
.SYNOPSIS
    Configura la base de datos para PSAG.Mantenimiento
.DESCRIPTION
    Soporta dos modos:
    - SQLite (por defecto): Solo crea directorios, la app inicializa la BD
    - SQL Server: Ejecuta scripts SQL en orden
.NOTES
    Ejecutar en el servidor como Administrador
#>

#Requires -RunAsAdministrator

param(
    [string]$SqlInstance = "",
    [string]$DatabaseName = "PSAG_Mantenimiento",
    [string]$InstallPath = "C:\PSAG_Mantenimiento",
    [switch]$UseSqlServer = $false,
    [switch]$IncludeDemoData = $false,
    [switch]$DropExisting = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PSAG.Mantenimiento - Base de Datos" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# DETECTAR MODO (SQLite vs SQL Server)
# ============================================

if (-not $UseSqlServer -and [string]::IsNullOrWhiteSpace($SqlInstance)) {
    # Modo SQLite (por defecto)
    Write-Host "Modo: SQLite (base de datos embebida)" -ForegroundColor Yellow
    Write-Host ""

    # Crear directorios necesarios
    $dataPath = Join-Path $InstallPath "data"
    $logsPath = Join-Path $InstallPath "logs"

    Write-Host "[1/2] Creando directorios..." -ForegroundColor Yellow

    if (-not (Test-Path $dataPath)) {
        New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
        Write-Host "  Creado: $dataPath" -ForegroundColor Green
    } else {
        Write-Host "  Existe: $dataPath" -ForegroundColor Gray
    }

    if (-not (Test-Path $logsPath)) {
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        Write-Host "  Creado: $logsPath" -ForegroundColor Green
    } else {
        Write-Host "  Existe: $logsPath" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "[2/2] Configurando appsettings.Production.json..." -ForegroundColor Yellow

    $apiSettingsPath = Join-Path $InstallPath "Api\appsettings.Production.json"
    $dbFilePath = Join-Path $dataPath "mantenimiento.db"

    if (Test-Path $apiSettingsPath) {
        # Leer y actualizar el archivo
        $settings = Get-Content $apiSettingsPath -Raw | ConvertFrom-Json

        # Actualizar connection string para SQLite
        $settings.ConnectionStrings.DefaultConnection = "Data Source=$dbFilePath"

        # Guardar
        $settings | ConvertTo-Json -Depth 10 | Set-Content $apiSettingsPath -Encoding UTF8

        Write-Host "  Actualizado: $apiSettingsPath" -ForegroundColor Green
    } else {
        Write-Host "  ADVERTENCIA: No se encontro $apiSettingsPath" -ForegroundColor Yellow
        Write-Host "  La aplicacion usara la configuracion por defecto" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  CONFIGURACION SQLite COMPLETADA" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "La base de datos se creara automaticamente" -ForegroundColor White
    Write-Host "al iniciar la aplicacion por primera vez." -ForegroundColor White
    Write-Host ""
    Write-Host "Ubicacion: $dbFilePath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usuario inicial:" -ForegroundColor Yellow
    Write-Host "  Email: admin@psag.local" -ForegroundColor Gray
    Write-Host "  Password: Admin123!" -ForegroundColor Gray
    Write-Host ""
    Write-Host "IMPORTANTE: Cambiar la contrasena en el primer acceso" -ForegroundColor Yellow
    Write-Host ""

    exit 0
}

# ============================================
# MODO SQL SERVER
# ============================================

if ([string]::IsNullOrWhiteSpace($SqlInstance)) {
    $SqlInstance = "localhost\SQLEXPRESS"
}

Write-Host "Modo: SQL Server" -ForegroundColor Yellow
Write-Host "Servidor: $SqlInstance" -ForegroundColor Gray
Write-Host "Base de datos: $DatabaseName" -ForegroundColor Gray
Write-Host ""

# Obtener ruta de scripts SQL
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$databasePath = Join-Path $projectRoot "database"

if (-not (Test-Path $databasePath)) {
    # Intentar en la ubicacion de deploy
    $databasePath = Join-Path $scriptPath "database"
}

if (-not (Test-Path $databasePath)) {
    throw "No se encontro la carpeta de scripts SQL: $databasePath"
}

Write-Host "Scripts SQL: $databasePath" -ForegroundColor Gray
Write-Host ""

# ============================================
# FUNCION PARA EJECUTAR SQL
# ============================================

function Invoke-SqlScript {
    param(
        [string]$Server,
        [string]$Database,
        [string]$ScriptPath,
        [string]$Description
    )

    Write-Host "  Ejecutando: $Description" -ForegroundColor Gray

    $content = Get-Content $ScriptPath -Raw

    # Reemplazar nombre de base de datos si es necesario
    $content = $content -replace 'PSAG_Mantenimiento', $Database

    try {
        # Usar sqlcmd si esta disponible
        $sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue

        if ($sqlcmdPath) {
            # Guardar contenido temporal
            $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
            $content | Out-File -FilePath $tempFile -Encoding UTF8

            $result = & sqlcmd -S $Server -d $Database -i $tempFile -b 2>&1

            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue

            if ($LASTEXITCODE -ne 0) {
                throw "Error ejecutando script: $result"
            }
        } else {
            # Usar Invoke-Sqlcmd si esta disponible (modulo SqlServer)
            $sqlModule = Get-Module -ListAvailable -Name SqlServer

            if ($sqlModule) {
                Import-Module SqlServer -ErrorAction SilentlyContinue
                Invoke-Sqlcmd -ServerInstance $Server -Database $Database -Query $content -ErrorAction Stop
            } else {
                throw "No se encontro sqlcmd ni el modulo SqlServer. Instalar SQL Server Management Tools."
            }
        }

        Write-Host "    OK" -ForegroundColor Green
    } catch {
        Write-Host "    ERROR: $_" -ForegroundColor Red
        throw
    }
}

# ============================================
# 1. VERIFICAR CONEXION SQL
# ============================================

Write-Host "[1/4] Verificando conexion a SQL Server..." -ForegroundColor Yellow

try {
    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue

    if ($sqlcmd) {
        $testResult = & sqlcmd -S $SqlInstance -Q "SELECT 1" -h -1 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "No se puede conectar a $SqlInstance"
        }
        Write-Host "  Conexion exitosa via sqlcmd" -ForegroundColor Green
    } else {
        Write-Host "  ADVERTENCIA: sqlcmd no encontrado" -ForegroundColor Yellow
        Write-Host "  Intentando con modulo SqlServer..." -ForegroundColor Gray

        $sqlModule = Get-Module -ListAvailable -Name SqlServer
        if (-not $sqlModule) {
            Write-Host ""
            Write-Host "Para ejecutar este script necesitas:" -ForegroundColor Red
            Write-Host "  1. SQL Server Management Studio (incluye sqlcmd)" -ForegroundColor Gray
            Write-Host "  2. O instalar modulo: Install-Module SqlServer" -ForegroundColor Gray
            Write-Host ""
            throw "Herramientas SQL no encontradas"
        }

        Import-Module SqlServer
        Invoke-Sqlcmd -ServerInstance $SqlInstance -Query "SELECT 1" -ErrorAction Stop | Out-Null
        Write-Host "  Conexion exitosa via SqlServer module" -ForegroundColor Green
    }
} catch {
    Write-Host ""
    Write-Host "ERROR: No se puede conectar a SQL Server" -ForegroundColor Red
    Write-Host "Verificar que:" -ForegroundColor Yellow
    Write-Host "  1. SQL Server Express esta corriendo" -ForegroundColor Gray
    Write-Host "  2. El nombre de instancia es correcto: $SqlInstance" -ForegroundColor Gray
    Write-Host "  3. Autenticacion Windows esta habilitada" -ForegroundColor Gray
    Write-Host ""
    throw
}

# ============================================
# 2. VERIFICAR/CREAR BASE DE DATOS
# ============================================

Write-Host ""
Write-Host "[2/4] Verificando base de datos..." -ForegroundColor Yellow

# Verificar si existe
$dbExists = & sqlcmd -S $SqlInstance -Q "SELECT name FROM sys.databases WHERE name = '$DatabaseName'" -h -1 2>$null

if ($dbExists -match $DatabaseName) {
    if ($DropExisting) {
        Write-Host "  Base de datos existe. Eliminando (DropExisting=`$true)..." -ForegroundColor Yellow
        & sqlcmd -S $SqlInstance -Q "ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DatabaseName]" 2>$null
        Write-Host "  Base de datos eliminada" -ForegroundColor DarkGray
        $dbExists = $null
    } else {
        Write-Host "  Base de datos '$DatabaseName' ya existe" -ForegroundColor Green
        Write-Host ""
        Write-Host "  ADVERTENCIA: Los scripts se ejecutaran sobre la BD existente." -ForegroundColor Yellow
        Write-Host "  Usar -DropExisting para recrear desde cero." -ForegroundColor Yellow
        Write-Host ""

        $continue = Read-Host "  Continuar? (S/N)"
        if ($continue -ne "S" -and $continue -ne "s") {
            Write-Host "Operacion cancelada" -ForegroundColor Yellow
            exit 0
        }
    }
}

if (-not ($dbExists -match $DatabaseName)) {
    Write-Host "  Creando base de datos..." -ForegroundColor Gray
    Invoke-SqlScript -Server $SqlInstance -Database "master" -ScriptPath (Join-Path $databasePath "01_create_database.sql") -Description "Crear base de datos"
}

# ============================================
# 3. EJECUTAR SCRIPTS DE ESTRUCTURA
# ============================================

Write-Host ""
Write-Host "[3/4] Creando estructura de base de datos..." -ForegroundColor Yellow

$structureScripts = @(
    @{ File = "02_create_schema.sql"; Desc = "Crear esquema [mnt]" },
    @{ File = "03_create_identity.sql"; Desc = "Crear tablas Identity" },
    @{ File = "04_create_tables.sql"; Desc = "Crear tablas principales" },
    @{ File = "05_create_constraints_indexes.sql"; Desc = "Crear constraints e indices" },
    @{ File = "06_create_views.sql"; Desc = "Crear vistas" },
    @{ File = "11_create_tables_proceso.sql"; Desc = "Crear tablas de proceso" },
    @{ File = "12_create_tables_inspeccion.sql"; Desc = "Crear tablas de inspeccion" }
)

foreach ($script in $structureScripts) {
    $scriptFile = Join-Path $databasePath $script.File
    if (Test-Path $scriptFile) {
        Invoke-SqlScript -Server $SqlInstance -Database $DatabaseName -ScriptPath $scriptFile -Description $script.Desc
    } else {
        Write-Host "  Omitido (no existe): $($script.File)" -ForegroundColor DarkGray
    }
}

# ============================================
# 4. CARGAR DATOS INICIALES
# ============================================

Write-Host ""
Write-Host "[4/4] Cargando datos iniciales..." -ForegroundColor Yellow

$seedScripts = @(
    @{ File = "07_seed_catalogs.sql"; Desc = "Cargar catalogos" },
    @{ File = "08_seed_roles.sql"; Desc = "Cargar roles y permisos" }
)

foreach ($script in $seedScripts) {
    $scriptFile = Join-Path $databasePath $script.File
    if (Test-Path $scriptFile) {
        Invoke-SqlScript -Server $SqlInstance -Database $DatabaseName -ScriptPath $scriptFile -Description $script.Desc
    }
}

# Datos de demo (opcional)
if ($IncludeDemoData) {
    Write-Host ""
    Write-Host "Cargando datos de demostración..." -ForegroundColor Yellow

    $demoScripts = @(
        @{ File = "09_seed_demo_data.sql"; Desc = "Datos demo detenciones" },
        @{ File = "15_seed_proceso_demo.sql"; Desc = "Datos demo proceso" },
        @{ File = "16_seed_inspeccion_demo.sql"; Desc = "Datos demo inspeccion" }
    )

    foreach ($script in $demoScripts) {
        $scriptFile = Join-Path $databasePath $script.File
        if (Test-Path $scriptFile) {
            Invoke-SqlScript -Server $SqlInstance -Database $DatabaseName -ScriptPath $scriptFile -Description $script.Desc
        }
    }
}

# ============================================
# RESUMEN
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  BASE DE DATOS CONFIGURADA" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Base de datos: $DatabaseName" -ForegroundColor White
Write-Host "Servidor: $SqlInstance" -ForegroundColor Gray
Write-Host ""
Write-Host "Connection String para appsettings.Production.json:" -ForegroundColor Yellow
Write-Host ""
Write-Host "Server=$SqlInstance;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" -ForegroundColor Cyan
Write-Host ""

# Verificar tablas creadas
Write-Host "Tablas creadas:" -ForegroundColor Gray
$tables = & sqlcmd -S $SqlInstance -d $DatabaseName -Q "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME" -h -1 -W 2>$null
$tables | Where-Object { $_ -and $_.Trim() } | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

Write-Host ""
Write-Host "IMPORTANTE: Crear usuario administrador inicial" -ForegroundColor Yellow
Write-Host "  El primer usuario se crea via API /api/auth/register" -ForegroundColor Gray
Write-Host "  o ejecutando el script 08_seed_roles.sql que incluye usuarios de prueba" -ForegroundColor Gray
Write-Host ""
