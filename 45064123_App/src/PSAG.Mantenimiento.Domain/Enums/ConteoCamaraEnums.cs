namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Estado de un carril en un minuto, según la cámara (T-610). Es el estado con más segundos en el minuto.
/// </summary>
public enum EstadoCarrilCamara
{
    /// <summary>Pasan rollos a la frecuencia de corte.</summary>
    MARCHA = 1,
    /// <summary>No pasa nada por el carril.</summary>
    PARADA = 2,
    /// <summary>Movimiento irregular: rollos pegados o avance a los tirones.</summary>
    ACUMULACION = 3,
    /// <summary>No hubo video (corte del NVR, servicio detenido).</summary>
    SIN_SENAL = 4
}

/// <summary>
/// Tipo de parada detectada por la cámara (T-610).
/// </summary>
public enum TipoParadaCamara
{
    /// <summary>En algún momento quedaron quietos todos los carriles: paró la cortadora.</summary>
    PARADA = 1,
    /// <summary>Nunca pararon todos: uno o más carriles quietos o irregulares mientras otros seguían.</summary>
    ACUMULACION = 2,
    /// <summary>
    /// Cámara de la rebobinadora (T-610, cámara 10): el rodillo siguió girando pero no pasó ninguna manga por la
    /// mesa (manga trabada o máquina vacía). Cuenta como parada; el empalme no llega acá porque siguen pasando mangas.
    /// </summary>
    SIN_MANGAS = 3
}

/// <summary>
/// Cruce de una parada de cámara con las detenciones cargadas (se calcula al consultar, no se guarda:
/// las detenciones se cargan después de la parada).
/// </summary>
public enum CruceDetencionCamara
{
    /// <summary>Hay una detención de la misma línea con horario que se superpone.</summary>
    CON_DETENCION = 1,
    /// <summary>Hay una detención de la misma línea, día y turno pero sin horario: puede ser esta.</summary>
    POSIBLE = 2,
    /// <summary>No hay ninguna detención que la explique: el supervisor la tiene que revisar.</summary>
    SIN_DETENCION = 3
}

/// <summary>
/// Alerta de la cámara (T-610): no hay imagen confiable. Mientras dura, el servicio no detecta paradas
/// (no crea detenciones falsas); queda registrada para que se vea en la app.
/// </summary>
public enum TipoAlertaCamara
{
    /// <summary>La imagen quedó congelada: el reloj de la cámara no avanza o el cuadro es idéntico.</summary>
    CONGELADA = 1,
    /// <summary>No llega video del grabador (pérdida de conexión).</summary>
    SIN_VIDEO = 2
}
