using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TiempoDetencionCalculadorTests
{
    private static readonly DateOnly Fecha = new(2026, 8, 20);
    private static readonly TimeZoneInfo Argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private static TiempoCargado Solicitud(
        TimeOnly? horaInicio = null,
        TimeOnly? horaFin = null,
        OrigenTiempo? origen = OrigenTiempo.DECLARADO,
        int? minutosDeclarados = null,
        DateTimeOffset? parada = null,
        DateTimeOffset? arranque = null) =>
        new(Fecha, horaInicio, horaFin, origen, minutosDeclarados, parada, arranque);

    [Fact]
    public void ConHorasDeInicioYFin_EsCalculadoConLasHorasDeLaPlanta()
    {
        var tiempo = TiempoDetencionCalculador.Calcular(
            Solicitud(horaInicio: new TimeOnly(5, 40), horaFin: new TimeOnly(8, 5), minutosDeclarados: 999),
            Argentina);

        Assert.Equal(OrigenTiempo.CALCULADO, tiempo.Origen);
        Assert.Equal(145, tiempo.MinutosEfectivos);
        Assert.Null(tiempo.MinutosDeclarados);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 5, 40, 0, TimeSpan.FromHours(-3)), tiempo.FechaHoraParada);
    }

    [Fact]
    public void DeclaradoConMinutos_UsaLosMinutos()
    {
        var tiempo = TiempoDetencionCalculador.Calcular(Solicitud(minutosDeclarados: 25), Argentina);

        Assert.Equal(OrigenTiempo.DECLARADO, tiempo.Origen);
        Assert.Equal(25, tiempo.MinutosEfectivos);
        Assert.Equal(25, tiempo.MinutosDeclarados);
        Assert.Null(tiempo.FechaHoraParada);
    }

    [Fact]
    public void CalculadoConFechasDeParadaYArranque_CalculaLosMinutosDeLasFechas()
    {
        // La previsualización antes ignoraba estas fechas y daba 0 o los minutos declarados.
        var parada = new DateTimeOffset(2026, 8, 20, 5, 0, 0, TimeSpan.FromHours(-3));
        var arranque = parada.AddMinutes(90);

        var tiempo = TiempoDetencionCalculador.Calcular(
            Solicitud(origen: OrigenTiempo.CALCULADO, parada: parada, arranque: arranque), Argentina);

        Assert.Equal(OrigenTiempo.CALCULADO, tiempo.Origen);
        Assert.Equal(90, tiempo.MinutosEfectivos);
        Assert.Equal(parada, tiempo.FechaHoraParada);
        Assert.Equal(arranque, tiempo.FechaHoraArranque);
    }

    [Fact]
    public void CalculadoSinFechas_DaCeroMinutos()
    {
        var tiempo = TiempoDetencionCalculador.Calcular(Solicitud(origen: OrigenTiempo.CALCULADO), Argentina);

        Assert.Equal(0, tiempo.MinutosEfectivos);
    }

    [Fact]
    public void OrigenNoValido_SeTomaComoDeclarado()
    {
        var tiempo = TiempoDetencionCalculador.Calcular(
            Solicitud(origen: null, minutosDeclarados: 40), Argentina);

        Assert.Equal(OrigenTiempo.DECLARADO, tiempo.Origen);
        Assert.Equal(40, tiempo.MinutosEfectivos);
    }

    [Fact]
    public void PlcTodaviaNoCalcula_DaCeroMinutos()
    {
        var tiempo = TiempoDetencionCalculador.Calcular(
            Solicitud(origen: OrigenTiempo.PLC, minutosDeclarados: 40), Argentina);

        Assert.Equal(0, tiempo.MinutosEfectivos);
    }
}
