using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Copia los campos de eliminación y análisis de la entidad a cualquier vista de detención.
/// Es el único lugar donde se hace.
/// </summary>
public static class DatosAnalisisMapper
{
    public static T ConDatosAnalisis<T>(T dto, Detencion detencion) where T : DatosAnalisisDetencion
    {
        dto.Eliminada = detencion.Eliminada;
        dto.RequiereAnalisis = detencion.RequiereAnalisis;
        dto.ResponsableRol = detencion.ResponsableRol;
        dto.ResponsableUsuarioId = detencion.ResponsableUsuarioId;
        dto.CausaFalla = detencion.CausaFalla;
        dto.PlanAccion = detencion.PlanAccion;
        dto.AnalisisPorUsuarioId = detencion.AnalisisPorUsuarioId;
        dto.FechaHoraAnalisis = detencion.FechaHoraAnalisis;
        dto.EquipoCritico = detencion.Equipo?.Critico ?? false;
        dto.AnalisisPendiente = AnalisisFallaReglas.EstaAbierto(detencion);
        dto.EstadoAnalisisDescripcion = detencion.ObtenerDescripcionAnalisis();
        return dto;
    }
}
