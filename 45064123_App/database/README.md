# Scripts de Base de Datos - PSAG Mantenimiento

Scripts SQL para crear y configurar la base de datos del sistema de gestión de paradas de máquina.

## Orden de Ejecución

Ejecutar los scripts en orden numérico:

| # | Script | Descripción |
|---|--------|-------------|
| 01 | `01_create_database.sql` | Crea la base de datos PSAG_Mantenimiento |
| 02 | `02_create_schema.sql` | Crea el esquema [mnt] para tablas de negocio |
| 03 | `03_create_identity.sql` | Crea tablas de ASP.NET Core Identity |
| 04 | `04_create_tables.sql` | Crea tablas de negocio |
| 05 | `05_create_constraints_indexes.sql` | Crea constraints, FKs e índices |
| 06 | `06_create_views.sql` | Crea vistas para consultas comunes |
| 07 | `07_seed_catalogs.sql` | Datos iniciales de catálogos |
| 08 | `08_seed_roles.sql` | Roles del sistema y usuario admin |
| 09 | `09_seed_demo_data.sql` | **SOLO DESARROLLO** - Datos de prueba |
| 10 | `10_permissions_example.sql` | Ejemplo de configuración de permisos |
| 17 | `17_t76_ajustes.sql` | Ajustes T-76 (idempotente): eliminación lógica y análisis de detenciones, reglas de análisis, criticidad de equipos, color de tipos de falla, tiempo programado por día, estados con PENDIENTE_JUSTIFICACION y vistas sin eliminadas |
| 18 | `18_t76_tiempo_programado_historico.sql` | Horas programadas históricas de Tango 2 (marzo a agosto 2026, columna HS PROG del Excel del cliente). Idempotente; generado con `tools/generar_18_tiempo_programado.py` |
| 19 | `19_t541_t542_horas_programadas.sql` | T-541/T-542 (idempotente): diagnóstico de los turnos activos (si suman más de 24 h) con corrección puntual opcional del fin de T3 de 06:00 a 05:00 (`@CorregirFinT3 = 1`, confirmar antes con la planta), y tablas del calendario base de horas por línea (`mnt.CalendariosLinea`, `mnt.CalendariosLineaTurnos`) |
| 20 | `20_t543_t544_analisis_fallas.sql` | T-543/T-544 (idempotente): tablas `mnt.AnalisisCambios` (historial de ediciones de análisis cerrados: quién, cuándo, valor anterior y nuevo) y `mnt.AnalisisAdjuntos` (fotos JPG/PNG del análisis, hasta 10 MB c/u y 5 por análisis). Los archivos van a disco, a la carpeta `Adjuntos:Carpeta` del appsettings de la API (en producción, `C:\PSAG\Mantenimiento\Adjuntos`, con permiso de escritura para el usuario del app pool) |
| 21 | `21_t545_control_de_proceso.sql` | T-545 Control de Proceso (ex Registros), idempotente: rango mín/máx y frecuencia en `Parametros`, tipo `OK_NOOK`, `RegistrosProceso.ProductoId` opcional, corrige `Turno 1 (Manana)` → `Turno 1 (Mañana)`. Trae comentado el bloque para cargar los puntos de control de una línea cuando llegue la planilla del cliente |
| 22 | `22_t546_t547_tareas_mantenimiento.sql` | Tareas de mantenimiento T-546/T-547 (idempotente): tabla `mnt.TareasMantenimiento` con estados PENDIENTE → PROGRAMADA → CERRADA, checks, FKs a líneas y equipos e índices |
| 23 | `23_t611_calidad_defectos.sql` | T-611 Control de Calidad por cámaras (idempotente): tablas `mnt.PuntosControlCalidad` (cámaras por línea), `mnt.TiposDefectoCalidad`, `mnt.RegistrosCalidad` (cada detección con imagen, confianza, dato medido, turno, producto y detención opcionales, revisión PENDIENTE / CONFIRMADO / FALSO_POSITIVO) y `mnt.RegistrosCalidadRevisiones` (historial de quién revisó y cuándo). **No carga datos**: los maestros arrancan vacíos y se cargan desde Maestros → Calidad (o con el bloque comentado al final del script) cuando llegue la ficha de criterios. Las imágenes van a `Calidad:CarpetaImagenes` (en producción `C:\PSAG\Mantenimiento\ImagenesCalidad`). Ver `docs/CALIDAD.md` |
| 24 | `24_t545_plantilla_control_proceso_tango2.sql` | T-545 plantilla de Control de Proceso de TANGO 2 desde la planilla del cliente "Control Proceso Tango 2 (30mts)" (idempotente, identifica por código y solo inserta lo que falta): producto `HIGIENICO 30 MTS` asociado a la línea (buscada por Nombre), 6 grupos `T2-G01`..`T2-G06`, 57 puntos POR_TURNO (56 numéricos + Observaciones de texto) y 54 estándares con el STD de la hoja. Rango `a - b` → mín/máx; `> 10mm` → mínimo 10 inclusivo; valor único → solo texto de referencia (no marca desvío); `-` → sin estándar. Acentos con `NCHAR`; correr con `sqlcmd -I -b -f 65001`. Requiere 11 y 21 |
| 25 | `25_t610_conteo_camara.sql` | T-610 Conteo de producción y paradas por cámara (idempotente): `mnt.ConteosCamaraMinuto` (rollos por minuto y carril, segundos en marcha / parada / acumulación / sin señal, único por punto+minuto+carril) y `mnt.ParadasCamara` (inicio, fin, duración, tipo PARADA/ACUMULACION, carriles, único por identificador del servicio). Insert-only. La cámara es un punto de control de T-611 (requiere 23). **No carga datos** (bloque comentado para dar de alta `T2-CAM09`). Ver `docs/CONTEO_CAMARA.md` |
| 26 | `26_t610_detencion_automatica_camara.sql` | T-610 Detención automática por cámara (idempotente): cámara asociada en `mnt.PuntosControlCalidad` (cuerpo, número de cámara, crea detenciones, umbral en minutos = 5); en `mnt.Detenciones` origen MANUAL/CAMARA, `IdentificadorParadaCamara` único (idempotencia), cámara y arranque detectado (sugerencia), y `UsuarioLineaId` si faltaba; tipo `SIN_MANGAS` en `mnt.ParadasCamara`; tabla `mnt.AlertasCamara` (congelada / sin video). Requiere 17, 23 y 25. **No carga datos** (bloque comentado para `T2-CAM10`). Ver `docs/CONTEO_CAMARA.md` |
| 27 | `27_inspecciones_empaquetadora_tmc_tango2.sql` | Inspecciones con el formato de Control de Proceso (reunión 03/11/2026 [78:17]) y plan de la EMPAQUETADORA TMC de TANGO 2 (planilla de Sebastián Ferretti), idempotente: crea las tablas de 12 si faltan; agrega `ItemsPlanInspeccion.Codigo/ParteEquipo/MetodoDetalle/QuienRealiza/FrecuenciaDias` (las filas viejas toman responsable y frecuencia de su plan), `EjecucionesInspeccion.Puesto/UsuarioCierreId` y `EjecucionesInspeccionDetalle.EstandarSnapshot`, con CHECKs e índice; carga el plan `INS-T2-EMP-TMC` con 13 puntos `T2-EMP-01..13` (11 diarios de OP. PACKAGING, 2 cada 10 días de MECÁNICO), asociado al equipo de la línea cuyo nombre contiene "EMPAQUETADORA" si hay uno solo. Solo inserta lo que falta (por código). Acentos con `NCHAR`; correr con `sqlcmd -I -b -f 65001` |
| 28 | `28_eficiencia_objetivo_camara.sql` | Eficiencia por turno en el Dashboard (reunión 03/11, idempotente): en `mnt.PuntosControlCalidad` agrega `ObjetivoPorHora` (DECIMAL(10,2) NULL, unidades por hora en marcha plena; objetivo del turno = objetivo por hora x horas programadas del turno) y `MetodoConteo` (NULL, `CARRILES` = rollos / `MANGAS`). Requiere 23, 25 y 26. **No carga valores** (bloque comentado para el método de `T2-CAM09`/`T2-CAM10`; el objetivo lo pasa Emiliano y se carga en Maestros → Calidad) |
| 29 | `29_snapshot_rango_control_proceso.sql` | Auditoría Control de Proceso (idempotente): en `mnt.RegistrosProcesoDetalle` agrega `EstandarConRango` (BIT NULL), snapshot de si el estándar contra el que se comparó el valor ese día tenía rango. Un valor cargado sin estándar sigue mostrando "Falta estándar" aunque después se cargue uno. Las filas existentes quedan en NULL (la app decide por `ValorEstandarSnapshot`). Requiere 11 y 21. **Correr antes de publicar la API** |
| 30 | `30_umbral_detencion_segundos.sql` | Umbral de detención de la cámara en segundos (reunión 10/11, idempotente): en `mnt.PuntosControlCalidad` agrega `SegundosUmbralDetencion` (INT NULL, CHECK NULL o 30..14400). Con valor manda sobre `MinutosUmbralDetencion` (para probar en planta con un umbral corto); vacío = se usan los minutos como antes. Requiere 23 y 26. **No carga valores** (se carga en Maestros → Calidad). **Correr antes de publicar la API** |
| 99 | `99_drop_objects_development_only.sql` | **SOLO DESARROLLO** - Limpieza total |

> **Producción (22/10/2026):** la base de producción se armó sin los scripts 11 y 12, así que no tenía las tablas de Registros/Control de Proceso ni de Inspecciones. Se crearon en el deploy de T-539 junto con 19 a 22. Al instalar en otro servidor, correr 11 y 12 antes que 21.

## Requisitos

- SQL Server 2019 o superior
- Permisos de administrador para crear la base de datos

## Ejecución Rápida

```powershell
# Desde SQL Server Management Studio o sqlcmd
sqlcmd -S localhost -E -i 01_create_database.sql
sqlcmd -S localhost -E -i 02_create_schema.sql
# ... continuar con cada script
```

## Notas Importantes

1. **Script 09**: Solo para desarrollo/pruebas. NO ejecutar en producción.
2. **Script 99**: Elimina TODOS los objetos. Solo para reiniciar en desarrollo.
3. **Usuario admin**: La contraseña debe establecerse desde la aplicación.
4. Los scripts son idempotentes (pueden re-ejecutarse sin error).

## Estructura de la Base de Datos

### Esquema [dbo] - ASP.NET Identity
- `AspNetUsers` - Usuarios (con campos personalizados)
- `AspNetRoles` - Roles del sistema
- `AspNetUserRoles` - Asignación usuario-rol
- `AspNetUserClaims` - Claims de usuario
- `AspNetRoleClaims` - Claims de rol (permisos)
- `AspNetUserLogins` - Logins externos
- `AspNetUserTokens` - Tokens de usuario

### Esquema [mnt] - Negocio
- `Lineas` - Líneas de producción (6 en Converting)
- `Maquinas` - Máquinas por línea
- `TiposEvento` - Catálogo de tipos de parada
- `Causas` - Catálogo de causas
- `Eventos` - Eventos de parada (tabla principal)
- `EventosEstadoHistorial` - Historial de cambios de estado
- `EventosNotas` - Notas de seguimiento
- `UsuariosLineas` - Asignación de usuarios a líneas
- `Auditoria` - Registro de auditoría

### Vistas
- `VW_EventosDetalle` - Vista completa de eventos
- `VW_EventosAbiertos` - Eventos no cerrados
- `VW_EventosIndicadores` - Indicadores agregados
- `VW_LineasConMaquinas` - Líneas con sus máquinas
- `VW_UsuariosConLineas` - Usuarios con líneas asignadas
