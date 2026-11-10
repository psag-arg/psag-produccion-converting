namespace PSAG.Mantenimiento.Application.DTOs.ConteoCamara;

// ===================== Ingesta (servicio de conteo, T-610) =====================

/// <summary>
/// Lote que manda el servicio de conteo cada ~30 s (JSON). Contrato en docs/CONTEO_CAMARA.md.
/// </summary>
public class IngestaConteoCamaraRequest
{
    /// <summary>Código del punto de control (Maestros → Calidad), ej.: T2-CAM09. Define la línea.</summary>
    public string PuntoControl { get; set; } = string.Empty;
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
    /// <summary>CONGELADA o SIN_VIDEO.</summary>
    public string Tipo { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public string? Detalle { get; set; }
}

public class MinutoCamaraIngesta
{
    public DateTimeOffset Minuto { get; set; }
    public int Carril { get; set; }
    public decimal Rollos { get; set; }
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
    public int? PuntoControlCalidadId { get; set; }
}

public class ParadaCamaraDto
{
    public long ParadaCamaraId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string PuntoControlCodigo { get; set; } = string.Empty;
    public string? TurnoNombre { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Carriles { get; set; } = string.Empty;
    public bool InicioIncierto { get; set; }
    public bool FinIncierto { get; set; }

    /// <summary>La parada llegó al umbral de detención de su cámara (las demás son microcortes, sólo log).</summary>
    public bool SuperaUmbral { get; set; }

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
/// Producción de un día de planta según las cámaras, por cámara (nunca se suman rollos de la 09 con mangas de la 10).
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
    public int PuntoControlCalidadId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;

    /// <summary>rollos (cámara de carriles), mangas o unidades.</summary>
    public string Unidad { get; set; } = string.Empty;
    public List<int> Carriles { get; set; } = [];
    public List<ProduccionCamaraHoraDto> Horas { get; set; } = [];
    public decimal Total { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }

    /// <summary>Total por turno materializado dentro de la ventana del turno de ese día (igual que la eficiencia).</summary>
    public List<ProduccionCamaraTurnoDto> Turnos { get; set; } = [];
}

public class ProduccionCamaraTurnoDto
{
    public int? TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset? Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public decimal Total { get; set; }
    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
}

/// <summary>Alerta de cámara congelada o sin video (abierta o cerrada hace poco).</summary>
public class AlertaCamaraDto
{
    public long AlertaCamaraId { get; set; }
    public string PuntoControlCodigo { get; set; } = string.Empty;
    public string PuntoControlNombre { get; set; } = string.Empty;
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
    public Dictionary<int, decimal> RollosPorCarril { get; set; } = [];
    public decimal TotalRollos { get; set; }

    /// <summary>Minutos-carril en marcha / con datos (4 carriles x 60 min = 240 en una hora completa).</summary>
    public int MinutosCarrilMarcha { get; set; }
    public int MinutosCarrilConDatos { get; set; }
}

// ===================== Eficiencia por turno (Dashboard, reunión 03/11) =====================

/// <summary>
/// Eficiencia de las cámaras que cuentan producción, para los turnos de un día de planta. Un turno nocturno es del
/// día en que empieza (21:00 del día a 05:00 del siguiente).
/// </summary>
public class EficienciaCamaraDiaDto
{
    public DateOnly Fecha { get; set; }
    public List<EficienciaCamaraPuntoDto> Camaras { get; set; } = [];
}

public class EficienciaCamaraPuntoDto
{
    public int PuntoControlCalidadId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? NumeroCamara { get; set; }

    /// <summary>CARRILES, MANGAS o null (sin cargar en Maestros: la unidad se dedujo de los datos).</summary>
    public string? MetodoConteo { get; set; }

    /// <summary>rollos, mangas o unidades.</summary>
    public string Unidad { get; set; } = string.Empty;

    public decimal? ObjetivoPorHora { get; set; }

    /// <summary>Umbral de detención de la cámara: las paradas más cortas son microcortes.</summary>
    public int MinutosUmbralDetencion { get; set; }

    public List<EficienciaTurnoDto> Turnos { get; set; } = [];
}

public class EficienciaTurnoDto
{
    public int TurnoId { get; set; }
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;

    /// <summary>Inicio y fin del turno en hora de la planta.</summary>
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }

    /// <summary>Horas programadas del turno (calendario de la línea y excepciones del día), en minutos.</summary>
    public int MinutosProgramados { get; set; }

    /// <summary>Unidades contadas por la cámara en el turno (minutos con ese turno materializado, dentro de la ventana).</summary>
    public decimal Contadas { get; set; }

    /// <summary>ObjetivoPorHora x horas programadas. Null si la cámara no tiene objetivo cargado.</summary>
    public decimal? Objetivo { get; set; }

    /// <summary>Contadas / objetivo x 100. Null sin objetivo o con objetivo 0 (turno no programado).</summary>
    public decimal? Porcentaje { get; set; }

    /// <summary>OK, SIN_OBJETIVO o NO_PROGRAMADO.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>SIN_EMPEZAR, EN_CURSO o TERMINADO.</summary>
    public string Momento { get; set; } = string.Empty;

    /// <summary>Turno en curso: parte del objetivo que corresponde al tiempo transcurrido.</summary>
    public decimal? ObjetivoHastaAhora { get; set; }

    /// <summary>Minutos de parada detectados por la cámara dentro del turno (todas las paradas, recortadas al turno).</summary>
    public int MinutosParada { get; set; }

    /// <summary>Paradas de la cámara que empezaron en el turno y duraron menos que el umbral (sólo log).</summary>
    public int Microcortes { get; set; }

    /// <summary>Paradas que empezaron en el turno y llegaron al umbral.</summary>
    public int ParadasLargas { get; set; }

    public int MinutosConDatos { get; set; }
    public int MinutosMarcha { get; set; }
}
