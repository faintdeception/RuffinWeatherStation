using System;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Models
{
    public class TemperatureMeasurement
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestampNanoseconds")] 
        public long? TimestampNanoseconds { get; set; }

        [JsonPropertyName("timestampMs")]
        public DateTime TimestampMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? TimestampStr { get; set; }

        [JsonPropertyName("fields")]
        public WeatherFields? Fields { get; set; } = new();
        
        [JsonPropertyName("tags")]
        public WeatherTags? Tags { get; set; } = new();
    }

    public class WeatherFields
    {
        [JsonPropertyName("deviceTemperature")]
        public double DeviceTemperature { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonIgnore]
        public double TemperatureF => 32 + (Temperature * 9 / 5);

        [JsonPropertyName("humidity")]
        public double Humidity { get; set; }

        [JsonPropertyName("dewpoint")]
        public double Dewpoint { get; set; }

        [JsonIgnore]
        public double DewpointF => 32 + (Dewpoint * 9 / 5);

        [JsonPropertyName("lux")]
        public double Lux { get; set; }

        [JsonPropertyName("pressure")]
        public double Pressure { get; set; }

        [JsonPropertyName("windSpeed")]
        public double WindSpeed { get; set; }

        [JsonPropertyName("rain")]
        public double Rain { get; set; }

        [JsonPropertyName("windDirection")]
        public double WindDirection { get; set; }  // Changed from int to double

        [JsonPropertyName("windDirectionCardinal")]
        public string? WindDirectionCardinal { get; set; } = string.Empty;
    }

    public class WeatherTags
    {
        [JsonPropertyName("location")]
        public string? Location { get; set; } = string.Empty;

        [JsonPropertyName("sensorType")]
        public string? SensorType { get; set; } = string.Empty;
    }
}