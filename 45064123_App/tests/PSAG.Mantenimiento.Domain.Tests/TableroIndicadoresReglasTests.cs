using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-770 iteración 2 · Reglas de los 4 indicadores de Ferretti: semáforo de eficiencia (65 / 85), objetivo del turno
/// completo con cambio de producto, velocidad minuto a minuto (dos metrajes, parada en 0, sin datos null), minutos
/// pendientes desde la parada de cámara, detenciones por tipo de falla y alcance por línea.
/// </summary>
public class TableroIndicadoresReglasTests
{
    private static DateTimeOffset H(int hora, int minuto = 0) => new(2026, 11, 23, hora, minuto, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(null, "SIN_DATO")]
    [InlineData(0.0, "ROJO")]
    [InlineData(64.9, "ROJO")]
    [InlineData(65.0, "AMARILLO")]
    [InlineData(68.0, "AMARILLO")]
    [InlineData(85.0, "AMARILLO")]
    [InlineData(85.1, "VERDE")]
    [InlineData(120.0, "VERDE")]
    public void Eficiencia_Semaforo(double? porcentaje, string esperado) =>
        Assert.Equal(esperado, TableroIndicadoresReglas.NivelEficiencia(porcentaje is { } p ? (decimal)p : null));

    // T-805: el objetivo del turno completo es ObjetivoTurnoReglas (ver ObjetivoTurnoReglasTests).

    [Fact]
    public void Porcentaje_SoloConLosDos()
    {
        Assert.Equal(49m, TableroIndicadoresReglas.Porcentaje(4116, 8400));
        Assert.Null(TableroIndicadoresReglas.Porcentaje(null, 8400));
        Assert.Null(TableroIndicadoresReglas.Porcentaje(100, null));
        Assert.Null(TableroIndicadoresReglas.Porcentaje(100, 0));
    }

    [Fact]
    public void Velocidad_PorMinuto_DosMetrajes_ParadaEnCero_YSinDatosNull()
    {
        const int cam = 7;
        var minutos = new List<MinutoConteo>
        {
            new(cam, H(5, 0), 10, 60), new(cam, H(5, 0), 10, 60),   // dos carriles: 20 mangas
            new(cam, H(5, 1), 0, 0),                                // parada: fila con 0
            // 05:02 sin fila → sin datos
            new(cam, H(5, 3), 13, 60),                              // ya es SH 50
            new(99, H(5, 3), 500, 60)                               // otra cámara: no suma
        };
        var tramos = new[] { new TramoMetraje(H(5), H(5, 3), 30m), new TramoMetraje(H(5, 3), H(13), 50m) };

        var serie = TableroIndicadoresReglas.VelocidadPorMinuto(minutos, cam, tramos, H(5).AddSeconds(20), H(5, 4));

        Assert.Equal([600m, 0m, null, 650m], serie);
    }

    [Fact]
    public void Velocidad_SinMetrajeOSinCamara_EsNull()
    {
        var minutos = new List<MinutoConteo> { new(1, H(5), 20, 60) };
        Assert.Equal([null], TableroIndicadoresReglas.VelocidadPorMinuto(minutos, 1, [new TramoMetraje(H(5), H(6), null)], H(5), H(5, 1)));
        Assert.Equal([null], TableroIndicadoresReglas.VelocidadPorMinuto(minutos, null, [new TramoMetraje(H(5), H(6), 30)], H(5), H(5, 1)));
        Assert.Empty(TableroIndicadoresReglas.VelocidadPorMinuto(minutos, 1, [], H(6), H(5)));
    }

    [Fact]
    public void MinutosPendiente_DesdeLaParadaDeCamara()
    {
        Assert.Equal(10, TableroIndicadoresReglas.MinutosPendiente(H(14), H(14, 10), H(17)));    // arranque detectado
        Assert.Equal(180, TableroIndicadoresReglas.MinutosPendiente(H(14), null, H(17)));       // sigue parada: hasta ahora
        Assert.Equal(0, TableroIndicadoresReglas.MinutosPendiente(null, null, H(17)));          // sin hora de parada
        Assert.Equal(0, TableroIndicadoresReglas.MinutosPendiente(H(17), null, H(16)));         // nunca negativo
        Assert.Equal(3, TableroIndicadoresReglas.MinutosPendiente(H(14), H(14, 2).AddSeconds(30), H(17)));
    }

    [Fact]
    public void DetencionesPorTipo_BarrasDelMaestro_SinDeclarar_YTotalQueCuenta()
    {
        var tipos = new[]
        {
            new TipoFallaTablero(2, "FALLA EQUIPO", 2, true, true),
            new TipoFallaTablero(1, "FALLA PROCESO", 1, true, true),
            new TipoFallaTablero(3, "PARADA PROGRAMADA", 3, false, true),
            new TipoFallaTablero(4, "VIEJO", 4, true, false)       // inactivo sin detenciones: no va
        };
        var detenciones = new[]
        {
            new DetencionTablero(1, 12, false, true),
            new DetencionTablero(1, 8, false, true),
            new DetencionTablero(2, 5, false, true),
            new DetencionTablero(3, 30, false, false),
            new DetencionTablero(null, 10, true, true),
            new DetencionTablero(null, 10, true, true)
        };

        var r = TableroIndicadoresReglas.DetencionesPorTipo(detenciones, tipos);

        Assert.Equal(["FALLA PROCESO|20|2", "FALLA EQUIPO|5|1", "PARADA PROGRAMADA|30|1", "Sin declarar|20|2"],
            r.Barras.Select(b => $"{b.Nombre}|{b.Minutos}|{b.Cantidad}"));
        Assert.True(r.Barras[^1].SinDeclarar);
        Assert.False(r.Barras[2].Cuenta);
        Assert.Equal(45, r.MinutosTotal);
        Assert.Equal(20, r.MinutosPendientes);
        Assert.Equal(2, r.CantidadPendientes);
        Assert.Equal(30, r.MinutosNoCuentan);

        // Sin detenciones: los tipos activos en 0 y "Sin declarar" en 0
        var vacio = TableroIndicadoresReglas.DetencionesPorTipo([], tipos);
        Assert.Equal(4, vacio.Barras.Count);
        Assert.All(vacio.Barras, b => Assert.Equal(0, b.Minutos));
        Assert.Equal(0, vacio.MinutosTotal);

        // Declarada sin tipo (o de un tipo que no está en el maestro): barra "Sin tipo"
        var sinTipo = TableroIndicadoresReglas.DetencionesPorTipo([new DetencionTablero(null, 7, false, true)], tipos);
        Assert.Contains(sinTipo.Barras, b => b.Nombre == "Sin tipo" && b.Minutos == 7);
        Assert.Equal(7, sinTipo.MinutosTotal);
    }

    [Fact]
    public void Cifras_PendientesDeclaradasTotalesYMicrocortes_ConDatosMixtos()
    {
        var detenciones = new[]
        {
            new DetencionTablero(1, 12, false, true),     // declarada (manual o de cámara) que cuenta
            new DetencionTablero(1, 8, false, true),
            new DetencionTablero(null, 5, false, true),   // declarada sin tipo: cuenta
            new DetencionTablero(3, 30, false, false),    // programada: no cuenta, aparte
            new DetencionTablero(null, 10, true, true),   // pendiente (minutos desde la parada de cámara)
            new DetencionTablero(null, 7, true, true)
        };

        var c = TableroIndicadoresReglas.Cifras(detenciones, cantidadMicrocortes: 3, segundosMicrocortes: 150);

        Assert.Equal(new CifraDetenciones(2, 17), c.Pendientes);
        Assert.Equal(new CifraDetenciones(3, 25), c.Declaradas);
        Assert.Equal(new CifraDetenciones(5, 42), c.Totales);              // = declaradas + pendientes
        Assert.Equal(new CifraDetenciones(3, 3), c.Microcortes);           // 150 s → 3 min; NO suman en totales
        Assert.Equal(new CifraDetenciones(1, 30), c.Programadas);          // fuera de totales
        Assert.Equal(c.Declaradas.Minutos + c.Pendientes.Minutos, c.Totales.Minutos);

        // Coincide con el total del gráfico por tipo (las barras que cuentan, incluida "Sin declarar")
        var porTipo = TableroIndicadoresReglas.DetencionesPorTipo(detenciones, [new TipoFallaTablero(1, "FALLA PROCESO", 1, true, true),
            new TipoFallaTablero(3, "PARADA PROGRAMADA", 3, false, true)]);
        Assert.Equal(porTipo.MinutosTotal, c.Totales.Minutos);
        Assert.Equal(porTipo.MinutosNoCuentan, c.Programadas.Minutos);

        var vacio = TableroIndicadoresReglas.Cifras([], 0, 0);
        Assert.Equal(default, vacio.Totales);
        Assert.Equal(default, vacio.Microcortes);

        // Microcortes de pocos segundos: 1 microcorte de 20 s = 0 min (la cantidad igual se ve)
        Assert.Equal(new CifraDetenciones(1, 0), TableroIndicadoresReglas.Cifras([], 1, 20).Microcortes);
    }

    [Theory]
    [InlineData(RolesSistema.Administrador, new int[] { 2 }, 1, true)]
    [InlineData(RolesSistema.Mantenimiento, new int[] { 2 }, 1, true)]
    [InlineData(RolesSistema.Supervisor, new int[] { 2 }, 1, false)]
    [InlineData(RolesSistema.Supervisor, new int[] { 1, 2 }, 1, true)]
    [InlineData(RolesSistema.Supervisor, new int[0], 1, true)]       // sin líneas asignadas: todas
    [InlineData(RolesSistema.Gerencia, new int[0], 1, true)]
    public void AlcanzaLinea_ReglaDeT802(string rol, int[] lineas, int linea, bool esperado) =>
        Assert.Equal(esperado, TableroIndicadoresReglas.AlcanzaLinea([rol], lineas, linea));
}
