using PSAG.Mantenimiento.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace PSAG.Mantenimiento.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        var (statusCode, message, errors) = exception switch
        {
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                domainEx.Message,
                domainEx.Errors?.ToList() ?? new List<string>()
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "No autorizado",
                new List<string>()
            ),
            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Recurso no encontrado",
                new List<string>()
            ),
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                argEx.Message,
                new List<string>()
            ),
            InvalidOperationException invEx => (
                HttpStatusCode.BadRequest,
                invEx.Message,
                new List<string>()
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "Ha ocurrido un error interno",
                new List<string>()
            )
        };

        // Log según severidad
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception,
                "Error no manejado. CorrelationId: {CorrelationId}, Path: {Path}",
                correlationId, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Error de negocio: {Message}. CorrelationId: {CorrelationId}, Path: {Path}",
                message, correlationId, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse
        {
            Type = GetErrorType(statusCode),
            Title = GetErrorTitle(statusCode),
            Status = (int)statusCode,
            Detail = message,
            Instance = context.Request.Path,
            CorrelationId = correlationId,
            Errors = errors.Count > 0 ? errors : null
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static string GetErrorType(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        HttpStatusCode.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
        HttpStatusCode.Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        HttpStatusCode.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        HttpStatusCode.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
    };

    private static string GetErrorTitle(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Solicitud inválida",
        HttpStatusCode.Unauthorized => "No autorizado",
        HttpStatusCode.Forbidden => "Acceso denegado",
        HttpStatusCode.NotFound => "Recurso no encontrado",
        HttpStatusCode.Conflict => "Conflicto",
        _ => "Error interno del servidor"
    };
}

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public List<string>? Errors { get; set; }
}
