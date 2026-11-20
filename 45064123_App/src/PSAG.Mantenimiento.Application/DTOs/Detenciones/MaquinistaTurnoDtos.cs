namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Lo que se le pregunta al operario después del login (T-783): si va a ser el maquinista del turno en curso en
/// las líneas donde tiene asignación para ese turno. Sin asignación, sólo el aviso.
/// </summary>
public class PropuestaMaquinistaTurnoDto
{
    /// <summary>Hay que mostrarle la pantalla: alguna línea por confirmar, o el aviso de que no tiene relación.</summary>
    public bool Preguntar { get; set; }

    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
    public DateOnly? FechaTurno { get; set; }

    /// <summary>Aviso cuando no tiene relación turno – maquinista – línea: no se lo asigna.</summary>
    public string? Aviso { get; set; }

    public List<OpcionMaquinistaTurnoDto> Opciones { get; set; } = [];
}

public class OpcionMaquinistaTurnoDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int UsuarioLineaId { get; set; }

    /// <summary>La asignación no tiene turno fijo (cubre cualquiera).</summary>
    public bool SinTurnoFijo { get; set; }

    /// <summary>Ya es el maquinista confirmado de este turno: no se le vuelve a preguntar por esta línea.</summary>
    public bool YaConfirmado { get; set; }

    /// <summary>Otro maquinista ya confirmado (o cargado por el supervisor) para este turno; si confirma, lo reemplaza.</summary>
    public string? MaquinistaActualUsuarioId { get; set; }
    public string? MaquinistaActualNombre { get; set; }
    public DateTimeOffset? MaquinistaActualDesde { get; set; }
    public string? MaquinistaActualOrigen { get; set; }
}

public class ConfirmarMaquinistaTurnoRequest
{
    public int LineaId { get; set; }
}

public class ConfirmacionMaquinistaTurnoDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly FechaTurno { get; set; }

    /// <summary>Ya era el maquinista confirmado: no se agregó otro registro.</summary>
    public bool YaEstaba { get; set; }

    /// <summary>Maquinista que figuraba antes para el turno (si lo reemplazó).</summary>
    public string? ReemplazadoUsuarioId { get; set; }
    public string? ReemplazadoNombre { get; set; }

    /// <summary>Detenciones pendientes de declaración del turno que pasaron a tomarlo como maquinista.</summary>
    public int DetencionesReasignadas { get; set; }
}

/// <summary>Pantalla del supervisor "Maquinistas del turno": una fila por línea y turno que empezó ese día.</summary>
public class MaquinistasTurnoDiaDto
{
    public DateOnly Fecha { get; set; }
    public List<MaquinistaTurnoFilaDto> Filas { get; set; } = [];
}

public class MaquinistaTurnoFilaDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public bool EnCurso { get; set; }
    public bool PuedeCorregir { get; set; }

    /// <summary>El maquinista vigente (último registro), o null si nadie confirmó ni lo cargó el supervisor.</summary>
    public MaquinistaTurnoRegistroDto? Vigente { get; set; }

    /// <summary>Sin vigente: el que tomarían las detenciones según Maestros → Maquinistas (regla de siempre).</summary>
    public string? SegunAsignacionUsuarioId { get; set; }
    public string? SegunAsignacionNombre { get; set; }

    /// <summary>Todos los registros del turno, del más viejo al más nuevo.</summary>
    public List<MaquinistaTurnoRegistroDto> Historial { get; set; } = [];

    /// <summary>Maquinistas asignados a la línea (Maestros → Maquinistas): entre ellos elige el supervisor.</summary>
    public List<AsignacionMaquinistaDto> Asignaciones { get; set; } = [];
}

public class MaquinistaTurnoRegistroDto
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string Origen { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;
    public string? RegistradoPorNombre { get; set; }
    public string? Observacion { get; set; }
}

public class AsignacionMaquinistaDto
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
}

public class CorregirMaquinistaTurnoRequest
{
    public int LineaId { get; set; }
    public int TurnoId { get; set; }

    /// <summary>Día en que empezó el turno.</summary>
    public DateOnly FechaTurno { get; set; }

    public int UsuarioLineaId { get; set; }
    public string? Observacion { get; set; }
}

public class CorreccionMaquinistaTurnoDto
{
    public int DetencionesReasignadas { get; set; }
}
