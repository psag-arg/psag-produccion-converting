using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Ventana y minutos programados de un turno de planta en un día, y la unidad de una magnitud. Funciones puras: no
/// tocan la base. T-798 etapa 5: la eficiencia por cámara de la etapa 4 se reemplazó por los indicadores
/// (<see cref="IndicadoresProduccionCalculador"/>, un solo cálculo); acá quedan sólo las reglas de tiempo que usan
/// el turno de producción y las horas programadas.
/// </summary>
public static class EficienciaCamaraCalculador
{
    /// <summary>
    /// Inicio y fin del turno en hora de pared de la planta. Un turno que cruza medianoche (ej.: 21:00 a 05:00)
    /// pertenece al día en que empieza, como en el calendario de la línea: el del 02/11 va del 02/11 21:00 al
    /// 03/11 05:00.
    /// </summary>
    public static (DateTime Inicio, DateTime Fin) VentanaTurno(DateOnly dia, Turno turno)
    {
        var inicio = dia.ToDateTime(turno.HoraInicio);
        return (inicio, inicio.AddMinutes(turno.DuracionMinutos()));
    }

    /// <summary>
    /// Minutos programados de un turno en un día, con la misma regla que las horas programadas del día
    /// (<see cref="TiempoProgramadoCalculador"/>, T-541/T-542):
    /// <list type="number">
    /// <item>si el turno está inactivo, o el calendario vigente de la línea no lo incluye ese día de la semana, 0;</item>
    /// <item>si no hay excepción para el día, la duración del turno (un nocturno cuenta entero en el día en que empieza);</item>
    /// <item>si hay excepción (EXCEPCION o HISTORICO), las horas del día se reparten entre los turnos del día en
    /// proporción a su duración, sin pasar la duración del turno (0 h = no se trabaja).</item>
    /// </list>
    /// Sin excepciones, la suma de los turnos del día es igual a las horas programadas del día.
    /// </summary>
    public static int MinutosProgramadosTurno(
        DateOnly dia,
        Turno turno,
        IEnumerable<Turno> turnos,
        IEnumerable<CalendarioLinea> calendariosDeLaLinea,
        IEnumerable<TiempoProgramadoDia> excepcionesDeLaLinea)
    {
        if (!turno.Activo)
            return 0;

        var activos = turnos.Where(t => t.Activo).ToList();
        var version = calendariosDeLaLinea
            .Where(c => c.Activo && c.VigenteDesde <= dia)
            .OrderBy(c => c.VigenteDesde)
            .LastOrDefault();
        var delDia = version == null
            ? activos
            : activos.Where(t => version.TurnosDelDia(dia.DayOfWeek).Contains(t.TurnoId)).ToList();
        if (delDia.All(t => t.TurnoId != turno.TurnoId))
            return 0;

        var duracion = turno.DuracionMinutos();
        var excepcion = excepcionesDeLaLinea.FirstOrDefault(e => e.Activo && e.Fecha == dia);
        if (excepcion == null)
            return duracion;

        var minutosDia = (int)Math.Round(excepcion.Horas * 60m, MidpointRounding.AwayFromZero);
        var baseDia = TurnoReglas.MinutosProgramados(delDia);
        if (minutosDia <= 0 || baseDia <= 0)
            return 0;
        if (minutosDia >= baseDia)
            return duracion;
        return (int)Math.Round((decimal)duracion * minutosDia / baseDia, MidpointRounding.AwayFromZero);
    }

    /// <summary>Unidad que se muestra según la magnitud de la cámara ("mangas", "rollos", ...); sin magnitud, "unidades".</summary>
    public static string Unidad(string? magnitud) => magnitud?.ToUpperInvariant() switch
    {
        MagnitudProduccion.Mangas => "mangas",
        MagnitudProduccion.Rollos => "rollos",
        MagnitudProduccion.Paquetes => "paquetes",
        MagnitudProduccion.Bolsones => "bolsones",
        MagnitudProduccion.Pallets => "pallets",
        _ => "unidades"
    };
}
