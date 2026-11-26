using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// T-786: vista previa de la declaración manual del supervisor (corte de energía). Sólo para mostrar: el turno y
/// los minutos que valen los calcula la API con la misma regla (turno de la hora desde; hasta menor que desde = día
/// siguiente).
/// </summary>
public static class DeclaracionManualUi
{
    public static readonly string[] MotivosRapidos = ["Corte de energía", "Reinicio del sistema", "Falla de cámara"];

    /// <summary>Minutos entre desde y hasta; si hasta es menor o igual, termina al día siguiente.</summary>
    public static int Minutos(TimeOnly desde, TimeOnly hasta)
    {
        var minutos = (int)(hasta.ToTimeSpan() - desde.ToTimeSpan()).TotalMinutes;
        return minutos > 0 ? minutos : minutos + 24 * 60;
    }

    /// <summary>Turno activo que contiene la hora (contempla los que cruzan medianoche), o null.</summary>
    public static TurnoDto? TurnoDe(IEnumerable<TurnoDto> turnos, TimeOnly hora)
    {
        var h = hora.ToTimeSpan();
        return turnos.Where(t => t.Activo).FirstOrDefault(t => t.HoraInicio < t.HoraFin
            ? h >= t.HoraInicio && h < t.HoraFin
            : h >= t.HoraInicio || h < t.HoraFin);
    }

    /// <summary>Lo que falta para poder guardar, o null.</summary>
    public static string? Falta(int lineaId, TimeOnly? desde, TimeOnly? hasta, int? tipoFallaId, string? motivo) =>
        lineaId <= 0 ? "Elegí la máquina."
        : desde == null || hasta == null ? "Completá la hora desde y hasta."
        : desde == hasta ? "La hora hasta tiene que ser distinta de la hora desde."
        : tipoFallaId is not > 0 ? "Elegí el tipo de falla."
        : string.IsNullOrWhiteSpace(motivo) ? "Escribí el motivo."
        : null;
}
