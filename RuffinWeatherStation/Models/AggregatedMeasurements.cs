using System;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Models
{
    public class HourlyMeasurement
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("timestamp_ms")]
        public DateTime TimestampMs { get; set; }

        [JsonPropertyName("hour_timestamp")]
        public long? HourTimestamp { get; set; }

        [JsonPropertyName("fields")]
        public HourlyFields Fields { get; set; } = new();
        
        [JsonPropertyName("tags")]
        public WeatherTags? Tags { get; set; } = new();
    }

    public class HourlyFields
    {
        [JsonPropertyName("temperature")]
        public TemperatureStats Temperature { get; set; } = new();

        [JsonPropertyName("humidity")]
        public AverageStats Humidity { get; set; } = new();

        [JsonPropertyName("pressure")]
        public AverageStats Pressure { get; set; } = new();

        [JsonPropertyName("wind_speed")]
        public WindStats WindSpeed { get; set; } = new();

        [JsonPropertyName("lux")]
        public AverageStats Lux { get; set; } = new();

        [JsonPropertyName("sample_count")]
        public int SampleCount { get; set; }
    }

    public class DailyMeasurement
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("timestamp_ms")]
        public DateTime TimestampMs { get; set; }

        [JsonPropertyName("day_timestamp")]
        public long? DayTimestamp { get; set; }

        [JsonPropertyName("fields")]
        public DailyFields Fields { get; set; } = new();
        
        [JsonPropertyName("tags")]
        public WeatherTags? Tags { get; set; } = new();
    }

    public class DailyFields
    {
        [JsonPropertyName("temperature")]
        public TemperatureStats Temperature { get; set; } = new();

        [JsonPropertyName("humidity")]
        public AverageStats Humidity { get; set; } = new();

        [JsonPropertyName("pressure")]
        public AverageStats Pressure { get; set; } = new();

        [JsonPropertyName("wind_speed")]
        public WindStats WindSpeed { get; set; } = new();

        [JsonPropertyName("lux")]
        public AverageStats Lux { get; set; } = new();

        [JsonPropertyName("rain")]
        public RainStats Rain { get; set; } = new();

        [JsonPropertyName("sample_count")]
        public int SampleCount { get; set; }
    }

    // Stats models for hourly and daily aggregated data
    public class TemperatureStats
    {
        [JsonPropertyName("avg")]
        public double Avg { get; set; }

        [JsonPropertyName("min")]
        public double Min { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }
        
        // Fahrenheit conversion properties
        [JsonIgnore]
        public double AvgF => 32 + (Avg * 9 / 5);
        
        [JsonIgnore]
        public double MinF => 32 + (Min * 9 / 5);
        
        [JsonIgnore]
        public double MaxF => 32 + (Max * 9 / 5);
    }

    public class AverageStats
    {
        [JsonPropertyName("avg")]
        public double Avg { get; set; }
    }

    public class WindStats
    {
        [JsonPropertyName("avg")]
        public double Avg { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }
    }

    public class RainStats
    {
        [JsonPropertyName("sum")]
        public double Sum { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }
    }
}