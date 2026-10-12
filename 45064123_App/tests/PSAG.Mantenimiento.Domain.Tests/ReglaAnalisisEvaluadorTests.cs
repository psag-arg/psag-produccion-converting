using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class ReglaAnalisisEvaluadorTests
{
    private const int Linea = 1;
    private const int Equipo = 10;
    private const int TipoFalla = 100;

    private static ReglaAnalisis Regla(
        int id,
        int minutosMinimos = 30,
        int? lineaId = Linea,
        int tipoFallaId = TipoFalla,
        int? equipoId = Equipo,
        bool activo = true) => new()
    {
        ReglaAnalisisId = id,
        LineaId = lineaId,
        TipoFallaId = tipoFallaId,
        EquipoId = equipoId,
        MinutosMinimos = minutosMinimos,
        ResponsableRol = "MANTENIMIENTO",
        Activo = activo
    };

    [Fact]
    public void SinReglas_DevuelveNull()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([], Linea, Equipo, TipoFalla, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void ReglaQueCoincide_SeDevuelve()
    {
        var regla = Regla(1, minutosMinimos: 30);

        var resultado = ReglaAnalisisEvaluador.Evaluar([regla], Linea, Equipo, TipoFalla, 30);

        Assert.Same(regla, resultado);
    }

    [Fact]
    public void MinutosPorDebajoDelMinimo_NoAplica()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1, minutosMinimos: 30)], Linea, Equipo, TipoFalla, 29);

        Assert.Null(resultado);
    }

    [Fact]
    public void ReglaInactiva_SeIgnora()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1, activo: false)], Linea, Equipo, TipoFalla, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void TipoFallaDistinto_NoAplica()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1, tipoFallaId: 999)], Linea, Equipo, TipoFalla, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void DetencionSinTipoFalla_NoAplicaNingunaRegla()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1)], Linea, Equipo, null, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void LineaDistinta_NoAplica()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1, lineaId: 2)], Linea, Equipo, TipoFalla, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void LineaNula_AplicaATodasLasLineas()
    {
        var regla = Regla(1, lineaId: null);

        Assert.Same(regla, ReglaAnalisisEvaluador.Evaluar([regla], 7, Equipo, TipoFalla, 60));
    }

    [Fact]
    public void EquipoNulo_AplicaATodosLosEquipos()
    {
        var regla = Regla(1, equipoId: null);

        Assert.Same(regla, ReglaAnalisisEvaluador.Evaluar([regla], Linea, 999, TipoFalla, 60));
        Assert.Same(regla, ReglaAnalisisEvaluador.Evaluar([regla], Linea, null, TipoFalla, 60));
    }

    [Fact]
    public void EquipoConcretoNoAplicaSiLaDetencionNoTieneEquipo()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1, equipoId: Equipo)], Linea, null, TipoFalla, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void EquipoConcretoDistinto_NoAplica()
    {
        var resultado = ReglaAnalisisEvaluador.Evaluar([Regla(1, equipoId: Equipo)], Linea, 999, TipoFalla, 60);

        Assert.Null(resultado);
    }

    [Fact]
    public void VariasQueCoinciden_GanaLaDeMayorMinutosMinimos()
    {
        var chica = Regla(1, minutosMinimos: 10);
        var grande = Regla(2, minutosMinimos: 60);

        Assert.Same(grande, ReglaAnalisisEvaluador.Evaluar([chica, grande], Linea, Equipo, TipoFalla, 90));
        Assert.Same(grande, ReglaAnalisisEvaluador.Evaluar([grande, chica], Linea, Equipo, TipoFalla, 90));
    }

    [Fact]
    public void EmpateDeMinutos_GanaLaDeMenorId()
    {
        var mayorId = Regla(7, minutosMinimos: 30);
        var menorId = Regla(3, minutosMinimos: 30);

        Assert.Same(menorId, ReglaAnalisisEvaluador.Evaluar([mayorId, menorId], Linea, Equipo, TipoFalla, 60));
    }

    [Fact]
    public void LaInactivaConMayorMinimo_NoGana()
    {
        var inactiva = Regla(1, minutosMinimos: 60, activo: false);
        var activa = Regla(2, minutosMinimos: 10);

        Assert.Same(activa, ReglaAnalisisEvaluador.Evaluar([inactiva, activa], Linea, Equipo, TipoFalla, 90));
    }

    [Fact]
    public void BuscarDuplicada_EncuentraLaReglaActivaIgual()
    {
        var existente = new ReglaAnalisis { ReglaAnalisisId = 1, LineaId = 7, TipoFallaId = 3, MinutosMinimos = 60, Activo = true };

        Assert.Same(existente, ReglaAnalisisEvaluador.BuscarDuplicada([existente], 7, null, 3, 60));
    }

    [Fact]
    public void BuscarDuplicada_IgnoraLaQueSeEditaLasInactivasYLasDistintas()
    {
        var reglas = new[]
        {
            new ReglaAnalisis { ReglaAnalisisId = 1, LineaId = 7, TipoFallaId = 3, MinutosMinimos = 60, Activo = true },
            new ReglaAnalisis { ReglaAnalisisId = 2, LineaId = 7, TipoFallaId = 3, MinutosMinimos = 60, Activo = false },
            new ReglaAnalisis { ReglaAnalisisId = 3, LineaId = null, TipoFallaId = 3, MinutosMinimos = 60, Activo = true },
        };

        Assert.Null(ReglaAnalisisEvaluador.BuscarDuplicada(reglas, 7, null, 3, 60, reglaAnalisisIdExcluida: 1));
        Assert.Null(ReglaAnalisisEvaluador.BuscarDuplicada(reglas, 7, 5, 3, 60));
        Assert.Null(ReglaAnalisisEvaluador.BuscarDuplicada(reglas, 7, null, 3, 90));
    }

    [Fact]
    public void Motivo_SinTipoDeFalla()
    {
        var motivo = ReglaAnalisisEvaluador.MotivoSinAnalisis([Regla(1)], Linea, Equipo, null, null, 500);

        Assert.Equal("No requiere análisis: no tiene tipo de falla", motivo);
    }

    [Fact]
    public void Motivo_TipoSinReglaActivaParaSuLinea()
    {
        var reglas = new[] { Regla(1, activo: false), Regla(2, lineaId: 99), Regla(3, tipoFallaId: 7) };

        var motivo = ReglaAnalisisEvaluador.MotivoSinAnalisis(reglas, Linea, Equipo, TipoFalla, "CAMBIO DE FTO.", 500);

        Assert.Equal("No requiere análisis: «CAMBIO DE FTO.» no tiene regla de análisis", motivo);
    }

    [Fact]
    public void Motivo_DuraMenosQueLaReglaMasBaja()
    {
        var reglas = new[] { Regla(1, minutosMinimos: 120), Regla(2, minutosMinimos: 60, equipoId: null) };

        var motivo = ReglaAnalisisEvaluador.MotivoSinAnalisis(reglas, Linea, Equipo, TipoFalla, "FALLA EQUIPO", 25);

        Assert.Equal("No requiere análisis: dura 25 min y la regla de FALLA EQUIPO pide 60 o más", motivo);
    }

    [Fact]
    public void Motivo_ReglaQueHoyAplica_DiceQueNoExistiaAlCargarla()
    {
        var motivo = ReglaAnalisisEvaluador.MotivoSinAnalisis([Regla(1, minutosMinimos: 30)], Linea, Equipo, TipoFalla, "FALLA EQUIPO", 45);

        Assert.Equal("No requiere análisis: cuando se cargó no había una regla de FALLA EQUIPO que le aplicara", motivo);
    }
}
