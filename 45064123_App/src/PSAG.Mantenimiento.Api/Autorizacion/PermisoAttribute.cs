using Microsoft.AspNetCore.Authorization;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.Autorizacion;

/// <summary>
/// Mapa endpoint → (opción, acción) de T-802. Exige usuario autenticado (es un <see cref="AuthorizeAttribute"/>) y que la
/// unión de los permisos de sus roles tenga la acción en la opción (o en alguna de las opciones, si se pasan varias:
/// sirve para las lecturas que usan varias pantallas). Sin permiso: 403 con ProblemDetails en castellano.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PermisoAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public PermisoAttribute(string codigoOpcion, AccionPermiso accion)
        : this(accion, codigoOpcion)
    {
    }

    public PermisoAttribute(AccionPermiso accion, params string[] codigosOpcion)
    {
        if (codigosOpcion.Length == 0)
            throw new ArgumentException("Falta la opción del permiso.", nameof(codigosOpcion));
        Accion = accion;
        Codigos = codigosOpcion;
    }

    public AccionPermiso Accion { get; }

    public IReadOnlyList<string> Codigos { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermisoRequirement(Codigos, Accion)];
}

public sealed class PermisoRequirement(IReadOnlyList<string> codigos, AccionPermiso accion) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Codigos { get; } = codigos;
    public AccionPermiso Accion { get; } = accion;
}
