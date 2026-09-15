namespace PSAG.Mantenimiento.Application.DTOs.Proceso;

/// <summary>
/// Request para iniciar o retomar un registro de proceso.
/// Si ya existe uno para la combinación, lo retoma.
/// </summary>
public class IniciarRegistroProcesoRequest
{
    public int LineaId { get; set; }
    public int ProductoId { get; set; }
}

/// <summary>
/// Request para guardar valores de parámetros.
/// </summary>
public class GuardarValoresRequest
{
    public long RegistroProcesoId { get; set; }
    public List<ValorParametroRequest> Valores { get; set; } = new();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class ValorParametroRequest
{
    public int ParametroId { get; set; }
    public decimal? ValorReal { get; set; }
    public string? ValorRealTexto { get; set; }
    public string? Observacion { get; set; }
}

/// <summary>
/// Respuesta al iniciar/retomar un registro.
/// </summary>
public class IniciarRegistroProcesoResponse
{
    public long RegistroProcesoId { get; set; }
    public bool EsNuevo { get; set; }
    public string Estado { get; set; } = "PARCIAL";
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Request para filtrar registros de proceso.
/// </summary>
public class RegistroProcesoFiltroRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? LineaId { get; set; }
    public int? ProductoId { get; set; }
    public int? TurnoId { get; set; }
    public string? Estado { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? OrdenarPor { get; set; }
    public bool OrdenDescendente { get; set; } = true;
}

/// <summary>
/// Request para cerrar un registro de proceso.
/// </summary>
public class CerrarRegistroProcesoRequest
{
    public long RegistroProcesoId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
