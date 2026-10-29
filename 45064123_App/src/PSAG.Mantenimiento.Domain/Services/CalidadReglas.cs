using System.Security.Cryptography;
using System.Text;
using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del registro de defectos de calidad (T-611). Funciones puras: la API las usa para validar, no sólo la pantalla.
/// </summary>
public static class CalidadReglas
{
    public const string MensajeSinPermisoRevision =
        "Sólo supervisión, mantenimiento o el administrador pueden marcar un registro como confirmado o falso positivo.";
    public const string MensajeSinPermisoCargaManual =
        "Los registros de calidad manuales los cargan supervisión o el administrador.";
    public const string MensajeSinPermisoMaestros =
        "Los puntos de control y los tipos de defecto los administra el administrador.";

    /// <summary>Largo mínimo de Calidad:ClaveIngesta. Una clave más corta se toma como no configurada.</summary>
    public const int LargoMinimoClave = 16;

    public const int LargoMaximoObservaciones = 1000;
    public const int LargoMaximoComentario = 500;
    public const int LargoMaximoUnidad = 20;
    public const int LargoMaximoIdentificadorExterno = 100;

    /// <summary>
    /// Tolerancia para fechas "en el futuro": el reloj del detector puede estar unos minutos adelantado.
    /// </summary>
    public static readonly TimeSpan ToleranciaFuturo = TimeSpan.FromMinutes(10);

    /// <summary>Marcan confirmado / falso positivo: supervisión, mantenimiento y administrador. El operario sólo ve.</summary>
    public static readonly IReadOnlyList<string> RolesRevision =
        [RolesSistema.Supervisor, RolesSistema.Mantenimiento, RolesSistema.Administrador];

    /// <summary>Cargan registros manuales: supervisión y administrador.</summary>
    public static readonly IReadOnlyList<string> RolesCargaManual =
        [RolesSistema.Supervisor, RolesSistema.Administrador];

    public static bool PuedeRevisar(IEnumerable<string> roles) => roles.Any(RolesRevision.Contains);

    public static bool PuedeCargarManual(IEnumerable<string> roles) => roles.Any(RolesCargaManual.Contains);

    public static bool PuedeAdministrarMaestros(IEnumerable<string> roles) => roles.Contains(RolesSistema.Administrador);

    /// <summary>
    /// La clave está configurada y es usable (no vacía y con el largo mínimo).
    /// </summary>
    public static bool ClaveConfigurada(string? claveConfigurada) =>
        !string.IsNullOrWhiteSpace(claveConfigurada) && claveConfigurada.Trim().Length >= LargoMinimoClave;

    /// <summary>
    /// Compara la clave recibida con la configurada en tiempo constante: se comparan los SHA-256 de ambas,
    /// así ni el contenido ni el largo de la clave cambian lo que tarda la comparación.
    /// </summary>
    public static bool ClaveValida(string? claveRecibida, string? claveConfigurada)
    {
        if (!ClaveConfigurada(claveConfigurada) || string.IsNullOrEmpty(claveRecibida))
            return false;

        var esperada = SHA256.HashData(Encoding.UTF8.GetBytes(claveConfigurada!.Trim()));
        var recibida = SHA256.HashData(Encoding.UTF8.GetBytes(claveRecibida.Trim()));
        return CryptographicOperations.FixedTimeEquals(esperada, recibida);
    }

    /// <summary>
    /// Motivo por el que los datos medidos no se pueden guardar, o null si están bien.
    /// </summary>
    public static string? MotivoDatosInvalidos(decimal? confianza, decimal? valorMedido, string? unidad)
    {
        if (confianza is { } c && (c < 0m || c > 1m))
            return "La confianza tiene que estar entre 0 y 1 (ej.: 0.87).";

        if (valorMedido is { } v && v < 0m)
            return "El valor medido no puede ser negativo.";

        if (unidad?.Trim().Length > LargoMaximoUnidad)
            return $"La unidad admite hasta {LargoMaximoUnidad} caracteres.";

        return null;
    }

    /// <summary>
    /// La fecha de la detección no puede ser posterior a ahora (con la tolerancia por relojes desfasados).
    /// </summary>
    public static bool EsFechaFutura(DateTimeOffset fechaHora, DateTimeOffset ahora) => fechaHora > ahora + ToleranciaFuturo;

    /// <summary>
    /// Códigos de puntos de control y defectos: sin espacios a los costados y en mayúsculas,
    /// así "t2-cam1" y "T2-CAM1" son el mismo.
    /// </summary>
    public static string NormalizarCodigo(string? codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();
}
