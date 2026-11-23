using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-805 · La regla única del objetivo del turno (<see cref="ObjetivoTurnoReglas"/>): huecos sin producto al tramo más
/// cercano, reparto sobre los minutos programados y objetivo a la hora = la misma repartición cortada en "ahora".
/// Turno 2 (13–21) del 23/11, SH 30: 11.200 mangas / 6.500 bolsones por turno; SH 50: 6.720 mangas.
/// </summary>
public class ObjetivoTurnoReglasTests
{
    private static DateTimeOffset H(int hora, int minuto = 0, int segundo = 0) => new(2026, 11, 23, hora, minuto, segundo, TimeSpan.FromHours(-3));

    private static decimal Objetivo(int objetivoTurno, decimal minutos) =>
        Math.Round(IndicadoresProduccionCalculador.ObjetivoTramo(objetivoTurno, 480, minutos)!.Value, 1, MidpointRounding.AwayFromZero);

    [Fact]
    public void DeclaracionTardia_ProductoALas1305_ObjetivoDelTurnoEntero_YALaHoraDesdeElInicioDelTurno()
    {
        var ahora = H(14, 9, 32);
        var fin = ObjetivoTurnoReglas.FinTramo(H(13, 5), null, null, H(21), ahora);
        var r = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13, 5), fin)], H(13), H(21), 480);

        // Los 5 minutos sin producto van al único tramo: 11.200 / 6.500 (antes daba 11.083 / 6.432)
        Assert.Equal(480m, r.MinutosTurno(0));
        Assert.Equal(11200m, Objetivo(11200, r.MinutosTurno(0)));
        Assert.Equal(6500m, Objetivo(6500, r.MinutosTurno(0)));

        // A la hora: 69 min 32 s del turno (no 64 min 32 s del tramo)
        var aLaHora = r.MinutosHasta(0, ahora);
        Assert.Equal(69.533m, Math.Round(aLaHora, 3));
        Assert.Equal(1622.4m, Objetivo(11200, aLaHora));
        Assert.Equal(941.6m, Objetivo(6500, aLaHora));

        // Sin corte (turno cerrado) = el del turno
        Assert.Equal(r.MinutosTurno(0), r.MinutosHasta(0, null));
        // Objetivo de la hora 13–14: la hora entera (los 5 min sin producto incluidos)
        Assert.Equal(60m, r.MinutosEntre(0, H(13), H(14)));
    }

    [Fact]
    public void CambioDeProducto_ConHuecoSinProducto_ElHuecoSeParteEntreLosDosTramos()
    {
        // SH 30 13:00–15:00, sin producto 15:00–15:20, SH 50 desde 15:20 (abierto); ahora 16:00
        var ahora = H(16);
        var r = ObjetivoTurnoReglas.Repartir(
            [new TramoParaObjetivo(H(13), H(15)), new TramoParaObjetivo(H(15, 20), ObjetivoTurnoReglas.FinTramo(H(15, 20), null, null, H(21), ahora))],
            H(13), H(21), 480);

        Assert.Equal(130m, r.MinutosTurno(0));   // 120 + 10 del hueco
        Assert.Equal(350m, r.MinutosTurno(1));   // 10 del hueco + 340
        Assert.Equal(7933.3m, Objetivo(11200, r.MinutosTurno(0)) + Objetivo(6720, r.MinutosTurno(1)));   // 3.033,3 + 4.900

        // A las 16: SH 30 entero (130) + SH 50 de 15:10 a 16:00 (50)
        Assert.Equal(130m, r.MinutosHasta(0, ahora));
        Assert.Equal(50m, r.MinutosHasta(1, ahora));
    }

    [Fact]
    public void UnSoloProductoTodoElTurno_Y_CambioSinHueco_ComoAntes()
    {
        var uno = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13), H(21))], H(13), H(21), 480);
        Assert.Equal(11200m, Objetivo(11200, uno.MinutosTurno(0)));

        // T2 con cambio (SH 30 3 h + SH 50 5 h) = 4.200 + 4.200 (diseño 2.10)
        var dos = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13), H(16)), new TramoParaObjetivo(H(16), H(21))], H(13), H(21), 480);
        Assert.Equal(8400m, Objetivo(11200, dos.MinutosTurno(0)) + Objetivo(6720, dos.MinutosTurno(1)));
    }

    [Fact]
    public void TurnoCortadoA6Horas_YCierreTarde_SeRepartenLosProgramados()
    {
        // Día con 360 min programados y el tramo todo el turno: 8.400
        var cortado = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13), H(21))], H(13), H(21), 360);
        Assert.Equal(8400m, Objetivo(11200, cortado.MinutosTurno(0)));
        // A mitad del turno, la mitad del objetivo (la misma repartición cortada)
        Assert.Equal(4200m, Objetivo(11200, cortado.MinutosHasta(0, H(17))));

        // Cierre tarde (21:30): 510 min repartidos sobre 480
        var tarde = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13), H(21, 30))], H(13), H(21), 480);
        Assert.Equal(480m, tarde.MinutosTurno(0));
    }

    [Fact]
    public void CierreAntesDeHora_LoQueFaltaVaAlUltimoTramo()
    {
        var r = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13), H(19))], H(13), H(21), 480);
        Assert.Equal(480m, r.MinutosTurno(0));
    }

    [Fact]
    public void SinTramos_SinObjetivo_YFueraDeProgramacionSoloLaDuracion()
    {
        Assert.Empty(ObjetivoTurnoReglas.Repartir([], H(13), H(21), 480).Intervalos);

        var fuera = ObjetivoTurnoReglas.Repartir([new TramoParaObjetivo(H(13, 30), H(15))], H(13), H(21), 0);
        Assert.Equal(90m, fuera.MinutosTurno(0));
    }

    [Fact]
    public void FinTramo_AbiertoHastaElFinProgramado_OAhoraSiSePaso_OElFinReal()
    {
        Assert.Equal(H(21), ObjetivoTurnoReglas.FinTramo(H(13), null, null, H(21), H(14)));
        Assert.Equal(H(21, 10), ObjetivoTurnoReglas.FinTramo(H(13), null, null, H(21), H(21, 10)));
        Assert.Equal(H(19), ObjetivoTurnoReglas.FinTramo(H(13), null, H(19), H(21), H(20)));
        Assert.Equal(H(15), ObjetivoTurnoReglas.FinTramo(H(13), H(15), null, H(21), H(20)));
    }
}
