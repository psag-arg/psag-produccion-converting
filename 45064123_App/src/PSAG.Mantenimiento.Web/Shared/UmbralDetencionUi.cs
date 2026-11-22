using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Campo "Desde [N] [min | s] de parada" de Maestros → Cámaras (reunión 10/11/2026; T-798 etapa 4): el umbral de
/// detención de la cámara se guarda en un solo campo en segundos (<c>UmbralDetencionSegundos</c>) y se carga en minutos
/// (lo normal) o en segundos (para probar en planta). La validación de rangos la hace la API; el texto legible del
/// umbral ("5 min", "45 s") también lo arma la API (UmbralTexto).
/// </summary>
public static class UmbralDetencionUi
{
    public const string Minutos = "min";
    public const string Segundos = "s";

    public const int MinutosMinimo = 1;
    public const int MinutosMaximo = 240;
    public const int SegundosMinimo = 30;
    public const int SegundosMaximo = 14400;
    public const int MinutosPorDefecto = 5;

    public const string TextoAyuda =
        "Para probar se puede poner en segundos (mínimo 30); después volvé a 5 minutos. " +
        "Las detenciones de prueba se eliminan desde Detenciones (administrador).";

    /// <summary>Valor y unidad para mostrar: minutos si es un número entero de minutos (o vacío = 5 min); si no, segundos.</summary>
    public static (int Valor, string Unidad) Leer(CamaraDto camara) => camara.UmbralDetencionSegundos switch
    {
        null => (MinutosPorDefecto, Minutos),
        { } s when s % 60 == 0 => (s / 60, Minutos),
        { } s => (s, Segundos)
    };

    /// <summary>Pasa lo que cargó el usuario a la cámara, en segundos.</summary>
    public static void Aplicar(CamaraDto camara, int valor, string unidad) =>
        camara.UmbralDetencionSegundos = unidad == Segundos ? valor : valor * 60;

    public static int Minimo(string unidad) => unidad == Segundos ? SegundosMinimo : MinutosMinimo;

    public static int Maximo(string unidad) => unidad == Segundos ? SegundosMaximo : MinutosMaximo;
}
