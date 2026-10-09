using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PSAG.Mantenimiento.Infrastructure.Data.Conversions;

/// <summary>
/// Guarda DateTimeOffset como DateTime UTC. Sólo se aplica en SQLite (desarrollo),
/// porque su proveedor no sabe ordenar por DateTimeOffset.
/// </summary>
public sealed class DateTimeOffsetUtcConverter : ValueConverter<DateTimeOffset, DateTime>
{
    public DateTimeOffsetUtcConverter() : base(
        v => v.UtcDateTime,
        v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)))
    {
    }
}
