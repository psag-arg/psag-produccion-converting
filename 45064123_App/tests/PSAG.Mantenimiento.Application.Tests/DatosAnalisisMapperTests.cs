using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Application.Tests;

public class DatosAnalisisMapperTests
{
    [Fact]
    public void CopiaLosCamposDeAnalisisYElEquipoCritico()
    {
        var detencion = new Detencion
        {
            Eliminada = true,
            RequiereAnalisis = true,
            EstadoActual = EstadoDetencion.EN_SEGUIMIENTO,
            ResponsableRol = "MANTENIMIENTO",
            CausaFalla = "Cadena estirada",
            PlanAccion = "Revisar cada 15 días",
            AnalisisPorUsuarioId = "usuario-1",
            FechaHoraAnalisis = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
            Equipo = new Equipo { Critico = true }
        };

        var dto = DatosAnalisisMapper.ConDatosAnalisis(new DetencionDto(), detencion);

        Assert.True(dto.Eliminada);
        Assert.True(dto.RequiereAnalisis);
        Assert.Equal("MANTENIMIENTO", dto.ResponsableRol);
        Assert.Equal("Cadena estirada", dto.CausaFalla);
        Assert.Equal("Revisar cada 15 días", dto.PlanAccion);
        Assert.Equal("usuario-1", dto.AnalisisPorUsuarioId);
        Assert.Equal(detencion.FechaHoraAnalisis, dto.FechaHoraAnalisis);
        Assert.True(dto.EquipoCritico);
        Assert.True(dto.AnalisisPendiente);
        Assert.Equal("En análisis", dto.EstadoAnalisisDescripcion);
    }

    [Fact]
    public void SinAnalisis_NoMarcaNadaPendiente()
    {
        var dto = DatosAnalisisMapper.ConDatosAnalisis(new DetencionDto(), new Detencion());

        Assert.False(dto.RequiereAnalisis);
        Assert.False(dto.AnalisisPendiente);
        Assert.False(dto.EquipoCritico);
        Assert.Null(dto.EstadoAnalisisDescripcion);
    }
}
