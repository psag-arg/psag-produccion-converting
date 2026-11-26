using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del maestro de cámaras (CAM_Camaras, T-798 etapa 4, diseño 2.4). Funciones puras: las usan el ABM de
/// Maestros → Cámaras y los tests. Los CHECK de la tabla dicen lo mismo (la base no deja pasar lo que esto rechaza).
/// </summary>
public static class CamaraReglas
{
    public const int CarrilesMaximo = 16;
    public const int CanalMaximo = 999;

    /// <summary>Minutos sin datos de la cámara oficial, con el turno abierto, para abrir la alerta SIN_DATOS (config).</summary>
    public const int MinutosSinDatosPorDefecto = 5;

    /// <summary>Último dato recibido: verde hasta 5 min, amarillo hasta 30 min, rojo después (o nunca).</summary>
    public const int MinutosUltimoDatoVerde = 5;
    public const int MinutosUltimoDatoAmarillo = 30;

    /// <summary>Normaliza un texto de catálogo (mayúsculas, sin espacios al borde; vacío = null).</summary>
    public static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    /// <summary>
    /// Motivo por el que la cámara no se puede guardar (o null). Valida los datos propios; la unicidad del código, la
    /// línea, el equipo y la cámara oficial única por línea y magnitud las valida el servicio contra la base.
    /// Espera <see cref="Camara.Funcion"/>, <see cref="Camara.Magnitud"/> y <see cref="Camara.MetodoDeteccion"/> ya normalizados.
    /// </summary>
    public static string? MotivoInvalida(Camara c)
    {
        var faltan = new List<string>();
        if (c.LineaId <= 0) faltan.Add("línea");
        if (string.IsNullOrWhiteSpace(c.Codigo)) faltan.Add("código");
        if (string.IsNullOrWhiteSpace(c.Nombre)) faltan.Add("nombre");
        if (faltan.Count > 0)
            return "Falta completar: " + string.Join(", ", faltan) + ".";

        if (c.Codigo.Length > Camara.LargoCodigo)
            return $"El código admite hasta {Camara.LargoCodigo} caracteres.";
        if (c.Codigo.Any(char.IsWhiteSpace))
            return "El código no puede tener espacios (ej.: T2-CAM10).";
        if (c.Nombre.Length > Camara.LargoNombre)
            return $"El nombre admite hasta {Camara.LargoNombre} caracteres.";
        if (c.NumeroCanalGrabador is < 1 or > CanalMaximo)
            return $"El canal del grabador tiene que estar entre 1 y {CanalMaximo}.";
        if (!FuncionCamara.Todas.Contains(c.Funcion))
            return "La función tiene que ser CONTEO o CALIDAD.";
        if (c.Magnitud != null && !MagnitudProduccion.Todas.Contains(c.Magnitud))
            return "La magnitud tiene que ser MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS.";
        if (c.MetodoDeteccion != null && !MetodoDeteccionCamara.Todos.Contains(c.MetodoDeteccion))
            return "El método de detección tiene que ser CARRILES o PASOS.";
        if (c.CantidadCarriles is < 1 or > CarrilesMaximo)
            return $"La cantidad de carriles tiene que estar entre 1 y {CarrilesMaximo}.";
        if (DetencionCamaraReglas.MotivoUmbralInvalido(c.UmbralDetencionSegundos) is { } motivoUmbral)
            return motivoUmbral;

        if (c.Funcion == FuncionCamara.Conteo)
        {
            if (c.Magnitud == null)
                return "Una cámara de conteo tiene que decir qué cuenta (magnitud: mangas, rollos, paquetes, bolsones o pallets).";
            if (c.MetodoDeteccion == null)
                return "Una cámara de conteo tiene que tener método de detección (CARRILES o PASOS).";
            if (c.CreaDetenciones && !c.DetectaParadas)
                return "Para crear detenciones la cámara tiene que detectar paradas.";
            return null;
        }

        // CALIDAD: lo del conteo queda vacío.
        if (c.Magnitud != null || c.MetodoDeteccion != null || c.CantidadCarriles != null || c.DetectaParadas
            || c.CreaDetenciones || c.UmbralDetencionSegundos != null || c.CuentaProduccionOficial)
            return "Una cámara de calidad no cuenta producción: sin magnitud, método, carriles, paradas, detenciones, umbral ni producción oficial.";
        return null;
    }

    /// <summary>Estado del último dato para Maestros → Cámaras: VERDE, AMARILLO, ROJO (o SIN_DATOS si nunca mandó).</summary>
    public static string EstadoUltimoDato(DateTimeOffset? ultimo, DateTimeOffset ahora)
    {
        if (ultimo is not { } u)
            return "SIN_DATOS";
        var minutos = (ahora - u).TotalMinutes;
        return minutos <= MinutosUltimoDatoVerde ? "VERDE" : minutos <= MinutosUltimoDatoAmarillo ? "AMARILLO" : "ROJO";
    }
}
