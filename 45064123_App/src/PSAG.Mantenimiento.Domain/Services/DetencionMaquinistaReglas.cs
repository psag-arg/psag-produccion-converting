using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Maquinista editable de la detención (reunión 17/11/2026). El sistema lo sugiere (el del turno/línea, ver
/// <see cref="DetencionCamaraReglas.ElegirMaquinista"/>) y quien carga, declara o edita la detención lo puede
/// cambiar. Funciones puras: la API resuelve la asignación y si el usuario está activo (Identity) y llama acá.
/// </summary>
public static class DetencionMaquinistaReglas
{
    /// <summary>Origen de la entrada del historial que deja un cambio de maquinista.</summary>
    public const string OrigenCambioMaquinista = "MAQUINISTA";

    /// <summary>
    /// Motivo por el que el maquinista elegido no sirve para la detención, o null. Tiene que existir la asignación
    /// (Maestros → Maquinistas), ser de la MISMA línea que la detención y el usuario tiene que existir y estar
    /// activo (un usuario dado de baja conserva sus asignaciones porque las detenciones viejas las usan).
    /// </summary>
    public static string? MotivoMaquinistaInvalido(UsuarioLinea? asignacion, int lineaIdDetencion, bool usuarioActivo)
    {
        if (asignacion == null)
            return "El maquinista seleccionado no existe.";
        if (asignacion.LineaId != lineaIdDetencion)
            return "El maquinista seleccionado no está asignado a la línea de la detención.";
        if (!usuarioActivo)
            return "El maquinista seleccionado está dado de baja.";
        return null;
    }

    /// <summary>Texto del historial: "Maquinista cambiado: Juan Pérez → Ana Gómez". Vacío se muestra "sin maquinista".</summary>
    public static string ObservacionCambio(string? anterior, string? nuevo) =>
        $"Maquinista cambiado: {Nombre(anterior)} → {Nombre(nuevo)}";

    private static string Nombre(string? nombre) => string.IsNullOrWhiteSpace(nombre) ? "sin maquinista" : nombre.Trim();
}
