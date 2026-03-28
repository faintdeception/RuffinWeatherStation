using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Api.Models
{
    public class HistoricalDailyRecord
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [BsonElement("month_day")]
        [JsonPropertyName("month_day")]
        public string MonthDay { get; set; } = string.Empty;

        [BsonElement("location")]
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [BsonElement("high")]
        [JsonPropertyName("high")]
        public HistoricalDailyExtreme? High { get; set; }

        [BsonElement("low")]
        [JsonPropertyName("low")]
        public HistoricalDailyExtreme? Low { get; set; }

        [BsonElement("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class HistoricalDailyExtreme
    {
        [BsonElement("value")]
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [BsonElement("date")]
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [BsonElement("day_timestamp")]
        [JsonPropertyName("day_timestamp")]
        public long? DayTimestamp { get; set; }
    }

    public class HistoricalDailyRecordResponse
    {
        [JsonPropertyName("hasData")]
        public bool HasData { get; set; }

        [JsonPropertyName("requestedDate")]
        public string RequestedDate { get; set; } = string.Empty;

        [JsonPropertyName("month_day")]
        public string MonthDay { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("high")]
        public HistoricalDailyExtreme? High { get; set; }

        [JsonPropertyName("low")]
        public HistoricalDailyExtreme? Low { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}