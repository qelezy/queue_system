using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApplication.Models.Reports.Contracts;

public sealed class OrdinalIgnoreCaseStringDictionaryJsonConverter : JsonConverter<Dictionary<string, string?>>
{
    public override Dictionary<string, string?> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for customParams.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dict;

            var key = reader.GetString() ?? string.Empty;
            reader.Read();
            dict[key] = ReadStringValue(ref reader);
        }

        throw new JsonException("Unexpected end when reading customParams object.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, string?> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kv in value)
        {
            writer.WritePropertyName(kv.Key);
            if (kv.Value is null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(kv.Value);
        }

        writer.WriteEndObject();
    }

    private static string? ReadStringValue(ref Utf8JsonReader reader) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out var n) => n.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => throw new JsonException($"Unexpected token {reader.TokenType} in customParams value.")
        };
}
