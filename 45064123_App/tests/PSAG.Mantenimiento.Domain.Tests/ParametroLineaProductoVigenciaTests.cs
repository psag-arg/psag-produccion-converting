using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-798 · Vigencia de PRD_ParametrosLineaProducto: el vigente es el de mayor VigenteDesde &lt;= fecha.
/// </summary>
public class ParametroLineaProductoVigenciaTests
{
    private const int Tango2 = 1;
    private const int Futura2 = 2;
    private const int Sh30 = 10;
    private const int Sh50 = 20;

    private static int _id;

    private static ParametroLineaProducto P(int linea, int producto, int anio, int mes, int dia, bool habilitado = true, int? mangas = null) => new()
    {
        ParametroLineaProductoId = ++_id,
        LineaId = linea,
        ProductoMaestroId = producto,
        VigenteDesde = new DateOnly(anio, mes, dia),
        Habilitado = habilitado,
        MangasObjetivoTurno = mangas
    };

    [Fact]
    public void Vigente_EsElDeMayorVigenteDesdeMenorOIgualALaFecha()
    {
        var parametros = new[]
        {
            P(Tango2, Sh30, 2026, 1, 1),
            P(Tango2, Sh30, 2026, 11, 1, mangas: 11200),
            P(Tango2, Sh30, 2027, 1, 1, mangas: 12000)
        };

        Assert.Equal(11200, ParametroLineaProductoVigencia.Vigente(parametros, Tango2, Sh30, new DateOnly(2026, 11, 15))!.MangasObjetivoTurno);
        Assert.Null(ParametroLineaProductoVigencia.Vigente(parametros, Tango2, Sh30, new DateOnly(2026, 10, 31))!.MangasObjetivoTurno);
        Assert.Equal(12000, ParametroLineaProductoVigencia.Vigente(parametros, Tango2, Sh30, new DateOnly(2027, 3, 1))!.MangasObjetivoTurno);
    }

    [Fact]
    public void Vigente_ElDiaDeInicioYaEsVigente()
    {
        var parametros = new[] { P(Tango2, Sh30, 2026, 1, 1), P(Tango2, Sh30, 2026, 11, 1, mangas: 11200) };

        Assert.Equal(11200, ParametroLineaProductoVigencia.Vigente(parametros, Tango2, Sh30, new DateOnly(2026, 11, 1))!.MangasObjetivoTurno);
    }

    [Fact]
    public void Vigente_AntesDelPrimero_NoHay()
    {
        var parametros = new[] { P(Tango2, Sh30, 2026, 11, 1) };

        Assert.Null(ParametroLineaProductoVigencia.Vigente(parametros, Tango2, Sh30, new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Vigente_NoMezclaLineasNiProductos()
    {
        var parametros = new[]
        {
            P(Tango2, Sh30, 2026, 11, 1, mangas: 11200),
            P(Tango2, Sh50, 2026, 11, 1, mangas: 6720),
            P(Futura2, Sh30, 2026, 11, 10, mangas: 9000)
        };

        Assert.Equal(6720, ParametroLineaProductoVigencia.Vigente(parametros, Tango2, Sh50, new DateOnly(2026, 11, 20))!.MangasObjetivoTurno);
        Assert.Equal(9000, ParametroLineaProductoVigencia.Vigente(parametros, Futura2, Sh30, new DateOnly(2026, 11, 20))!.MangasObjetivoTurno);
        Assert.Null(ParametroLineaProductoVigencia.Vigente(parametros, Futura2, Sh30, new DateOnly(2026, 11, 5)));
        Assert.Equal(3, ParametroLineaProductoVigencia.Vigentes(parametros, new DateOnly(2026, 11, 20)).Count);
    }

    [Fact]
    public void ProductosHabilitados_UnVigenteDeshabilitadoTapaAlAnterior()
    {
        var parametros = new[]
        {
            P(Tango2, Sh30, 2026, 1, 1),
            P(Tango2, Sh30, 2026, 12, 1, habilitado: false),
            P(Tango2, Sh50, 2026, 1, 1)
        };

        Assert.Equal([Sh30, Sh50], ParametroLineaProductoVigencia.ProductosHabilitados(parametros, Tango2, new DateOnly(2026, 11, 30)).Order());
        Assert.Equal([Sh50], ParametroLineaProductoVigencia.ProductosHabilitados(parametros, Tango2, new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void ProductosHabilitados_UnoNuevoHabilitadoApareceDesdeSuFecha()
    {
        var parametros = new[]
        {
            P(Tango2, Sh30, 2026, 1, 1, habilitado: false),
            P(Tango2, Sh30, 2026, 11, 1)
        };

        Assert.Empty(ParametroLineaProductoVigencia.ProductosHabilitados(parametros, Tango2, new DateOnly(2026, 10, 31)));
        Assert.Equal([Sh30], ParametroLineaProductoVigencia.ProductosHabilitados(parametros, Tango2, new DateOnly(2026, 11, 1)));
        Assert.Empty(ParametroLineaProductoVigencia.ProductosHabilitados(parametros, Futura2, new DateOnly(2026, 11, 1)));
    }
}
