using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-611: endpoint de ingesta del detector. Clave correcta / incorrecta / ausente / sin configurar,
/// y el pedido completo hasta la base. También que los atributos de rol coincidan con CalidadReglas.
/// </summary>
public class CalidadIngestaTests : CalidadTestBase
{
    private const string Clave = "clave-ingesta-de-prueba-0123456789";

    private static AuthorizationFilterContext Contexto(string? claveConfigurada, string? claveEnviada)
    {
        var valores = new Dictionary<string, string?>();
        if (claveConfigurada != null)
            valores[ClaveIngestaCalidadAttribute.ClaveConfiguracion] = claveConfigurada;

        var servicios = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(valores).Build())
            .AddLogging()
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = servicios };
        http.Request.Path = "/api/calidad/ingesta";
        if (claveEnviada != null)
            http.Request.Headers[ClaveIngestaCalidadAttribute.Header] = claveEnviada;

        return new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());
    }

    private static int? StatusDe(AuthorizationFilterContext contexto) => (contexto.Result as ObjectResult)?.StatusCode;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta")]
    public void SinClaveConfigurada_Responde503(string? configurada)
    {
        var contexto = Contexto(configurada, Clave);

        new ClaveIngestaCalidadAttribute().OnAuthorization(contexto);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, StatusDe(contexto));
    }

    [Fact]
    public void SinHeader_Responde401()
    {
        var contexto = Contexto(Clave, null);

        new ClaveIngestaCalidadAttribute().OnAuthorization(contexto);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusDe(contexto));
    }

    [Theory]
    [InlineData("otra-clave-cualquiera-0123456789")]
    [InlineData("CLAVE-INGESTA-DE-PRUEBA-0123456789")]
    [InlineData("x")]
    public void ClaveIncorrecta_Responde401(string enviada)
    {
        var contexto = Contexto(Clave, enviada);

        new ClaveIngestaCalidadAttribute().OnAuthorization(contexto);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusDe(contexto));
        var problema = Assert.IsType<ProblemDetails>(((ObjectResult)contexto.Result!).Value);
        Assert.DoesNotContain(Clave, problema.Detail);
    }

    [Fact]
    public void ClaveCorrecta_DejaPasar()
    {
        var contexto = Contexto(Clave, Clave);

        new ClaveIngestaCalidadAttribute().OnAuthorization(contexto);

        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task PedidoCompleto_CreaRegistroAutomatico201_YReintentoDevuelve200()
    {
        var controller = new CalidadIngestaController(Service, NullLogger<CalidadIngestaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var pedido = new IngestaCalidadRequest
        {
            PuntoControl = "TEST-CAM-T2",
            TipoDefecto = "TEST-RABO",
            FechaHora = HoraPlantaTexto(1, 11),
            Confianza = "0.91",
            ValorMedido = "12",
            IdentificadorExterno = "cam1-000123"
        };

        var primera = await controller.Ingestar(pedido, Archivo(Png));
        var creada = Assert.IsType<ObjectResult>(primera);
        Assert.Equal(StatusCodes.Status201Created, creada.StatusCode);
        var respuesta = Assert.IsType<IngestaCalidadRespuesta>(creada.Value);
        Assert.Equal("PENDIENTE", respuesta.EstadoRevision);
        Assert.Equal(TurnoManana.Nombre, respuesta.TurnoNombre);

        var registro = await Context.RegistrosCalidad.AsNoTracking().SingleAsync();
        Assert.Equal(OrigenRegistroCalidad.AUTOMATICO, registro.Origen);

        var segunda = await controller.Ingestar(pedido, Archivo(Png));
        var ok = Assert.IsType<OkObjectResult>(segunda);
        Assert.True(((IngestaCalidadRespuesta)ok.Value!).Duplicado);
        Assert.Equal(1, await Context.RegistrosCalidad.CountAsync());
    }

    [Fact]
    public async Task PedidoConImagenInvalida_Responde400()
    {
        var controller = new CalidadIngestaController(Service, NullLogger<CalidadIngestaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var resultado = await controller.Ingestar(
            new IngestaCalidadRequest { PuntoControl = "TEST-CAM-T2", TipoDefecto = "TEST-RABO" },
            Archivo("no es imagen"u8.ToArray()));

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(resultado).StatusCode);
        Assert.Empty(Context.RegistrosCalidad);
    }

    private static FormFile Archivo(byte[] bytes, string nombre = "frame.png") =>
        new(new MemoryStream(bytes), 0, bytes.Length, "imagen", nombre);

    // ===================== Atributos de los controllers =====================

    [Fact]
    public void Ingesta_EsAnonimaPeroConClave()
    {
        var tipo = typeof(CalidadIngestaController);
        Assert.NotNull(tipo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(tipo.GetCustomAttribute<ClaveIngestaCalidadAttribute>());
    }

    [Theory]
    [InlineData(nameof(CalidadController.Revisar), "Revision")]
    [InlineData(nameof(CalidadController.VincularDetencion), "Revision")]
    [InlineData(nameof(CalidadController.CrearManual), "Carga")]
    public void Controller_RolesDeCadaAccion_CoincidenConLasReglas(string accion, string tipo)
    {
        var atributo = typeof(CalidadController).GetMethod(accion)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single(a => a.Roles != null);
        var roles = atributo.Roles!.Split(',').OrderBy(r => r);
        var esperados = (tipo == "Carga" ? CalidadReglas.RolesCargaManual : CalidadReglas.RolesRevision).OrderBy(r => r);

        Assert.Equal(esperados, roles);
        Assert.DoesNotContain("OPERARIO", roles);
        Assert.DoesNotContain("GERENCIA", roles);
    }

    [Fact]
    public void Controllers_NoTienenEndpointsDeBorrado()
    {
        var borrados = new[] { typeof(CalidadController), typeof(CalidadIngestaController) }
            .SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes<HttpDeleteAttribute>().Any());

        Assert.Empty(borrados);
    }

    [Fact]
    public void Controller_LecturaParaTodosLosAutenticados()
    {
        Assert.NotNull(typeof(CalidadController).GetCustomAttribute<AuthorizeAttribute>());
        var get = typeof(CalidadController).GetMethod(nameof(CalidadController.GetRegistros))!;
        Assert.DoesNotContain(get.GetCustomAttributes<AuthorizeAttribute>(), a => a.Roles != null);
    }
}
