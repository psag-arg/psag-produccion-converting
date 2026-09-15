# Arquitectura - PSAG.Mantenimiento

## Visión General

Monolito modular con arquitectura de capas, diseñado para operar en red interna de planta industrial.

```
┌─────────────────────────────────────────────────────────────┐
│                    Blazor WebAssembly                       │
│                    (PC fija / Tablet)                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ HTTPS / JWT
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     ASP.NET Core API                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ Controllers │  │ Middleware  │  │ Auth (JWT/Identity) │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      Application                            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  Services   │  │    DTOs     │  │    Validators       │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                        Domain                               │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  Entities   │  │    Enums    │  │    Interfaces       │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │Repositories │  │  DbContext  │  │   Identity/Auth     │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      SQL Server                             │
│                    Esquema [mnt]                            │
└─────────────────────────────────────────────────────────────┘
```

## Capas

### Domain
- Entidades de negocio (Detencion, Linea, Equipo, etc.)
- Enumeraciones (EstadoDetencion, TipoNota, OrigenTiempo)
- Interfaces de repositorio
- Excepciones de dominio
- **Sin dependencias externas**

### Application
- DTOs de entrada y salida
- Servicios de aplicación
- Validaciones con FluentValidation
- Mapeos
- **Depende solo de Domain**

### Infrastructure
- Entity Framework Core DbContext
- Configuraciones de entidades
- Implementación de repositorios
- ASP.NET Core Identity
- Servicios de autenticación
- **Depende de Domain y Application**

### Api
- Controllers REST
- Middleware (excepciones, correlación)
- Configuración de autenticación JWT
- Swagger/OpenAPI
- **Depende de todas las capas**

### Web (Blazor)
- Componentes de UI
- Servicios cliente
- Gestión de estado
- **Consume solo la API via HTTP**

## Decisiones de Diseño

### Sin microservicios
El alcance actual no justifica la complejidad. Un monolito bien estructurado es suficiente y más simple de operar en planta.

### Sin CDN
La red de planta no tiene salida a Internet. Todo el frontend se empaqueta como archivos estáticos.

### Estados como Enums
Los estados de detención tienen lógica de transición definida. No deben ser modificables por usuarios, por eso son enums en dominio y no tablas.

### Soft Delete
Nunca se borran registros de negocio. Se usa campo `Activo` para catálogos.

## Puntos de Extensión Futuros

Documentados en [ROADMAP.md](ROADMAP.md):
- Integración con PLC para detección automática de paradas
- Generación de órdenes de trabajo desde NO OK
- Bloqueo de cierre de turno si hay detenciones sin declarar
