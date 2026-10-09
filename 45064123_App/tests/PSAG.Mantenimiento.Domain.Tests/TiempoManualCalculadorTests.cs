using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TiempoManualCalculadorTests
{
    private static readonly DateOnly Fecha = new(2026, 8, 20);
    private static readonly TimeZoneInfo Argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    private static readonly TimeZoneInfo Tokio = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    [Fact]
    public void MismoDia_CalculaMinutosEntreInicioYFin()
    {
        var (parada, arranque, minutos) = TiempoManualCalculador.Calcular(
            Fecha, new TimeOnly(5, 40), new TimeOnly(8, 5), Argentina);

        Assert.Equal(145, minutos);
        Assert.Equal(Fecha.ToDateTime(new TimeOnly(5, 40)), parada.DateTime);
        Assert.Equal(Fecha.ToDateTime(new TimeOnly(8, 5)), arranque.DateTime);
    }

    [Fact]
    public void FinAnteriorAlInicio_TerminaAlDiaSiguiente()
    {
        var (_, arranque, minutos) = TiempoManualCalculador.Calcular(
            Fecha, new TimeOnly(22, 0), new TimeOnly(6, 0), Argentina);

        Assert.Equal(480, minutos);
        Assert.Equal(Fecha.AddDays(1).ToDateTime(new TimeOnly(6, 0)), arranque.DateTime);
    }

    [Fact]
    public void HorasDeLaPlanta_TienenElOffsetDeLaZonaNoDelServidor()
    {
        var manual = TiempoManualCalculador.Calcular(Fecha, new TimeOnly(5, 40), new TimeOnly(8, 5), Tokio);

        Assert.Equal(TimeSpan.FromHours(9), manual.Parada.Offset);
        Assert.Equal(TimeSpan.FromHours(9), manual.Arranque.Offset);
        Assert.Equal(145, manual.Minutos);
    }

    [Fact]
    public void HorasDeArgentina_TienenOffsetMenosTres()
    {
        var manual = TiempoManualCalculador.Calcular(Fecha, new TimeOnly(5, 40), new TimeOnly(8, 5), Argentina);

        Assert.Equal(new DateTimeOffset(2026, 8, 20, 5, 40, 0, TimeSpan.FromHours(-3)), manual.Parada);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 8, 5, 0, TimeSpan.FromHours(-3)), manual.Arranque);
    }

    [Fact]
    public void InicioIgualFin_DaCeroMinutos()
    {
        var (_, _, minutos) = TiempoManualCalculador.Calcular(
            Fecha, new TimeOnly(10, 0), new TimeOnly(10, 0), Argentina);

        Assert.Equal(0, minutos);
    }
}
