using System.Text;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Exceptions;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-611: reglas de revisión y permisos del registro de calidad, clave de ingesta y validación de la imagen.
/// </summary>
public class CalidadReglasTests
{
    private const string ClaveBuena = "clave-de-prueba-larga-1234";

    // ===================== Revisión =====================

    [Theory]
    [InlineData(EstadoRevisionCalidad.PENDIENTE, EstadoRevisionCalidad.CONFIRMADO, true)]
    [InlineData(EstadoRevisionCalidad.PENDIENTE, EstadoRevisionCalidad.FALSO_POSITIVO, true)]
    [InlineData(EstadoRevisionCalidad.CONFIRMADO, EstadoRevisionCalidad.FALSO_POSITIVO, true)]
    [InlineData(EstadoRevisionCalidad.FALSO_POSITIVO, EstadoRevisionCalidad.CONFIRMADO, true)]
    [InlineData(EstadoRevisionCalidad.CONFIRMADO, EstadoRevisionCalidad.PENDIENTE, false)]
    [InlineData(EstadoRevisionCalidad.FALSO_POSITIVO, EstadoRevisionCalidad.PENDIENTE, false)]
    [InlineData(EstadoRevisionCalidad.CONFIRMADO, EstadoRevisionCalidad.CONFIRMADO, false)]
    [InlineData(EstadoRevisionCalidad.PENDIENTE, EstadoRevisionCalidad.PENDIENTE, false)]
    public void Transiciones(EstadoRevisionCalidad desde, EstadoRevisionCalidad hacia, bool permitida)
    {
        Assert.Equal(permitida, desde.PuedeTransicionarA(hacia));
    }

    [Fact]
    public void Revisar_GuardaQuienYCuando_YDejaHistorial()
    {
        var registro = new RegistroCalidad { RegistroCalidadId = 7 };
        var ahora = new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.FromHours(-3));

        registro.Revisar(EstadoRevisionCalidad.FALSO_POSITIVO, "sup-1", ahora, "Era un reflejo");

        Assert.Equal(EstadoRevisionCalidad.FALSO_POSITIVO, registro.EstadoRevision);
        Assert.Equal("sup-1", registro.RevisadoPorUsuarioId);
        Assert.Equal(ahora, registro.FechaHoraRevision);
        var fila = Assert.Single(registro.Revisiones);
        Assert.Equal(EstadoRevisionCalidad.PENDIENTE, fila.EstadoAnterior);
        Assert.Equal(EstadoRevisionCalidad.FALSO_POSITIVO, fila.EstadoNuevo);
        Assert.Equal("Era un reflejo", fila.Comentario);
        Assert.Equal("sup-1", fila.UsuarioId);
    }

    [Fact]
    public void Corregir_ConfirmadoAFalsoPositivo_SumaOtraFila()
    {
        var registro = new RegistroCalidad();
        registro.Revisar(EstadoRevisionCalidad.CONFIRMADO, "sup-1", DateTimeOffset.UtcNow, null);
        registro.Revisar(EstadoRevisionCalidad.FALSO_POSITIVO, "admin", DateTimeOffset.UtcNow, "Corrección");

        Assert.Equal(2, registro.Revisiones.Count);
        Assert.Equal("admin", registro.RevisadoPorUsuarioId);
        Assert.Equal(EstadoRevisionCalidad.CONFIRMADO, registro.Revisiones.Last().EstadoAnterior);
    }

    [Fact]
    public void Revisar_VolverAPendiente_SeRechaza()
    {
        var registro = new RegistroCalidad();
        registro.Revisar(EstadoRevisionCalidad.CONFIRMADO, "sup-1", DateTimeOffset.UtcNow, null);

        Assert.Throws<TransicionEstadoInvalidaException>(() =>
            registro.Revisar(EstadoRevisionCalidad.PENDIENTE, "sup-1", DateTimeOffset.UtcNow, null));
        Assert.Single(registro.Revisiones);
    }

    // ===================== Permisos =====================

    [Theory]
    [InlineData("SUPERVISOR", true)]
    [InlineData("MANTENIMIENTO", true)]
    [InlineData("ADMINISTRADOR", true)]
    [InlineData("OPERARIO", false)]
    [InlineData("GERENCIA", false)]
    public void PuedeRevisar_SegunRol(string rol, bool puede)
    {
        Assert.Equal(puede, CalidadReglas.PuedeRevisar([rol]));
    }

    [Theory]
    [InlineData("SUPERVISOR", true)]
    [InlineData("ADMINISTRADOR", true)]
    [InlineData("MANTENIMIENTO", false)]
    [InlineData("OPERARIO", false)]
    [InlineData("GERENCIA", false)]
    public void PuedeCargarManual_SegunRol(string rol, bool puede)
    {
        Assert.Equal(puede, CalidadReglas.PuedeCargarManual([rol]));
    }

    [Fact]
    public void Maestros_SoloAdministrador()
    {
        Assert.True(CalidadReglas.PuedeAdministrarMaestros(["ADMINISTRADOR"]));
        Assert.False(CalidadReglas.PuedeAdministrarMaestros(["SUPERVISOR", "MANTENIMIENTO"]));
    }

    // ===================== Clave de ingesta =====================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("corta")]
    public void ClaveNoConfiguradaOCorta_NoHabilita(string? configurada)
    {
        Assert.False(CalidadReglas.ClaveConfigurada(configurada));
        Assert.False(CalidadReglas.ClaveValida(configurada, configurada));
    }

    [Fact]
    public void ClaveCorrecta_Valida()
    {
        Assert.True(CalidadReglas.ClaveValida(ClaveBuena, ClaveBuena));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("otra-clave-cualquiera-9999")]
    [InlineData("CLAVE-DE-PRUEBA-LARGA-1234")]
    [InlineData("clave-de-prueba-larga-123")]
    public void ClaveIncorrecta_NoValida(string? recibida)
    {
        Assert.False(CalidadReglas.ClaveValida(recibida, ClaveBuena));
    }

    // ===================== Datos medidos =====================

    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData("0", "0", "mm", true)]
    [InlineData("1", "14.5", "mm", true)]
    [InlineData("1.01", null, null, false)]
    [InlineData("-0.1", null, null, false)]
    [InlineData(null, "-1", null, false)]
    public void DatosMedidos(string? confianza, string? valor, string? unidad, bool validos)
    {
        var motivo = CalidadReglas.MotivoDatosInvalidos(
            confianza == null ? null : decimal.Parse(confianza, System.Globalization.CultureInfo.InvariantCulture),
            valor == null ? null : decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture),
            unidad);

        Assert.Equal(validos, motivo == null);
    }

    [Fact]
    public void FechaFutura_ConTolerancia()
    {
        var ahora = DateTimeOffset.UtcNow;
        Assert.False(CalidadReglas.EsFechaFutura(ahora.AddMinutes(5), ahora));
        Assert.True(CalidadReglas.EsFechaFutura(ahora.AddMinutes(30), ahora));
    }

    [Fact]
    public void Codigos_SeNormalizan()
    {
        Assert.Equal("T2-CAM1", CalidadReglas.NormalizarCodigo("  t2-cam1 "));
    }

    // ===================== Imagen (mismas reglas que las fotos del análisis) =====================

    private static readonly byte[] CabeceraPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] CabeceraJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    [Fact]
    public void ImagenReal_SeAcepta()
    {
        Assert.Null(AdjuntoImagenReglas.MotivoRechazoImagen("frame.png", 2048, CabeceraPng));
        Assert.Null(AdjuntoImagenReglas.MotivoRechazoImagen("frame.jpg", 2048, CabeceraJpeg));
    }

    [Fact]
    public void TextoConExtensionDeImagen_SeRechazaPorContenido()
    {
        Assert.NotNull(AdjuntoImagenReglas.MotivoRechazoImagen("frame.png", 100, Encoding.ASCII.GetBytes("<script>")));
    }

    [Theory]
    [InlineData("frame.gif", 100)]
    [InlineData("frame.png", 0)]
    [InlineData("frame.png", AdjuntoImagenReglas.TamanoMaximoBytes + 1)]
    public void ImagenInvalida_SeRechaza(string nombre, long tamano)
    {
        Assert.NotNull(AdjuntoImagenReglas.MotivoRechazoImagen(nombre, tamano, CabeceraPng));
    }

    [Fact]
    public void ImagenesDeCalidad_NoTienenTopeDeCincoComoElAnalisis()
    {
        // El tope de 5 es por análisis; la regla de imagen sola no lo aplica.
        Assert.NotNull(AdjuntoImagenReglas.MotivoRechazo("f.png", 100, CabeceraPng, AdjuntoImagenReglas.MaximoPorAnalisis));
        Assert.Null(AdjuntoImagenReglas.MotivoRechazoImagen("f.png", 100, CabeceraPng));
    }
}
