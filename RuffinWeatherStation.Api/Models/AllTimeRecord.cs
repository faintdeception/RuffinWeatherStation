using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Api.Models
{
    [BsonIgnoreExtraElements]
    public class AllTimeRecordDocument
    {
        [BsonElement("field")]
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [BsonElement("location")]
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [BsonElement("record_type")]
        [JsonPropertyName("record_type")]
        public string RecordType { get; set; } = string.Empty;

        [BsonElement("value")]
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [BsonElement("timestamp")]
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [BsonElement("date")]
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [BsonElement("context")]
        [JsonPropertyName("context")]
        public AllTimeRecordContext? Context { get; set; }

        [BsonElement("source")]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [BsonElement("sensor_type")]
        [JsonPropertyName("sensor_type")]
        public string? SensorType { get; set; }
    }

    public class AllTimeRecordContext
    {
        [BsonElement("day")]
        [JsonPropertyName("day")]
        public AllTimeRecordDayContext? Day { get; set; }

        [BsonElement("conditions")]
        [JsonPropertyName("conditions")]
        public AllTimeRecordConditionsContext? Conditions { get; set; }
    }

    public class AllTimeRecordDayContext
    {
        [BsonElement("date")]
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [BsonElement("avg")]
        [JsonPropertyName("avg")]
        public double? Avg { get; set; }

        [BsonElement("min")]
        [JsonPropertyName("min")]
        public double? Min { get; set; }

        [BsonElement("max")]
        [JsonPropertyName("max")]
        public double? Max { get; set; }
    }

    public class AllTimeRecordConditionsContext
    {
        [BsonElement("humidity")]
        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }

        [BsonElement("wind_speed")]
        [JsonPropertyName("wind_speed")]
        public double? WindSpeed { get; set; }

        [BsonElement("lux")]
        [JsonPropertyName("lux")]
        public double? Lux { get; set; }
    }

    public class AllTimeRecordEntry
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("context")]
        public AllTimeRecordContext? Context { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("sensor_type")]
        public string? SensorType { get; set; }
    }

    public class AllTimeRecordsResponse
    {
        [JsonPropertyName("hasData")]
        public bool HasData { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("temperature")]
        public AllTimeRecordEntry? Temperature { get; set; }

        [JsonPropertyName("wind_speed")]
        public AllTimeRecordEntry? WindSpeed { get; set; }

        [JsonPropertyName("daily_rainfall")]
        public AllTimeRecordEntry? DailyRainfall { get; set; }
    }
}
