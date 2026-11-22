namespace PSAG.Mantenimiento.Application.DTOs.ConteoCamara;

// ===================== Ingesta (servicio de conteo, T-610) =====================

/// <summary>
/// Lote que manda el servicio de conteo cada ~30 s (JSON). Contrato en docs/CONTEO_CAMARA.md.
/// </summary>
public class IngestaConteoCamaraRequest
{
    /// <summary>Código de la cámara (Maestros → Cámaras), ej.: T2-CAM09. Define la línea (T-798 etapa 4).</summary>
    public string? Camara { get; set; }

    /// <summary>
    /// Formato viejo (transición): el código que antes era del punto de control. Si no viene <see cref="Camara"/>, la
    /// cámara se busca por este código (la migración conservó los códigos).
    /// </summary>
    public string? PuntoControl { get; set; }

    /// <summary>Código que vale: <see cref="Camara"/> o, si no viene, <see cref="PuntoControl"/>.</summary>
    public string? CodigoCamara => string.IsNullOrWhiteSpace(Camara) ? PuntoControl : Camara;

    public List<MinutoCamaraIngesta> Minutos { get; set; } = [];
    public List<ParadaCamaraIngesta> Paradas { get; set; } = [];

    /// <summary>
    /// Paradas todavía abiertas (la máquina sigue parada), con la última hora en que se la vio parada. Si la cámara
    /// crea detenciones y la parada llega al umbral, se da de alta la detención sin esperar el arranque.
    /// </summary>
    public List<ParadaEnCursoIngesta> ParadasEnCurso { get; set; } = [];

    /// <summary>Alertas de cámara congelada o sin video: se abren con inicio y se cierran reenviando con fin.</summary>
    public List<AlertaCamaraIngesta> Alertas { get; set; } = [];
}

public class ParadaEnCursoIngesta
{
    /// <summary>El mismo identificador que tendrá la parada cuando cierre (punto + inicio).</summary>
    public string IdentificadorExterno { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }

    /// <summary>Última hora en que la cámara vio la máquina parada.</summary>
    public DateTimeOffset Hasta { get; set; }

    public string Tipo { get; set; } = string.Empty;
    public bool InicioIncierto { get; set; }
}

public class AlertaCamaraIngesta
{
    /// <summary>CONGELADA, SIN_VIDEO o SIN_CONFIGURACION (SIN_DATOS la abre la API, no el servicio).</summary>
    public string Tipo { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public string? Detalle { get; set; }
}

public class MinutoCamaraIngesta
{
    public DateTimeOffset Minuto { get; set; }
    public int Carril { get; set; }

    /// <summary>Unidades del minuto en la magnitud de la cámara (T-798 etapa 4).</summary>
    public decimal? Cantidad { get; set; }

    /// <summary>Formato viejo (transición): se usa si no viene <see cref="Cantidad"/>.</summary>
    public decimal? Rollos { get; set; }

    /// <summary>La que vale: <see cref="Cantidad"/> o, si no viene, <see cref="Rollos"/>.</summary>
    public decimal? CantidadEfectiva => Cantidad ?? Rollos;

    public int SegundosMarcha { get; set; }
    public int SegundosParada { get; set; }
    public int SegundosAcumulacion { get; set; }
    public int SegundosSinSenal { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ParadaCamaraIngesta
{
    public string IdentificadorExterno { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }
}

public class IngestaConteoCamaraRespuesta
{
    public int MinutosNuevos { get; set; }
    public int MinutosDuplicados { get; set; }
    public int ParadasNuevas { get; set; }
    public int ParadasDuplicadas { get; set; }

    /// <summary>Detenciones automáticas creadas con este lote (pendientes de declaración).</summary>
    public int DetencionesCreadas { get; set; }

    /// <summary>Detenciones automáticas que ya existían (reenvío o parada que sigue): no se duplican.</summary>
    public int DetencionesExistentes { get; set; }

    /// <summary>Paradas que llegaron al umbral pero no se pudieron dar de alta (sin turno para la hora).</summary>
    public int DetencionesSinTurno { get; set; }

    /// <summary>Minutos que quedaron sin turno de producción (ningún turno de planta cubre la hora). No se pierden.</summary>
    public int MinutosSinTurno { get; set; }

    public int AlertasNuevas { get; set; }
    public int AlertasCerradas { get; set; }
}

// ===================== Consultas =====================

/// <summary>Filtro del listado de paradas por cámara (días de planta).</summary>
public class ParadasCamaraFiltroRequest
{
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int? LineaId { get; set; }

    /// <summary>Sólo las que no tienen detención cargada (SIN_DETENCION).</summary>
    public bool SoloSinDetencion { get; set; }

    /// <summary>Duración mínima en segundos (para ocultar microparadas). 0 = todas.</summary>
    public int SegundosMinimos { get; set; }

    /// <summary>Sólo las de esa cámara (la 09 y la 10 son de la misma línea).</summary>
    public int? CamaraId { get; set; }

    /// <summary>T-784: true = sólo las que llegaron al umbral; false = sólo las microparadas; null = todas.</summary>
    public bool? SuperaUmbral { get; set; }
}

public class ParadaCamaraDto
{
    public long ParadaCamaraId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int CamaraId { get; set; }
    public string CamaraCodigo { get; set; } = string.Empty;
    public long? TurnoProduccionId { get; set; }
    public string? TurnoNombre { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Carriles { get; set; } = string.Empty;
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }

    /// <summary>La parada llegó al umbral de detención guardado al ingresarla (las demás son microparadas).</summary>
    public bool SuperaUmbral { get; set; }

    /// <summary>Supera el umbral y su cámara crea detenciones: tiene (o tendrá) detención automática para declarar.</summary>
    public bool CreaDetencion { get; set; }

    /// <summary>Umbral contra el que se marcó la parada ("5 min", "45 s"), o null si no se conoce.</summary>
    public string? UmbralTexto { get; set; }

    /// <summary>CON_DETENCION, POSIBLE o SIN_DETENCION (calculado al consultar).</summary>
    public string Cruce { get; set; } = string.Empty;
    public List<string> CodigosDetencion { get; set; } = [];
}

public class ParadasCamaraResultado
{
    public List<ParadaCamaraDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int SinDetencion { get; set; }
    public int MinutosSinDetencion { get; set; }
}

/// <summary>
/// Producción de un día de planta según las cámaras, por cámara (nunca se suman rollos de la 09 con mangas de la 10;
/// cada cámara cuenta en su magnitud).
/// El día de planta va del inicio del primer turno al fin del último, con las mismas ventanas que la eficiencia:
/// el nocturno es del día en que empieza (con turnos 05-13-21-05, de 05:00 del día a 05:00 del siguiente).
/// </summary>
public class ProduccionCamaraDto
{
    public DateOnly Fecha { get; set; }
    public int? LineaId { get; set; }

    /// <summary>Ventana del día de planta, en hora de la planta.</summary>
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }

    public List<ProduccionCamaraPuntoDto> Camaras { get; set; } = [];
}

/// <summary>Producción de una cámara: por hora y carril, y por turno.</summary>
public class ProduccionCamaraPuntoDto
{
    public int CamaraId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;

    /// <summary>MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS (de la cámara).</summary>
    public string? Magnitud { get; set; }

    /// <summary>Cuenta la producción oficial de su línea y magnitud.</summary>
    public bool CuentaProduccionOficial { get; set; }

    /// <summary>mangas, rollos, bolsones... (de la magnitud).</summary>
    public string Unidad { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public List<ProduccionCamaraHoraDto> Horas { get; set; } = [];
    public decimal Total { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }

    /// <summary>Total por turno de producción del día (los minutos sin turno de producción van en "Sin turno").</summary>
    public List<ProduccionCamaraTurnoDto> Turnos { get; set; } = [];
}

public class ProduccionCamaraTurnoDto
{
    public long? TurnoProduccionId { get; set; }
    public int? TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset? Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public decimal Total { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
}

/// <summary>Alerta de cámara (congelada, sin video, sin datos, sin configuración), abierta o cerrada hace poco.</summary>
public class AlertaCamaraDto
{
    public long AlertaCamaraId { get; set; }
    public int CamaraId { get; set; }
    public string CamaraCodigo { get; set; } = string.Empty;
    public string CamaraNombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public bool Activa { get; set; }
    public string? Detalle { get; set; }
}

public class ProduccionCamaraHoraDto
{
    /// <summary>Inicio de la hora, en hora de la planta.</summary>
    public DateTimeOffset Hora { get; set; }
    public Dictionary<int, decimal> CantidadPorCarril { get; set; } = [];
    public decimal Total { get; set; }

    /// <summary>Minutos-carril en marcha / con datos (4 carriles x 60 min = 240 en una hora completa).</summary>
    public int MinutosCarrilMarcha { get; set; }
    public int MinutosCarrilConDatos { get; set; }
}

