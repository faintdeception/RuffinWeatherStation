using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Api.Models
{
    public class WeatherPrediction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("date")]
        public string Date { get; set; }

        [BsonElement("location")]
        public string Location { get; set; }

        [BsonElement("created_at")]
        public string CreatedAt { get; set; }

        [BsonElement("prediction_12h")]
        public PredictionData Prediction12h { get; set; }

        [BsonElement("prediction_24h")]
        public PredictionData Prediction24h { get; set; }

        [BsonElement("reasoning")]
        public string Reasoning { get; set; }

        [BsonElement("confidence")]
        public double Confidence { get; set; }
    }

    public class PredictionData
    {
        [BsonElement("temperature")]
        public MinMaxValue Temperature { get; set; }

        [BsonElement("humidity")]
        public MinMaxValue Humidity { get; set; }

        [BsonElement("pressure")]
        public MinMaxValue Pressure { get; set; }

        [BsonElement("wind_speed")]
        public MinMaxValue WindSpeed { get; set; }
    }

    public class MinMaxValue
    {
        [BsonElement("min")]
        public double Min { get; set; }

        [BsonElement("max")]
        public double Max { get; set; }
    }
}