namespace PSAG.Mantenimiento.Web.Models;

public class LineaCalendarioDto
{
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool PuedeEditar { get; set; }
}

public class CalendarioLineaDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public bool PuedeEditar { get; set; }
    public decimal HorasEstandar { get; set; }
    public List<TurnoDto> Turnos { get; set; } = new();
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
/// DiaSemana: 0 = domingo ... 6 = sábado (System.DayOfWeek).
/// </summary>
public class CalendarioLineaDiaDto
{
    public int DiaSemana { get; set; }
    public List<int> TurnoIds { get; set; } = new();
    public decimal Horas { get; set; }
}
