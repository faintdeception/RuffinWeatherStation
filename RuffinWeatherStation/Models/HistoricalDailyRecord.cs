using System;
using System.Text.Json.Serialization;

namespace RuffinWeatherStation.Models
{
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

    public class HistoricalDailyExtreme
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("day_timestamp")]
        public long? DayTimestamp { get; set; }
    }
}