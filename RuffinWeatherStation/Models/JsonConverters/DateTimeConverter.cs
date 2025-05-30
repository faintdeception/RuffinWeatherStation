using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Models.JsonConverters
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? dateStr = reader.GetString();
            return dateStr != null ? DateTime.Parse(dateStr) : DateTime.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("O")); // ISO 8601 format
        }
    }
}