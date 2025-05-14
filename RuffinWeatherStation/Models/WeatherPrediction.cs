using System;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Models
{
    public class WeatherPrediction
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("prediction_12h")]
        public PredictionData Prediction12h { get; set; }

        [JsonPropertyName("prediction_24h")]
        public PredictionData Prediction24h { get; set; }

        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
        
        // Returns the confidence as a percentage
        public string ConfidencePercentage => $"{Confidence * 100:0}%";
        
        // Format the creation date for display
        [JsonIgnore]
        public DateTime CreatedAtDateTime 
        { 
            get;
        }
    }

    public class PredictionData
    {
        [JsonPropertyName("temperature")]
        public MinMaxValue Temperature { get; set; }

        [JsonPropertyName("humidity")]
        public MinMaxValue Humidity { get; set; }

        [JsonPropertyName("pressure")]
        public MinMaxValue Pressure { get; set; }

        [JsonPropertyName("wind_speed")]
        public MinMaxValue WindSpeed { get; set; }
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