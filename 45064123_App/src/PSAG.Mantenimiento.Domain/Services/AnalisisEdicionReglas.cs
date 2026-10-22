using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Edición de un análisis ya cerrado (desde la Base de soluciones) y quién puede adjuntar fotos. Funciones puras.
/// </summary>
public static class AnalisisEdicionReglas
{
    /// <summary>
    /// Un análisis cerrado solo lo corrigen administración y supervisión.
    /// </summary>
    public static bool PuedeEditarCerrado(IEnumerable<string> roles)
    {
        var rolesUsuario = roles.ToList();
        return rolesUsuario.Contains(RolesSistema.Administrador) || rolesUsuario.Contains(RolesSistema.Supervisor);
    }

    /// <summary>
    /// Fotos: mientras el análisis está abierto, quien puede completarlo; ya cerrado, quien puede editarlo cerrado.
    /// </summary>
    public static bool PuedeAdjuntar(Detencion detencion, string usuarioId, IEnumerable<string> roles)
    {
        var rolesUsuario = roles.ToList();
        if (AnalisisFallaReglas.EstaAbierto(detencion))
            return AnalisisFallaReglas.PuedeEditar(detencion, usuarioId, rolesUsuario);

        return detencion.EstadoActual == EstadoDetencion.CERRADA && PuedeEditarCerrado(rolesUsuario);
    }

    /// <summary>
    /// Un registro de historial por cada campo que cambia. Los textos llegan ya normalizados (null = vacío).
    /// </summary>
    public static List<AnalisisCambio> Cambios(
        Detencion detencion,
        string? solucion,
        string? causa,
        string? plan,
        string usuarioId,
        DateTimeOffset fechaHora)
    {
        var cambios = new List<AnalisisCambio>();

        void Comparar(string campo, string? anterior, string? nuevo)
        {
            if (string.Equals(anterior ?? string.Empty, nuevo ?? string.Empty, StringComparison.Ordinal))
                return;

            cambios.Add(new AnalisisCambio
            {
                DetencionId = detencion.DetencionId,
                Campo = campo,
                ValorAnterior = anterior,
                ValorNuevo = nuevo,
                UsuarioId = usuarioId,
                FechaHora = fechaHora
            });
        }

        Comparar(AnalisisCambio.CampoSolucion, detencion.AccionesRealizadas, solucion);
        Comparar(AnalisisCambio.CampoCausa, detencion.CausaFalla, causa);
        Comparar(AnalisisCambio.CampoPlanAccion, detencion.PlanAccion, plan);
        return cambios;
    }
}
