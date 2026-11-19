using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>Reunión 17/11/2026: el maquinista de la detención se puede cambiar y el cambio queda en el historial.</summary>
public class DetencionMaquinistaReglasTests
{
    private static readonly UsuarioLinea DeLaLinea = new() { UsuarioLineaId = 7, UsuarioId = "maq-1", LineaId = 1 };

    [Fact]
    public void Valido_AsignacionDeLaLineaYUsuarioActivo() =>
        Assert.Null(DetencionMaquinistaReglas.MotivoMaquinistaInvalido(DeLaLinea, 1, usuarioActivo: true));

    [Fact]
    public void Invalido_NoExiste() =>
        Assert.Contains("no existe", DetencionMaquinistaReglas.MotivoMaquinistaInvalido(null, 1, true));

    [Fact]
    public void Invalido_DeOtraLinea() =>
        Assert.Contains("no está asignado a la línea", DetencionMaquinistaReglas.MotivoMaquinistaInvalido(DeLaLinea, 2, true));

    [Fact]
    public void Invalido_UsuarioDadoDeBaja() =>
        Assert.Contains("dado de baja", DetencionMaquinistaReglas.MotivoMaquinistaInvalido(DeLaLinea, 1, usuarioActivo: false));

    [Fact]
    public void CambiarMaquinista_DejaHistorialConQuienCuandoAnteriorYNuevo_SinCambiarEstado()
    {
        var d = new Detencion { UsuarioLineaId = 3, EstadoActual = EstadoDetencion.REGISTRADA };
        var antes = DateTimeOffset.UtcNow;

        Assert.True(d.CambiarMaquinista(7, "Juan Pérez", "Ana Gómez", "sup-1", "corr-1"));

        Assert.Equal(7, d.UsuarioLineaId);
        Assert.Equal(EstadoDetencion.REGISTRADA, d.EstadoActual);
        var h = Assert.Single(d.HistorialEstados);
        Assert.Equal(EstadoDetencion.REGISTRADA, h.EstadoAnterior);
        Assert.Equal(EstadoDetencion.REGISTRADA, h.EstadoNuevo);
        Assert.Equal("sup-1", h.UsuarioId);
        Assert.Equal("corr-1", h.CorrelationId);
        Assert.Equal(DetencionMaquinistaReglas.OrigenCambioMaquinista, h.OrigenAccion);
        Assert.InRange(h.FechaHora, antes, DateTimeOffset.UtcNow);
        Assert.StartsWith("Maquinista cambiado: Juan Pérez → Ana Gómez", h.Observacion);
        Assert.Contains("3 → 7", h.Observacion);
    }

    [Fact]
    public void CambiarMaquinista_SinMaquinistaPrevio_LoDiceEnElHistorial()
    {
        var d = new Detencion { EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION };
        Assert.True(d.CambiarMaquinista(7, null, "Ana Gómez", "oper-1"));
        Assert.StartsWith("Maquinista cambiado: sin maquinista → Ana Gómez", d.HistorialEstados.Single().Observacion);
    }

    [Fact]
    public void CambiarMaquinista_AlMismo_NoHaceNada()
    {
        var d = new Detencion { UsuarioLineaId = 7 };
        Assert.False(d.CambiarMaquinista(7, "Ana Gómez", "Ana Gómez", "sup-1"));
        Assert.Empty(d.HistorialEstados);
    }
}
