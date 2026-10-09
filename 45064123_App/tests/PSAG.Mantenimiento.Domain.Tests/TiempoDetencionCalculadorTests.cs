using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TiempoDetencionCalculadorTests
{
    private static readonly DateOnly Fecha = new(2026, 8, 20);
    private static readonly TimeZoneInfo Argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private static readonly DateTimeOffset Parada = new(2026, 8, 20, 5, 40, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Arranque = new(2026, 8, 20, 8, 5, 0, TimeSpan.FromHours(-3));

    private static readonly TiempoDetencion Calculada = new(OrigenTiempo.CALCULADO, 145, null, Parada, Arranque);

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

    [Fact]
    public void EditarSoloLaDescripcion_ConservaUnaCalculada()
    {
        // Lo que manda el front al editar sin tocar las horas: sin horas, origen CALCULADO, minutos declarados viejos.
        var edicion = TiempoDetencionCalculador.Editar(
            Calculada,
            Solicitud(origen: OrigenTiempo.CALCULADO, minutosDeclarados: 999),
            Argentina);

        Assert.Equal(Calculada, edicion.Tiempo);
        Assert.False(edicion.RederivarTurno);
    }

    [Fact]
    public void EditarSoloLaDescripcion_ConservaUnaPlc()
    {
        var plc = new TiempoDetencion(OrigenTiempo.PLC, 60, null, Parada, Parada.AddMinutes(60));

        var edicion = TiempoDetencionCalculador.Editar(plc, Solicitud(origen: OrigenTiempo.PLC), Argentina);

        Assert.Equal(plc, edicion.Tiempo);
        Assert.False(edicion.RederivarTurno);
    }

    [Fact]
    public void EditarUnaDeclarada_ActualizaLosMinutos()
    {
        var declarada = new TiempoDetencion(OrigenTiempo.DECLARADO, 25, 25, null, null);

        var edicion = TiempoDetencionCalculador.Editar(declarada, Solicitud(minutosDeclarados: 40), Argentina);

        Assert.Equal(OrigenTiempo.DECLARADO, edicion.Tiempo.Origen);
        Assert.Equal(40, edicion.Tiempo.MinutosEfectivos);
        Assert.Equal(40, edicion.Tiempo.MinutosDeclarados);
        Assert.False(edicion.RederivarTurno);
    }

    [Fact]
    public void EditarConHorasNuevas_RecalculaYRederivaElTurno()
    {
        var edicion = TiempoDetencionCalculador.Editar(
            Calculada,
            Solicitud(horaInicio: new TimeOnly(6, 0), horaFin: new TimeOnly(6, 30)),
            Argentina);

        Assert.Equal(OrigenTiempo.CALCULADO, edicion.Tiempo.Origen);
        Assert.Equal(30, edicion.Tiempo.MinutosEfectivos);
        Assert.True(edicion.RederivarTurno);
    }

    [Fact]
    public void EditarConLasMismasHoras_NoRederivaElTurno()
    {
        // Mismo horario que ya tiene: el turno materializado no se toca aunque el turno vigente haya cambiado.
        var edicion = TiempoDetencionCalculador.Editar(
            Calculada,
            Solicitud(horaInicio: new TimeOnly(5, 40), horaFin: new TimeOnly(8, 5)),
            Argentina);

        Assert.Equal(145, edicion.Tiempo.MinutosEfectivos);
        Assert.False(edicion.RederivarTurno);
    }

    [Fact]
    public void EditarDeclaradaConHoras_PasaACalculadaYRederivaElTurno()
    {
        var declarada = new TiempoDetencion(OrigenTiempo.DECLARADO, 25, 25, null, null);

        var edicion = TiempoDetencionCalculador.Editar(
            declarada,
            Solicitud(horaInicio: new TimeOnly(5, 40), horaFin: new TimeOnly(8, 5)),
            Argentina);

        Assert.Equal(OrigenTiempo.CALCULADO, edicion.Tiempo.Origen);
        Assert.Equal(145, edicion.Tiempo.MinutosEfectivos);
        Assert.Null(edicion.Tiempo.MinutosDeclarados);
        Assert.True(edicion.RederivarTurno);
    }

    [Fact]
    public void EditarConFechasExplicitas_LasTomaTalCualSinRederivarElTurno()
    {
        var nuevaParada = Parada.AddHours(1);
        var nuevaArranque = nuevaParada.AddMinutes(20);

        var edicion = TiempoDetencionCalculador.Editar(
            Calculada,
            Solicitud(origen: OrigenTiempo.CALCULADO, parada: nuevaParada, arranque: nuevaArranque),
            Argentina);

        Assert.Equal(20, edicion.Tiempo.MinutosEfectivos);
        Assert.Equal(nuevaParada, edicion.Tiempo.FechaHoraParada);
        Assert.False(edicion.RederivarTurno);
    }
}
