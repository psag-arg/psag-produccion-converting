using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class AnalisisFallaReglasTests
{
    private const string Mantenimiento = "MANTENIMIENTO";
    private const string Operario = "OPERARIO";
    private const string Administrador = "ADMINISTRADOR";

    private static Detencion Detencion(
        string? responsableRol = Mantenimiento,
        string? responsableUsuarioId = null,
        bool requiereAnalisis = true) => new()
    {
        RequiereAnalisis = requiereAnalisis,
        ResponsableRol = responsableRol,
        ResponsableUsuarioId = responsableUsuarioId,
        EstadoActual = EstadoDetencion.REGISTRADA
    };

    [Fact]
    public void ResponsableDeRol_PuedeEditar()
    {
        var detencion = Detencion(responsableRol: Mantenimiento);

        Assert.True(AnalisisFallaReglas.PuedeEditar(detencion, "usuario-1", [Mantenimiento]));
    }

    [Fact]
    public void ResponsableDeUsuario_PuedeEditarAunqueNoTengaElRol()
    {
        var detencion = Detencion(responsableRol: null, responsableUsuarioId: "usuario-1");

        Assert.True(AnalisisFallaReglas.PuedeEditar(detencion, "usuario-1", [Operario]));
    }

    [Fact]
    public void Administrador_PuedeEditarAunqueNoSeaResponsable()
    {
        var detencion = Detencion(responsableRol: Mantenimiento);

        Assert.True(AnalisisFallaReglas.PuedeEditar(detencion, "admin-1", [Administrador]));
    }

    [Fact]
    public void OperarioSinAsignacion_NoPuedeEditar()
    {
        var detencion = Detencion(responsableRol: Mantenimiento);

        Assert.False(AnalisisFallaReglas.PuedeEditar(detencion, "operario-1", [Operario]));
    }

    [Fact]
    public void UsuarioConOtroUsuarioAsignado_NoPuedeEditarAunqueTengaElRol()
    {
        var detencion = Detencion(responsableRol: null, responsableUsuarioId: "otro-usuario");

        Assert.False(AnalisisFallaReglas.PuedeEditar(detencion, "usuario-1", [Mantenimiento]));
    }

    [Fact]
    public void EsResponsable_NoSeConfundeConRolDistintoDelDeLaRegla()
    {
        var detencion = Detencion(responsableRol: Mantenimiento);

        Assert.False(AnalisisFallaReglas.EsResponsable(detencion, "usuario-1", ["SUPERVISOR"]));
    }

    [Theory]
    [InlineData(EstadoDetencion.REGISTRADA, true)]
    [InlineData(EstadoDetencion.EN_SEGUIMIENTO, true)]
    [InlineData(EstadoDetencion.CERRADA, false)]
    [InlineData(EstadoDetencion.PENDIENTE_JUSTIFICACION, false)]
    public void EstaAbierto_SoloRegistradaOEnSeguimientoConAnalisis(EstadoDetencion estado, bool esperado)
    {
        var detencion = Detencion(requiereAnalisis: true);
        detencion.EstadoActual = estado;

        Assert.Equal(esperado, AnalisisFallaReglas.EstaAbierto(detencion));
    }

    [Fact]
    public void EstaAbierto_SinRequiereAnalisis_NoEstaAbierto()
    {
        var sinAnalisis = Detencion(requiereAnalisis: false);

        Assert.False(AnalisisFallaReglas.EstaAbierto(sinAnalisis));
    }

    [Fact]
    public void EstaAbiertoExpr_DaLoMismoQueEstaAbierto_enTodosLosEstados()
    {
        var expresion = AnalisisFallaReglas.EstaAbiertoExpr.Compile();

        foreach (EstadoDetencion estado in Enum.GetValues<EstadoDetencion>())
        {
            foreach (var requiere in new[] { true, false })
            {
                var detencion = Detencion(requiereAnalisis: requiere);
                detencion.EstadoActual = estado;

                Assert.True(
                    expresion(detencion) == AnalisisFallaReglas.EstaAbierto(detencion),
                    $"Diferencia en {estado} con RequiereAnalisis={requiere}");
            }
        }
    }

    [Fact]
    public void CierreSinSolucion_DevuelveMotivo()
    {
        var motivo = AnalisisFallaReglas.MotivoNoCerrable(null, "Cadena estirada");

        Assert.NotNull(motivo);
        Assert.Contains("solución", motivo);
    }

    [Fact]
    public void CierreSinCausa_DevuelveMotivo()
    {
        var motivo = AnalisisFallaReglas.MotivoNoCerrable("Se cambia cadena", "   ");

        Assert.NotNull(motivo);
        Assert.Contains("causa", motivo);
    }

    [Fact]
    public void CierreConSolucionYCausa_NoDevuelveMotivo()
    {
        var motivo = AnalisisFallaReglas.MotivoNoCerrable("Se cambia cadena", "Cadena estirada");

        Assert.Null(motivo);
    }
}
