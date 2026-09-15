using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using System.Text.Json;

namespace PSAG.Mantenimiento.Infrastructure.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly IAuditoriaRepository _auditoriaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditoriaService> _logger;

    public AuditoriaService(
        IAuditoriaRepository auditoriaRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuditoriaService> logger)
    {
        _auditoriaRepository = auditoriaRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        string usuarioId,
        TipoAccionAuditoria accion,
        string entidad,
        string? entidadId,
        object? valoresAnteriores,
        object? valoresNuevos,
        string? descripcion = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditoria = new Auditoria
            {
                UsuarioId = usuarioId,
                FechaHora = DateTimeOffset.UtcNow,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId,
                ValoresAnteriores = valoresAnteriores != null
                    ? JsonSerializer.Serialize(valoresAnteriores)
                    : null,
                ValoresNuevos = valoresNuevos != null
                    ? JsonSerializer.Serialize(valoresNuevos)
                    : null,
                Descripcion = descripcion
            };

            await _auditoriaRepository.RegistrarAsync(auditoria, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // No lanzar excepción para no interrumpir el flujo principal
            _logger.LogError(ex, "Error al registrar auditoría para {Entidad} {EntidadId}", entidad, entidadId);
        }
    }

    public async Task<Result<PagedResult<AuditoriaDto>>> ObtenerPaginadoAsync(
        AuditoriaFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _auditoriaRepository.GetPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            filtro.UsuarioId,
            filtro.Accion,
            filtro.Entidad,
            filtro.FechaDesde,
            filtro.FechaHasta,
            cancellationToken);

        var dtos = items.Select(a => new AuditoriaDto
        {
            AuditoriaId = a.AuditoriaId,
            UsuarioId = a.UsuarioId,
            UsuarioNombre = a.UsuarioId, // TODO: Join con usuarios
            FechaHora = a.FechaHora,
            Accion = a.Accion.ToString(),
            Entidad = a.Entidad,
            EntidadId = a.EntidadId,
            Descripcion = a.Descripcion,
            DireccionIp = a.DireccionIp
        }).ToList();

        var result = new PagedResult<AuditoriaDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = filtro.Page,
            PageSize = filtro.PageSize
        };

        return Result<PagedResult<AuditoriaDto>>.Success(result);
    }
}
