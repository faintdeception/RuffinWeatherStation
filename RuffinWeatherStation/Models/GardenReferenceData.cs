namespace RuffinWeatherStation.Models;

public class GardenReferenceData
{
    public int Year { get; set; }
    public string LocationLabel { get; set; } = string.Empty;
    public string AverageLastFrostMonthDay { get; set; } = string.Empty;
    public DateOnly AverageLastFrostDate { get; set; }
    public GardenSeasonStartDates SeasonStarts { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}

public class GardenSeasonStartDates
{
    public DateOnly Spring { get; set; }
    public DateOnly Summer { get; set; }
    public DateOnly Fall { get; set; }
    public DateOnly Winter { get; set; }
}
