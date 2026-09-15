namespace PSAG.Mantenimiento.Web.Models;

public class DetencionDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int? TipoFallaId { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int? DetalleFallaId { get; set; }
    public string? DetalleFallaNombre { get; set; }
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    // Modelo de tiempo
    public int? MinutosDeclarados { get; set; }
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranque { get; set; }
    public string OrigenTiempo { get; set; } = string.Empty;
    public int MinutosEfectivos { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public string? AccionesRealizadas { get; set; }
    public string? ResolucionDescripcion { get; set; }
    public DateTimeOffset? FechaHoraCierre { get; set; }
    public string? CreadoPorUsuarioId { get; set; }
    public DateTimeOffset FechaHoraCreacion { get; set; }
    public DateTimeOffset? FechaHoraModificacion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class DetencionDetalleDto : DetencionDto
{
    public List<DetencionNotaDto> Notas { get; set; } = new();
    public List<DetencionHistorialDto> HistorialEstados { get; set; } = new();
}

public class DetencionNotaDto
{
    public long DetencionNotaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string TipoNota { get; set; } = string.Empty;
    public bool EsNotaDeCierre { get; set; }
}

public class DetencionHistorialDto
{
    public long DetencionEstadoHistorialId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

public class CrearDetencionRequest
{
    public int LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }
    public int? UsuarioLineaId { get; set; }  // Maquinista asignado (opcional, para admin/supervisor)
    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int? MinutosDeclarados { get; set; }
    public string OrigenTiempo { get; set; } = "DECLARADO";
    public string Descripcion { get; set; } = string.Empty;
}

public class CrearDetencionResponse
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int MinutosEfectivos { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }
}

public class CerrarDetencionRequest
{
    public string? AccionesRealizadas { get; set; }
    public string ResolucionDescripcion { get; set; } = string.Empty;
    public string? ObservacionCierre { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class ResumenTurnoResponse
{
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public List<ResumenTurnoItem> Items { get; set; } = new();
    public int TotalMinutos { get; set; }
}

public class ResumenTurnoItem
{
    public int? TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public int CantidadDetenciones { get; set; }
    public int TotalMinutos { get; set; }
}
