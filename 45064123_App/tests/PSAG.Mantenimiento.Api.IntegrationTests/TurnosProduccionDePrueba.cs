using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-798 etapa 3: toda detención cuelga de un turno de producción. Los tests que arman detenciones a mano piden acá
/// el turno (línea + turno de planta + fecha de producción); si no existe se crea CERRADO por SISTEMA, como la
/// conversión de datos (opción B).
/// </summary>
internal static class TurnosProduccionDePrueba
{
    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    public static long TurnoProduccionId(this MantenimientoDbContext context, int lineaId, int turnoId, DateOnly fechaProduccion)
    {
        var existente = context.TurnosProduccion.Local
                            .FirstOrDefault(t => t.LineaId == lineaId && t.TurnoId == turnoId && t.FechaProduccion == fechaProduccion)
                        ?? context.TurnosProduccion
                            .FirstOrDefault(t => t.LineaId == lineaId && t.TurnoId == turnoId && t.FechaProduccion == fechaProduccion);
        if (existente != null)
            return existente.TurnoProduccionId;

        var turno = context.Turnos.Find(turnoId) ?? throw new InvalidOperationException($"No existe el turno {turnoId}.");
        var (inicio, fin) = TurnoProduccionReglas.Ventana(fechaProduccion, turno, ZonaPlanta);
        var nuevo = new TurnoProduccion
        {
            LineaId = lineaId,
            TurnoId = turnoId,
            FechaProduccion = fechaProduccion,
            InicioProgramado = inicio,
            FinProgramado = fin,
            InicioReal = inicio,
            FinReal = fin,
            Estado = EstadoTurnoProduccion.CERRADO,
            OrigenApertura = OrigenAperturaTurno.SISTEMA,
            CreadoPorUsuarioId = "SISTEMA",
            ModificadoPorUsuarioId = "SISTEMA"
        };
        context.TurnosProduccion.Add(nuevo);
        context.SaveChanges();
        return nuevo.TurnoProduccionId;
    }
}
