namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Largo máximo de los textos del análisis de falla, iguales a los de la API (AnalisisService.ValidarLargos).
/// Acá sólo limitan los textarea; la API vuelve a validar al guardar.
/// </summary>
public static class LargosAnalisis
{
    public const int Solucion = 4000;
    public const int CausaYPlan = 2000;
}
