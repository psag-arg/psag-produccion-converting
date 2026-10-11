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
    public void AsignadaAExpr_DaLoMismoQueEsResponsable()
    {
        var roles = new[] { Operario, "GERENCIA" };
        var casos = new[]
        {
            Detencion(responsableRol: Mantenimiento, responsableUsuarioId: null),
            Detencion(responsableRol: Operario, responsableUsuarioId: null),
            Detencion(responsableRol: null, responsableUsuarioId: "usuario-1"),
            Detencion(responsableRol: null, responsableUsuarioId: "otro-usuario"),
            Detencion(responsableRol: Mantenimiento, responsableUsuarioId: "otro-usuario"),
            Detencion(responsableRol: null, responsableUsuarioId: null),
        };
        var expresion = AnalisisFallaReglas.AsignadaAExpr("usuario-1", roles).Compile();

        foreach (var caso in casos)
        {
            Assert.Equal(
                AnalisisFallaReglas.EsResponsable(caso, "usuario-1", roles),
                expresion(caso));
        }
    }

    [Theory]
    [InlineData("OPERARIO")]
    [InlineData("SUPERVISOR")]
    [InlineData("MANTENIMIENTO")]
    public void RolResponsable_VeLasPestaniasDeAnalisisAunSinAnalisisAsignados(string rol)
    {
        Assert.True(AnalisisFallaReglas.MuestraAnalisis("usuario-1", [rol], []));
    }

    [Fact]
    public void Administrador_VeLasPestaniasDeAnalisis()
    {
        Assert.True(AnalisisFallaReglas.MuestraAnalisis("admin-1", [Administrador], []));
    }

    [Fact]
    public void RolSinResponsabilidad_NoVeAnalisisSiNoTieneNadaAsignado()
    {
        Assert.False(AnalisisFallaReglas.MuestraAnalisis("gerente-1", ["GERENCIA"], []));
    }

    [Fact]
    public void RolSinResponsabilidad_VeAnalisisSiTieneUnoAsignadoPorUsuario()
    {
        var asignado = Detencion(responsableRol: null, responsableUsuarioId: "gerente-1");

        Assert.True(AnalisisFallaReglas.MuestraAnalisis("gerente-1", ["GERENCIA"], [asignado]));
    }

    [Fact]
    public void RolSinResponsabilidad_NoVeAnalisisAsignadoAOtroUsuario()
    {
        var deOtro = Detencion(responsableRol: null, responsableUsuarioId: "otro-usuario");

        Assert.False(AnalisisFallaReglas.MuestraAnalisis("gerente-1", ["GERENCIA"], [deOtro]));
    }

    [Fact]
    public void SinAnalisisIniciado_PuedeQuitarLaMarca()
    {
        var detencion = Detencion(requiereAnalisis: true);

        Assert.True(AnalisisFallaReglas.PuedeQuitarMarcaAnalisis(detencion));
    }

    [Fact]
    public void TextoDeEspaciosNoCuentaComoAnalisisIniciado()
    {
        var detencion = Detencion(requiereAnalisis: true);
        detencion.AccionesRealizadas = "   ";

        Assert.True(AnalisisFallaReglas.PuedeQuitarMarcaAnalisis(detencion));
    }

    [Fact]
    public void AnalisisEnSeguimiento_NoPierdeLaMarcaAunqueLaReglaYNoAplique()
    {
        var detencion = Detencion(requiereAnalisis: true);
        detencion.EstadoActual = EstadoDetencion.EN_SEGUIMIENTO;

        Assert.False(AnalisisFallaReglas.PuedeQuitarMarcaAnalisis(detencion));
    }

    [Theory]
    [InlineData("solucion")]
    [InlineData("causa")]
    [InlineData("plan")]
    public void AnalisisConTextoCargado_NoPierdeLaMarca(string campo)
    {
        var detencion = Detencion(requiereAnalisis: true);
        switch (campo)
        {
            case "solucion": detencion.AccionesRealizadas = "Se cambia cadena"; break;
            case "causa": detencion.CausaFalla = "Cadena estirada"; break;
            case "plan": detencion.PlanAccion = "Revisar cada 15 días"; break;
        }

        Assert.False(AnalisisFallaReglas.PuedeQuitarMarcaAnalisis(detencion));
    }

    [Fact]
    public void AnalisisConFechaDeAnalisis_NoPierdeLaMarca()
    {
        var detencion = Detencion(requiereAnalisis: true);
        detencion.FechaHoraAnalisis = DateTimeOffset.UtcNow;

        Assert.False(AnalisisFallaReglas.PuedeQuitarMarcaAnalisis(detencion));
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

    [Fact]
    public void BorradorVacio_NoSeGuardaSiElAnalisisNoEmpezo()
    {
        Assert.Equal("Escribí algo antes de guardar el borrador.",
            AnalisisFallaReglas.MotivoBorradorNoGuardable(EstadoDetencion.REGISTRADA, " ", null, ""));
    }

    [Fact]
    public void BorradorConAlgunTexto_SeGuarda()
    {
        Assert.Null(AnalisisFallaReglas.MotivoBorradorNoGuardable(EstadoDetencion.REGISTRADA, null, null, "Revisar rodamiento"));
    }

    [Fact]
    public void BorradorVacio_SePermiteSiYaEstabaEnAnalisis()
    {
        Assert.Null(AnalisisFallaReglas.MotivoBorradorNoGuardable(EstadoDetencion.EN_SEGUIMIENTO, null, null, null));
    }
}
