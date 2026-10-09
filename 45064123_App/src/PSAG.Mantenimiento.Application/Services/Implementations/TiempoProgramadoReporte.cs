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
    public const string Estandar = "ESTANDAR";

    public static IReadOnlyList<TiempoProgramadoDiaDto> DiasDeLinea(
        int lineaId,
        string lineaNombre,
        DateOnly desde,
        DateOnly hasta,
        IEnumerable<Turno> turnos,
        IEnumerable<TiempoProgramadoDia> excepciones)
    {
        var excepcionPorFecha = excepciones.Where(e => e.Activo).ToDictionary(e => e.Fecha);
        var dias = new List<TiempoProgramadoDiaDto>();

        foreach (var dia in TiempoProgramadoCalculador.MinutosProgramadosPorDia(desde, hasta, turnos, excepciones))
        {
            var hayExcepcion = excepcionPorFecha.TryGetValue(dia.Fecha, out var excepcion);

            dias.Add(new TiempoProgramadoDiaDto
            {
                Fecha = dia.Fecha,
                LineaId = lineaId,
                LineaNombre = lineaNombre,
                Minutos = dia.Minutos,
                Horas = Horas(dia.Minutos),
                Origen = hayExcepcion ? excepcion!.Origen : Estandar,
                Motivo = hayExcepcion ? excepcion!.Motivo : null,
                TiempoProgramadoDiaId = hayExcepcion ? excepcion!.TiempoProgramadoDiaId : null
            });
        }

        return dias;
    }

    public static Dictionary<int, int> MinutosPorMes(IEnumerable<TiempoProgramadoDiaDto> dias) =>
        dias.GroupBy(d => d.Fecha.Month).ToDictionary(g => g.Key, g => g.Sum(d => d.Minutos));

    public static double Horas(int minutos) => Math.Round(minutos / 60.0, 2);
}
