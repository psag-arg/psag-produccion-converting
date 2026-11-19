using Microsoft.AspNetCore.Identity;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Maquinista elegido al cargar, declarar o editar una detención (reunión 17/11/2026). La regla es de
/// <see cref="DetencionMaquinistaReglas"/>; acá se resuelve lo que necesita Identity (usuario activo y nombres).
/// </summary>
public static class MaquinistaDetencion
{
    /// <summary>
    /// Valida la asignación elegida contra la línea de la detención: existe, es de esa línea y el usuario existe y
    /// está activo. Devuelve el maquinista con los nombres para el historial, o el motivo del rechazo.
    /// </summary>
    public static async Task<(MaquinistaElegido? Elegido, string? Error)> ValidarAsync(
        IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager,
        int usuarioLineaId, int lineaIdDetencion, int? usuarioLineaIdAnterior)
    {
        var asignacion = await unitOfWork.UsuariosLineas.GetByIdAsync(usuarioLineaId);
        var usuario = asignacion == null ? null : await userManager.FindByIdAsync(asignacion.UsuarioId);
        var motivo = DetencionMaquinistaReglas.MotivoMaquinistaInvalido(asignacion, lineaIdDetencion, usuario is { Activo: true });
        if (motivo != null)
            return (null, motivo);

        string? nombreAnterior = null;
        if (usuarioLineaIdAnterior is { } anteriorId)
        {
            var anterior = await unitOfWork.UsuariosLineas.GetByIdAsync(anteriorId);
            var usuarioAnterior = anterior == null ? null : await userManager.FindByIdAsync(anterior.UsuarioId);
            nombreAnterior = usuarioAnterior?.NombreVisible ?? anterior?.UsuarioId;
        }
        return (new MaquinistaElegido(asignacion!, usuario!.NombreVisible, nombreAnterior), null);
    }
}
