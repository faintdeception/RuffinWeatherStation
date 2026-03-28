namespace RuffinWeatherStation.Models;

using System.Text.Json.Serialization;

public class GardenPlantProfile
{
    public string PlantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("requiredConsequtiveNights")]
    public int RequiredConsecutiveNights { get; set; }

    public int DaysAfterLastFrostToTransplant { get; set; }
    public int DaysBeforeLastFrostToStartIndoors { get; set; }
    public string Notes { get; set; } = string.Empty;
}
