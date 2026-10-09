namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Badge de estado de una detención. "Requiere análisis" no es un estado: la detención sigue REGISTRADA
/// y el servidor informa AnalisisPendiente.
/// </summary>
public static class EstadoUi
{
    // Bootstrap 5.3 no trae un badge naranja.
    private const string EstiloRequiereAnalisis = "background-color:#fd7e14;color:#fff";

    public static string BadgeClass(string estado, bool analisisPendiente) => estado switch
    {
        "REGISTRADA" when analisisPendiente => "badge",
        "REGISTRADA" => "badge bg-primary",
        "EN_SEGUIMIENTO" => "badge bg-warning text-dark",
        "CERRADA" => "badge bg-success",
        _ => "badge bg-secondary"
    };

    public static string? BadgeStyle(string estado, bool analisisPendiente) =>
        estado == "REGISTRADA" && analisisPendiente ? EstiloRequiereAnalisis : null;

    public static string Texto(string estadoDescripcion, string? estadoAnalisisDescripcion) =>
        estadoAnalisisDescripcion ?? estadoDescripcion;
}
