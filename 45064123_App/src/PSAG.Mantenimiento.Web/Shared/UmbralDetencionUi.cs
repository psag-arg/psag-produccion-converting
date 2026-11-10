using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Campo "Desde [N] [min | s] de parada" de Maestros → Calidad (reunión 10/11/2026): el umbral de detención de la
/// cámara se carga en minutos (lo normal) o en segundos (para probar en planta). La validación de rangos la hace la
/// API; el texto legible del umbral ("5 min", "45 s") también lo arma la API (UmbralTexto).
/// </summary>
public static class UmbralDetencionUi
{
    public const string Minutos = "min";
    public const string Segundos = "s";

    public const int MinutosMinimo = 1;
    public const int MinutosMaximo = 240;
    public const int SegundosMinimo = 30;
    public const int SegundosMaximo = 14400;

    public const string TextoAyuda =
        "Para probar se puede poner en segundos (mínimo 30); después volvé a 5 minutos. " +
        "Las detenciones de prueba se eliminan desde Detenciones (administrador).";

    /// <summary>Valor y unidad con que se guardó el umbral: si tiene segundos, en segundos; si no, en minutos.</summary>
    public static (int Valor, string Unidad) Leer(PuntoControlCalidadDto punto) =>
        punto.SegundosUmbralDetencion is { } s ? (s, Segundos) : (punto.MinutosUmbralDetencion, Minutos);

    /// <summary>
    /// Pasa lo que cargó el usuario al punto: en segundos, SegundosUmbralDetencion = valor y los minutos quedan como
    /// estaban; en minutos, MinutosUmbralDetencion = valor y se borran los segundos.
    /// </summary>
    public static void Aplicar(PuntoControlCalidadDto punto, int valor, string unidad)
    {
        if (unidad == Segundos)
        {
            punto.SegundosUmbralDetencion = valor;
        }
        else
        {
            punto.MinutosUmbralDetencion = valor;
            punto.SegundosUmbralDetencion = null;
        }
    }

    public static int Minimo(string unidad) => unidad == Segundos ? SegundosMinimo : MinutosMinimo;

    public static int Maximo(string unidad) => unidad == Segundos ? SegundosMaximo : MinutosMaximo;
}
