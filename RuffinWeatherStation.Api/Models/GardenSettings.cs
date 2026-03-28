namespace RuffinWeatherStation.Api.Models;

public class GardenSettings
{
    // Month/day format allows stable env var overrides regardless of year.
    public string AverageLastFrostMonthDay { get; set; } = "03-30";
    public string LocationLabel { get; set; } = "backyard";
}
