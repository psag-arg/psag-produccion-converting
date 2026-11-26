using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>Eficiencia por turno de la cámara (reunión 03/11): objetivo = objetivo por hora x horas programadas.</summary>
public class EficienciaCamaraCalculadorTests
{
    private static readonly DateOnly Lunes = new(2026, 11, 2);

    // Turnos reales de la planta: 05 a 13, 13 a 21 y 21 a 05 (el turno 3 cruza medianoche).
    private static readonly Turno T1 = Turno(1, "T1", 5, 13);
    private static readonly Turno T2 = Turno(2, "T2", 13, 21);
    private static readonly Turno T3 = Turno(3, "T3", 21, 5);
    private static readonly Turno[] Turnos = [T1, T2, T3];

    private static Turno Turno(int id, string codigo, int inicio, int fin, bool activo = true) => new()
    {
        TurnoId = id, Codigo = codigo, Nombre = codigo,
        HoraInicio = new TimeOnly(inicio, 0), HoraFin = new TimeOnly(fin, 0), Activo = activo
    };

    private static TiempoProgramadoDia Excepcion(DateOnly fecha, decimal horas) => new()
    {
        LineaId = 1, Fecha = fecha, Horas = horas, Origen = "EXCEPCION", Activo = true
    };

    private static CalendarioLinea Calendario(DateOnly desde, params (DayOfWeek Dia, int TurnoId)[] turnos)
    {
        var c = new CalendarioLinea { LineaId = 1, VigenteDesde = desde, Activo = true };
        foreach (var (dia, id) in turnos)
            c.Turnos.Add(new CalendarioLineaTurno { DiaSemana = dia, TurnoId = id });
        return c;
    }

    // ---------- Ventana y horas programadas ----------

    [Fact]
    public void VentanaTurno_DeDia_VaDeInicioAFinDelMismoDia()
    {
        var (inicio, fin) = EficienciaCamaraCalculador.VentanaTurno(Lunes, T1);

        Assert.Equal(new DateTime(2026, 11, 2, 5, 0, 0), inicio);
        Assert.Equal(new DateTime(2026, 11, 2, 13, 0, 0), fin);
    }

    [Fact]
    public void VentanaTurno_Noche_CruzaMedianocheYEsDelDiaEnQueEmpieza()
    {
        var (inicio, fin) = EficienciaCamaraCalculador.VentanaTurno(Lunes, T3);

        Assert.Equal(new DateTime(2026, 11, 2, 21, 0, 0), inicio);
        Assert.Equal(new DateTime(2026, 11, 3, 5, 0, 0), fin);
    }

    [Fact]
    public void MinutosProgramados_SinCalendario_EsLaDuracionDelTurno_IncluidoElDeNoche()
    {
        Assert.Equal(480, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T1, Turnos, [], []));
        Assert.Equal(480, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T3, Turnos, [], []));
    }

    [Fact]
    public void MinutosProgramados_SinExcepciones_SumanLasHorasDelDia()
    {
        // Misma regla que las horas programadas del día (T-541): 3 turnos de 8 h = 24 h, no 25.
        var dia = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, Turnos, [], []).Single();
        var suma = Turnos.Sum(t => EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, t, Turnos, [], []));

        Assert.Equal(dia.Minutos, suma);
        Assert.Equal(24 * 60, suma);
    }

    [Fact]
    public void MinutosProgramados_TurnoQueElCalendarioNoIncluye_Es0()
    {
        // La línea el lunes trabaja sólo T1 y T2.
        var calendario = Calendario(Lunes.AddDays(-7), (DayOfWeek.Monday, 1), (DayOfWeek.Monday, 2));

        Assert.Equal(480, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T2, Turnos, [calendario], []));
        Assert.Equal(0, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T3, Turnos, [calendario], []));
    }

    [Fact]
    public void MinutosProgramados_TurnoInactivo_Es0()
    {
        var inactivo = Turno(4, "T4", 1, 3, activo: false);

        Assert.Equal(0, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, inactivo, [.. Turnos, inactivo], [], []));
    }

    [Fact]
    public void MinutosProgramados_ExcepcionDeCeroHoras_NoSeTrabaja()
    {
        Assert.Equal(0, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T1, Turnos, [], [Excepcion(Lunes, 0)]));
    }

    [Fact]
    public void MinutosProgramados_ExcepcionParcial_SeRepartePorDuracion()
    {
        // 16 h en un día de 24 h: cada turno de 8 h queda en 5 h 20 min.
        var minutos = EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T3, Turnos, [], [Excepcion(Lunes, 16)]);

        Assert.Equal(320, minutos);
    }

    [Fact]
    public void MinutosProgramados_ExcepcionMayorAlDia_NoPasaLaDuracionDelTurno()
    {
        var calendario = Calendario(Lunes.AddDays(-7), (DayOfWeek.Monday, 1));

        Assert.Equal(480, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T1, Turnos, [calendario], [Excepcion(Lunes, 12)]));
    }

    [Fact]
    public void MinutosProgramados_ExcepcionDeOtroDia_NoAfecta()
    {
        Assert.Equal(480, EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T1, Turnos, [], [Excepcion(Lunes.AddDays(1), 0)]));
    }

    // ---------- Unidad ----------

    [Theory]
    [InlineData("MANGAS", "mangas")]
    [InlineData("rollos", "rollos")]
    [InlineData("BOLSONES", "bolsones")]
    [InlineData("PAQUETES", "paquetes")]
    [InlineData("PALLETS", "pallets")]
    [InlineData(null, "unidades")]
    public void Unidad_SegunLaMagnitudDeLaCamara(string? magnitud, string esperada)
    {
        Assert.Equal(esperada, EficienciaCamaraCalculador.Unidad(magnitud));
    }
}
