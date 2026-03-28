namespace RuffinWeatherStation.Models;

public class TodayRainfallDataPoint
{
    public DateTime Timestamp { get; set; }
    public double RainIncrement { get; set; }
    public double AccumulatedRain { get; set; }
}
