namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Detención automática (cámara) para la pantalla de declaración. Horas en hora de la planta.
/// </summary>
public class DeclaracionDetencionDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public DateTimeOffset? FechaHoraParada { get; set; }

    /// <summary>Arranque que vio la cámara: sugerencia, no es la hora declarada.</summary>
    public DateTimeOffset? FechaHoraArranqueDetectada { get; set; }

    public string OrigenRegistro { get; set; } = string.Empty;
    public string? PuntoControlCodigo { get; set; }
    public string? PuntoControlNombre { get; set; }

    /// <summary>Cuerpo que mira la cámara. La pantalla lo trae elegido como equipo y el operador lo puede cambiar.</summary>
    public string? CuerpoCamaraNombre { get; set; }

    /// <summary>Equipo del cuerpo de la cámara, para precargar el equipo (Ferretti 04/11: manda la rebobinadora).</summary>
    public int? CuerpoCamaraEquipoId { get; set; }

    public string? MaquinistaUsuarioId { get; set; }
    public string? MaquinistaNombre { get; set; }

    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public bool PendienteDeclaracion { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Lo que completa el operador. La hora de arranque es hora de pared de la planta (HH:mm): si es menor que la
/// de parada, es del día siguiente (parada que cruza medianoche). Sin hora, minutos declarados.
/// </summary>
public class DeclararDetencionRequest
{
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public TimeOnly? HoraArranque { get; set; }
    public int? MinutosDeclarados { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class DeclararDetencionResponse
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int MinutosEfectivos { get; set; }
    public bool RequiereAnalisis { get; set; }
}

/// <summary>Aviso al operador: se acerca el fin del turno y tiene detenciones sin declarar. No bloquea nada.</summary>
public class AvisoFinTurnoDto
{
    public bool Mostrar { get; set; }

    /// <summary>Pendientes de declaración de las líneas del usuario, de este turno y de turnos anteriores.</summary>
    public int Pendientes { get; set; }

    /// <summary>De <see cref="Pendientes"/>, las que pararon en el turno en curso.</summary>
    public int PendientesTurnoActual { get; set; }

    public int? MinutosParaFin { get; set; }
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }

    /// <summary>Día en que empezó el turno en curso (el nocturno de las 02:00 empezó el día anterior). Turno + fecha identifican al turno.</summary>
    public DateOnly? FechaInicioTurno { get; set; }
    public int MinutosAviso { get; set; }
}
