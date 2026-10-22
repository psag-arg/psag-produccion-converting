namespace PSAG.Mantenimiento.Application.DTOs.Analisis;

/// <summary>
/// Identificación de fallas de una detención: falla (lo declara el operario) + solución, causa y plan.
/// Los nombres de usuario los completa la API.
/// </summary>
public class AnalisisDetencionDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public bool RequiereAnalisis { get; set; }

    public string? TipoFallaNombre { get; set; }

    // Regla que lo disparó y responsable
    public int? MinutosMinimosRegla { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
    public string? ResponsableUsuarioNombre { get; set; }
    public string? ResponsableNombre { get; set; }

    // Análisis
    public string? AccionesRealizadas { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public string? AnalisisPorUsuarioId { get; set; }
    public string? AnalisisPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }

    /// <summary>
    /// Puede completar Solución, Causa y Plan, y cerrar el análisis.
    /// </summary>
    public bool PuedeEditar { get; set; }

    /// <summary>
    /// Análisis cerrado que el usuario puede corregir (administración y supervisión).
    /// </summary>
    public bool PuedeEditarCerrado { get; set; }

    /// <summary>
    /// Puede adjuntar fotos: en borrador quien completa el análisis; cerrado, quien puede editarlo.
    /// </summary>
    public bool PuedeAdjuntar { get; set; }

    public List<AdjuntoAnalisisDto> Adjuntos { get; set; } = new();

    /// <summary>
    /// Ediciones hechas después de cerrar, de la más reciente a la más vieja.
    /// </summary>
    public List<AnalisisCambioDto> Cambios { get; set; } = new();

    public List<SolucionDto> FallasParecidas { get; set; } = new();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Foto del análisis. El archivo se baja por GET api/analisis/{detencionId}/adjuntos/{adjuntoId}.
/// </summary>
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

/// <summary>
/// Un cambio del historial de un análisis cerrado.
/// </summary>
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

/// <summary>
/// Resultado de la base de soluciones: Total cuenta todas las que coinciden; Items trae como máximo Limite.
/// </summary>
public class SolucionesResultadoDto
{
    public int Total { get; set; }
    public int Limite { get; set; }
    public List<SolucionDto> Items { get; set; } = new();

    /// <summary>
    /// El usuario puede editar los análisis cerrados (muestra el botón Editar en cada ficha).
    /// </summary>
    public bool PuedeEditar { get; set; }
}

/// <summary>
/// Solución de una falla cerrada (fila de la base de soluciones o de fallas parecidas).
/// </summary>
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

/// <summary>
/// Datos que se guardan del análisis. Al cerrar, Solución y Causa son obligatorias.
/// </summary>
public class GuardarAnalisisRequest
{
    public string? AccionesRealizadas { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Filtros de la base de soluciones.
/// </summary>
public class BuscarSolucionesRequest
{
    public string? Busqueda { get; set; }
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
}

/// <summary>
/// Bandeja "Mis pendientes" del usuario actual, según sus roles.
/// </summary>
public class MisPendientesDto
{
    /// <summary>
    /// Muestra las pestañas de análisis (Para analizar, En análisis, Cerradas por mí).
    /// </summary>
    public bool VeAnalisis { get; set; }

    /// <summary>
    /// Muestra la pestaña Para declarar (operario).
    /// </summary>
    public bool VeDeclaracion { get; set; }

    public bool EsAdministrador { get; set; }

    public List<PendienteAnalisisDto> ParaAnalizar { get; set; } = new();
    public List<PendienteAnalisisDto> EnAnalisis { get; set; } = new();
    public List<PendienteAnalisisDto> CerradasPorMi { get; set; } = new();
    public List<PendienteDeclaracionDto> ParaDeclarar { get; set; } = new();

    public int CerradasEsteMes { get; set; }

    /// <summary>
    /// Días promedio de espera de los análisis abiertos visibles. Null si no hay ninguno.
    /// </summary>
    public double? DemoraPromedioDias { get; set; }

    /// <summary>
    /// Responsables de los análisis abiertos, para el filtro del administrador.
    /// </summary>
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

    /// <summary>
    /// Días desde la fecha de la detención. Solo para análisis abiertos.
    /// </summary>
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

/// <summary>
/// Opción del filtro de responsable: un rol o un usuario asignado.
/// </summary>
public class ResponsableOpcionDto
{
    public string? Rol { get; set; }
    public string? UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
