using System;
using System.Text.Json.Serialization;
using RuffinWeatherStation.Models.JsonConverters;

namespace RuffinWeatherStation.Models
{
    public class WeatherPrediction
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        [JsonConverter(typeof(DateTimeConverter))]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("prediction_12h")]
        public PredictionData Prediction12h { get; set; } = new();

        [JsonPropertyName("prediction_24h")]
        public PredictionData Prediction24h { get; set; } = new();

        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
        
        // Returns the confidence as a percentage
        public string ConfidencePercentage => $"{Confidence * 100:0}%";
        
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }

        public static WeatherPrediction CreateError(string errorMessage)
        {
            return new WeatherPrediction
            {
                HasError = true,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    public class PredictionData
    {
        [JsonPropertyName("temperature")]
        public MinMaxValue Temperature { get; set; } = new();

        [JsonPropertyName("humidity")]
        public MinMaxValue Humidity { get; set; } = new();

        [JsonPropertyName("pressure")]
        public MinMaxValue Pressure { get; set; } = new();

        [JsonPropertyName("wind_speed")]
        public MinMaxValue WindSpeed { get; set; } = new();
    }

    public class MinMaxValue
    {
        [JsonPropertyName("min")]
        public double Min { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }
        
        [JsonPropertyName("avg")]
        public double Avg { get; set; }
        
        // Helper property to get the average if the Avg property isn't set
        [JsonIgnore]
        public double Average => Avg > 0 ? Avg : (Min + Max) / 2;
        
        // Format the range as a string
        public string Range => $"{Min:0.0} - {Max:0.0}";
    }
}