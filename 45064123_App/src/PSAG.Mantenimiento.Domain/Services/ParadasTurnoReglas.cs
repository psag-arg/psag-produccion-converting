namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Una parada de cámara vista desde un turno: su inicio, los segundos que caen en ESE turno y si cuenta en la cantidad.</summary>
public readonly record struct ParadaEnTurno(DateTimeOffset Inicio, int Segundos, bool Cuenta);

/// <summary>
/// Turno automático (punto D) · Paradas de cámara que cruzan el cambio de turno. La parada (CAM_Paradas) es UNA fila,
/// colgada del turno en que empezó, y su clasificación (microcorte o detención real) sale de su duración TOTAL
/// (<see cref="DetencionCamaraReglas.SuperaUmbral(Entities.ParadaCamara)"/>), nunca de cada parte. Al mirarla desde un
/// turno, cada turno se queda con los segundos que le tocan; en la CANTIDAD cuenta una sola vez, en el turno donde empezó.
/// </summary>
public static class ParadasTurnoReglas
{
    /// <summary>
    /// Paradas del turno <paramref name="turnoProduccionId"/> (ventana [<paramref name="inicioTurno"/>, <paramref name="finTurno"/>)):
    /// las suyas (empezaron en él), cortadas en el fin del turno, cuentan 1; las de otro turno que empezaron antes y siguen
    /// en este aportan sus segundos dentro de la ventana y no cuentan.
    /// </summary>
    public static IReadOnlyList<ParadaEnTurno> DelTurno(
        long turnoProduccionId, DateTimeOffset inicioTurno, DateTimeOffset finTurno,
        IEnumerable<(long? TurnoProduccionId, DateTimeOffset Inicio, DateTimeOffset Fin, int Segundos)> paradas)
    {
        var resultado = new List<ParadaEnTurno>();
        foreach (var (turno, inicio, fin, segundos) in paradas)
        {
            if (turno == turnoProduccionId)
            {
                // Entera dentro del turno: su duración guardada; si sigue después del fin, sólo hasta el fin.
                resultado.Add(new ParadaEnTurno(inicio, fin > finTurno && inicio < finTurno ? Segundos(inicio, finTurno) : segundos, true));
            }
            else if (inicio < inicioTurno && fin > inicioTurno)
            {
                var hasta = fin < finTurno ? fin : finTurno;
                resultado.Add(new ParadaEnTurno(inicioTurno, Segundos(inicioTurno, hasta), false));
            }
        }
        return resultado;
    }

    private static int Segundos(DateTimeOffset desde, DateTimeOffset hasta) =>
        hasta <= desde ? 0 : (int)Math.Round((hasta - desde).TotalSeconds, MidpointRounding.AwayFromZero);
}
