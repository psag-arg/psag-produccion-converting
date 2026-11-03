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
    public string? UsuarioNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public bool Completada { get; set; }

    /// <summary>EN_CURSO o CERRADA (cerrada = firmada, ya no se edita).</summary>
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;

    /// <summary>Quién realiza (PACKAGING, MECANICO...). Null = todos los puntos.</summary>
    public string? Puesto { get; set; }
    public string PuestoDescripcion { get; set; } = string.Empty;
    public string? UsuarioCierreId { get; set; }
    public string? UsuarioCierreNombre { get; set; }
    public int CantidadOk { get; set; }
    public int CantidadNoOk { get; set; }
    public int CantidadTotal { get; set; }
    public string? ObservacionesGenerales { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class EjecucionInspeccionCompletoDto : EjecucionInspeccionDto
{
    public string? PlanDescripcion { get; set; }

    /// <summary>Puntos que vencen hoy (los que hay que responder para cerrar).</summary>
    public int CantidadVencenHoy { get; set; }
    public int CantidadVencenHoyRespondidos { get; set; }
    public bool PuedeCerrar { get; set; }
    public List<EjecucionInspeccionDetalleDto> Detalles { get; set; } = new();
}

/// <summary>
/// Un punto de la inspección con su respuesta (si ya se respondió).
/// </summary>
public class EjecucionInspeccionDetalleDto
{
    public long EjecucionInspeccionDetalleId { get; set; }
    public long EjecucionInspeccionId { get; set; }
    public int ItemPlanInspeccionId { get; set; }
    public int NumeroItem { get; set; }
    public string? Codigo { get; set; }
    public string? ParteEquipo { get; set; }

    /// <summary>Tareas.</summary>
    public string ItemDescripcion { get; set; } = string.Empty;

    /// <summary>Estándar: si ya se respondió, el que tenía ese día (snapshot).</summary>
    public string CondicionEstandar { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string QuienRealiza { get; set; } = string.Empty;
    public string QuienRealizaDescripcion { get; set; } = string.Empty;
    public int FrecuenciaDias { get; set; }
    public string FrecuenciaDescripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool VenceHoy { get; set; } = true;
    public DateOnly? UltimaRespuesta { get; set; }
    public DateOnly? ProximoVencimiento { get; set; }

    /// <summary>El punto ya no está en el plan (se desactivó) pero tenía respuesta: se ve, no se edita.</summary>
    public bool FueraDePlantilla { get; set; }

    /// <summary>OK, NO_OK o null (sin responder).</summary>
    public string? Resultado { get; set; }
    public string? ResultadoDescripcion { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset? FechaHoraRegistro { get; set; }
}
