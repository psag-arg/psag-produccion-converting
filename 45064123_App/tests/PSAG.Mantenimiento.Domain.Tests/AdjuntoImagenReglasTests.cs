using System.Text;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-544: solo fotos JPG o PNG reales, hasta 10 MB y hasta 5 por análisis.
/// </summary>
public class AdjuntoImagenReglasTests
{
    private static readonly byte[] CabeceraJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] CabeceraPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] CabeceraGif = Encoding.ASCII.GetBytes("GIF89a..");
    private static readonly byte[] Texto = Encoding.ASCII.GetBytes("hola que");

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("FOTO.JPEG")]
    public void JpegReal_SeAcepta(string nombre)
    {
        Assert.Null(AdjuntoImagenReglas.MotivoRechazo(nombre, 1000, CabeceraJpeg, 0));
        Assert.Equal(AdjuntoImagenReglas.Jpeg, AdjuntoImagenReglas.DetectarTipo(CabeceraJpeg));
    }

    [Fact]
    public void PngReal_SeAcepta()
    {
        Assert.Null(AdjuntoImagenReglas.MotivoRechazo("captura.png", 1000, CabeceraPng, 4));
        Assert.Equal(AdjuntoImagenReglas.Png, AdjuntoImagenReglas.DetectarTipo(CabeceraPng));
    }

    [Fact]
    public void ArchivoDeTextoRenombradoAJpg_SeRechazaPorContenido()
    {
        var motivo = AdjuntoImagenReglas.MotivoRechazo("virus.jpg", 1000, Texto, 0);

        Assert.Equal("El archivo no es una imagen JPG o PNG válida.", motivo);
    }

    [Fact]
    public void GifConExtensionPng_SeRechaza()
    {
        Assert.NotNull(AdjuntoImagenReglas.MotivoRechazo("animacion.png", 1000, CabeceraGif, 0));
    }

    [Theory]
    [InlineData("documento.pdf")]
    [InlineData("foto.gif")]
    [InlineData("foto")]
    [InlineData("foto.jpg.exe")]
    public void ExtensionNoPermitida_SeRechazaAunqueElContenidoSeaImagen(string nombre)
    {
        var motivo = AdjuntoImagenReglas.MotivoRechazo(nombre, 1000, CabeceraJpeg, 0);

        Assert.Equal("Solo se pueden adjuntar fotos JPG o PNG.", motivo);
    }

    [Fact]
    public void MasDeDiezMegas_SeRechazaConMensajeClaro()
    {
        var motivo = AdjuntoImagenReglas.MotivoRechazo("foto.jpg", AdjuntoImagenReglas.TamanoMaximoBytes + 1, CabeceraJpeg, 0);

        Assert.NotNull(motivo);
        Assert.Contains("10 MB", motivo);
    }

    [Fact]
    public void ExactamenteDiezMegas_SeAcepta()
    {
        Assert.Null(AdjuntoImagenReglas.MotivoRechazo("foto.jpg", AdjuntoImagenReglas.TamanoMaximoBytes, CabeceraJpeg, 0));
    }

    [Fact]
    public void ArchivoVacio_SeRechaza()
    {
        Assert.Equal("El archivo está vacío.", AdjuntoImagenReglas.MotivoRechazo("foto.jpg", 0, [], 0));
    }

    [Fact]
    public void SextaFoto_SeRechaza()
    {
        var motivo = AdjuntoImagenReglas.MotivoRechazo("foto.jpg", 1000, CabeceraJpeg, AdjuntoImagenReglas.MaximoPorAnalisis);

        Assert.Equal("El análisis ya tiene 5 fotos, que es el máximo.", motivo);
    }
}
