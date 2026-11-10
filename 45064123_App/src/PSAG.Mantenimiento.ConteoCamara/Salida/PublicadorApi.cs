using System.Text.Json;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Salida;

/// <summary>
/// Junta minutos y paradas, cada SegundosEntreEnvios los escribe como un lote en la carpeta de pendientes
/// y manda a la API todos los pendientes en orden. Un lote se borra sólo cuando la API lo aceptó (2xx):
/// si la API está caída o rechaza (falta el punto de control, clave mal), el lote queda en disco y se reintenta.
/// Un lote que la API rechaza por su contenido termina en <see cref="CarpetaRechazados"/> (ver EnviarPendientesAsync).
/// La API es idempotente (minuto+carril y identificador de parada), así que reenviar no duplica.
/// <para>
/// Por esa misma idempotencia un minuto no se puede mandar en dos partes: la segunda se ignora. Al detener el
/// servicio el minuto de reloj en curso no se manda: se guarda en <see cref="ArchivoMinutoEnCurso"/> y, al
/// arrancar, se suma al mismo minuto si el video vuelve dentro de él; si no, se manda como quedó.
/// </para>
/// </summary>
public sealed class PublicadorApi : ISalidaConteo
{
    public const string RutaIngesta = "api/conteo-camara/ingesta";
    public const string HeaderClave = "X-Clave-Ingesta";

    /// <summary>Subcarpeta de la de pendientes con los lotes que la API rechazó por su contenido (para revisar a mano).</summary>
    public const string CarpetaRechazados = "rechazados";

    /// <summary>Minuto de reloj que estaba en curso al detener el servicio (no empieza con "lote-": no se envía).</summary>
    public const string ArchivoMinutoEnCurso = "minuto-en-curso.json";

    public const int IntentosAntesDeRechazar = 3;

    private readonly OpcionesConteo _opciones;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly Func<DateTimeOffset> _ahora;
    private readonly string _carpeta;
    private readonly string _carpetaRechazados;
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _rechazos = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MinutoLote> _retomados;
    private LoteConteo _actual;

    public PublicadorApi(OpcionesConteo opciones, HttpClient http, ILogger logger, Func<DateTimeOffset>? ahora = null)
    {
        _opciones = opciones;
        _http = http;
        _logger = logger;
        _ahora = ahora ?? (() => DateTimeOffset.UtcNow);
        _carpeta = Path.IsPathRooted(opciones.Api.CarpetaPendientes)
            ? opciones.Api.CarpetaPendientes
            : Path.Combine(AppContext.BaseDirectory, opciones.Api.CarpetaPendientes);
        _carpetaRechazados = Path.Combine(_carpeta, CarpetaRechazados);
        Directory.CreateDirectory(_carpeta);
        _actual = Nuevo();
        _retomados = LeerMinutoEnCurso();
    }

    private LoteConteo Nuevo() => new() { PuntoControl = _opciones.PuntoControl };

    private string RutaMinutoEnCurso => Path.Combine(_carpeta, ArchivoMinutoEnCurso);

    private List<MinutoLote> LeerMinutoEnCurso()
    {
        if (!File.Exists(RutaMinutoEnCurso)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<MinutoLote>>(File.ReadAllText(RutaMinutoEnCurso), LoteConteo.Json) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("No se pudo leer {Archivo} ({Mensaje}): se descarta el minuto en curso anterior", ArchivoMinutoEnCurso, ex.Message);
            return [];
        }
    }

    public ResultadoVentana? UltimaVentana { get; private set; }

    public void Ventana(ResultadoVentana ventana) => UltimaVentana = ventana;

    public void Minutos(IReadOnlyList<ConteoMinuto> minutos)
    {
        lock (_lock)
        {
            foreach (var m in minutos)
            {
                var lote = MinutoLote.Desde(m);
                // El resto del minuto que estaba en curso al detener el servicio: se suma y va una sola vez.
                var i = _retomados.FindIndex(r => r.Minuto == lote.Minuto && r.Carril == lote.Carril);
                if (i >= 0)
                {
                    lote = Sumar(_retomados[i], lote);
                    _retomados.RemoveAt(i);
                }
                _actual.Minutos.Add(lote);
            }
            // El análisis ya entregó minutos posteriores: lo retenido no se va a completar, va como quedó.
            if (minutos.Count > 0)
            {
                var ultimo = minutos.Max(m => m.Minuto);
                LiberarRetomados(r => r.Minuto <= ultimo);
            }
        }
    }

    private void LiberarRetomados(Predicate<MinutoLote> condicion)
    {
        _actual.Minutos.AddRange(_retomados.FindAll(condicion));
        _retomados.RemoveAll(condicion);
    }

    /// <summary>Las dos partes de un minuto partido por un reinicio. Lo que falta para 60 s sigue siendo SIN_SENAL.</summary>
    internal static MinutoLote Sumar(MinutoLote a, MinutoLote b)
    {
        var marcha = a.SegundosMarcha + b.SegundosMarcha;
        var parada = a.SegundosParada + b.SegundosParada;
        var acumulacion = a.SegundosAcumulacion + b.SegundosAcumulacion;
        var exceso = marcha + parada + acumulacion - 60;   // redondeos de las dos partes
        if (exceso > 0)
        {
            if (marcha >= parada && marcha >= acumulacion) marcha -= exceso;
            else if (parada >= acumulacion) parada -= exceso;
            else acumulacion -= exceso;
        }
        return MinutoLote.Desde(new ConteoMinuto(a.Minuto, a.Carril, (double)(a.Rollos + b.Rollos), marcha, parada, acumulacion,
            Math.Max(0, 60 - marcha - parada - acumulacion)));
    }

    public void Parada(ParadaDetectada parada)
    {
        var lote = ParadaLote.Desde(_opciones.PuntoControl, parada);
        _logger.LogInformation("{Tipo} detectada: {Inicio:HH:mm:ss} a {Fin:HH:mm:ss} ({Segundos} s), carriles {Carriles}",
            parada.Tipo, parada.Inicio, parada.Fin, parada.DuracionSegundos, string.Join(",", parada.Carriles));
        lock (_lock) _actual.Paradas.Add(lote);
    }

    public void ParadaEnCurso(ParadaEnCurso parada)
    {
        var lote = ParadaEnCursoLote.Desde(_opciones.PuntoControl, parada);
        lock (_lock)
        {
            // Sólo interesa la última vista de cada parada abierta.
            _actual.ParadasEnCurso.RemoveAll(p => p.IdentificadorExterno == lote.IdentificadorExterno);
            _actual.ParadasEnCurso.Add(lote);
        }
    }

    public void Alerta(AlertaVideo alerta)
    {
        if (alerta.Fin == null)
            _logger.LogWarning("Alerta de cámara {Punto}: {Tipo} desde {Inicio:HH:mm:ss}", _opciones.PuntoControl, alerta.Tipo, alerta.Inicio);
        else
            _logger.LogInformation("Alerta de cámara {Punto} cerrada: {Tipo} de {Inicio:HH:mm:ss} a {Fin:HH:mm:ss}", _opciones.PuntoControl, alerta.Tipo, alerta.Inicio, alerta.Fin);
        lock (_lock) _actual.Alertas.Add(AlertaLote.Desde(alerta));
    }

    /// <summary>
    /// Pasa lo juntado a un archivo de pendientes (escritura atómica: .tmp y rename). Con
    /// <paramref name="apartarMinutoEnCurso"/> (al detener el servicio) el minuto de reloj actual no va al lote:
    /// se guarda en <see cref="ArchivoMinutoEnCurso"/> para completarlo al arrancar.
    /// </summary>
    public void GuardarPendiente(bool apartarMinutoEnCurso = false)
    {
        LoteConteo lote;
        List<MinutoLote>? enCurso = null;
        lock (_lock)
        {
            // Retenido de antes del arranque que ya no se puede completar (pasó su minuto y no llegó video).
            var ahora = _ahora();
            LiberarRetomados(r => r.Minuto.AddMinutes(2) <= ahora);
            if (apartarMinutoEnCurso)
            {
                enCurso = [.. _retomados, .. _actual.Minutos.Where(m => m.Minuto.AddMinutes(1) > ahora)];
                _actual.Minutos.RemoveAll(m => m.Minuto.AddMinutes(1) > ahora);
                _retomados.Clear();
            }
            lote = _actual;
            _actual = Nuevo();
        }
        if (!lote.Vacio)
        {
            var nombre = $"lote-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..40] + ".json";
            EscribirAtomico(Path.Combine(_carpeta, nombre), JsonSerializer.Serialize(lote, LoteConteo.Json));
        }

        // El archivo del minuto en curso se borra recién cuando lo retenido ya quedó escrito en un lote.
        if (enCurso is { Count: > 0 })
            EscribirAtomico(RutaMinutoEnCurso, JsonSerializer.Serialize(enCurso, LoteConteo.Json));
        else if (File.Exists(RutaMinutoEnCurso) && SinRetenidos)
            File.Delete(RutaMinutoEnCurso);
    }

    private bool SinRetenidos
    {
        get { lock (_lock) return _retomados.Count == 0; }
    }

    private static void EscribirAtomico(string destino, string contenido)
    {
        File.WriteAllText(destino + ".tmp", contenido);
        File.Move(destino + ".tmp", destino, overwrite: true);
    }

    /// <summary>
    /// Manda los pendientes en orden. API caída, 5xx, timeout o clave mal (401/403/404/408/429): corta y el
    /// próximo ciclo reintenta desde el mismo lote, sin desordenar. Un 4xx propio del lote (400 de validación,
    /// 413...) no traba la cola: se sigue con los siguientes y, después de <see cref="IntentosAntesDeRechazar"/>
    /// rechazos en ciclos en que la API sí aceptó otro lote, se aparta a <see cref="CarpetaRechazados"/>.
    /// Si dos lotes seguidos reciben el mismo rechazo el problema no es del lote (ej. falta el punto de control
    /// en Maestros, que también es 400): se corta como antes y no se aparta nada.
    /// </summary>
    public async Task EnviarPendientesAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opciones.Api.ClaveIngesta))
        {
            _logger.LogWarning("Sin ConteoCamara:Api:ClaveIngesta: los lotes quedan en {Carpeta} hasta configurarla", _carpeta);
            return;
        }

        var aceptados = 0;
        var rechazados = new List<string>();
        string? rechazoAnterior = null;
        foreach (var archivo in Directory.GetFiles(_carpeta, "lote-*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var nombre = Path.GetFileName(archivo);
            try
            {
                using var contenido = new StringContent(await File.ReadAllTextAsync(archivo, ct), System.Text.Encoding.UTF8, "application/json");
                using var pedido = new HttpRequestMessage(HttpMethod.Post, RutaIngesta) { Content = contenido };
                pedido.Headers.Add(HeaderClave, _opciones.Api.ClaveIngesta);
                using var respuesta = await _http.SendAsync(pedido, ct);
                if (respuesta.IsSuccessStatusCode)
                {
                    File.Delete(archivo);
                    _rechazos.Remove(nombre);
                    aceptados++;
                    rechazoAnterior = null;
                    continue;
                }

                var status = (int)respuesta.StatusCode;
                var detalle = await respuesta.Content.ReadAsStringAsync(ct);
                var motivo = MotivoRechazo(detalle);
                if (!EsRechazoDelLote(status) || motivo == rechazoAnterior)
                {
                    _logger.LogError("La API rechazó {Archivo}: {Status} {Detalle}", nombre, status, Recortar(detalle));
                    // No se descarta nada: lo más común es que falte el punto de control en Maestros o la clave.
                    // Se corrige eso y el próximo ciclo manda todo lo atrasado.
                    return;
                }
                rechazoAnterior = motivo;
                _rechazos[nombre] = _rechazos.GetValueOrDefault(nombre) + 1;
                rechazados.Add(archivo);
                _logger.LogWarning("La API rechazó {Archivo} ({Intento}/{Maximo}): {Status} {Detalle}; se sigue con los siguientes",
                    nombre, _rechazos[nombre], IntentosAntesDeRechazar, status, Recortar(detalle));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("API no disponible ({Mensaje}); se reintenta más tarde", ex.Message);
                return;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("La API no respondió a tiempo; se reintenta más tarde");
                return;
            }
        }

        // Sólo se aparta un lote si la API aceptó otro en este ciclo: prueba de que el problema es ese lote.
        if (aceptados == 0) return;
        foreach (var archivo in rechazados.Where(a => _rechazos[Path.GetFileName(a)] >= IntentosAntesDeRechazar))
        {
            var nombre = Path.GetFileName(archivo);
            Directory.CreateDirectory(_carpetaRechazados);
            File.Move(archivo, Path.Combine(_carpetaRechazados, nombre), overwrite: true);
            _rechazos.Remove(nombre);
            _logger.LogWarning("Lote {Archivo} rechazado {Intentos} veces por la API: se aparta a {Carpeta} para no trabar los siguientes",
                nombre, IntentosAntesDeRechazar, _carpetaRechazados);
        }
    }

    /// <summary>
    /// 4xx que dependen del contenido del lote. 401/403 (clave), 404 (ruta: API vieja), 408 y 429 son del
    /// servidor o de la configuración y afectan a todos los lotes por igual.
    /// </summary>
    internal static bool EsRechazoDelLote(int status) =>
        status is >= 400 and < 500 and not (401 or 403 or 404 or 408 or 429);

    /// <summary>El "detail" del ProblemDetails (sin traceId, que cambia en cada respuesta) o el cuerpo entero.</summary>
    private static string MotivoRechazo(string cuerpo)
    {
        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString()!;
        }
        catch (JsonException) { }
        return cuerpo;
    }

    private static string Recortar(string texto) => texto.Length > 500 ? texto[..500] : texto;

    public int Pendientes => Directory.GetFiles(_carpeta, "lote-*.json").Length;
}
