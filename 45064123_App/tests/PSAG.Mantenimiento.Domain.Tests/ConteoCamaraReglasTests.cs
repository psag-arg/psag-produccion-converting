using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-610: validación de lo que manda el servicio de conteo y cruce de paradas de cámara con detenciones.</summary>
public class ConteoCamaraReglasTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 28, 10, 15, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void MinutoValido() => Assert.Null(ConteoCamaraReglas.MotivoMinutoInvalido(T, 1, 166.8m, 50, 10, 0, 0));

    [Theory]
    [InlineData(30, 1, 100, 60, 0, 0, 0)]   // segundos distintos de cero
    [InlineData(0, 0, 100, 60, 0, 0, 0)]    // carril 0
    [InlineData(0, 17, 100, 60, 0, 0, 0)]   // carril fuera de rango
    [InlineData(0, 1, -1, 60, 0, 0, 0)]     // rollos negativos
    [InlineData(0, 1, 100, 50, 0, 0, 0)]    // no suma 60
    [InlineData(0, 1, 100, 70, -10, 0, 0)]  // negativo
    public void MinutoInvalido(int segundo, int carril, decimal rollos, int marcha, int parada, int acumulacion, int sinSenal) =>
        Assert.NotNull(ConteoCamaraReglas.MotivoMinutoInvalido(T.AddSeconds(segundo), carril, rollos, marcha, parada, acumulacion, sinSenal));

    [Fact]
    public void ParadaInvalida()
    {
        Assert.Null(ConteoCamaraReglas.MotivoParadaInvalida(T, T.AddMinutes(1), [1, 2], "X"));
        Assert.NotNull(ConteoCamaraReglas.MotivoParadaInvalida(T, T, [1], "X"));
        Assert.NotNull(ConteoCamaraReglas.MotivoParadaInvalida(T, T.AddDays(8), [1], "X"));
        Assert.NotNull(ConteoCamaraReglas.MotivoParadaInvalida(T, T.AddMinutes(1), [], "X"));
        Assert.NotNull(ConteoCamaraReglas.MotivoParadaInvalida(T, T.AddMinutes(1), [1], " "));
        Assert.NotNull(ConteoCamaraReglas.MotivoParadaInvalida(T, T.AddMinutes(1), [99], "X"));
    }

    [Fact]
    public void CarrilesTexto_OrdenadoSinRepetidos() => Assert.Equal("1,2,4", ConteoCamaraReglas.CarrilesTexto([4, 2, 1, 2]));

    [Fact]
    public void IntervaloDetencion_UsaArranqueOMinutosEfectivos()
    {
        var conArranque = new Detencion { FechaHoraParada = T, FechaHoraArranque = T.AddMinutes(7), MinutosEfectivos = 99 };
        var sinArranque = new Detencion { FechaHoraParada = T, MinutosEfectivos = 12 };
        var sinHora = new Detencion { MinutosEfectivos = 12 };

        Assert.Equal((T, T.AddMinutes(7)), ConteoCamaraReglas.IntervaloDetencion(conArranque));
        Assert.Equal((T, T.AddMinutes(12)), ConteoCamaraReglas.IntervaloDetencion(sinArranque));
        Assert.Null(ConteoCamaraReglas.IntervaloDetencion(sinHora));
    }

    private static ParadaCamara Parada(int minutos = 5) =>
        new() { LineaId = 1, TurnoId = 1, FechaHoraInicio = T, FechaHoraFin = T.AddMinutes(minutos) };

    [Theory]
    [InlineData(-20, 16, "CON_DETENCION")]   // termina 1 min después de que arrancó la parada de cámara
    [InlineData(9, 3, "CON_DETENCION")]      // empieza 4 min después del fin: dentro de la tolerancia de 5
    [InlineData(11, 3, "SIN_DETENCION")]     // empieza 6 min después del fin
    [InlineData(-30, 20, "SIN_DETENCION")]   // terminó 10 min antes
    public void Cruce_PorHorarioConTolerancia(int minutosDesdeInicio, int duracion, string esperado)
    {
        var d = new Detencion { Codigo = "D1", LineaId = 1, TurnoId = 1, Fecha = new DateOnly(2026, 10, 28), FechaHoraParada = T.AddMinutes(minutosDesdeInicio), MinutosEfectivos = duracion };
        var (cruce, _) = ConteoCamaraReglas.Cruzar(Parada(), new DateOnly(2026, 10, 28), [d]);
        Assert.Equal(esperado, cruce.ToString());
    }

    [Fact]
    public void Cruce_OtraLineaOEliminada_NoCuenta()
    {
        var otraLinea = new Detencion { Codigo = "D1", LineaId = 2, FechaHoraParada = T, MinutosEfectivos = 5 };
        var eliminada = new Detencion { Codigo = "D2", LineaId = 1, FechaHoraParada = T, MinutosEfectivos = 5, Eliminada = true };
        Assert.Equal(CruceDetencionCamara.SIN_DETENCION, ConteoCamaraReglas.Cruzar(Parada(), new DateOnly(2026, 10, 28), [otraLinea, eliminada]).Cruce);
    }

    [Fact]
    public void Cruce_SinHorarioMismoDiaYTurno_EsPosible()
    {
        var mismoTurno = new Detencion { Codigo = "D1", LineaId = 1, TurnoId = 1, Fecha = new DateOnly(2026, 10, 28), MinutosEfectivos = 5 };
        var otroTurno = new Detencion { Codigo = "D2", LineaId = 1, TurnoId = 2, Fecha = new DateOnly(2026, 10, 28), MinutosEfectivos = 5 };
        var (cruce, codigos) = ConteoCamaraReglas.Cruzar(Parada(), new DateOnly(2026, 10, 28), [mismoTurno, otroTurno]);
        Assert.Equal(CruceDetencionCamara.POSIBLE, cruce);
        Assert.Equal(["D1"], codigos);
    }
}
