using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// T-805 · Indicadores del turno que ya están en pantalla, para que la barra de turno y el tablero muestren los MISMOS
/// números (mismo cálculo y mismo "ahora"). El tablero se anota como fuente del turno que muestra y publica cada
/// respuesta (<c>TableroTurnoDto.Indicadores</c>); mientras haya fuente, la barra no pide los suyos: usa lo publicado.
/// Sin tablero abierto, la barra pide <c>api/indicadores/turno/{id}</c> (el mismo cálculo de la API) y lo publica.
/// Scoped = uno por pestaña del navegador (Blazor WebAssembly).
/// </summary>
public sealed class IndicadoresEnPantalla
{
    private readonly Dictionary<long, IndicadoresTurnoDto> _ultimos = [];
    private readonly Dictionary<long, int> _fuentes = [];
    private readonly object _lock = new();

    /// <summary>Se publicó un cálculo nuevo de un turno.</summary>
    public event Action<IndicadoresTurnoDto>? Publicado;

    /// <summary>Guarda el cálculo (si no es más viejo que el que había) y avisa.</summary>
    public void Publicar(IndicadoresTurnoDto indicadores)
    {
        lock (_lock)
        {
            if (_ultimos.TryGetValue(indicadores.TurnoProduccionId, out var previo) && previo.CalculadoEn > indicadores.CalculadoEn)
                return;
            _ultimos[indicadores.TurnoProduccionId] = indicadores;
        }
        Publicado?.Invoke(indicadores);
    }

    /// <summary>Último cálculo publicado del turno (null si no hay).</summary>
    public IndicadoresTurnoDto? Ultimo(long turnoProduccionId)
    {
        lock (_lock)
            return _ultimos.GetValueOrDefault(turnoProduccionId);
    }

    /// <summary>Hay una pantalla (el tablero) que publica este turno: la barra usa lo suyo en lugar de pedirlo aparte.</summary>
    public bool TieneFuente(long turnoProduccionId)
    {
        lock (_lock)
            return _fuentes.GetValueOrDefault(turnoProduccionId) > 0;
    }

    /// <summary>
    /// Qué indicadores usa la barra para el turno: los publicados por la fuente si la hay (no pide nada); si no, null
    /// (la barra los pide a la API y los publica).
    /// </summary>
    public IndicadoresTurnoDto? ParaBarra(long turnoProduccionId) =>
        TieneFuente(turnoProduccionId) ? Ultimo(turnoProduccionId) : null;

    /// <summary>Se anota como fuente del turno; al liberar el resultado deja de serlo.</summary>
    public IDisposable AnotarFuente(long turnoProduccionId)
    {
        lock (_lock)
            _fuentes[turnoProduccionId] = _fuentes.GetValueOrDefault(turnoProduccionId) + 1;
        return new Fuente(this, turnoProduccionId);
    }

    private void Liberar(long turnoProduccionId)
    {
        lock (_lock)
        {
            var n = _fuentes.GetValueOrDefault(turnoProduccionId) - 1;
            if (n > 0)
                _fuentes[turnoProduccionId] = n;
            else
                _fuentes.Remove(turnoProduccionId);
        }
    }

    private sealed class Fuente(IndicadoresEnPantalla dueño, long turnoProduccionId) : IDisposable
    {
        private bool _liberada;

        public void Dispose()
        {
            if (_liberada)
                return;
            _liberada = true;
            dueño.Liberar(turnoProduccionId);
        }
    }
}
