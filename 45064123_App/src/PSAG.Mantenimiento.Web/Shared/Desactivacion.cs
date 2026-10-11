using Microsoft.JSInterop;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Desactivar un elemento de un catálogo desde su lista: confirmar, guardar y recargar.
/// </summary>
public static class Desactivacion
{
    public const string EfectoEnCarga = "Deja de aparecer al cargar detenciones.";

    /// <summary>
    /// Si la API lo rechaza (por ejemplo, tiene elementos activos que dependen), vuelve a activo y avisa el motivo.
    /// </summary>
    public static async Task ConfirmarYDesactivarAsync(this IJSRuntime js, string queEs, string efecto,
        Action<bool> marcarActivo, Func<Task> guardar, Func<Task> recargar, Action<string?> avisar)
    {
        var confirmado = await js.InvokeAsync<bool>("confirm",
            $"¿Desactivar {queEs}? {efecto} Se puede reactivar con \"Mostrar inactivos\" y Editar.");
        if (!confirmado) return;

        avisar(null);
        marcarActivo(false);
        try
        {
            await guardar();
        }
        catch (Exception ex)
        {
            marcarActivo(true);
            avisar(ex.Message);
            return;
        }
        await recargar();
    }
}
