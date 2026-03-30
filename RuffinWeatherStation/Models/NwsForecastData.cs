namespace RuffinWeatherStation.Models;

public class NwsForecastData
{
    public string Location { get; set; } = "backyard";
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime? SnapshotFetchedAtUtc { get; set; }
    public bool HasData { get; set; }
    public List<NwsForecastPeriodData> Periods { get; set; } = new();
}

public class NwsForecastPeriodData
{
    public string Name { get; set; } = string.Empty;
    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public bool IsDaytime { get; set; }
    public double? Temperature { get; set; }
    public string TemperatureUnit { get; set; } = "F";
    public string WindSpeedText { get; set; } = string.Empty;
    public double? WindSpeedMphMax { get; set; }
    public string WindDirection { get; set; } = string.Empty;
    public double? PrecipitationChancePercent { get; set; }
    public string ShortForecast { get; set; } = string.Empty;
    public string DetailedForecast { get; set; } = string.Empty;
}
