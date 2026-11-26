namespace PSAG.Mantenimiento.Application.DTOs.Camaras;

/// <summary>Cámara del maestro único (Maestros → Cámaras, T-798 etapa 4).</summary>
public class CamaraDto
{
    public int CamaraId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int? NumeroCanalGrabador { get; set; }

    /// <summary>CONTEO o CALIDAD.</summary>
    public string Funcion { get; set; } = string.Empty;

    /// <summary>MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS (sólo CONTEO).</summary>
    public string? Magnitud { get; set; }

    /// <summary>CARRILES o PASOS (sólo CONTEO).</summary>
    public string? MetodoDeteccion { get; set; }

    public int? CantidadCarriles { get; set; }
    public bool DetectaParadas { get; set; }
    public bool CreaDetenciones { get; set; }

    /// <summary>Umbral cargado (null = por defecto, 5 min).</summary>
    public int? UmbralDetencionSegundos { get; set; }

    /// <summary>Umbral efectivo en segundos y legible ("5 min", "45 s").</summary>
    public int UmbralSegundos { get; set; }
    public string UmbralTexto { get; set; } = string.Empty;

    public bool CuentaProduccionOficial { get; set; }
    public bool Habilitada { get; set; }
    public bool Activo { get; set; }

    /// <summary>Hora del último lote recibido (hora de planta), o null si nunca mandó.</summary>
    public DateTimeOffset? UltimoDatoRecibido { get; set; }

    /// <summary>Minutos desde el último dato (al consultar), o null.</summary>
    public int? MinutosDesdeUltimoDato { get; set; }

    /// <summary>VERDE (≤ 5 min), AMARILLO (≤ 30 min), ROJO o SIN_DATOS (nunca mandó).</summary>
    public string EstadoUltimoDato { get; set; } = string.Empty;
}

/// <summary>Alta y edición de una cámara. El código es el que tiene el servicio en su appsettings (Camara).</summary>
public class GuardarCamaraRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? NumeroCanalGrabador { get; set; }
    public string Funcion { get; set; } = "CONTEO";
    public string? Magnitud { get; set; }
    public string? MetodoDeteccion { get; set; }
    public int? CantidadCarriles { get; set; }
    public bool DetectaParadas { get; set; }
    public bool CreaDetenciones { get; set; }

    /// <summary>30 a 14400 segundos; vacío = 5 min.</summary>
    public int? UmbralDetencionSegundos { get; set; }

    public bool CuentaProduccionOficial { get; set; }
    public bool Habilitada { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Lo que pide el servicio PSAG_ConteoCamara (GET api/camaras/configuracion, con la clave de ingesta): cada cámara
/// habilitada y activa. El servicio corre sólo las de CONTEO para las que tiene calibración local.
/// </summary>
public class CamaraConfiguracionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;

    /// <summary>Canal del grabador (si el appsettings del servicio no trae ruta ni canal, usa éste).</summary>
    public int? Canal { get; set; }

    public string Funcion { get; set; } = string.Empty;
    public string? Magnitud { get; set; }
    public string? MetodoDeteccion { get; set; }
    public int? CantidadCarriles { get; set; }
    public bool DetectaParadas { get; set; }
    public bool CreaDetenciones { get; set; }

    /// <summary>Umbral efectivo de detención, en segundos.</summary>
    public int UmbralDetencionSegundos { get; set; }
}
