-- ==============================================================================
-- Script: 09_seed_demo_data.sql
-- Descripción: Datos de demostración para desarrollo/pruebas
-- ADVERTENCIA: NO ejecutar en producción
-- ==============================================================================

USE [PSAG_Mantenimiento]
GO

PRINT '=============================================='
PRINT 'ADVERTENCIA: Script de datos de demostración'
PRINT 'NO ejecutar en ambiente de producción'
PRINT '=============================================='
GO

-- ============================================================================
-- USUARIOS DE PRUEBA
-- ============================================================================
PRINT 'Creando usuarios de prueba...'

-- Obtener IDs de roles
DECLARE @AdminRoleId NVARCHAR(450)
DECLARE @GerenciaRoleId NVARCHAR(450)
DECLARE @MantenimientoRoleId NVARCHAR(450)
DECLARE @SupervisorRoleId NVARCHAR(450)
DECLARE @OperarioRoleId NVARCHAR(450)

SELECT @AdminRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'ADMINISTRADOR'
SELECT @GerenciaRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'GERENCIA'
SELECT @MantenimientoRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'MANTENIMIENTO'
SELECT @SupervisorRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'SUPERVISOR'
SELECT @OperarioRoleId = [Id] FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'OPERARIO'

-- Usuario Gerencia
DECLARE @GerenciaUserId NVARCHAR(450) = 'g1a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = N'GERENTE')
BEGIN
    INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [NombreCompleto], [Activo], [RequiereCambioPassword])
    VALUES (@GerenciaUserId, N'gerente', N'GERENTE', N'gerente@psag.local', N'GERENTE@PSAG.LOCAL', 1, NULL, NEWID(), NEWID(), 0, 0, 1, 0, N'Juan Pérez (Gerencia)', 1, 1)
    INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId]) VALUES (@GerenciaUserId, @GerenciaRoleId)
    PRINT '  - Usuario gerente creado'
END

-- Usuario Mantenimiento
DECLARE @MantUserId NVARCHAR(450) = 'm1a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = N'MANTENIMIENTO1')
BEGIN
    INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [NombreCompleto], [Activo], [RequiereCambioPassword])
    VALUES (@MantUserId, N'mantenimiento1', N'MANTENIMIENTO1', N'mantenimiento1@psag.local', N'MANTENIMIENTO1@PSAG.LOCAL', 1, NULL, NEWID(), NEWID(), 0, 0, 1, 0, N'Carlos García (Mantenimiento)', 1, 1)
    INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId]) VALUES (@MantUserId, @MantenimientoRoleId)
    PRINT '  - Usuario mantenimiento1 creado'
END

-- Usuario Supervisor
DECLARE @SupUserId NVARCHAR(450) = 's1a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = N'SUPERVISOR1')
BEGIN
    INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [NombreCompleto], [Activo], [RequiereCambioPassword])
    VALUES (@SupUserId, N'supervisor1', N'SUPERVISOR1', N'supervisor1@psag.local', N'SUPERVISOR1@PSAG.LOCAL', 1, NULL, NEWID(), NEWID(), 0, 0, 1, 0, N'María López (Supervisor L1-L3)', 1, 1)
    INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId]) VALUES (@SupUserId, @SupervisorRoleId)
    -- Asignar líneas 1, 2 y 3 al supervisor
    INSERT INTO [mnt].[UsuariosLineas] ([UsuarioId], [LineaId], [AsignadoPorUsuarioId])
    SELECT @SupUserId, [LineaId], 'SYSTEM-SEED' FROM [mnt].[Lineas] WHERE [Nombre] IN (N'Línea 1', N'Línea 2', N'Línea 3')
    PRINT '  - Usuario supervisor1 creado y asignado a líneas 1-3'
END

-- Usuario Operario 1
DECLARE @Op1UserId NVARCHAR(450) = 'o1a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = N'OPERARIO1')
BEGIN
    INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [NombreCompleto], [Activo], [RequiereCambioPassword])
    VALUES (@Op1UserId, N'operario1', N'OPERARIO1', N'operario1@psag.local', N'OPERARIO1@PSAG.LOCAL', 1, NULL, NEWID(), NEWID(), 0, 0, 1, 0, N'Pedro Rodríguez (Operario L1)', 1, 1)
    INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId]) VALUES (@Op1UserId, @OperarioRoleId)
    -- Asignar línea 1 al operario
    INSERT INTO [mnt].[UsuariosLineas] ([UsuarioId], [LineaId], [AsignadoPorUsuarioId])
    SELECT @Op1UserId, [LineaId], 'SYSTEM-SEED' FROM [mnt].[Lineas] WHERE [Nombre] = N'Línea 1'
    PRINT '  - Usuario operario1 creado y asignado a línea 1'
END

-- Usuario Operario 2
DECLARE @Op2UserId NVARCHAR(450) = 'o2a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = N'OPERARIO2')
BEGIN
    INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [NombreCompleto], [Activo], [RequiereCambioPassword])
    VALUES (@Op2UserId, N'operario2', N'OPERARIO2', N'operario2@psag.local', N'OPERARIO2@PSAG.LOCAL', 1, NULL, NEWID(), NEWID(), 0, 0, 1, 0, N'Ana Martínez (Operario L2)', 1, 1)
    INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId]) VALUES (@Op2UserId, @OperarioRoleId)
    -- Asignar línea 2 al operario
    INSERT INTO [mnt].[UsuariosLineas] ([UsuarioId], [LineaId], [AsignadoPorUsuarioId])
    SELECT @Op2UserId, [LineaId], 'SYSTEM-SEED' FROM [mnt].[Lineas] WHERE [Nombre] = N'Línea 2'
    PRINT '  - Usuario operario2 creado y asignado a línea 2'
END

PRINT 'Usuarios de prueba creados.'
GO

-- ============================================================================
-- EVENTOS DE DEMOSTRACIÓN
-- ============================================================================
PRINT 'Creando eventos de demostración...'

DECLARE @AdminUserId NVARCHAR(450) = 'b5a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
DECLARE @Op1UserId NVARCHAR(450) = 'o1a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'
DECLARE @MantUserId NVARCHAR(450) = 'm1a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'

DECLARE @Linea1Id INT, @Linea2Id INT, @Linea3Id INT
DECLARE @TipoFallaMec INT, @TipoFallaElec INT, @TipoCambioProd INT
DECLARE @CausaDesgaste INT, @CausaRotura INT
DECLARE @Maquina1L1 INT, @Maquina2L1 INT

SELECT @Linea1Id = [LineaId] FROM [mnt].[Lineas] WHERE [Nombre] = N'Línea 1'
SELECT @Linea2Id = [LineaId] FROM [mnt].[Lineas] WHERE [Nombre] = N'Línea 2'
SELECT @Linea3Id = [LineaId] FROM [mnt].[Lineas] WHERE [Nombre] = N'Línea 3'

SELECT @TipoFallaMec = [TipoEventoId] FROM [mnt].[TiposEvento] WHERE [Nombre] = N'Falla Mecánica'
SELECT @TipoFallaElec = [TipoEventoId] FROM [mnt].[TiposEvento] WHERE [Nombre] = N'Falla Eléctrica'
SELECT @TipoCambioProd = [TipoEventoId] FROM [mnt].[TiposEvento] WHERE [Nombre] = N'Cambio de Producto'

SELECT @CausaDesgaste = [CausaId] FROM [mnt].[Causas] WHERE [Nombre] = N'Desgaste de componente'
SELECT @CausaRotura = [CausaId] FROM [mnt].[Causas] WHERE [Nombre] = N'Rotura de pieza'

SELECT TOP 1 @Maquina1L1 = [MaquinaId] FROM [mnt].[Maquinas] WHERE [LineaId] = @Linea1Id ORDER BY [Orden]
SELECT TOP 1 @Maquina2L1 = [MaquinaId] FROM [mnt].[Maquinas] WHERE [LineaId] = @Linea1Id AND [MaquinaId] != @Maquina1L1 ORDER BY [Orden]

-- Evento 1: CERRADO (hace 3 días)
IF NOT EXISTS (SELECT 1 FROM [mnt].[Eventos] WHERE [Codigo] = N'EVT-2024-000001')
BEGIN
    DECLARE @Evt1Fecha DATETIMEOFFSET = DATEADD(DAY, -3, SYSDATETIMEOFFSET())
    DECLARE @Evt1FechaCierre DATETIMEOFFSET = DATEADD(HOUR, 2, @Evt1Fecha)

    INSERT INTO [mnt].[Eventos] (
        [Codigo], [LineaId], [MaquinaId], [TipoEventoId], [FechaHoraOcurrencia], [DescripcionInicial],
        [EstadoActual], [CausaId], [CausaDetalle], [Resolucion], [FechaHoraCierre], [CerradoPorUsuarioId],
        [CreadoPorUsuarioId], [ModificadoPorUsuarioId]
    )
    VALUES (
        N'EVT-2024-000001', @Linea1Id, @Maquina1L1, @TipoFallaMec, @Evt1Fecha,
        N'Se detectó vibración excesiva en el rodamiento principal del desenrollador.',
        'CERRADO', @CausaDesgaste, N'Rodamiento con desgaste avanzado por falta de lubricación periódica.',
        N'Se reemplazó el rodamiento y se lubricaron todos los puntos de la máquina. Se programó mantenimiento preventivo mensual.',
        @Evt1FechaCierre, @MantUserId, @Op1UserId, @MantUserId
    )

    DECLARE @Evt1Id BIGINT = SCOPE_IDENTITY()

    -- Historial de estados
    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt1Id, NULL, 'INICIADO', @Evt1Fecha, @Op1UserId, N'Evento registrado')

    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt1Id, 'INICIADO', 'EN_CURSO', DATEADD(MINUTE, 15, @Evt1Fecha), @MantUserId, N'Mantenimiento toma el evento')

    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt1Id, 'EN_CURSO', 'CERRADO', @Evt1FechaCierre, @MantUserId, N'Problema resuelto')

    -- Notas
    INSERT INTO [mnt].[EventosNotas] ([EventoId], [Texto], [UsuarioId], [FechaHora], [TipoNota])
    VALUES (@Evt1Id, N'La vibración se intensifica a altas velocidades.', @Op1UserId, DATEADD(MINUTE, 5, @Evt1Fecha), 'Operativa')

    INSERT INTO [mnt].[EventosNotas] ([EventoId], [Texto], [UsuarioId], [FechaHora], [TipoNota])
    VALUES (@Evt1Id, N'Se confirma desgaste del rodamiento 6205-2RS. Procedo a reemplazo.', @MantUserId, DATEADD(MINUTE, 30, @Evt1Fecha), 'Tecnica')

    INSERT INTO [mnt].[EventosNotas] ([EventoId], [Texto], [UsuarioId], [FechaHora], [TipoNota], [EsNotaDeCierre])
    VALUES (@Evt1Id, N'Rodamiento reemplazado. Máquina probada a velocidad nominal sin problemas.', @MantUserId, @Evt1FechaCierre, 'Cierre', 1)

    PRINT '  - Evento EVT-2024-000001 creado (CERRADO)'
END

-- Evento 2: EN_CURSO (hace 1 día)
IF NOT EXISTS (SELECT 1 FROM [mnt].[Eventos] WHERE [Codigo] = N'EVT-2024-000002')
BEGIN
    DECLARE @Evt2Fecha DATETIMEOFFSET = DATEADD(DAY, -1, SYSDATETIMEOFFSET())

    INSERT INTO [mnt].[Eventos] (
        [Codigo], [LineaId], [MaquinaId], [TipoEventoId], [FechaHoraOcurrencia], [DescripcionInicial],
        [EstadoActual], [CreadoPorUsuarioId], [ModificadoPorUsuarioId]
    )
    VALUES (
        N'EVT-2024-000002', @Linea1Id, @Maquina2L1, @TipoFallaElec, @Evt2Fecha,
        N'Motor de la impresora presenta arranques intermitentes y se escucha un zumbido anormal.',
        'EN_CURSO', @Op1UserId, @MantUserId
    )

    DECLARE @Evt2Id BIGINT = SCOPE_IDENTITY()

    -- Historial de estados
    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt2Id, NULL, 'INICIADO', @Evt2Fecha, @Op1UserId, N'Evento registrado')

    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt2Id, 'INICIADO', 'EN_CURSO', DATEADD(HOUR, 1, @Evt2Fecha), @MantUserId, N'Mantenimiento en evaluación')

    -- Notas
    INSERT INTO [mnt].[EventosNotas] ([EventoId], [Texto], [UsuarioId], [FechaHora], [TipoNota])
    VALUES (@Evt2Id, N'Se revisó el variador de frecuencia, presenta código de error F003.', @MantUserId, DATEADD(HOUR, 2, @Evt2Fecha), 'Tecnica')

    INSERT INTO [mnt].[EventosNotas] ([EventoId], [Texto], [UsuarioId], [FechaHora], [TipoNota])
    VALUES (@Evt2Id, N'Se solicitó repuesto del contactor principal. ETA: mañana.', @MantUserId, DATEADD(HOUR, 3, @Evt2Fecha), 'Tecnica')

    PRINT '  - Evento EVT-2024-000002 creado (EN_CURSO)'
END

-- Evento 3: INICIADO (hace 2 horas)
IF NOT EXISTS (SELECT 1 FROM [mnt].[Eventos] WHERE [Codigo] = N'EVT-2024-000003')
BEGIN
    DECLARE @Evt3Fecha DATETIMEOFFSET = DATEADD(HOUR, -2, SYSDATETIMEOFFSET())

    INSERT INTO [mnt].[Eventos] (
        [Codigo], [LineaId], [TipoEventoId], [FechaHoraOcurrencia], [DescripcionInicial],
        [EstadoActual], [CreadoPorUsuarioId], [ModificadoPorUsuarioId]
    )
    VALUES (
        N'EVT-2024-000003', @Linea2Id, @TipoCambioProd, @Evt3Fecha,
        N'Parada programada para cambio de bobina y ajuste de formato.',
        'INICIADO', @AdminUserId, @AdminUserId
    )

    DECLARE @Evt3Id BIGINT = SCOPE_IDENTITY()

    -- Historial de estados
    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt3Id, NULL, 'INICIADO', @Evt3Fecha, @AdminUserId, N'Cambio de producto iniciado')

    PRINT '  - Evento EVT-2024-000003 creado (INICIADO)'
END

-- Evento 4: CERRADO (hace 1 semana)
IF NOT EXISTS (SELECT 1 FROM [mnt].[Eventos] WHERE [Codigo] = N'EVT-2024-000004')
BEGIN
    DECLARE @Evt4Fecha DATETIMEOFFSET = DATEADD(DAY, -7, SYSDATETIMEOFFSET())
    DECLARE @Evt4FechaCierre DATETIMEOFFSET = DATEADD(HOUR, 4, @Evt4Fecha)

    INSERT INTO [mnt].[Eventos] (
        [Codigo], [LineaId], [TipoEventoId], [FechaHoraOcurrencia], [DescripcionInicial],
        [EstadoActual], [CausaId], [Resolucion], [FechaHoraCierre], [CerradoPorUsuarioId],
        [CreadoPorUsuarioId], [ModificadoPorUsuarioId]
    )
    VALUES (
        N'EVT-2024-000004', @Linea3Id, @TipoFallaMec, @Evt4Fecha,
        N'Faja transportadora patina y no avanza correctamente.',
        'CERRADO', @CausaRotura, N'Se reemplazó la faja y se ajustó la tensión. Funcionamiento normal.',
        @Evt4FechaCierre, @MantUserId, @Op1UserId, @MantUserId
    )

    DECLARE @Evt4Id BIGINT = SCOPE_IDENTITY()

    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt4Id, NULL, 'INICIADO', @Evt4Fecha, @Op1UserId, N'Evento registrado')

    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt4Id, 'INICIADO', 'EN_CURSO', DATEADD(MINUTE, 20, @Evt4Fecha), @MantUserId, N'Diagnóstico iniciado')

    INSERT INTO [mnt].[EventosEstadoHistorial] ([EventoId], [EstadoAnterior], [EstadoNuevo], [FechaHora], [UsuarioId], [Observacion])
    VALUES (@Evt4Id, 'EN_CURSO', 'CERRADO', @Evt4FechaCierre, @MantUserId, N'Faja reemplazada')

    PRINT '  - Evento EVT-2024-000004 creado (CERRADO)'
END

PRINT 'Eventos de demostración creados.'
GO

-- ============================================================================
-- REGISTROS DE AUDITORÍA DE EJEMPLO
-- ============================================================================
PRINT 'Creando registros de auditoría de ejemplo...'

DECLARE @AdminUserId NVARCHAR(450) = 'b5a7c9d1-e3f2-4a5b-8c6d-7e9f0a1b2c3d'

INSERT INTO [mnt].[Auditoria] ([UsuarioId], [FechaHora], [Accion], [Entidad], [EntidadId], [Descripcion], [DireccionIp])
VALUES (@AdminUserId, DATEADD(DAY, -7, SYSDATETIMEOFFSET()), 'LOGIN', 'Usuario', @AdminUserId, 'Inicio de sesión exitoso', '192.168.1.100')

INSERT INTO [mnt].[Auditoria] ([UsuarioId], [FechaHora], [Accion], [Entidad], [EntidadId], [Descripcion], [DireccionIp])
VALUES (@AdminUserId, DATEADD(DAY, -3, SYSDATETIMEOFFSET()), 'CREATE', 'Evento', '1', 'Nuevo evento de parada registrado', '192.168.1.100')

INSERT INTO [mnt].[Auditoria] ([UsuarioId], [FechaHora], [Accion], [Entidad], [EntidadId], [Descripcion], [DireccionIp])
VALUES (@AdminUserId, DATEADD(DAY, -1, SYSDATETIMEOFFSET()), 'UPDATE', 'Evento', '1', 'Estado actualizado a CERRADO', '192.168.1.100')

PRINT 'Registros de auditoría creados.'
GO

PRINT '=============================================='
PRINT 'Datos de demostración creados exitosamente.'
PRINT '=============================================='
GO
