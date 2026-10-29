using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Punto de entrada del detector de calidad (T-611). No usa sesión de usuario: se autentica con la clave de
/// servicio del header X-Clave-Ingesta, que se compara en tiempo constante con Calidad:ClaveIngesta.
/// Sin clave configurada responde 503. Contrato completo en docs/CALIDAD.md.
/// </summary>
[Route("api/calidad/ingesta")]
[ApiController]
[AllowAnonymous]
[ClaveIngestaCalidad]
public class CalidadIngestaController : ApiControllerBase
{
    private readonly IRegistroCalidadService _registros;
    private readonly ILogger<CalidadIngestaController> _logger;

    public CalidadIngestaController(IRegistroCalidadService registros, ILogger<CalidadIngestaController> logger)
    {
        _registros = registros;
        _logger = logger;
    }

    /// <summary>
    /// Registra una detección con su imagen (multipart/form-data, imagen en el campo "imagen").
    /// 201 si la creó; 200 si el identificadorExterno ya existía (reintento, no duplica).
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(CalidadController.LimitePedidoImagen)]
    [RequestFormLimits(MultipartBodyLengthLimit = CalidadController.LimitePedidoImagen)]
    [ProducesResponseType(typeof(IngestaCalidadRespuesta), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IngestaCalidadRespuesta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ingestar([FromForm] IngestaCalidadRequest request, IFormFile? imagen)
    {
        await using var contenido = imagen?.OpenReadStream();
        var entrante = imagen != null ? new ImagenEntrante(imagen.FileName, imagen.Length, contenido!) : null;

        var resultado = await _registros.IngestarAsync(request, entrante);
        if (resultado.IsFailure)
        {
            _logger.LogWarning("Ingesta de calidad rechazada: {Motivo}", resultado.ErrorMessage);
            return ProblemaDe(resultado, "Datos de la detección inválidos");
        }

        var respuesta = resultado.Value!;
        return respuesta.Duplicado
            ? Ok(respuesta)
            : StatusCode(StatusCodes.Status201Created, respuesta);
    }
}

/// <summary>
/// Filtro de autorización de la ingesta: corre antes de leer el cuerpo, así un pedido sin clave no sube la imagen.
/// La clave se lee en cada pedido, así que cambiarla en appsettings (o en la variable de entorno
/// Calidad__ClaveIngesta) no necesita reiniciar si la configuración se recarga.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ClaveIngestaCalidadAttribute : Attribute, IAuthorizationFilter
{
    public const string Header = "X-Clave-Ingesta";
    public const string ClaveConfiguracion = "Calidad:ClaveIngesta";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var configuracion = http.RequestServices.GetRequiredService<IConfiguration>();
        var logger = http.RequestServices.GetRequiredService<ILogger<ClaveIngestaCalidadAttribute>>();
        var configurada = configuracion[ClaveConfiguracion];

        if (!CalidadReglas.ClaveConfigurada(configurada))
        {
            logger.LogWarning(
                "Ingesta de calidad deshabilitada: {Clave} vacía o con menos de {Largo} caracteres",
                ClaveConfiguracion, CalidadReglas.LargoMinimoClave);
            context.Result = Problema(context, StatusCodes.Status503ServiceUnavailable,
                "Ingesta de calidad no habilitada",
                "La ingesta del detector no está configurada en el servidor (Calidad:ClaveIngesta).");
            return;
        }

        var recibida = http.Request.Headers[Header].ToString();
        if (!CalidadReglas.ClaveValida(recibida, configurada))
        {
            logger.LogWarning("Ingesta de calidad con clave {Estado} desde {Ip}",
                string.IsNullOrEmpty(recibida) ? "ausente" : "incorrecta", http.Connection.RemoteIpAddress);
            context.Result = Problema(context, StatusCodes.Status401Unauthorized,
                "Clave de ingesta inválida",
                $"Falta el header {Header} o la clave no es correcta.");
        }
    }

    private static ObjectResult Problema(AuthorizationFilterContext context, int status, string titulo, string detalle) =>
        new(new ProblemDetails
        {
            Title = titulo,
            Detail = detalle,
            Status = status,
            Instance = context.HttpContext.Request.Path
        })
        { StatusCode = status };
}
