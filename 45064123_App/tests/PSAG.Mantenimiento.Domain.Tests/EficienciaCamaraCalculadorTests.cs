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

    // ---------- Objetivo y % ----------

    [Fact]
    public void ObjetivoTurno_EsObjetivoPorHoraPorHorasProgramadas()
    {
        Assert.Equal(10_560m, EficienciaCamaraCalculador.ObjetivoTurno(1320m, 480));
        Assert.Equal(7_040m, EficienciaCamaraCalculador.ObjetivoTurno(1320m, 320));
    }

    [Fact]
    public void ObjetivoTurno_DeNoche_UsaLasOchoHorasAunqueCruceMedianoche()
    {
        var minutos = EficienciaCamaraCalculador.MinutosProgramadosTurno(Lunes, T3, Turnos, [], []);

        Assert.Equal(8_000m, EficienciaCamaraCalculador.ObjetivoTurno(1000m, minutos));
    }

    [Fact]
    public void ObjetivoTurno_SinObjetivoCargado_EsNull()
    {
        Assert.Null(EficienciaCamaraCalculador.ObjetivoTurno(null, 480));
        Assert.Equal(EstadoEficiencia.SinObjetivo, EficienciaCamaraCalculador.Estado(null, 480));
    }

    [Fact]
    public void Porcentaje_ContadasSobreObjetivo()
    {
        Assert.Equal(87.5m, EficienciaCamaraCalculador.Porcentaje(7000m, 8000m));
        Assert.Equal(112.3m, EficienciaCamaraCalculador.Porcentaje(8984m, 8000m));
    }

    [Fact]
    public void Porcentaje_SinObjetivo_EsNull_YNoCero()
    {
        Assert.Null(EficienciaCamaraCalculador.Porcentaje(5000m, null));
    }

    [Fact]
    public void Porcentaje_ObjetivoCero_NoDividePorCero()
    {
        // Turno no programado: objetivo por hora cargado, 0 horas → objetivo 0.
        var objetivo = EficienciaCamaraCalculador.ObjetivoTurno(1000m, 0);

        Assert.Equal(0m, objetivo);
        Assert.Null(EficienciaCamaraCalculador.Porcentaje(350m, objetivo));
        Assert.Null(EficienciaCamaraCalculador.Porcentaje(0m, 0m));
        Assert.Equal(EstadoEficiencia.NoProgramado, EficienciaCamaraCalculador.Estado(1000m, 0));
    }

    [Fact]
    public void Porcentaje_SinContadas_ConObjetivo_EsCero()
    {
        Assert.Equal(0m, EficienciaCamaraCalculador.Porcentaje(0m, 8000m));
        Assert.Equal(EstadoEficiencia.Ok, EficienciaCamaraCalculador.Estado(1000m, 480));
    }

    // ---------- Turno en curso ----------

    [Fact]
    public void Momento_YObjetivoHastaAhora()
    {
        var inicio = new DateTimeOffset(2026, 11, 2, 21, 0, 0, TimeSpan.FromHours(-3));
        var fin = inicio.AddHours(8);

        Assert.Equal(MomentoTurno.SinEmpezar, EficienciaCamaraCalculador.Momento(inicio.AddMinutes(-1), inicio, fin));
        Assert.Equal(MomentoTurno.EnCurso, EficienciaCamaraCalculador.Momento(inicio.AddHours(4), inicio, fin));
        Assert.Equal(MomentoTurno.Terminado, EficienciaCamaraCalculador.Momento(fin, inicio, fin));

        // A las 01:00 (mitad del turno de noche) corresponde la mitad del objetivo.
        Assert.Equal(4000m, EficienciaCamaraCalculador.ObjetivoHastaAhora(8000m, inicio.AddHours(4), inicio, fin));
        Assert.Null(EficienciaCamaraCalculador.ObjetivoHastaAhora(8000m, fin.AddMinutes(1), inicio, fin));
        Assert.Null(EficienciaCamaraCalculador.ObjetivoHastaAhora(null, inicio.AddHours(4), inicio, fin));
    }

    // ---------- Paradas ----------

    [Fact]
    public void MinutosParada_RecortaALaVentanaDelTurno()
    {
        var desde = new DateTimeOffset(2026, 11, 3, 5, 0, 0, TimeSpan.FromHours(-3));
        var hasta = desde.AddHours(8);
        var paradas = new[]
        {
            (desde.AddMinutes(-10), desde.AddMinutes(5)),   // viene del turno de noche: cuentan 5
            (desde.AddHours(1), desde.AddHours(1).AddMinutes(12)), // 12
            (hasta.AddMinutes(-3), hasta.AddMinutes(20)),   // sigue en el turno siguiente: cuentan 3
            (hasta.AddHours(1), hasta.AddHours(2))          // afuera
        };

        Assert.Equal(20, EficienciaCamaraCalculador.MinutosParadaEnVentana(paradas, desde, hasta));
    }

    // ---------- Unidad y validaciones ----------

    [Theory]
    [InlineData("MANGAS", 4, "mangas")]
    [InlineData("CARRILES", 1, "rollos")]
    [InlineData("carriles", 0, "rollos")]
    [InlineData(null, 4, "rollos")]
    [InlineData(null, 1, "mangas")]
    [InlineData(null, 0, "unidades")]
    public void Unidad_SegunMetodo_OSeDeduceDeLosCarriles(string? metodo, int carriles, string esperada)
    {
        Assert.Equal(esperada, EficienciaCamaraCalculador.Unidad(metodo, carriles));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1320.5, true)]
    [InlineData(0.0, false)]
    [InlineData(-5.0, false)]
    [InlineData(100001.0, false)]
    public void MotivoObjetivoInvalido(double? valor, bool valido)
    {
        var motivo = EficienciaCamaraCalculador.MotivoObjetivoInvalido(valor is { } v ? (decimal)v : null);
        Assert.Equal(valido, motivo == null);
    }

    [Theory]
    [InlineData(null, true, null)]
    [InlineData("  ", true, null)]
    [InlineData("mangas", true, "MANGAS")]
    [InlineData("CARRILES", true, "CARRILES")]
    [InlineData("OTRO", false, "OTRO")]
    public void TryNormalizarMetodo(string? metodo, bool valido, string? normalizado)
    {
        Assert.Equal(valido, EficienciaCamaraCalculador.TryNormalizarMetodo(metodo, out var r));
        Assert.Equal(normalizado, r);
    }
}
