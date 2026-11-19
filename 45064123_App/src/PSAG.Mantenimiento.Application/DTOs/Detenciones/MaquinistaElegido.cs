using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Maquinista que eligió el usuario al declarar o editar la detención (reunión 17/11/2026), ya validado por la API
/// (existe, usuario activo; ver DetencionMaquinistaReglas). Los nombres van al historial de la detención:
/// Application no conoce Identity, la API los resuelve.
/// </summary>
public sealed record MaquinistaElegido(UsuarioLinea Asignacion, string NombreNuevo, string? NombreAnterior);
