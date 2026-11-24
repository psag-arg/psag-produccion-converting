using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PSAG.Mantenimiento.Domain.Interfaces;

namespace PSAG.Mantenimiento.Api.Autorizacion;

/// <summary>
/// T-802 · La acción se puede usar sin turno abierto aunque el usuario esté obligado a turno: auth, el menú
/// (<c>api/permisos/mios</c>), inicio de turno (propuesta, abrir, relevar, actual, mío) y las listas que usa esa pantalla.
/// Con turno abierto tampoco se le aplica el alcance del turno (la acción hace sus propias validaciones).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SinTurnoObligatorioAttribute : Attribute;

/// <summary>
/// Turno automático (B.6 / B.9) · Acción que el obligado a turno puede usar durante la TOLERANCIA de su turno anterior (el
/// turno de máquina ya cerró y todavía no venció ABM_Turnos.ToleranciaCierreOperadorMinutos): declarar y corregir las
/// detenciones de ese turno, ver sus pendientes y terminar su turno. Todo se ajusta a ESE turno (como con turno abierto).
/// Con <see cref="SoloLectura"/>, sólo las lecturas (p. ej. los catálogos que usa la pantalla de declarar).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PermitidoEnToleranciaAttribute : Attribute
{
    public bool SoloLectura { get; init; }
}

/// <summary>Qué identifica el parámetro de ruta que <see cref="DelTurnoAttribute"/> controla.</summary>
public enum RecursoDelTurno
{
    /// <summary>Un turno de producción (PRD_TurnosProduccion.TurnoProduccionId).</summary>
    TurnoProduccion,

    /// <summary>Una detención (DET_Detenciones.DetencionId; el análisis de falla usa el mismo id).</summary>
    Detencion
}

/// <summary>
/// T-802 · Para el obligado a turno con turno abierto, el recurso del parámetro (turno de producción o detención) tiene que
/// ser de SU turno; si no, 403 "Fuera de tu turno". Si no existe, sigue la acción (que responde 404).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class DelTurnoAttribute(RecursoDelTurno recurso, string parametro) : Attribute
{
    public RecursoDelTurno Recurso { get; } = recurso;
    public string Parametro { get; } = parametro;
}

/// <summary>Turno abierto del usuario obligado a turno (en el que es el operador vigente).</summary>
public sealed record TurnoDelUsuario(
    long TurnoProduccionId, int LineaId, string LineaNombre, int TurnoId, string TurnoNombre, DateOnly FechaProduccion,
    bool EnTolerancia = false)
{
    public string Descripcion => $"{TurnoNombre} de {LineaNombre} del {FechaProduccion:dd/MM/yyyy}";
}

public static class TurnoObligatorio
{
    private const string Clave = "T802.TurnoObligado";

    /// <summary>Extensión "codigo" del ProblemDetails del 409: la Web lo usa para volver a "Inicio de turno".</summary>
    public const string CodigoTurnoRequerido = "TURNO_REQUERIDO";

    public const string CodigoFueraDelTurno = "FUERA_DEL_TURNO";

    public const string TituloTurnoRequerido = "Turno requerido";
    public const string MensajeTurnoRequerido = "Abrí o tomá un turno para empezar (Producción → Inicio de turno).";
    public const string TituloFueraDelTurno = "Fuera de tu turno";

    public static string MensajeFueraDelTurno(TurnoDelUsuario t) => t.EnTolerancia
        ? $"Terminó el {t.Descripcion}: en la tolerancia sólo podés declarar y corregir las detenciones de ese turno."
        : $"Tenés abierto el {t.Descripcion}: sólo podés trabajar sobre ese turno y esa línea.";

    /// <summary>Turno del obligado a turno de esta request (lo guarda el filtro); null si no está obligado o no tiene.</summary>
    public static TurnoDelUsuario? De(HttpContext? http) =>
        http != null && http.Items.TryGetValue(Clave, out var t) ? t as TurnoDelUsuario : null;

    internal static void Guardar(HttpContext http, TurnoDelUsuario turno) => http.Items[Clave] = turno;

    internal static ObjectResult Problema(int status, string titulo, string detalle, string codigo, string instancia)
    {
        var problema = new ProblemDetails { Title = titulo, Detail = detalle, Status = status, Instance = instancia };
        problema.Extensions["codigo"] = codigo;
        return new ObjectResult(problema) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}

/// <summary>
/// T-802 · Obligado a turno, aplicado en la API (no sólo en la Web). Para un usuario con algún rol
/// <c>ObligadoATurno</c> (CFG_RolesConfiguracion):
/// <list type="bullet">
/// <item>Sin turno abierto en el que sea el operador vigente: toda acción de negocio responde <b>409</b> "Abrí o tomá un
/// turno para empezar" (extensión <c>codigo</c> = TURNO_REQUERIDO), salvo las marcadas con
/// <see cref="SinTurnoObligatorioAttribute"/>.</item>
/// <item>Con turno abierto, lo que reciben las acciones se ajusta al turno: en las <b>lecturas</b> (GET) los filtros
/// <c>lineaId</c>, <c>turnoId</c>, <c>turnoProduccionId</c> y los rangos <c>fechaDesde</c>/<c>fechaHasta</c>
/// (<c>desde</c>/<c>hasta</c> de tipo fecha) se fijan a la línea, el turno y la fecha de producción del turno (los
/// parámetros sueltos y las propiedades de los objetos de filtro); en las <b>escrituras</b> una <c>lineaId</c> o
/// <c>turnoProduccionId</c> distinta de la del turno se rechaza con <b>403</b> "Fuera de tu turno". Los recursos por id
/// marcados con <see cref="DelTurnoAttribute"/> tienen que ser del turno. Los casos que dependen de la hora (la detención
/// cae en el turno) los valida cada acción con <see cref="TurnoObligatorio.De"/>.</item>
/// </list>
/// Corre después de la autorización: sin permiso sigue siendo 403 "Sin permiso".
/// </summary>
public sealed class TurnoObligatorioFilter : IAsyncActionFilter
{
    private static readonly string[] NombresLinea = ["lineaId"];
    private static readonly string[] NombresTurnoProduccion = ["turnoProduccionId"];
    private static readonly string[] NombresTurno = ["turnoId"];
    private static readonly string[] NombresRangoFechas = ["fechaDesde", "fechaHasta", "desde", "hasta"];

    private readonly IUnitOfWork _unitOfWork;
    private readonly Application.Services.Interfaces.ITurnoProduccionService _turnos;

    public TurnoObligatorioFilter(IUnitOfWork unitOfWork, Application.Services.Interfaces.ITurnoProduccionService turnos)
    {
        _unitOfWork = unitOfWork;
        _turnos = turnos;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var usuarioId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (http.User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(usuarioId)
            || context.ActionDescriptor.EndpointMetadata.Any(m => m is IAllowAnonymous))
        {
            await next();
            return;
        }

        var permisos = await PermisosDeRequest.ObtenerAsync(http, http.User);
        if (!permisos.ObligadoATurno)
        {
            await next();
            return;
        }

        var exenta = context.ActionDescriptor.EndpointMetadata.Any(m => m is SinTurnoObligatorioAttribute);
        var tp = (await _unitOfWork.TurnosProduccion.GetAbiertosDeOperadorAsync(usuarioId, http.RequestAborted)).FirstOrDefault();
        if (tp == null)
        {
            // Turno automático: en la tolerancia de su turno anterior, sólo lo marcado (declarar / corregir sus detenciones,
            // sus pendientes, terminar su turno), ajustado a ese turno.
            var permitida = context.ActionDescriptor.EndpointMetadata.OfType<PermitidoEnToleranciaAttribute>().LastOrDefault();
            if (permitida != null && (!permitida.SoloLectura || HttpMethods.IsGet(http.Request.Method))
                && await _turnos.ObtenerSesionEnToleranciaAsync(usuarioId, http.RequestAborted) is { } anterior)
            {
                var enTolerancia = new TurnoDelUsuario(anterior.TurnoProduccionId, anterior.LineaId, anterior.Linea?.Nombre ?? $"la línea {anterior.LineaId}",
                    anterior.TurnoId, anterior.Turno?.Nombre ?? "turno", anterior.FechaProduccion, EnTolerancia: true);
                TurnoObligatorio.Guardar(http, enTolerancia);
                if (!await DentroDelTurnoAsync(context, enTolerancia))
                {
                    context.Result = TurnoObligatorio.Problema(StatusCodes.Status403Forbidden, TurnoObligatorio.TituloFueraDelTurno,
                        TurnoObligatorio.MensajeFueraDelTurno(enTolerancia), TurnoObligatorio.CodigoFueraDelTurno, http.Request.Path);
                    return;
                }
                await next();
                return;
            }
            if (exenta)
            {
                await next();
                return;
            }
            context.Result = TurnoObligatorio.Problema(StatusCodes.Status409Conflict, TurnoObligatorio.TituloTurnoRequerido,
                TurnoObligatorio.MensajeTurnoRequerido, TurnoObligatorio.CodigoTurnoRequerido, http.Request.Path);
            return;
        }

        var turno = new TurnoDelUsuario(tp.TurnoProduccionId, tp.LineaId, tp.Linea?.Nombre ?? $"la línea {tp.LineaId}",
            tp.TurnoId, tp.Turno?.Nombre ?? "turno", tp.FechaProduccion);
        TurnoObligatorio.Guardar(http, turno);

        if (!exenta && !await DentroDelTurnoAsync(context, turno))
        {
            context.Result = TurnoObligatorio.Problema(StatusCodes.Status403Forbidden, TurnoObligatorio.TituloFueraDelTurno,
                TurnoObligatorio.MensajeFueraDelTurno(turno), TurnoObligatorio.CodigoFueraDelTurno, http.Request.Path);
            return;
        }

        await next();
    }

    /// <summary>Ajusta los filtros de las lecturas al turno y dice si una escritura (o un recurso por id) queda dentro.</summary>
    private async Task<bool> DentroDelTurnoAsync(ActionExecutingContext context, TurnoDelUsuario turno)
    {
        var lectura = HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method);

        foreach (var parametro in context.ActionDescriptor.Parameters)
        {
            var tipo = parametro.ParameterType;
            context.ActionArguments.TryGetValue(parametro.Name, out var valor);
            if (EsSimple(tipo))
            {
                if (!Ajustar(parametro.Name, tipo, valor, lectura, turno, out var nuevo, out var cambia))
                    return false;
                if (cambia)
                    context.ActionArguments[parametro.Name] = nuevo;
            }
            else if (valor != null && tipo.IsClass && tipo != typeof(string))
            {
                foreach (var propiedad in valor.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && EsSimple(p.PropertyType)))
                {
                    if (!Ajustar(propiedad.Name, propiedad.PropertyType, propiedad.GetValue(valor), lectura, turno, out var nuevo, out var cambia))
                        return false;
                    if (cambia)
                        propiedad.SetValue(valor, nuevo);
                }
            }
        }

        foreach (var del in context.ActionDescriptor.EndpointMetadata.OfType<DelTurnoAttribute>())
        {
            if (!context.ActionArguments.TryGetValue(del.Parametro, out var id) || id == null)
                continue;
            var turnoDelRecurso = del.Recurso switch
            {
                RecursoDelTurno.TurnoProduccion => Convert.ToInt64(id),
                _ => (await _unitOfWork.Detenciones.GetByIdAsync(Convert.ToInt64(id), context.HttpContext.RequestAborted))?.TurnoProduccionId
            };
            if (turnoDelRecurso != null && turnoDelRecurso != turno.TurnoProduccionId)
                return false;
        }
        return true;
    }

    private static bool EsSimple(Type tipo)
    {
        var t = Nullable.GetUnderlyingType(tipo) ?? tipo;
        return t == typeof(int) || t == typeof(long) || t == typeof(DateOnly);
    }

    private static bool Es(string nombre, string[] nombres) => nombres.Any(n => string.Equals(n, nombre, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Regla de un valor: false = fuera del turno (escritura con otra línea / otro turno de producción). En lecturas, el
    /// valor nuevo (<paramref name="nuevo"/>) es el del turno.
    /// </summary>
    private static bool Ajustar(string nombre, Type tipo, object? valor, bool lectura, TurnoDelUsuario turno, out object? nuevo, out bool cambia)
    {
        nuevo = null;
        cambia = false;
        var t = Nullable.GetUnderlyingType(tipo) ?? tipo;

        object? delTurno = null;
        if (t == typeof(int) && Es(nombre, NombresLinea))
            delTurno = turno.LineaId;
        else if (t == typeof(long) && Es(nombre, NombresTurnoProduccion))
            delTurno = turno.TurnoProduccionId;
        else if (lectura && t == typeof(int) && Es(nombre, NombresTurno))
            delTurno = turno.TurnoId;
        else if (lectura && t == typeof(DateOnly) && Es(nombre, NombresRangoFechas))
            delTurno = turno.FechaProduccion;
        if (delTurno == null || Equals(valor, delTurno))
            return true;

        if (lectura)
        {
            nuevo = delTurno;
            cambia = true;
            return true;
        }
        // Escritura: sin valor (opcional) no hay nada que comparar; con otro valor, fuera del turno.
        return valor == null;
    }
}
