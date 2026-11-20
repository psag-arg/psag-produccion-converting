using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-786: aviso de superposición al declarar a mano (por ejemplo, después de un corte de energía).</summary>
public class DetencionSuperpuestaTests
{
    private static readonly DateTimeOffset Desde = new(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Hasta = Desde.AddMinutes(40);

    private static Detencion Con(DateTimeOffset? parada, DateTimeOffset? arranque = null, DateTimeOffset? detectado = null, int minutos = 0) => new()
    {
        FechaHoraParada = parada, FechaHoraArranque = arranque, FechaHoraArranqueDetectada = detectado, MinutosEfectivos = minutos,
        EstadoActual = arranque == null ? EstadoDetencion.PENDIENTE_JUSTIFICACION : EstadoDetencion.REGISTRADA
    };

    [Fact]
    public void DeclaradaQueSePisa_Superpone()
    {
        Assert.True(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(-10), Desde.AddMinutes(5)), Desde, Hasta));   // empezó antes, terminó adentro
        Assert.True(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(30), Hasta.AddMinutes(20)), Desde, Hasta));   // empezó adentro
        Assert.True(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(-10), Hasta.AddMinutes(10)), Desde, Hasta));  // la cubre
    }

    [Fact]
    public void Contigua_NoSuperpone()
    {
        Assert.False(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(-30), Desde), Desde, Hasta));   // terminó justo cuando empieza
        Assert.False(DetencionCargaReglas.SeSuperpone(Con(Hasta, Hasta.AddMinutes(5)), Desde, Hasta));     // empieza justo cuando termina
    }

    [Fact]
    public void PendienteDeLaCamara_SinArranque_CuentaElInstanteDeLaParadaOElArranqueQueVio()
    {
        Assert.True(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(15)), Desde, Hasta));                      // creada al reiniciarse
        Assert.False(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(-15)), Desde, Hasta));                    // instante antes
        Assert.True(DetencionCargaReglas.SeSuperpone(Con(Desde.AddMinutes(-15), detectado: Desde.AddMinutes(5)), Desde, Hasta));
    }

    [Fact]
    public void SinHorario_NoSePuedeComparar() =>
        Assert.False(DetencionCargaReglas.SeSuperpone(Con(null, minutos: 30), Desde, Hasta));
}
