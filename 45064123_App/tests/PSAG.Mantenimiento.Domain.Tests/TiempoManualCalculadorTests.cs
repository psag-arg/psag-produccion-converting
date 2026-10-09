using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TiempoManualCalculadorTests
{
    private static readonly DateOnly Fecha = new(2026, 8, 20);

    [Fact]
    public void MismoDia_CalculaMinutosEntreInicioYFin()
    {
        var (parada, arranque, minutos) = TiempoManualCalculador.Calcular(
            Fecha, new TimeOnly(5, 40), new TimeOnly(8, 5));

        Assert.Equal(145, minutos);
        Assert.Equal(Fecha.ToDateTime(new TimeOnly(5, 40)), parada.DateTime);
        Assert.Equal(Fecha.ToDateTime(new TimeOnly(8, 5)), arranque.DateTime);
    }

    [Fact]
    public void FinAnteriorAlInicio_TerminaAlDiaSiguiente()
    {
        var (_, arranque, minutos) = TiempoManualCalculador.Calcular(
            Fecha, new TimeOnly(22, 0), new TimeOnly(6, 0));

        Assert.Equal(480, minutos);
        Assert.Equal(Fecha.AddDays(1).ToDateTime(new TimeOnly(6, 0)), arranque.DateTime);
    }

    [Fact]
    public void InicioIgualFin_DaCeroMinutos()
    {
        var (_, _, minutos) = TiempoManualCalculador.Calcular(
            Fecha, new TimeOnly(10, 0), new TimeOnly(10, 0));

        Assert.Equal(0, minutos);
    }
}
