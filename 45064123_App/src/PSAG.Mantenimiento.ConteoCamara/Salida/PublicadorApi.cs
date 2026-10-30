using System.Text.Json;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Salida;

/// <summary>
/// Junta minutos y paradas, cada SegundosEntreEnvios los escribe como un lote en la carpeta de pendientes
/// y manda a la API todos los pendientes en orden. Un lote se borra sólo cuando la API lo aceptó (2xx):
/// si la API está caída o rechaza (falta el punto de control, clave mal), el lote queda en disco y se reintenta.
/// La API es idempotente (minuto+carril y identificador de parada), así que reenviar no duplica.
/// </summary>
public sealed class PublicadorApi : ISalidaConteo
{
    public const string RutaIngesta = "api/conteo-camara/ingesta";
    public const string HeaderClave = "X-Clave-Ingesta";

    private readonly OpcionesConteo _opciones;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly string _carpeta;
    private readonly object _lock = new();
    private LoteConteo _actual;

    public PublicadorApi(OpcionesConteo opciones, HttpClient http, ILogger logger)
    {
        _opciones = opciones;
        _http = http;
        _logger = logger;
        _carpeta = Path.IsPathRooted(opciones.Api.CarpetaPendientes)
            ? opciones.Api.CarpetaPendientes
            : Path.Combine(AppContext.BaseDirectory, opciones.Api.CarpetaPendientes);
        Directory.CreateDirectory(_carpeta);
        _actual = Nuevo();
    }

    private LoteConteo Nuevo() => new() { PuntoControl = _opciones.PuntoControl };

    public ResultadoVentana? UltimaVentana { get; private set; }

    public void Ventana(ResultadoVentana ventana) => UltimaVentana = ventana;

    public void Minutos(IReadOnlyList<ConteoMinuto> minutos)
    {
        lock (_lock) _actual.Minutos.AddRange(minutos.Select(MinutoLote.Desde));
    }

    public void Parada(ParadaDetectada parada)
    {
        var lote = ParadaLote.Desde(_opciones.PuntoControl, parada);
        _logger.LogInformation("{Tipo} detectada: {Inicio:HH:mm:ss} a {Fin:HH:mm:ss} ({Segundos} s), carriles {Carriles}",
            parada.Tipo, parada.Inicio, parada.Fin, parada.DuracionSegundos, string.Join(",", parada.Carriles));
        lock (_lock) _actual.Paradas.Add(lote);
    }

    /// <summary>Pasa lo juntado a un archivo de pendientes (escritura atómica: .tmp y rename).</summary>
    public void GuardarPendiente()
    {
        LoteConteo lote;
        lock (_lock)
        {
            if (_actual.Vacio) return;
            lote = _actual;
            _actual = Nuevo();
        }
        var nombre = $"lote-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..40] + ".json";
        var destino = Path.Combine(_carpeta, nombre);
        File.WriteAllText(destino + ".tmp", JsonSerializer.Serialize(lote, LoteConteo.Json));
        File.Move(destino + ".tmp", destino, overwrite: true);
    }

    /// <summary>Manda los pendientes en orden. Corta en el primer error para no desordenar.</summary>
    public async Task EnviarPendientesAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opciones.Api.ClaveIngesta))
        {
            _logger.LogWarning("Sin ConteoCamara:Api:ClaveIngesta: los lotes quedan en {Carpeta} hasta configurarla", _carpeta);
            return;
        }

        foreach (var archivo in Directory.GetFiles(_carpeta, "lote-*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var contenido = new StringContent(await File.ReadAllTextAsync(archivo, ct), System.Text.Encoding.UTF8, "application/json");
                using var pedido = new HttpRequestMessage(HttpMethod.Post, RutaIngesta) { Content = contenido };
                pedido.Headers.Add(HeaderClave, _opciones.Api.ClaveIngesta);
                using var respuesta = await _http.SendAsync(pedido, ct);
                if (respuesta.IsSuccessStatusCode)
                {
                    File.Delete(archivo);
                    continue;
                }

                var detalle = await respuesta.Content.ReadAsStringAsync(ct);
                _logger.LogError("La API rechazó {Archivo}: {Status} {Detalle}", Path.GetFileName(archivo),
                    (int)respuesta.StatusCode, detalle.Length > 500 ? detalle[..500] : detalle);
                // No se descarta nada: lo más común es que falte el punto de control en Maestros o la clave.
                // Se corrige eso y el próximo ciclo manda todo lo atrasado.
                return;
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
    }

    public int Pendientes => Directory.GetFiles(_carpeta, "lote-*.json").Length;
}
