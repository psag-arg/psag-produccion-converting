namespace PSAG.Mantenimiento.Application.DTOs.Inspeccion;

public class EjecucionInspeccionDto
{
    public long EjecucionInspeccionId { get; set; }
    public int PlanInspeccionId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string PlanCodigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public bool Completada { get; set; }
    public int CantidadOk { get; set; }
    public int CantidadNoOk { get; set; }
    public int CantidadTotal { get; set; }
    public string? ObservacionesGenerales { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class EjecucionInspeccionCompletoDto : EjecucionInspeccionDto
{
    public List<EjecucionInspeccionDetalleDto> Detalles { get; set; } = new();
}

public class EjecucionInspeccionDetalleDto
{
    public long EjecucionInspeccionDetalleId { get; set; }
    public long EjecucionInspeccionId { get; set; }
    public int ItemPlanInspeccionId { get; set; }
    public int NumeroItem { get; set; }
    public string ItemDescripcion { get; set; } = string.Empty;
    public string CondicionEstandar { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public string? ResultadoDescripcion { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset? FechaHoraRegistro { get; set; }
}
