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
