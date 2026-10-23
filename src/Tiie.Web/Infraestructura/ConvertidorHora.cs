using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tiie.Web.Infraestructura;

/// <summary>La API envía y recibe las horas como <c>HH:mm</c> (docs/06-api.md#convenciones).</summary>
public sealed class ConvertidorHora : JsonConverter<TimeOnly>
{
    public const string Formato = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        if (TimeOnly.TryParseExact(texto, Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            || TimeOnly.TryParseExact(texto, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out hora))
            return hora;
        throw new JsonException("La hora debe tener el formato HH:mm.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString(Formato, CultureInfo.InvariantCulture));
    }
}
