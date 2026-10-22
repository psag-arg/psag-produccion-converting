namespace PSAG.Mantenimiento.Web.Models;

public class AnalisisDetencionDto
{
    public long DetencionId { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int? MinutosMinimosRegla { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
    public string? ResponsableUsuarioNombre { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? AccionesRealizadas { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public string? AnalisisPorUsuarioId { get; set; }
    public string? AnalisisPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }
    public bool PuedeEditar { get; set; }
    public bool PuedeEditarCerrado { get; set; }
    public bool PuedeAdjuntar { get; set; }
    public List<AdjuntoAnalisisDto> Adjuntos { get; set; } = new();
    public List<AnalisisCambioDto> Cambios { get; set; } = new();
    public List<SolucionDto> FallasParecidas { get; set; } = new();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class AdjuntoAnalisisDto
{
    public long AnalisisAdjuntoId { get; set; }
    public long DetencionId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }
    public DateTimeOffset FechaHora { get; set; }
}

public class AnalisisCambioDto
{
    public string Campo { get; set; } = string.Empty;
    public string CampoDescripcion { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }
    public DateTimeOffset FechaHora { get; set; }
}

public class SolucionesResultadoDto
{
    public int Total { get; set; }
    public int Limite { get; set; }
    public List<SolucionDto> Items { get; set; } = new();
    public bool PuedeEditar { get; set; }
}

public class SolucionDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int MinutosEfectivos { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? AccionesRealizadas { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public string? AnalisisPorUsuarioId { get; set; }
    public string? AnalisisPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }
    public List<AdjuntoAnalisisDto> Adjuntos { get; set; } = new();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class GuardarAnalisisRequest
{
    public string? AccionesRealizadas { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class MisPendientesDto
{
    public bool VeAnalisis { get; set; }
    public bool VeDeclaracion { get; set; }
    public bool EsAdministrador { get; set; }
    public List<PendienteAnalisisDto> ParaAnalizar { get; set; } = new();
    public List<PendienteAnalisisDto> EnAnalisis { get; set; } = new();
    public List<PendienteAnalisisDto> CerradasPorMi { get; set; } = new();
    public List<PendienteDeclaracionDto> ParaDeclarar { get; set; } = new();
    public int CerradasEsteMes { get; set; }
    public double? DemoraPromedioDias { get; set; }
    public List<ResponsableOpcionDto> Responsables { get; set; } = new();
}

public class PendienteAnalisisDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public bool EquipoCritico { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int MinutosEfectivos { get; set; }
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public string? EstadoAnalisisDescripcion { get; set; }
    public int? DiasEsperando { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
    public string? ResponsableUsuarioNombre { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? AnalisisPorUsuarioId { get; set; }
    public string? AnalisisPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }
}

public class PendienteDeclaracionDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranque { get; set; }
    public int MinutosEfectivos { get; set; }
    public string EstadoActualDescripcion { get; set; } = string.Empty;
}

public class ResponsableOpcionDto
{
    public string? Rol { get; set; }
    public string? UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class ConteoPendientesDto
{
    public int Total { get; set; }
}

public class BuscarSolucionesFiltro
{
    public string? Busqueda { get; set; }
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
}
