namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

public class TurnoDto
{
    public int TurnoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class CrearTurnoRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public int Orden { get; set; }
}

public class ActualizarTurnoRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}
