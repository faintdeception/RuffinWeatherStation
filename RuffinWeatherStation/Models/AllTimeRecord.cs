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

        [JsonPropertyName("humidity")]
        public AllTimeRecordEntry? Humidity { get; set; }
    }
}
