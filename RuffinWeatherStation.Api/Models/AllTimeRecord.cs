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
