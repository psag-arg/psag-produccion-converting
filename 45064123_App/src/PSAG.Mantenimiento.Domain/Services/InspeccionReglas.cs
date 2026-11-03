using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Resultado de validar lo que cargó el operador en un punto de inspección.
/// </summary>
public sealed record ResultadoPuntoInspeccion(string? Error, bool SinValor, ResultadoInspeccion? Resultado, string? Observacion)
{
    public bool EsValido => Error == null;
}

/// <summary>
/// Reglas de Inspecciones (planilla "Estándares de inspección", reunión 03/11/2026 [78:17]): funciona igual que
/// Control de Proceso. Cada punto se responde OK / NO OK (columna "Estado"); NO OK pide observación y abre un
/// hallazgo en el Panel NO OK. La frecuencia es por punto (diaria, cada 10 días...) y se cuenta desde la última
/// vez que se respondió. Funciones puras: la API las aplica al guardar y la UI solo las anticipa.
/// </summary>
public static class InspeccionReglas
{
    public const int LargoObservacion = 500;   // EjecucionesInspeccionDetalle.Observacion
    public const int LargoTareas = 300;        // ItemsPlanInspeccion.Descripcion
    public const int LargoEstandar = 300;      // ItemsPlanInspeccion.CondicionEstandar
    public const int LargoParteEquipo = 150;
    public const int LargoMetodo = 200;
    public const int LargoCodigoItem = 50;
    public const int FrecuenciaMaximaDias = 365;

    /// <summary>
    /// Valida la respuesta de un punto. Sin respuesta no es error (la inspección puede quedar a medias y
    /// completarse después). Acepta "OK", "NO OK", "NO_OK", "nook".
    /// </summary>
    public static ResultadoPuntoInspeccion ValidarRespuesta(string? resultado, string? observacion)
    {
        var obs = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();
        if (obs != null && obs.Length > LargoObservacion)
            return new ResultadoPuntoInspeccion($"La observación no puede superar {LargoObservacion} caracteres.", false, null, null);

        if (string.IsNullOrWhiteSpace(resultado))
            return new ResultadoPuntoInspeccion(null, true, null, obs);

        var normalizado = Normalizar(resultado);
        if (normalizado == null)
            return new ResultadoPuntoInspeccion("El estado solo puede ser OK o NO OK.", false, null, null);

        if (normalizado == ResultadoInspeccion.NO_OK && obs == null)
            return new ResultadoPuntoInspeccion("Está NO OK: la observación es obligatoria.", false, null, null);

        return new ResultadoPuntoInspeccion(null, false, normalizado, obs);
    }

    /// <summary>"OK", "ok" → OK; "NO OK", "NO_OK", "nook" → NO_OK; otra cosa → null.</summary>
    public static ResultadoInspeccion? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var compacto = new string(valor.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        return compacto switch
        {
            "OK" => ResultadoInspeccion.OK,
            "NOOK" => ResultadoInspeccion.NO_OK,
            _ => null
        };
    }

    /// <summary>
    /// El punto vence en <paramref name="hoy"/>: nunca se respondió o ya pasaron <paramref name="frecuenciaDias"/>
    /// días desde la última respuesta. Una diaria respondida hoy no vence hasta mañana.
    /// </summary>
    public static bool VenceHoy(DateOnly? ultimaRespuesta, int frecuenciaDias, DateOnly hoy) =>
        ultimaRespuesta == null || hoy.DayNumber - ultimaRespuesta.Value.DayNumber >= Math.Max(1, frecuenciaDias);

    /// <summary>Próxima fecha en que vence el punto (null si nunca se respondió: vence ya).</summary>
    public static DateOnly? ProximoVencimiento(DateOnly? ultimaRespuesta, int frecuenciaDias) =>
        ultimaRespuesta?.AddDays(Math.Max(1, frecuenciaDias));

    /// <summary>"Diaria", "Semanal", "Cada 10 días".</summary>
    public static string DescribirFrecuencia(int frecuenciaDias) => frecuenciaDias switch
    {
        <= 1 => "Diaria",
        7 => "Semanal",
        _ => $"Cada {frecuenciaDias} días"
    };

    /// <summary>
    /// El punto entra en una inspección de <paramref name="puestoEjecucion"/> (null = todos los puntos).
    /// </summary>
    public static bool AplicaAPuesto(TipoPuesto quienRealiza, TipoPuesto? puestoEjecucion) =>
        puestoEjecucion == null || quienRealiza == puestoEjecucion.Value;

    /// <summary>
    /// "PACKAGING", "OP. PACKAGING", "Operador Packaging", "MECANICO", "Mecánico" → puesto. Vacío → null (todos).
    /// </summary>
    public static bool TryLeerPuesto(string? valor, out TipoPuesto? puesto)
    {
        puesto = null;
        if (string.IsNullOrWhiteSpace(valor) || valor.Trim().Equals("TODOS", StringComparison.OrdinalIgnoreCase))
            return true;

        if (Enum.TryParse<TipoPuesto>(valor.Trim(), true, out var p) && Enum.IsDefined(p))
        {
            puesto = p;
            return true;
        }

        var letras = new string(valor.Normalize(System.Text.NormalizationForm.FormD)
            .Where(char.IsLetter).ToArray()).ToUpperInvariant();
        TipoPuesto? leido = letras switch
        {
            "OPPACKAGING" or "OPERADORPACKAGING" or "PACKAGING" => TipoPuesto.PACKAGING,
            "MECANICO" => TipoPuesto.MECANICO,
            "MAQUINISTA" => TipoPuesto.MAQUINISTA,
            "AYUDANTE" => TipoPuesto.AYUDANTE,
            _ => null
        };
        puesto = leido;
        return leido != null;
    }

    /// <summary>
    /// Motivo por el que la definición de un punto (maestro) no se puede guardar, o null si es válida.
    /// </summary>
    public static string? MotivoDefinicionInvalida(
        string? parteEquipo, string? tareas, string? estandar, string? metodo, string? codigo, TipoPuesto quienRealiza, int frecuenciaDias)
    {
        if (string.IsNullOrWhiteSpace(parteEquipo))
            return "La parte / equipo es obligatoria.";
        if (string.IsNullOrWhiteSpace(tareas))
            return "Las tareas son obligatorias.";
        if (string.IsNullOrWhiteSpace(estandar))
            return "El estándar es obligatorio.";
        if (!Enum.IsDefined(quienRealiza))
            return "Quién realiza no es válido.";
        if (frecuenciaDias < 1 || frecuenciaDias > FrecuenciaMaximaDias)
            return $"La frecuencia tiene que ser entre 1 y {FrecuenciaMaximaDias} días.";

        return Largo(parteEquipo, LargoParteEquipo, "La parte / equipo")
            ?? Largo(tareas, LargoTareas, "Las tareas")
            ?? Largo(estandar, LargoEstandar, "El estándar")
            ?? Largo(metodo, LargoMetodo, "El método")
            ?? Largo(codigo, LargoCodigoItem, "El código");
    }

    private static string? Largo(string? valor, int maximo, string campo) =>
        valor != null && valor.Trim().Length > maximo ? $"{campo} no puede superar {maximo} caracteres." : null;
}
