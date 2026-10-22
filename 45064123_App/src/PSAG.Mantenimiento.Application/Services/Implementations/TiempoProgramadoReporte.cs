using PSAG.Mantenimiento.Application.DTOs.Dashboard;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Tiempo programado para los reportes: detalle diario de una línea con su origen y suma por mes.
/// Función pura: recibe los datos ya cargados.
/// </summary>
public static class TiempoProgramadoReporte
{
    /// <summary>
    /// Días de la línea con origen: excepción o histórico del día, calendario base de la línea,
    /// o estándar de planta (todos los turnos activos) si la línea no tiene calendario vigente.
    /// </summary>
    public static IReadOnlyList<TiempoProgramadoDiaDto> DiasDeLinea(
        int lineaId,
        string lineaNombre,
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<Turno> turnos,
        IEnumerable<CalendarioLinea> calendarios,
        IEnumerable<TiempoProgramadoDia> excepciones)
    {
        var activas = excepciones.Where(e => e.Activo).ToList();
        var excepcionPorFecha = activas.ToDictionary(e => e.Fecha);
        var dias = new List<TiempoProgramadoDiaDto>();

        foreach (var dia in TiempoProgramadoCalculador.MinutosProgramadosPorDia(
                     desde, hasta, turnos, calendarios.Where(c => c.LineaId == lineaId), activas))
        {
            var hayExcepcion = excepcionPorFecha.TryGetValue(dia.Fecha, out var excepcion);

            dias.Add(new TiempoProgramadoDiaDto
            {
                Fecha = dia.Fecha,
                LineaId = lineaId,
                LineaNombre = lineaNombre,
                Minutos = dia.Minutos,
                Horas = Horas(dia.Minutos),
                Origen = dia.Origen,
                Turnos = dia.Turnos,
                Motivo = hayExcepcion ? excepcion!.Motivo : null,
                TiempoProgramadoDiaId = hayExcepcion ? excepcion!.TiempoProgramadoDiaId : null
            });
        }

        return dias;
    }

    public static Dictionary<int, int> MinutosPorMes(IEnumerable<TiempoProgramadoDiaDto> dias) =>
        dias.GroupBy(d => d.Fecha.Month).ToDictionary(g => g.Key, g => g.Sum(d => d.Minutos));

    public static double Horas(int minutos) => Math.Round(minutos / 60.0, 2);

    /// <summary>
    /// Último día que cuenta como programado: el tiempo futuro no entra en el denominador.
    /// </summary>
    public static DateOnly HastaHoy(int anio, DateOnly hoy)
    {
        var finDeAnio = new DateOnly(anio, 12, 31);
        return hoy < finDeAnio ? hoy : finDeAnio;
    }

    /// <summary>
    /// Meses que muestra un reporte mensual: desde el primer mes con datos hasta el mes en curso
    /// (o el último mes con datos), con tiempo programado. Un mes sin detenciones en el medio
    /// aparece en 0 en lugar de desaparecer y subir el promedio.
    /// </summary>
    public static IReadOnlyList<int> MesesDelReporte(
        int anio,
        int mesDesde,
        int mesHasta,
        IEnumerable<int> mesesConDatos,
        IReadOnlyDictionary<int, int> minutosProgramadosPorMes,
        DateOnly hoy)
    {
        var conDatos = mesesConDatos.Where(m => m >= mesDesde && m <= mesHasta).ToHashSet();
        if (conDatos.Count == 0)
            return Array.Empty<int>();

        var ultimoEmpezado = anio < hoy.Year ? 12 : anio == hoy.Year ? hoy.Month : 0;
        var hasta = Math.Min(mesHasta, Math.Max(ultimoEmpezado, conDatos.Max()));

        return Enumerable.Range(conDatos.Min(), Math.Max(0, hasta - conDatos.Min() + 1))
            .Where(m => conDatos.Contains(m) || minutosProgramadosPorMes.GetValueOrDefault(m) > 0)
            .ToList();
    }
}
