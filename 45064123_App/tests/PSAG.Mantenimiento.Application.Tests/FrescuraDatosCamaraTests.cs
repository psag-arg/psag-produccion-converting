using PSAG.Mantenimiento.Application.Common;

namespace PSAG.Mantenimiento.Application.Tests;

/// <summary>"Datos hasta las HH:mm" y aviso de datos viejos del Dashboard (reunión 17/11/2026).</summary>
public class FrescuraDatosCamaraTests
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);
    private static DateTimeOffset A(int hora, int minuto, int segundo = 0) => new(2026, 11, 17, hora, minuto, segundo, Planta);

    [Fact]
    public void DatosHasta_EsElFinDelUltimoMinutoCerrado()
    {
        // El minuto 10:15 cubre de 10:15:00 a 10:16:00: hay datos hasta las 10:16.
        Assert.Equal(A(10, 16), FrescuraDatosCamara.DatosHasta(A(10, 15), A(10, 17, 40)));
    }

    [Fact]
    public void DatosHasta_SinDatos_EsNull()
    {
        Assert.Null(FrescuraDatosCamara.DatosHasta(null, A(10, 0)));
        Assert.Null(FrescuraDatosCamara.MinutosSinDatosNuevos(null, A(10, 0)));
        Assert.Equal(EstadoDatosCamara.SinDatos, FrescuraDatosCamara.Estado(null, A(10, 0)));
    }

    [Fact]
    public void DatosHasta_NuncaPasaElUltimoMinutoEnteroDeAhora()
    {
        // Reloj del servidor de la cámara adelantado: llegó el minuto 10:17 cuando en la API son las 10:17:20.
        Assert.Equal(A(10, 17), FrescuraDatosCamara.DatosHasta(A(10, 17), A(10, 17, 20)));
    }

    [Fact]
    public void DatosHasta_IgnoraSegundosDelMinutoGuardado()
    {
        Assert.Equal(A(10, 16), FrescuraDatosCamara.DatosHasta(A(10, 15, 30), A(10, 20)));
    }

    [Fact]
    public void DatosHasta_ConservaLaZonaDelMinuto_AunqueAhoraVengaEnUtc()
    {
        var hasta = FrescuraDatosCamara.DatosHasta(A(10, 15), A(10, 17).ToUniversalTime());
        Assert.Equal(Planta, hasta!.Value.Offset);
        Assert.Equal(A(10, 16), hasta);
    }

    [Fact]
    public void ComparacionDelMailDeCalidad_ElPanelVaDosMinutosAtras()
    {
        // Máquina a las 11:19 (79 min desde las 10:00), el sistema con datos hasta las 11:16 (76 min): no es una
        // diferencia de conteo, es que el panel va atrás. Con "datos hasta" se compara contra el contador a las 11:16.
        var hasta = FrescuraDatosCamara.DatosHasta(A(11, 15), A(11, 19))!.Value;
        Assert.Equal(76, (hasta - A(10, 0)).TotalMinutes);
        Assert.Equal(3, FrescuraDatosCamara.MinutosSinDatosNuevos(hasta, A(11, 19)));
        Assert.Equal(EstadoDatosCamara.AlDia, FrescuraDatosCamara.Estado(hasta, A(11, 19)));
    }

    [Theory]
    [InlineData(10, 16, 0, EstadoDatosCamara.AlDia)]   // al día
    [InlineData(10, 18, 0, EstadoDatosCamara.AlDia)]   // atraso normal (1-2 min)
    [InlineData(10, 21, 59, EstadoDatosCamara.AlDia)]  // 5 min y pico: todavía no pasa el umbral
    [InlineData(10, 22, 0, EstadoDatosCamara.Atrasado)] // 6 min sin minutos nuevos
    [InlineData(13, 0, 0, EstadoDatosCamara.Atrasado)]
    public void Estado_AtrasadoConMasDeCincoMinutosSinDatosNuevos(int hora, int minuto, int segundo, string esperado)
    {
        Assert.Equal(esperado, FrescuraDatosCamara.Estado(A(10, 16), A(hora, minuto, segundo)));
    }

    [Fact]
    public void Estado_UmbralConfigurable()
    {
        Assert.Equal(EstadoDatosCamara.Atrasado, FrescuraDatosCamara.Estado(A(10, 16), A(10, 19), umbralMinutos: 2));
        Assert.Equal(EstadoDatosCamara.AlDia, FrescuraDatosCamara.Estado(A(10, 16), A(10, 30), umbralMinutos: 15));
    }

    [Fact]
    public void MinutosSinDatosNuevos_NuncaNegativo()
    {
        Assert.Equal(0, FrescuraDatosCamara.MinutosSinDatosNuevos(A(10, 16), A(10, 15)));
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(10, 10)]
    [InlineData(0, 2)]
    [InlineData(-3, 2)]
    [InlineData(1000, 120)]
    public void UmbralValido_Acotado(int? pedido, int esperado)
    {
        Assert.Equal(esperado, FrescuraDatosCamara.UmbralValido(pedido));
    }
}
