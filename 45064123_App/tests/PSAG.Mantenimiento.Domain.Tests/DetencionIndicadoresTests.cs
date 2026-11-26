using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-798 2.7: la regla de qué detenciones suman en indicadores está en un solo lugar.</summary>
public class DetencionIndicadoresTests
{
    private static readonly TipoFalla Cuenta = new() { TipoFallaId = 1, CuentaParaIndicadores = true };
    private static readonly TipoFalla Programada = new() { TipoFallaId = 2, CuentaParaIndicadores = false };

    [Fact]
    public void SinTipoCuenta_ProgramadaNo()
    {
        var lista = new[]
        {
            new Detencion { Codigo = "A", TipoFallaId = 1, TipoFalla = Cuenta, MinutosEfectivos = 10 },
            new Detencion { Codigo = "B", TipoFallaId = 2, TipoFalla = Programada, MinutosEfectivos = 45 },
            new Detencion { Codigo = "C", MinutosEfectivos = 5 }
        };

        Assert.Equal(15, lista.MinutosQueCuentan());
        Assert.Equal(["A", "C"], lista.QueCuentan().Select(d => d.Codigo));
        var expr = DetencionIndicadores.CuentaExpr.Compile();
        Assert.Equal([true, false, true], lista.Select(expr));
    }

    [Fact]
    public void ConTipoSinCargar_FallaEnVezDeContarMal()
    {
        Assert.Throws<InvalidOperationException>(() => DetencionIndicadores.Cuenta(new Detencion { Codigo = "X", TipoFallaId = 3 }));
    }
}
