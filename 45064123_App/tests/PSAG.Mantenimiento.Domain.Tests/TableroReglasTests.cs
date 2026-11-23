using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-770 · Reglas del tablero del operador: quién sigue un turno en línea, "sin datos" y mangas por hora.</summary>
public class TableroReglasTests
{
    private static readonly TimeZoneInfo Planta = TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");
    private static DateTimeOffset H(int hora, int minuto = 0) => new(2026, 11, 23, hora, minuto, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(true, true, 10L, 10L, true)]     // obligado, su turno
    [InlineData(true, true, 10L, 11L, false)]    // obligado, otro turno
    [InlineData(true, true, null, 10L, false)]   // obligado sin turno
    [InlineData(true, false, null, 11L, true)]   // supervisor: cualquier turno
    [InlineData(false, false, null, 11L, false)] // sin permiso
    public void PuedeSeguir(bool permiso, bool obligado, long? propio, long pedido, bool esperado) =>
        Assert.Equal(esperado, TableroReglas.PuedeSeguir(permiso, obligado, propio, pedido));

    [Fact]
    public void GrupoTurno() => Assert.Equal("turno-42", TableroReglas.GrupoTurno(42));

    [Fact]
    public void SinDatos_MasDe5MinutosConElTurnoAbierto()
    {
        Assert.False(TableroReglas.SinDatos(true, H(10, 0), H(10, 5)));
        Assert.True(TableroReglas.SinDatos(true, H(10, 0), H(10, 6)));
        Assert.True(TableroReglas.SinDatos(true, null, H(10, 6)));
        Assert.False(TableroReglas.SinDatos(false, null, H(10, 6)));   // turno cerrado: no se avisa
        Assert.Equal(6, TableroReglas.MinutosDesde(H(10, 0), H(10, 6).AddSeconds(30)));
        Assert.Null(TableroReglas.MinutosDesde(null, H(10, 6)));
        Assert.Equal(0, TableroReglas.MinutosDesde(H(10, 7), H(10, 6)));
    }

    [Fact]
    public void MangasPorHora_ObjetivoProrrateadoPorTramo_YRealDeLaCamara()
    {
        // Turno 05:00, ahora 07:30. Producto A 05:00–06:30 (480 mangas en 480 min = 1/min), B desde 06:30 sin objetivo.
        var tramos = new[]
        {
            new TramoObjetivo(H(5), H(6, 30), 480, 480),
            new TramoObjetivo(H(6, 30), H(7, 30), null, 480)
        };
        var minutos = new[]
        {
            new MinutoConteo(1, H(5, 10), 30m, 60), new MinutoConteo(1, H(5, 11), 25m, 60),
            new MinutoConteo(2, H(5, 12), 999m, 60),                // otra cámara: no suma
            new MinutoConteo(1, H(7, 5), 40m, 60)
        };

        var horas = TableroReglas.MangasPorHora(minutos, 1, tramos, H(5), H(7, 30), H(7, 30), Planta);

        Assert.Equal([H(5), H(6), H(7)], horas.Select(h => h.Hora));
        Assert.Equal(60m, horas[0].Objetivo);
        Assert.Equal(55m, horas[0].Real);
        Assert.Equal(2, horas[0].MinutosConDatos);
        Assert.Equal(30m, horas[1].Objetivo);   // sólo la media hora de A
        Assert.Equal(0m, horas[1].Real);
        Assert.Equal(0, horas[1].MinutosConDatos);
        Assert.Null(horas[2].Objetivo);         // B sin objetivo: "sin objetivo", nunca 0
        Assert.Equal(40m, horas[2].Real);
        Assert.True(horas[2].EnCurso);
        Assert.False(horas[0].EnCurso);
    }

    [Fact]
    public void MangasPorHora_SinCamaraOTurnoVacio()
    {
        Assert.Empty(TableroReglas.MangasPorHora([], 1, [], H(5), H(5), H(5), Planta));
        var horas = TableroReglas.MangasPorHora([], null, [new TramoObjetivo(H(5), H(6), 480, 480)], H(5, 20), H(6), H(5, 59), Planta);
        var h = Assert.Single(horas);
        Assert.Equal(H(5), h.Hora);
        Assert.Equal(40m, h.Objetivo);   // 05:20–06:00
        Assert.Equal(0, h.MinutosConDatos);
    }
}
