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
    public int? LineaId { get; set; }
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
    public string TipoDatoDescripcion { get; set; } = string.Empty;
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public string Frecuencia { get; set; } = "POR_TURNO";
    public string FrecuenciaDescripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class RegistroProcesoDto
{
    public long RegistroProcesoId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? ProductoId { get; set; }
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
    public string TipoDatoDescripcion { get; set; } = string.Empty;
    public string Frecuencia { get; set; } = string.Empty;
    public string FrecuenciaDescripcion { get; set; } = string.Empty;
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public int Orden { get; set; }
    public bool FueraDePlantilla { get; set; }
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
    public int? ProductoId { get; set; }
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

/// <summary>
/// Tipos de dato y frecuencias de los puntos de Control de Proceso (T-545). Mismos códigos que la API.
/// </summary>
public static class ControlProcesoCatalogo
{
    public const string Numerico = "NUMERICO";
    public const string Rango = "RANGO";
    public const string Texto = "TEXTO";
    public const string OkNoOk = "OK_NOOK";
    public const string Ok = "OK";
    public const string NoOk = "NO OK";

    public static readonly (string Codigo, string Nombre)[] Tipos =
    {
        (Numerico, "Numérico (rango mín/máx)"),
        (OkNoOk, "OK / No OK"),
        (Texto, "Texto")
    };

    public static readonly (string Codigo, string Nombre)[] Frecuencias =
    {
        ("POR_TURNO", "Por turno"),
        ("INICIO_TURNO", "Inicio de turno"),
        ("CAMBIO_PRODUCTO", "Cambio de producto"),
        ("CADA_HORA", "Cada hora"),
        ("CADA_2_HORAS", "Cada 2 horas"),
        ("DIARIA", "Diaria")
    };

    public static bool EsNumerico(string tipo) => tipo == Numerico || tipo == Rango;
}

public class GuardarGrupoControlRequest
{
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class GuardarPuntoControlRequest
{
    public int GrupoParametroId { get; set; }
    public string? Seccion { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoDato { get; set; } = ControlProcesoCatalogo.Numerico;
    public string? UnidadMedida { get; set; }
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public string Frecuencia { get; set; } = "POR_TURNO";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
