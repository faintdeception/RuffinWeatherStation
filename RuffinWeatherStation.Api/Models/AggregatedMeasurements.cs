using System;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RuffinWeatherStation.Api.Models
{
    [BsonIgnoreExtraElements]
    public class HourlyMeasurement
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [BsonElement("timestamp")]
        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [BsonElement("timestamp_ms")]
        [JsonPropertyName("timestamp_ms")]
        public DateTime TimestampMs { get; set; }

        [BsonElement("hour_timestamp")]
        [JsonPropertyName("hour_timestamp")]
        public long? HourTimestamp { get; set; }

        [BsonElement("fields")]
        [JsonPropertyName("fields")]
        public HourlyFields Fields { get; set; } = new();
        
        [BsonElement("tags")]
        [JsonPropertyName("tags")]
        public WeatherTags? Tags { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    public class HourlyFields
    {
        [BsonElement("temperature")]
        [JsonPropertyName("temperature")]
        public TemperatureStats Temperature { get; set; } = new();

        [BsonElement("humidity")]
        [JsonPropertyName("humidity")]
        public AverageStats Humidity { get; set; } = new();

        [BsonElement("pressure")]
        [JsonPropertyName("pressure")]
        public AverageStats Pressure { get; set; } = new();

        [BsonElement("wind_speed")]
        [JsonPropertyName("wind_speed")]
        public WindStats WindSpeed { get; set; } = new();

        [BsonElement("lux")]
        [JsonPropertyName("lux")]
        public AverageStats Lux { get; set; } = new();

        [BsonElement("sample_count")]
        [JsonPropertyName("sample_count")]
        public int SampleCount { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class DailyMeasurement
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [BsonElement("timestamp")]
        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [BsonElement("timestamp_ms")]
        [JsonPropertyName("timestamp_ms")]
        public DateTime TimestampMs { get; set; }

        [BsonElement("day_timestamp")]
        [JsonPropertyName("day_timestamp")]
        public long? DayTimestamp { get; set; }

        [BsonElement("fields")]
        [JsonPropertyName("fields")]
        public DailyFields Fields { get; set; } = new();
        
        [BsonElement("tags")]
        [JsonPropertyName("tags")]
        public WeatherTags? Tags { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    public class DailyFields
    {
        [BsonElement("temperature")]
        [JsonPropertyName("temperature")]
        public TemperatureStats Temperature { get; set; } = new();

        [BsonElement("humidity")]
        [JsonPropertyName("humidity")]
        public AverageStats Humidity { get; set; } = new();

        [BsonElement("pressure")]
        [JsonPropertyName("pressure")]
        public AverageStats Pressure { get; set; } = new();

        [BsonElement("wind_speed")]
        [JsonPropertyName("wind_speed")]
        public WindStats WindSpeed { get; set; } = new();

        [BsonElement("lux")]
        [JsonPropertyName("lux")]
        public AverageStats Lux { get; set; } = new();

        [BsonElement("rain")]
        [JsonPropertyName("rain")]
        public RainStats Rain { get; set; } = new();

        [BsonElement("sample_count")]
        [JsonPropertyName("sample_count")]
        public int SampleCount { get; set; }
    }

    // Stats models for hourly and daily aggregated data
    [BsonIgnoreExtraElements]
    public class TemperatureStats
    {
        [BsonElement("avg")]
        [JsonPropertyName("avg")]
        public double Avg { get; set; }

        [BsonElement("min")]
        [JsonPropertyName("min")]
        public double Min { get; set; }

        [BsonElement("max")]
        [JsonPropertyName("max")]
        public double Max { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class AverageStats
    {
        [BsonElement("avg")]
        [JsonPropertyName("avg")]
        public double Avg { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class WindStats
    {
        [BsonElement("avg")]
        [JsonPropertyName("avg")]
        public double Avg { get; set; }

        [BsonElement("max")]
        [JsonPropertyName("max")]
        public double Max { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class RainStats
    {
        [BsonElement("sum")]
        [JsonPropertyName("sum")]
        public double Sum { get; set; }

        [BsonElement("max")]
        [JsonPropertyName("max")]
        public double Max { get; set; }
    }
}