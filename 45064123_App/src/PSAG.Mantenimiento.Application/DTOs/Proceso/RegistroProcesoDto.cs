namespace PSAG.Mantenimiento.Application.DTOs.Proceso;

public class RegistroProcesoDto
{
    public long RegistroProcesoId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public string Estado { get; set; } = "PARCIAL";
    public string EstadoDescripcion { get; set; } = "Parcial";
    public int CantidadParametros { get; set; }
    public int CantidadCompletados { get; set; }
    public int CantidadDesvios { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class RegistroProcesoDetalleDto
{
    public long RegistroProcesoDetalleId { get; set; }
    public long RegistroProcesoId { get; set; }
    public int ParametroId { get; set; }
    public string ParametroCodigo { get; set; } = string.Empty;
    public string ParametroNombre { get; set; } = string.Empty;
    public string? Seccion { get; set; }
    public string? UnidadMedida { get; set; }
    public string TipoDato { get; set; } = "NUMERICO";
    public string? ValorEstandarSnapshot { get; set; }
    public decimal? ValorReal { get; set; }
    public string? ValorRealTexto { get; set; }
    public bool FueraDeEstandar { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset FechaHoraRegistro { get; set; }
}

public class RegistroProcesoCompletoDto : RegistroProcesoDto
{
    public List<GrupoConDetallesDto> Grupos { get; set; } = new();
}

public class GrupoConDetallesDto
{
    public int GrupoParametroId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public List<RegistroProcesoDetalleDto> Detalles { get; set; } = new();
}
