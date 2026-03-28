namespace RuffinWeatherStation.Models;

using System.Text.Json.Serialization;

public class GardenPlantProfile
{
    public string PlantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public string ActionType { get; set; } = "plant";
    public string? WindowStartMonthDay { get; set; }
    public string? WindowEndMonthDay { get; set; }
    public int? LeadDays { get; set; }
    public string? HarvestWindowStartMonthDay { get; set; }
    public string? HarvestWindowEndMonthDay { get; set; }
    public int? HarvestLeadDays { get; set; }

    [JsonPropertyName("minNightTempF")]
    public double? MinNightTempF { get; set; }

    [JsonPropertyName("requiredConsecutiveNights")]
    public int RequiredConsecutiveNights { get; set; }

    [JsonPropertyName("requiredConsequtiveNights")]
    public int RequiredConsecutiveNightsLegacy
    {
        set => RequiredConsecutiveNights = value;
    }

    public int? DaysAfterLastFrostToTransplant { get; set; }
    public int? DaysBeforeLastFrostToStartIndoors { get; set; }
    public string Notes { get; set; } = string.Empty;
}
