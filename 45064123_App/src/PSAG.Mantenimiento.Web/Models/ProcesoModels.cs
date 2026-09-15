namespace PSAG.Mantenimiento.Web.Models;

public class ProductoDto
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

public class GrupoParametroDto
{
    public int GrupoParametroId { get; set; }
    public int LineaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public List<ParametroDto> Parametros { get; set; } = new();
}

public class ParametroDto
{
    public int ParametroId { get; set; }
    public int GrupoParametroId { get; set; }
    public string? Seccion { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? UnidadMedida { get; set; }
    public string TipoDato { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

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
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public int CantidadCompletados { get; set; }
    public int CantidadDesvios { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class RegistroProcesoCompletoDto : RegistroProcesoDto
{
    public int CantidadParametros { get; set; }
    public List<GrupoConDetallesDto> Grupos { get; set; } = new();
}

public class GrupoConDetallesDto
{
    public int GrupoParametroId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public List<RegistroProcesoDetalleDto> Detalles { get; set; } = new();
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
    public string TipoDato { get; set; } = string.Empty;
    public string? ValorEstandarSnapshot { get; set; }
    public decimal? ValorReal { get; set; }
    public string? ValorRealTexto { get; set; }
    public bool FueraDeEstandar { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset FechaHoraRegistro { get; set; }
}

public class IniciarRegistroProcesoRequest
{
    public int LineaId { get; set; }
    public int ProductoId { get; set; }
}

public class IniciarRegistroProcesoResponse
{
    public long RegistroProcesoId { get; set; }
    public bool EsNuevo { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class GuardarValoresRequest
{
    public long RegistroProcesoId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ValorParametroRequest> Valores { get; set; } = new();
}

public class ValorParametroRequest
{
    public int ParametroId { get; set; }
    public decimal? ValorReal { get; set; }
    public string? ValorRealTexto { get; set; }
    public string? Observacion { get; set; }
}

public class CerrarRegistroProcesoRequest
{
    public long RegistroProcesoId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
