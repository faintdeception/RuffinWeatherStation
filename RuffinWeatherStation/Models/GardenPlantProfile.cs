namespace RuffinWeatherStation.Models;

public class GardenPlantProfile
{
    public string PlantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double MinSoilTempF { get; set; }
    public int RequiredConsecutiveDaysAtOrAboveTemp { get; set; }
    public int DaysAfterLastFrostToTransplant { get; set; }
    public int DaysBeforeLastFrostToStartIndoors { get; set; }
    public string Notes { get; set; } = string.Empty;
}
