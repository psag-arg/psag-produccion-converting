using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// T-770 · Textos y colores del tablero del operador (tablet). Los números los calcula la API; acá sólo se muestran.
/// "Sin datos" nunca es 0 y "sin objetivo" nunca es 0 %. Colores de estado de Bootstrap 5.3 (nada de CSS de tema propio).
/// </summary>
public static class TableroUi
{
    /// <summary>Refresco de respaldo (sin SignalR, o si se cae).</summary>
    public static readonly TimeSpan RefrescoRespaldo = TimeSpan.FromSeconds(10);

    /// <summary>Con SignalR conectado igual se refresca cada tanto (el objetivo "a la hora" avanza con el reloj).</summary>
    public static readonly TimeSpan RefrescoConectado = TimeSpan.FromSeconds(60);

    /// <summary>Varios avisos seguidos (ingesta cada ~30 s por cámara) se juntan en una sola recarga.</summary>
    public static readonly TimeSpan EsperaAgrupar = TimeSpan.FromSeconds(2);

    /// <summary>Estado de un KPI: verde, amarillo, rojo o gris (sin dato).</summary>
    public enum Nivel { SinDato, Bien, Atencion, Mal, Neutro }

    /// <summary>Eficiencia (mangas real ÷ objetivo a la hora): verde desde 95 %, amarillo desde 85 %, rojo debajo (como Presupuesto vs. real).</summary>
    public static Nivel NivelEficiencia(ComparacionProduccionDto c) =>
        c.Estado != "OK" || c.Porcentaje is not { } p ? Nivel.SinDato : p >= 95 ? Nivel.Bien : p >= 85 ? Nivel.Atencion : Nivel.Mal;

    /// <summary>Indisponibilidad del turno: verde hasta 5 %, amarillo hasta 15 %, rojo más (decisión T-770, ajustable).</summary>
    public static Nivel NivelIndisponibilidad(decimal? porcentaje) =>
        porcentaje is not { } p ? Nivel.SinDato : p <= 5 ? Nivel.Bien : p <= 15 ? Nivel.Atencion : Nivel.Mal;

    /// <summary>Detenciones reales: sin ninguna verde; con alguna amarillo; 3 o más rojo.</summary>
    public static Nivel NivelDetenciones(int cantidad) => cantidad == 0 ? Nivel.Bien : cantidad < 3 ? Nivel.Atencion : Nivel.Mal;

    /// <summary>Pendientes de declarar: ninguna verde, alguna rojo (hay que declararlas antes de irse).</summary>
    public static Nivel NivelPendientes(int cantidad) => cantidad == 0 ? Nivel.Bien : Nivel.Mal;

    /// <summary>Clases de la tarjeta del KPI (borde y fondo suave de Bootstrap).</summary>
    public static string ClaseTarjeta(Nivel n) => n switch
    {
        Nivel.Bien => "border-success bg-success-subtle",
        Nivel.Atencion => "border-warning bg-warning-subtle",
        Nivel.Mal => "border-danger bg-danger-subtle",
        Nivel.Neutro => "border-info bg-info-subtle",
        _ => "border-secondary bg-body-tertiary"
    };

    /// <summary>Clase del número grande.</summary>
    public static string ClaseNumero(Nivel n) => n switch
    {
        Nivel.Bien => "text-success-emphasis",
        Nivel.Atencion => "text-warning-emphasis",
        Nivel.Mal => "text-danger-emphasis",
        Nivel.Neutro => "text-info-emphasis",
        _ => "text-body-secondary"
    };

    /// <summary>"81 %" o "Sin datos" / "Sin objetivo".</summary>
    public static string TextoEficiencia(ComparacionProduccionDto c) => c.Estado switch
    {
        "OK" when c.Porcentaje is { } p => p.ToString("N0", CultureInfo.CurrentCulture) + " %",
        "SIN_OBJETIVO" => EficienciaUi.TextoSinObjetivo,
        _ => EficienciaUi.TextoSinDatos
    };

    /// <summary>"4.100" o "Sin datos" (real); "5.060" o "Sin objetivo" (objetivo).</summary>
    public static string TextoReal(decimal? real) => real is { } r ? EficienciaUi.Numero(r) : EficienciaUi.TextoSinDatos;

    public static string TextoObjetivo(decimal? objetivo) => objetivo is { } o ? EficienciaUi.Numero(o) : EficienciaUi.TextoSinObjetivo;

    /// <summary>Bolsones "120 / 150" o "Sin datos" si no hay real ni objetivo.</summary>
    public static string TextoBolsones(ComparacionProduccionDto b) =>
        b.Real == null && b.Objetivo == null ? EficienciaUi.TextoSinDatos : $"{TextoReal(b.Real)} / {TextoObjetivo(b.Objetivo)}";

    /// <summary>"12,5 %" o "Sin datos".</summary>
    public static string TextoPorcentaje(decimal? p) => p is { } v ? v.ToString("N1", CultureInfo.CurrentCulture) + " %" : EficienciaUi.TextoSinDatos;

    /// <summary>Duración de paradas: "45 s", "4 min 30 s", "1 h 05 min"; 0 = "0 min".</summary>
    public static string TextoDuracion(int segundos)
    {
        if (segundos <= 0)
            return "0 min";
        if (segundos < 60)
            return $"{segundos} s";
        if (segundos < 3600)
            return segundos % 60 == 0 ? $"{segundos / 60} min" : $"{segundos / 60} min {segundos % 60:00} s";
        var minutos = (int)Math.Round(segundos / 60m, MidpointRounding.AwayFromZero);
        return $"{minutos / 60} h {minutos % 60:00} min";
    }

    /// <summary>"Último dato de cámara: 14:32:05" o "Último dato de cámara: sin datos todavía" (o sin cámara).</summary>
    public static string TextoUltimoDato(CamaraTableroDto c) =>
        !c.TieneCamara ? "La línea no tiene cámara de conteo"
        : c.UltimoDato is { } u ? $"Último dato de cámara: {u.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}"
        : "Último dato de cámara: sin datos todavía";

    /// <summary>Alerta "hace más de 5 min sin datos": la decide la API (misma tolerancia que SIN_DATOS).</summary>
    public static string? TextoSinDatos(CamaraTableroDto c) =>
        !c.SinDatos ? null
        : c.MinutosDesdeUltimoDato is { } m
            ? $"La cámara no manda datos hace {EficienciaUi.Minutos(m)}. Sin datos no es producción cero: avisá a sistemas."
            : "La cámara no mandó datos en este turno. Sin datos no es producción cero: avisá a sistemas.";

    /// <summary>"05:00", "06:00"… de la hora de mangas.</summary>
    public static string TextoHora(DateTimeOffset h) => h.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Diferencia real − objetivo de una hora ("+120", "−300"; null sin objetivo).</summary>
    public static string? TextoDiferencia(MangasHoraDto h) =>
        h.Objetivo is { } o ? (h.Real - o >= 0 ? "+" : "−") + EficienciaUi.Numero(Math.Abs(h.Real - o)) : null;

    /// <summary>Color de la fila de una hora: real ≥ 95 % del objetivo verde, ≥ 85 % amarillo, menos rojo; sin objetivo gris.</summary>
    public static Nivel NivelHora(MangasHoraDto h) =>
        h.Objetivo is not { } o || o <= 0 ? Nivel.SinDato
        : h.Real * 100 / o >= 95 ? Nivel.Bien : h.Real * 100 / o >= 85 ? Nivel.Atencion : Nivel.Mal;

    /// <summary>Aviso al entrar con pendientes (T-792): "Tenés 2 detenciones para declarar".</summary>
    public static string? TextoAvisoPendientes(int cantidad) => cantidad switch
    {
        0 => null,
        1 => "Tenés 1 detención para declarar.",
        _ => $"Tenés {cantidad} detenciones para declarar."
    };

    /// <summary>"Tango 2 · T2 (13–21) · SH 30 desde 13:05 · Op. Juan".</summary>
    public static string Encabezado(TableroTurnoDto t)
    {
        var partes = new List<string>
        {
            t.LineaNombre,
            ProduccionUi.TextoTurno(t.TurnoCodigo, t.InicioProgramado, t.FinProgramado),
            t.ProductoNombre is { } p ? $"{p} desde {t.ProductoDesde:HH:mm}" : "Producto a completar",
            t.OperadorNombre is { } o ? $"Op. {o}" : "Sin operador"
        };
        if (!t.Abierto)
            partes.Add(t.Estado == "SIN_APERTURA" ? "Sin apertura" : "Cerrado");
        return string.Join(" · ", partes);
    }
}
