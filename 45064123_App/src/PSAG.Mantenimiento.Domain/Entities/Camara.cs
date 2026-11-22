using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Cámara (el dispositivo) — T-798 etapa 4, diseño 2.4. Maestro único: tiene canal, línea y equipo, y está
/// habilitada o no, sea de conteo o de calidad. Las columnas que son sólo del conteo quedan en nulo (o en 0) en las
/// cámaras de calidad (CHECK por función). Borrado lógico con <see cref="CatalogoBase.Activo"/>.
/// <para>
/// <see cref="Habilitada"/> es la única fuente de verdad para el servicio PSAG_ConteoCamara: pide
/// <c>GET api/camaras/configuracion</c> y corre sólo las habilitadas (con calibración local).
/// </para>
/// </summary>
public class Camara : CatalogoBase
{
    public const int LargoCodigo = 30;
    public const int LargoNombre = 100;

    public int CamaraId { get; set; }

    /// <summary>Clave con el servicio (appsettings → Camara). Única, en mayúsculas, sin significado (la línea va por FK).</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }

    /// <summary>Equipo (cuerpo) que ve. De referencia: la detención automática no lo copia (lo declara el operador).</summary>
    public int? EquipoId { get; set; }

    /// <summary>Canal del grabador (8, 9, 10): /Streaming/Channels/{canal}01.</summary>
    public int? NumeroCanalGrabador { get; set; }

    /// <summary>CONTEO o CALIDAD (<see cref="Enums.FuncionCamara"/>).</summary>
    public string Funcion { get; set; } = Enums.FuncionCamara.Conteo;

    /// <summary>MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS. Obligatoria si es CONTEO; nula si es CALIDAD.</summary>
    public string? Magnitud { get; set; }

    /// <summary>CARRILES (brillo por carril, cámara 09) o PASOS (pasos por una zona, cámaras 10 y 8). Sólo CONTEO.</summary>
    public string? MetodoDeteccion { get; set; }

    /// <summary>4 en la 09, 1 en la 10 y la 8. Sólo CONTEO.</summary>
    public byte? CantidadCarriles { get; set; }

    /// <summary>La cámara informa paradas (la 8 no: cuenta bolsones y nada más). Sólo CONTEO.</summary>
    public bool DetectaParadas { get; set; }

    /// <summary>Da de alta detenciones automáticas (pendientes de declaración) cuando una parada llega al umbral. Sólo CONTEO.</summary>
    public bool CreaDetenciones { get; set; }

    /// <summary>
    /// Umbral de detención en segundos (30 a 14400). Reemplaza minutos y segundos del punto de control. Null = el
    /// por defecto (5 min). Lo resuelve <c>DetencionCamaraReglas.UmbralSegundos</c>.
    /// </summary>
    public int? UmbralDetencionSegundos { get; set; }

    /// <summary>
    /// La cámara que alimenta el real de su magnitud en la línea. Una por línea y magnitud (índice único filtrado).
    /// Reemplaza "Cuenta para la eficiencia" (T-785).
    /// </summary>
    public bool CuentaProduccionOficial { get; set; }

    /// <summary>El servicio la corre. Única fuente de verdad (el appsettings del servidor ya no tiene "Activa").</summary>
    public bool Habilitada { get; set; }

    /// <summary>Hora del último lote recibido (se actualiza en cada ingesta).</summary>
    public DateTimeOffset? UltimoDatoRecibido { get; set; }

    public bool EsConteo => Funcion == Enums.FuncionCamara.Conteo;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Equipo? Equipo { get; set; }
}
