using System.Text.Json;
using System.Text.Json.Serialization;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Api.Configuration;

/// <summary>
/// Toda fecha y hora que sale de la API va con el offset de la planta (Planta:ZonaHoraria): mismo instante,
/// pero su hora de pared es la de la planta. La web la muestra tal cual, sin convertir a la zona del navegador.
/// </summary>
public sealed class FechaHoraPlantaJsonConverter(TimeZoneInfo zonaPlanta) : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(HoraPlanta.En(value, zonaPlanta)!.Value);
}
