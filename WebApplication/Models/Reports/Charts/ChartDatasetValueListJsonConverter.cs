using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApplication.Models.Reports.Charts;

/// <summary>Сериализует NaN в JSON null для пропусков на groupedBar-диаграмме.</summary>
public sealed class ChartDatasetValueListJsonConverter : JsonConverter<List<double>>
{
    public override List<double>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected array for chart dataset values.");

        var list = new List<double>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            list.Add(ReadValue(ref reader));
        }

        throw new JsonException("Unexpected end of chart dataset values array.");
    }

    public override void Write(Utf8JsonWriter writer, List<double> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            if (ChartDatasetValues.IsMissing(item))
                writer.WriteNullValue();
            else
                writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static double ReadValue(ref Utf8JsonReader reader) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => ChartDatasetValues.Missing,
            JsonTokenType.Number => reader.GetDouble(),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} in chart dataset values.")
        };
}
