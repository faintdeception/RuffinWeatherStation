using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Models
{
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

    public class AllTimeRecordContext
    {
        [JsonPropertyName("day")]
        public AllTimeRecordDayContext? Day { get; set; }

        [JsonPropertyName("conditions")]
        public AllTimeRecordConditionsContext? Conditions { get; set; }
    }

    public class AllTimeRecordDayContext
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("avg")]
        public double? Avg { get; set; }

        [JsonPropertyName("min")]
        public double? Min { get; set; }

        [JsonPropertyName("max")]
        public double? Max { get; set; }
    }

    public class AllTimeRecordConditionsContext
    {
        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }

        [JsonPropertyName("wind_speed")]
        public double? WindSpeed { get; set; }

        [JsonPropertyName("lux")]
        public double? Lux { get; set; }
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
