# PSAG.Mantenimiento

Sistema de gestión de paradas de máquina, variables de proceso e inspecciones para las líneas de Converting de Papelera San Andrés de Giles.

## Estructura del Proyecto

```
PSAG.Mantenimiento.sln
├── src/
│   ├── PSAG.Mantenimiento.Domain        # Entidades, enums, interfaces
│   ├── PSAG.Mantenimiento.Application   # DTOs, servicios, validaciones
│   ├── PSAG.Mantenimiento.Infrastructure# EF Core, repositorios, Identity
│   ├── PSAG.Mantenimiento.Api           # ASP.NET Core Web API
│   └── PSAG.Mantenimiento.Web           # Blazor WebAssembly
├── tests/
│   ├── PSAG.Mantenimiento.Domain.Tests
│   ├── PSAG.Mantenimiento.Application.Tests
│   └── PSAG.Mantenimiento.Api.IntegrationTests
├── database/                            # Scripts SQL numerados
├── docs/                                # Documentación técnica
└── scripts/                             # Scripts de utilidad
```

## Requisitos

- .NET 9 SDK
- SQL Server 2019+
- Node.js (solo para desarrollo de Blazor)

## Inicio Rápido

### 1. Base de Datos

Ejecutar los scripts de `database/` en orden numérico contra SQL Server:

```sql
-- 01_create_database.sql
-- 02_create_schema.sql
-- ...
-- 08_seed_roles.sql
```

### 2. Configuración

Copiar `appsettings.json` a `appsettings.Development.json` y configurar:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=PSAGMantenimiento;..."
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-min-32-chars"
  }
}
```

### 3. Ejecutar

```bash
# API (puerto 5001)
dotnet run --project src/PSAG.Mantenimiento.Api

# Web (puerto 5000)
dotnet run --project src/PSAG.Mantenimiento.Web
```

### 4. Swagger

API documentada en: `https://localhost:5001/swagger`

### 5. Tests

```bash
dotnet test                                    # toda la solución; integración con SQLite en memoria
```

Los tests de integración (`tests/PSAG.Mantenimiento.Api.IntegrationTests`) corren con dos motores:

| Modo | Cómo | Para qué |
|------|------|----------|
| SQLite en memoria (por defecto) | sin variables | rápido, una base nueva por test con `EnsureCreated` |
| **SQL Server** | `PSAG_TEST_MOTOR=SQLSERVER` y `PSAG_TEST_CONNECTION` | **criterio de verde para entregar**: el motor y el esquema de producción |

```bash
# Linux (DEV): cargar la cadena sin mostrarla y correr contra la base de TEST
set -a; . /workspace/.env; set +a
PSAG_TEST_MOTOR=SQLSERVER dotnet test tests/PSAG.Mantenimiento.Api.IntegrationTests

# Windows (PowerShell)
$env:PSAG_TEST_MOTOR = "SQLSERVER"
$env:PSAG_TEST_CONNECTION = "Server=...;Database=PSAG_Mantenimiento_TEST;User Id=...;Password=...;TrustServerCertificate=True"
dotnet test tests/PSAG.Mantenimiento.Api.IntegrationTests
```

En modo SQL Server:

- `PSAG_TEST_CONNECTION` tiene que apuntar a una base cuyo nombre termine en `_TEST` o `_PRUEBA` (si no, los tests
  se niegan a correr): **la base se vacía entera**. El login necesita ser `db_owner` de esa base.
- Una vez por corrida (fixture de la colección `BaseDeDatos`, `EsquemaSqlServer.cs`) se arma el esquema con los
  **scripts de `database/`** en el orden de producción: `dev/limpiar_base_dev.sql`, 02–08,
  `dev/seed_previo_t798_maestros.sql`, 17, 18, 11, 12, 19–30 y 31–38 (no `EnsureCreated`). Se corren por lotes `GO`
  como `sqlcmd -I -b` (no hace falta sqlcmd instalado) con el `USE` cambiado por la base de TEST. Tarda unos 6 s.
  `PSAG_TEST_ESQUEMA=REUSAR` lo saltea si la base ya tiene el esquema de T-798 (para iterar; para entregar, sin esa variable).
- Antes de cada test se borran todas las tablas (en orden de FK) y se reinician las identidades: cada test arranca
  con la base vacía, como en SQLite. Los tests que usan la base corren en serie (`[Collection(ColeccionBaseDeDatos.Nombre)]`
  en cada clase nueva que use `BaseDePrueba.Crear()`).
- `EsquemaScriptsVsModeloTests` compara la base armada por los scripts con el modelo de EF (tablas, columnas,
  nulabilidad, largos y precisión de decimales).
- Diferencias de motor a tener en cuenta al escribir tests: SQLite guarda los `decimal` sin la precisión de la columna
  (SQL Server redondea, p. ej. `DECIMAL(9,1)`), no usa el esquema `mnt` en los nombres de tabla, compara textos
  distinguiendo mayúsculas (la base es `Latin1_General_CI_AS`) y tira `SqliteException` en vez de `SqlException`
  (usar `DbException`).

## Documentación

- [Arquitectura](docs/ARQUITECTURA.md)
- [Modelo de Datos](docs/MODELO_DATOS.md)
- [Reglas de Negocio](docs/REGLAS_NEGOCIO.md)
- [API](docs/API.md)
- [Seguridad](docs/SEGURIDAD.md)
- [Despliegue IIS](docs/DESPLIEGUE_IIS.md)
- [Decisiones Técnicas](docs/DECISIONES.md)

## Stack Tecnológico

- **Backend**: ASP.NET Core 9 Web API
- **Frontend**: Blazor WebAssembly
- **Base de Datos**: SQL Server + Entity Framework Core 9
- **Autenticación**: JWT Bearer + ASP.NET Core Identity
- **Logging**: Serilog

## Contexto de Negocio

El sistema digitaliza tres ejes operativos de las líneas de Converting:

1. **Detenciones de máquina**: Registro y justificación de paradas
2. **Variables de proceso**: Verificación de parámetros vs estándares por producto
3. **Inspecciones de máquina**: Checklists preventivos por equipo

Ver [CLAUDE.md](CLAUDE.md) para trampas y convenciones críticas.
