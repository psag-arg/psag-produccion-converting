using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// Reunión 17/11/2026: marca de umbral de las paradas y agrupador de microcortes bajo una detención.
/// Caso real del 13/11, cámara T2-CAM10: 11:08:23–11:11:43, 11:12:03–11:14:03 y 11:14:43–11:23:03; sólo la última
/// superó 5 min y generó la detención; las dos primeras son microcortes que se agrupan.
/// </summary>
public class AgrupacionMicrocortesReglasTests
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);
    private const int Linea = 1;
    private const int Cam10 = 10;
    private const int Cam09 = 9;

    private static DateTimeOffset H(int h, int m, int s) => new(2026, 11, 13, h, m, s, Planta);

    private static ParadaCamara Parada(long id, DateTimeOffset inicio, DateTimeOffset fin, bool? supera, int punto = Cam10, int linea = Linea) => new()
    {
        ParadaCamaraId = id,
        LineaId = linea,
        PuntoControlCalidadId = punto,
        FechaHoraInicio = inicio,
        FechaHoraFin = fin,
        DuracionSegundos = (int)(fin - inicio).TotalSeconds,
        SuperaUmbral = supera,
        UmbralSegundos = supera == null ? null : 300,
        Tipo = TipoParadaCamara.SIN_MANGAS
    };

    // Caso real
    private static readonly ParadaCamara P1 = Parada(1, H(11, 8, 23), H(11, 11, 43), false);
    private static readonly ParadaCamara P2 = Parada(2, H(11, 12, 3), H(11, 14, 3), false);
    private static readonly ParadaCamara P3 = Parada(3, H(11, 14, 43), H(11, 23, 3), true);
    private static readonly DateTimeOffset InicioDetencion = H(11, 14, 43);

    // ===================== Marca de umbral (script 31) =====================

    [Theory]
    [InlineData(299, false)]
    [InlineData(300, true)]   // justo en el umbral: supera (inclusive, igual que la detención automática)
    [InlineData(301, true)]
    public void MarcaUmbral_EnMinutos_BordeInclusive(int segundos, bool esperado)
    {
        var punto = new PuntoControlCalidad { MinutosUmbralDetencion = 5 };
        var (supera, umbral) = DetencionCamaraReglas.MarcaUmbral(punto, H(11, 0, 0), H(11, 0, 0).AddSeconds(segundos));
        Assert.Equal(esperado, supera);
        Assert.Equal(300, umbral);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(31, true)]
    public void MarcaUmbral_SegundosMandanSobreMinutos(int segundos, bool esperado)
    {
        var punto = new PuntoControlCalidad { MinutosUmbralDetencion = 5, SegundosUmbralDetencion = 30 };
        var (supera, umbral) = DetencionCamaraReglas.MarcaUmbral(punto, H(11, 0, 0), H(11, 0, 0).AddSeconds(segundos));
        Assert.Equal(esperado, supera);
        Assert.Equal(30, umbral);
    }

    [Fact]
    public void MarcaUmbral_UnMilisegundoAntesDelUmbral_NoSupera()
    {
        var punto = new PuntoControlCalidad { MinutosUmbralDetencion = 5 };
        Assert.False(DetencionCamaraReglas.MarcaUmbral(punto, H(11, 0, 0), H(11, 0, 0).AddSeconds(300).AddMilliseconds(-1)).SuperaUmbral);
    }

    [Fact]
    public void CasoReal_SoloLaUltimaSuperaElUmbral()
    {
        var punto = new PuntoControlCalidad { MinutosUmbralDetencion = 5 };
        Assert.False(DetencionCamaraReglas.MarcaUmbral(punto, P1.FechaHoraInicio, P1.FechaHoraFin).SuperaUmbral);
        Assert.False(DetencionCamaraReglas.MarcaUmbral(punto, P2.FechaHoraInicio, P2.FechaHoraFin).SuperaUmbral);
        Assert.True(DetencionCamaraReglas.MarcaUmbral(punto, P3.FechaHoraInicio, P3.FechaHoraFin).SuperaUmbral);
    }

    // ===================== Microcortes en la ventana =====================

    [Fact]
    public void EsMicrocorte_UsaLaMarcaGuardadaAunqueHoyElUmbralSeaOtro()
    {
        // Guardada como microcorte con umbral 5 min; hoy el umbral es 30 s: sigue siendo microcorte (snapshot).
        Assert.True(AgrupacionMicrocortesReglas.EsMicrocorte(P1, umbralSegundosActual: 30));
        Assert.False(AgrupacionMicrocortesReglas.EsMicrocorte(P3, umbralSegundosActual: 3600));
    }

    [Fact]
    public void EsMicrocorte_SinMarca_ParadaVieja_SeEvaluaConElUmbralActual()
    {
        var vieja = Parada(9, H(11, 0, 0), H(11, 3, 20), supera: null);
        Assert.True(AgrupacionMicrocortesReglas.EsMicrocorte(vieja, 300));
        Assert.False(AgrupacionMicrocortesReglas.EsMicrocorte(vieja, 200));   // 200 s justo: supera
    }

    [Fact]
    public void EnVentana_CasoReal_LasDosPreviasSiLaQueGeneroLaDetencionNo()
    {
        Assert.True(AgrupacionMicrocortesReglas.EnVentana(P1, InicioDetencion));
        Assert.True(AgrupacionMicrocortesReglas.EnVentana(P2, InicioDetencion));
        Assert.False(AgrupacionMicrocortesReglas.EnVentana(P3, InicioDetencion));
    }

    [Theory]
    [InlineData(600, true)]    // justo 10 min antes: entra
    [InlineData(601, false)]   // 10:01 antes: afuera
    [InlineData(1, true)]
    [InlineData(0, false)]     // misma hora que la detención: no es previa
    [InlineData(-30, false)]   // posterior
    public void EnVentana_Bordes(int segundosAntes, bool esperado)
    {
        var p = Parada(5, InicioDetencion.AddSeconds(-segundosAntes), InicioDetencion.AddSeconds(-segundosAntes + 25), false);
        Assert.Equal(esperado, AgrupacionMicrocortesReglas.EnVentana(p, InicioDetencion));
    }

    [Fact]
    public void MismaCamaraOLinea_DetencionDeCamaraExigeLaMismaCamara_ManualCualquieraDeLaLinea()
    {
        var otraCamara = Parada(6, H(11, 10, 0), H(11, 10, 40), false, punto: Cam09);
        var otraLinea = Parada(7, H(11, 10, 0), H(11, 10, 40), false, linea: 2);
        Assert.True(AgrupacionMicrocortesReglas.MismaCamaraOLinea(P1, Linea, Cam10));
        Assert.False(AgrupacionMicrocortesReglas.MismaCamaraOLinea(otraCamara, Linea, Cam10));
        Assert.True(AgrupacionMicrocortesReglas.MismaCamaraOLinea(otraCamara, Linea, null));
        Assert.False(AgrupacionMicrocortesReglas.MismaCamaraOLinea(otraLinea, Linea, null));
    }

    // ===================== Validación al agrupar =====================

    [Fact]
    public void MotivoNoAgrupable_CasoReal_LosMicrocortesSePueden()
    {
        Assert.Null(AgrupacionMicrocortesReglas.MotivoNoAgrupable(P1, Linea, Cam10, InicioDetencion, 300, yaAgrupada: false));
        Assert.Null(AgrupacionMicrocortesReglas.MotivoNoAgrupable(P2, Linea, Cam10, InicioDetencion, 300, yaAgrupada: false));
    }

    [Fact]
    public void MotivoNoAgrupable_YaAgrupada_NoSeAgrupaDosVeces() =>
        Assert.Contains("ya está agrupada", AgrupacionMicrocortesReglas.MotivoNoAgrupable(P1, Linea, Cam10, InicioDetencion, 300, yaAgrupada: true));

    [Fact]
    public void MotivoNoAgrupable_Rechazos()
    {
        Assert.Contains("no existe", AgrupacionMicrocortesReglas.MotivoNoAgrupable(null, Linea, Cam10, InicioDetencion, 300, false));
        Assert.Contains("misma cámara o línea", AgrupacionMicrocortesReglas.MotivoNoAgrupable(
            Parada(7, H(11, 10, 0), H(11, 10, 40), false, linea: 2), Linea, null, InicioDetencion, 300, false));
        Assert.Contains("no tiene hora de parada", AgrupacionMicrocortesReglas.MotivoNoAgrupable(P1, Linea, Cam10, null, 300, false));
        Assert.Contains("superó el umbral", AgrupacionMicrocortesReglas.MotivoNoAgrupable(
            Parada(8, H(11, 5, 0), H(11, 10, 30), true), Linea, Cam10, InicioDetencion, 300, false));
        Assert.Contains("10 minutos previos", AgrupacionMicrocortesReglas.MotivoNoAgrupable(
            Parada(9, H(11, 0, 0), H(11, 0, 40), false), Linea, Cam10, InicioDetencion, 300, false));
    }

    // ===================== Tiempo agrupado (vista) =====================

    [Fact]
    public void Resumir_CasoReal_InicioRealYTotal_SinTocarMinutosEfectivos()
    {
        var detencion = new Detencion
        {
            FechaHoraParada = P3.FechaHoraInicio,
            FechaHoraArranque = H(11, 23, 0),
            FechaHoraArranqueDetectada = P3.FechaHoraFin,
            MinutosEfectivos = 8
        };

        var r = AgrupacionMicrocortesReglas.Resumir(detencion, [P2, P1]);

        Assert.Equal(H(11, 8, 23), r.InicioReal);
        Assert.Equal(H(11, 23, 0), r.FinReal);   // manda la declarada sobre la detectada
        Assert.Equal(2, r.CantidadMicrocortes);
        Assert.Equal(200 + 120, r.SegundosMicrocortes);
        Assert.Equal(480, r.SegundosDetencion);
        Assert.Equal(800, r.SegundosTotales);
        Assert.Equal(8, detencion.MinutosEfectivos);
    }

    [Fact]
    public void Resumir_PendienteSinArranqueDeclarado_UsaElDetectado_YSinHijosEsLaDetencion()
    {
        var pendiente = new Detencion { FechaHoraParada = P3.FechaHoraInicio, FechaHoraArranqueDetectada = P3.FechaHoraFin };
        var r = AgrupacionMicrocortesReglas.Resumir(pendiente, []);
        Assert.Equal(P3.FechaHoraInicio, r.InicioReal);
        Assert.Equal(P3.FechaHoraFin, r.FinReal);
        Assert.Equal(0, r.CantidadMicrocortes);
        Assert.Equal(0, r.SegundosTotales);
    }

    [Fact]
    public void Pregunta_ComoLaVeElOperario()
    {
        Assert.Equal("Hubo 2 microcortes antes de esta parada (11:08, 11:12). ¿Mismo motivo?",
            AgrupacionMicrocortesReglas.Pregunta([P2.FechaHoraInicio, P1.FechaHoraInicio]));
        Assert.Equal("Hubo 1 microcorte antes de esta parada (11:12). ¿Mismo motivo?",
            AgrupacionMicrocortesReglas.Pregunta([P2.FechaHoraInicio]));
    }

    [Fact]
    public void Desactivar_DejaQuienYCuando_SinBorrar()
    {
        var a = new DetencionAgrupacion { DetencionId = 1, ParadaCamaraId = 1, CreadoPorUsuarioId = "oper-1" };
        var ahora = DateTimeOffset.UtcNow;
        a.Desactivar("sup-1", ahora);
        Assert.False(a.Activo);
        Assert.Equal("sup-1", a.DesactivadoPorUsuarioId);
        Assert.Equal(ahora, a.FechaHoraDesactivacion);
        Assert.Equal("oper-1", a.CreadoPorUsuarioId);
    }
}
