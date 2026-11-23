using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Services;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>T-770 · Tablero del operador: colores de estado, formatos, "sin datos" (nunca 0) y a dónde cae después del turno.</summary>
public class TableroUiTests
{
    private static ComparacionProduccionDto Ok(decimal p) => new() { Estado = "OK", Porcentaje = p, Real = 1, Objetivo = 1 };

    [Theory]
    [InlineData(100, TableroUi.Nivel.Bien)]
    [InlineData(95, TableroUi.Nivel.Bien)]
    [InlineData(90, TableroUi.Nivel.Atencion)]
    [InlineData(84.9, TableroUi.Nivel.Mal)]
    public void Eficiencia_ColorPorUmbral(decimal porcentaje, TableroUi.Nivel esperado) =>
        Assert.Equal(esperado, TableroUi.NivelEficiencia(Ok(porcentaje)));

    [Fact]
    public void Eficiencia_SinDatosOSinObjetivo_EsGrisYNunca0()
    {
        var sinDatos = new ComparacionProduccionDto { Estado = "SIN_DATOS" };
        var sinObjetivo = new ComparacionProduccionDto { Estado = "SIN_OBJETIVO", Real = 100 };
        Assert.Equal(TableroUi.Nivel.SinDato, TableroUi.NivelEficiencia(sinDatos));
        Assert.Equal("Sin datos", TableroUi.TextoEficiencia(sinDatos));
        Assert.Equal("Sin objetivo", TableroUi.TextoEficiencia(sinObjetivo));
        Assert.Equal("81 %", TableroUi.TextoEficiencia(Ok(81.4m)));
        Assert.Equal("Sin datos", TableroUi.TextoReal(null));
        Assert.Equal("Sin objetivo", TableroUi.TextoObjetivo(null));
        Assert.Contains("border-secondary", TableroUi.ClaseTarjeta(TableroUi.Nivel.SinDato));
        Assert.Contains("border-danger", TableroUi.ClaseTarjeta(TableroUi.Nivel.Mal));
        Assert.Equal("text-success-emphasis", TableroUi.ClaseNumero(TableroUi.Nivel.Bien));
    }

    [Fact]
    public void Bolsones_SinNadaEsSinDatos_YConDatosRealSobreObjetivo()
    {
        Assert.Equal("Sin datos", TableroUi.TextoBolsones(new ComparacionProduccionDto { Estado = "SIN_DATOS" }));
        Assert.Equal("Sin datos / 150", TableroUi.TextoBolsones(new ComparacionProduccionDto { Objetivo = 150 }));
        Assert.Equal("120 / Sin objetivo", TableroUi.TextoBolsones(new ComparacionProduccionDto { Real = 120 }));
    }

    [Theory]
    [InlineData(null, TableroUi.Nivel.SinDato)]
    [InlineData(5.0, TableroUi.Nivel.Bien)]
    [InlineData(12.0, TableroUi.Nivel.Atencion)]
    [InlineData(20.0, TableroUi.Nivel.Mal)]
    public void Indisponibilidad_Color(double? porcentaje, TableroUi.Nivel esperado) =>
        Assert.Equal(esperado, TableroUi.NivelIndisponibilidad((decimal?)porcentaje));

    [Fact]
    public void DetencionesYPendientes_Color()
    {
        Assert.Equal(TableroUi.Nivel.Bien, TableroUi.NivelDetenciones(0));
        Assert.Equal(TableroUi.Nivel.Atencion, TableroUi.NivelDetenciones(2));
        Assert.Equal(TableroUi.Nivel.Mal, TableroUi.NivelDetenciones(3));
        Assert.Equal(TableroUi.Nivel.Bien, TableroUi.NivelPendientes(0));
        Assert.Equal(TableroUi.Nivel.Mal, TableroUi.NivelPendientes(1));
        Assert.Null(TableroUi.TextoAvisoPendientes(0));
        Assert.Equal("Tenés 1 detención para declarar.", TableroUi.TextoAvisoPendientes(1));
        Assert.Equal("Tenés 3 detenciones para declarar.", TableroUi.TextoAvisoPendientes(3));
    }

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(45, "45 s")]
    [InlineData(60, "1 min")]
    [InlineData(270, "4 min 30 s")]
    [InlineData(3900, "1 h 05 min")]
    public void Duracion(int segundos, string esperado) => Assert.Equal(esperado, TableroUi.TextoDuracion(segundos));

    [Fact]
    public void UltimoDatoDeCamara_YSinDatos()
    {
        var hora = new DateTimeOffset(2026, 11, 23, 14, 32, 5, TimeSpan.FromHours(-3));
        Assert.Equal("Último dato de cámara: 14:32:05", TableroUi.TextoUltimoDato(new CamaraTableroDto { TieneCamara = true, UltimoDato = hora }));
        Assert.Equal("Último dato de cámara: sin datos todavía", TableroUi.TextoUltimoDato(new CamaraTableroDto { TieneCamara = true }));
        Assert.Equal("La línea no tiene cámara de conteo", TableroUi.TextoUltimoDato(new CamaraTableroDto()));

        Assert.Null(TableroUi.TextoSinDatos(new CamaraTableroDto { TieneCamara = true, UltimoDato = hora }));
        Assert.Contains("hace 12 min", TableroUi.TextoSinDatos(new CamaraTableroDto { TieneCamara = true, SinDatos = true, MinutosDesdeUltimoDato = 12 }));
        Assert.Contains("no mandó datos", TableroUi.TextoSinDatos(new CamaraTableroDto { TieneCamara = true, SinDatos = true }));
    }

    [Fact]
    public void MangasPorHora_DiferenciaYColor()
    {
        var h = new MangasHoraDto { Hora = new DateTimeOffset(2026, 11, 23, 6, 0, 0, TimeSpan.FromHours(-3)), Objetivo = 600, Real = 480 };
        Assert.Equal("06:00", TableroUi.TextoHora(h.Hora));
        Assert.Equal("−120", TableroUi.TextoDiferencia(h));
        Assert.Equal(TableroUi.Nivel.Mal, TableroUi.NivelHora(h));
        Assert.Equal(TableroUi.Nivel.Bien, TableroUi.NivelHora(new MangasHoraDto { Objetivo = 100, Real = 96 }));
        Assert.Null(TableroUi.TextoDiferencia(new MangasHoraDto { Real = 10 }));
        Assert.Equal(TableroUi.Nivel.SinDato, TableroUi.NivelHora(new MangasHoraDto { Real = 10 }));
    }

    [Fact]
    public void Encabezado()
    {
        var t = new TableroTurnoDto
        {
            LineaNombre = "Tango 2", TurnoCodigo = "T2", Abierto = true, Estado = "ABIERTO",
            InicioProgramado = new DateTimeOffset(2026, 11, 23, 13, 0, 0, TimeSpan.FromHours(-3)),
            FinProgramado = new DateTimeOffset(2026, 11, 23, 21, 0, 0, TimeSpan.FromHours(-3)),
            ProductoNombre = "SH 30", ProductoDesde = new DateTimeOffset(2026, 11, 23, 13, 5, 0, TimeSpan.FromHours(-3)),
            OperadorNombre = "Juan"
        };
        Assert.Equal("Tango 2 · T2 (13–21) · SH 30 desde 13:05 · Op. Juan", TableroUi.Encabezado(t));
    }

    [Theory]
    [InlineData(null, true, "/produccion/tablero")]
    [InlineData("/", true, "/produccion/tablero")]
    [InlineData("/detenciones", true, "/produccion/tablero")]
    [InlineData("/control-proceso/nuevo", true, "/control-proceso/nuevo")]
    [InlineData(null, false, "/detenciones")]
    [InlineData("/calidad", false, "/calidad")]
    [InlineData("//afuera.com", true, "/produccion/tablero")]
    public void DespuesDeTomarElTurno_ElObligadoCaeEnElTablero(string? volver, bool obligado, string esperado) =>
        Assert.Equal(esperado, RutasLogin.DestinoTrasTurno(volver, obligado));

    [Fact]
    public void RutaDelTablero_ExigeSuOpcion() =>
        Assert.Equal(Opciones.ProduccionTablero, RutasPermiso.OpcionDe("produccion/tablero"));
}
