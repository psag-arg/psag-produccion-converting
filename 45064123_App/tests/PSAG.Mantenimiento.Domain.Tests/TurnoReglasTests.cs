using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TurnoReglasTests
{
    private static Turno T(int id, string nombre, int hi, int hf) => new()
    {
        TurnoId = id,
        Nombre = nombre,
        HoraInicio = new TimeOnly(hi, 0),
        HoraFin = new TimeOnly(hf, 0),
        Activo = true
    };

    private static readonly Turno[] Planta = [T(1, "Turno 1", 6, 14), T(2, "Turno 2", 14, 22), T(3, "Turno 3", 22, 6)];

    [Fact]
    public void TresTurnosConsecutivos_SonValidos()
    {
        Assert.Null(TurnoReglas.MotivoHorarioInvalido(Planta[2], Planta));
    }

    [Fact]
    public void TurnoQueSePisa_SeRechazaNombrandoAlOtro()
    {
        var motivo = TurnoReglas.MotivoHorarioInvalido(T(0, "Nuevo", 10, 18), Planta);

        Assert.Equal("Este turno se superpone con Turno 1 (06:00-14:00). Ajustá los horarios.", motivo);
    }

    [Fact]
    public void TurnoNocturnoQueSePisaDespuesDeMedianoche_SeRechaza()
    {
        Assert.NotNull(TurnoReglas.MotivoHorarioInvalido(T(0, "Madrugada", 4, 7), [T(3, "Turno 3", 22, 6)]));
    }

    [Fact]
    public void InicioIgualAFin_SeRechaza()
    {
        Assert.Equal("La hora de inicio y la de fin no pueden ser iguales.",
            TurnoReglas.MotivoHorarioInvalido(T(0, "Todo el día", 6, 6), []));
    }

    [Fact]
    public void SumaMayorA24h_SeRechaza()
    {
        // Un turno viejo de 24 h (inicio = fin) más otro no se superponen por tramos pero suman más de 24 h.
        var viejo = T(9, "Viejo", 0, 0);
        var motivo = TurnoReglas.MotivoHorarioInvalido(T(0, "Corto", 1, 2), [viejo]);

        Assert.NotNull(motivo);
    }

    [Fact]
    public void EditarUnTurnoNoSeComparaConsigoMismo()
    {
        Assert.Null(TurnoReglas.MotivoHorarioInvalido(T(1, "Turno 1", 6, 13), Planta));
    }

    [Fact]
    public void HistoricoSinCambios_NoSeConvierte()
    {
        var fila = new TiempoProgramadoDia { Origen = "HISTORICO", Horas = 8m, Activo = true };

        Assert.True(TurnoReglas.HistoricoSinCambios(fila, 8m, null));
        Assert.False(TurnoReglas.HistoricoSinCambios(fila, 6m, null));
        Assert.False(TurnoReglas.HistoricoSinCambios(fila, 8m, "Paro"));
    }

    [Fact]
    public void ReemplazarHistorico_DejaElValorAnteriorEnElMotivo()
    {
        Assert.Equal("antes 8 h históricas (HS PROG)", TurnoReglas.MotivoReemplazandoHistorico(8m, null));
        Assert.Equal("Paro · antes 8 h históricas (HS PROG)", TurnoReglas.MotivoReemplazandoHistorico(8m, "Paro"));
        Assert.Equal(200, TurnoReglas.MotivoReemplazandoHistorico(8m, new string('x', 250)).Length);
    }

    [Fact]
    public void MinutosProgramados_TresTurnosDeOchoSinSolapamiento_Veinticuatro_Horas()
    {
        Assert.Equal(24 * 60, TurnoReglas.MinutosProgramados(Planta));
        Assert.Equal(24 * 60, TurnoReglas.MinutosProgramados([T(1, "T1", 5, 13), T(2, "T2", 13, 21), T(3, "T3", 21, 5)]));
    }

    [Fact]
    public void MinutosProgramados_SumaLaDuracionDeCadaTurno()
    {
        // T1 05-13 más T3 21-06: 8 h + 9 h, aunque en el reloj de un día se pisen de 05 a 06.
        Assert.Equal(17 * 60, TurnoReglas.MinutosProgramados([T(1, "T1", 5, 13), T(3, "T3", 21, 6)]));
    }

    [Fact]
    public void MinutosProgramados_SinTurnos_Cero()
    {
        Assert.Equal(0, TurnoReglas.MinutosProgramados([]));
    }
}
