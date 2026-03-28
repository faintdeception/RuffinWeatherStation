using RuffinWeatherStation.Models;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace RuffinWeatherStation.Services;

public class GardenPlantProfileService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;

    public GardenPlantProfileService(HttpClient httpClient, NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
    }

    public async Task<(List<GardenPlantProfile> Profiles, List<string> Warnings, string? ErrorMessage)> GetProfilesAsync()
    {
        var profilesUrl = new Uri(new Uri(_navigationManager.BaseUri), "data/garden-plants.json");

        try
        {
            var profiles = await _httpClient.GetFromJsonAsync<List<GardenPlantProfile>>(profilesUrl);
            var resolvedProfiles = profiles ?? new List<GardenPlantProfile>();
            var warnings = ValidateProfiles(resolvedProfiles);
            return (resolvedProfiles, warnings, null);
        }
        catch (Exception ex)
        {
            var message = $"Could not load plant profiles from {profilesUrl}. {ex.Message}";
            Console.Error.WriteLine($"Error loading plant profiles: {message}");
            return (new List<GardenPlantProfile>(), new List<string>(), message);
        }
    }

    private static List<string> ValidateProfiles(List<GardenPlantProfile> profiles)
    {
        var warnings = new List<string>();

        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var label = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.PlantId : profile.DisplayName;
            var prefix = $"Profile '{label}'";

            if (string.IsNullOrWhiteSpace(profile.PlantId))
            {
                warnings.Add($"Profile index {i} is missing plantId.");
            }

            if (profile.RequiredConsecutiveNights < 0)
            {
                warnings.Add($"{prefix} has negative requiredConsecutiveNights ({profile.RequiredConsecutiveNights}).");
            }

            if (profile.RequiredConsecutiveNights > 30)
            {
                warnings.Add($"{prefix} has unusually high requiredConsecutiveNights ({profile.RequiredConsecutiveNights}).");
            }

            if (profile.MinNightTempC is <= -40 or >= 49)
            {
                warnings.Add($"{prefix} has suspicious minNightTempC ({profile.MinNightTempC}).");
            }

            if (profile.DaysBeforeLastFrostToStartIndoors is < 0)
            {
                warnings.Add($"{prefix} has negative daysBeforeLastFrostToStartIndoors ({profile.DaysBeforeLastFrostToStartIndoors}).");
            }

            if (profile.DaysAfterLastFrostToTransplant is < -120 or > 180)
            {
                warnings.Add($"{prefix} has unusual daysAfterLastFrostToTransplant ({profile.DaysAfterLastFrostToTransplant}).");
            }

            var hasPrimaryWindowStart = !string.IsNullOrWhiteSpace(profile.WindowStartMonthDay);
            var hasPrimaryWindowEnd = !string.IsNullOrWhiteSpace(profile.WindowEndMonthDay);
            if (hasPrimaryWindowStart ^ hasPrimaryWindowEnd)
            {
                warnings.Add($"{prefix} has incomplete primary window; both windowStartMonthDay and windowEndMonthDay are required.");
            }

            var hasSecondaryWindowStart = !string.IsNullOrWhiteSpace(profile.SecondaryWindowStartMonthDay);
            var hasSecondaryWindowEnd = !string.IsNullOrWhiteSpace(profile.SecondaryWindowEndMonthDay);
            if (hasSecondaryWindowStart ^ hasSecondaryWindowEnd)
            {
                warnings.Add($"{prefix} has incomplete secondary window; both secondaryWindowStartMonthDay and secondaryWindowEndMonthDay are required.");
            }

            ValidateMonthDayPair(profile.WindowStartMonthDay, profile.WindowEndMonthDay, prefix, "primary", warnings);
            ValidateMonthDayPair(profile.SecondaryWindowStartMonthDay, profile.SecondaryWindowEndMonthDay, prefix, "secondary", warnings);
            ValidateMonthDayPair(profile.HarvestWindowStartMonthDay, profile.HarvestWindowEndMonthDay, prefix, "harvest", warnings);

            if (!string.IsNullOrWhiteSpace(profile.LatestPlantMonthDay) && !TryParseMonthDay(profile.LatestPlantMonthDay, out _))
            {
                warnings.Add($"{prefix} has invalid latestPlantMonthDay '{profile.LatestPlantMonthDay}'. Expected MM-dd.");
            }

            if (profile.SupportsSuccessionPlanting && !string.IsNullOrWhiteSpace(profile.WindowStartMonthDay) && !string.IsNullOrWhiteSpace(profile.WindowEndMonthDay))
            {
                warnings.Add($"{prefix} is marked supportsSuccessionPlanting=true but also has a fixed primary window. Verify this intent.");
            }

            var action = profile.ActionType?.Trim().ToLowerInvariant() ?? "plant";
            if (action is not ("plant" or "buy" or "harvest" or "prep"))
            {
                warnings.Add($"{prefix} has unrecognized actionType '{profile.ActionType}'.");
            }

            if (action == "harvest" && string.IsNullOrWhiteSpace(profile.HarvestWindowStartMonthDay) && string.IsNullOrWhiteSpace(profile.HarvestWindowEndMonthDay))
            {
                warnings.Add($"{prefix} uses actionType 'harvest' without a harvest window.");
            }
        }

        var duplicateIds = profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.PlantId))
            .GroupBy(p => p.PlantId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicateIds)
        {
            warnings.Add($"Duplicate plantId detected: '{duplicate}'.");
        }

        return warnings;
    }

    private static void ValidateMonthDayPair(string? startMonthDay, string? endMonthDay, string profileLabel, string windowLabel, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(startMonthDay) && string.IsNullOrWhiteSpace(endMonthDay))
        {
            return;
        }

        if (!TryParseMonthDay(startMonthDay, out _))
        {
            warnings.Add($"{profileLabel} has invalid {windowLabel} window start '{startMonthDay}'. Expected MM-dd.");
        }

        if (!TryParseMonthDay(endMonthDay, out _))
        {
            warnings.Add($"{profileLabel} has invalid {windowLabel} window end '{endMonthDay}'. Expected MM-dd.");
        }
    }

    private static bool TryParseMonthDay(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateOnly.TryParseExact($"2026-{value}", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }
}
