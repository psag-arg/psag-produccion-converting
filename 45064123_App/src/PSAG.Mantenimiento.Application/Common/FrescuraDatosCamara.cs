namespace PSAG.Mantenimiento.Application.Common;

/// <summary>Estado de los datos de una cámara en el Dashboard ("Datos hasta las HH:mm").</summary>
public static class EstadoDatosCamara
{
    /// <summary>Llegan minutos nuevos: el atraso es el normal (1 o 2 minutos, lo que tarda el servicio en mandar el lote).</summary>
    public const string AlDia = "AL_DIA";

    /// <summary>Hace más del umbral que no llega ningún minuto nuevo: servicio de conteo detenido, cámara apagada o sin red.</summary>
    public const string Atrasado = "ATRASADO";

    /// <summary>La cámara nunca mandó datos (por ejemplo, recién dada de alta, como la T2-CAM08).</summary>
    public const string SinDatos = "SIN_DATOS";
}

/// <summary>
/// "Datos hasta las HH:mm" de una cámara (reunión 17/11/2026): el FIN del último minuto cerrado que hay en
/// mnt.ConteosCamaraMinuto. El panel va 1 o 2 minutos atrás del contador de la máquina; mostrarlo evita comparar el
/// contador de la máquina a una hora con lo que el sistema contó hasta otra (79 min de máquina contra 76 del sistema).
/// </summary>
public static class FrescuraDatosCamara
{
    /// <summary>Sin minutos nuevos por más de esto, el Dashboard avisa que los datos están viejos.</summary>
    public const int MinutosAtrasoPorDefecto = 5;

    public const int MinutosAtrasoMinimo = 2;
    public const int MinutosAtrasoMaximo = 120;

    /// <summary>Umbral pedido por la Web, acotado a un rango razonable (vacío o fuera de rango = 5 / el límite).</summary>
    public static int UmbralValido(int? minutos) =>
        minutos is { } m ? Math.Clamp(m, MinutosAtrasoMinimo, MinutosAtrasoMaximo) : MinutosAtrasoPorDefecto;

    /// <summary>
    /// Hora hasta la que hay datos: fin del último minuto guardado (el minuto 10:15 cubre de 10:15:00 a 10:16:00, así
    /// que hay datos hasta las 10:16). El servicio sólo manda minutos cerrados, pero si el reloj del servidor de la
    /// cámara va adelantado el minuto podría no haber terminado todavía: nunca se informa una hora posterior al
    /// último minuto entero de <paramref name="ahora"/>. Null si la cámara nunca mandó datos.
    /// </summary>
    public static DateTimeOffset? DatosHasta(DateTimeOffset? ultimoMinuto, DateTimeOffset ahora)
    {
        if (ultimoMinuto is not { } minuto)
            return null;
        var fin = InicioDelMinuto(minuto).AddMinutes(1);
        var tope = InicioDelMinuto(ahora);
        return fin > tope ? tope.ToOffset(fin.Offset) : fin;
    }

    /// <summary>Minutos enteros entre el fin de los datos y ahora (0 si no hay atraso). Null sin datos.</summary>
    public static int? MinutosSinDatosNuevos(DateTimeOffset? datosHasta, DateTimeOffset ahora) =>
        datosHasta is { } hasta ? (int)Math.Max(0, Math.Floor((ahora - hasta).TotalMinutes)) : null;

    /// <summary>AL_DIA, ATRASADO (más de <paramref name="umbralMinutos"/> sin minutos nuevos) o SIN_DATOS.</summary>
    public static string Estado(DateTimeOffset? datosHasta, DateTimeOffset ahora, int umbralMinutos = MinutosAtrasoPorDefecto)
    {
        var atraso = MinutosSinDatosNuevos(datosHasta, ahora);
        if (atraso == null)
            return EstadoDatosCamara.SinDatos;
        return atraso > umbralMinutos ? EstadoDatosCamara.Atrasado : EstadoDatosCamara.AlDia;
    }

    private static DateTimeOffset InicioDelMinuto(DateTimeOffset t) =>
        new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Offset);
}
