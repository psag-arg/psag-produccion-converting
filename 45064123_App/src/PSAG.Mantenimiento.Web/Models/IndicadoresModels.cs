namespace PSAG.Mantenimiento.Web.Models;

// T-798 etapa 5 · Indicadores (api/indicadores): espejo de los DTO de la API. Los números los calcula la API.

public class ComparacionProduccionDto
{
    public string Magnitud { get; set; } = string.Empty;
    public decimal? Objetivo { get; set; }
    public decimal? Real { get; set; }
    public decimal? Porcentaje { get; set; }

    /// <summary>OK, SIN_OBJETIVO o SIN_DATOS.</summary>
    public string Estado { get; set; } = string.Empty;
    public bool Provisorio { get; set; }
    public int TramosSinDatos { get; set; }
    public int TramosSinObjetivo { get; set; }
}

public class FilaPresupuestoRealDto
{
    public string Clave { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public long? TurnoProduccionId { get; set; }
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public DateOnly? FechaProduccion { get; set; }
    public string? TurnoCodigo { get; set; }
    public string? TurnoNombre { get; set; }
    public int? ProductoMaestroId { get; set; }
    public string? ProductoNombre { get; set; }
    public int Turnos { get; set; }
    public bool IncluyeAbiertos { get; set; }
    public int? MinutosProgramados { get; set; }
    public int? MinutosParadaQueCuentan { get; set; }
    public decimal? Indisponibilidad { get; set; }
    public ComparacionProduccionDto Mangas { get; set; } = new();
    public ComparacionProduccionDto Bolsones { get; set; } = new();
    public ComparacionProduccionDto Eficiencia { get; set; } = new();
    public decimal? VelocidadMetrosMinuto { get; set; }
}

public class PresupuestoRealDto
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int? LineaId { get; set; }
    public int? ProductoMaestroId { get; set; }
    public string Agrupar { get; set; } = "turno";
    public List<FilaPresupuestoRealDto> Filas { get; set; } = [];
    public FilaPresupuestoRealDto Total { get; set; } = new();
}
