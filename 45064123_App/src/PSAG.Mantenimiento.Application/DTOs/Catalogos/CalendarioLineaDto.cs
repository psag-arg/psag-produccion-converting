namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

/// <summary>
/// Calendario base de horas programadas de una línea: versiones activas y turnos para armar la grilla.
/// </summary>
public class CalendarioLineaDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public bool PuedeEditar { get; set; }

    /// <summary>
    /// Horas por día sin calendario: todos los turnos activos de la planta.
    /// </summary>
    public decimal HorasEstandar { get; set; }

    public List<TurnoDto> Turnos { get; set; } = new();

    /// <summary>
    /// Versiones activas, de la más reciente a la más vieja.
    /// </summary>
    public List<CalendarioLineaVersionDto> Versiones { get; set; } = new();
}

public class CalendarioLineaVersionDto
{
    public int CalendarioLineaId { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public List<CalendarioLineaDiaDto> Dias { get; set; } = new();
    public string ModificadoPor { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraModificacion { get; set; }
}

/// <summary>
/// Un día de la semana del calendario. DiaSemana: 0 = domingo ... 6 = sábado.
/// </summary>
public class CalendarioLineaDiaDto
{
    public int DiaSemana { get; set; }
    public List<int> TurnoIds { get; set; } = new();
    public decimal Horas { get; set; }
}

public class GuardarCalendarioLineaRequest
{
    public DateOnly VigenteDesde { get; set; }
    public List<CalendarioLineaDiaDto> Dias { get; set; } = new();
}

public class LineaCalendarioDto
{
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool PuedeEditar { get; set; }
}
