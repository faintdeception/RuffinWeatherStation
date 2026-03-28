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

    public async Task<(List<GardenPlantProfile> Profiles, string? ErrorMessage)> GetProfilesAsync()
    {
        var profilesUrl = new Uri(new Uri(_navigationManager.BaseUri), "data/garden-plants.json");

        try
        {
            var profiles = await _httpClient.GetFromJsonAsync<List<GardenPlantProfile>>(profilesUrl);
            return (profiles ?? new List<GardenPlantProfile>(), null);
        }
        catch (Exception ex)
        {
            var message = $"Could not load plant profiles from {profilesUrl}. {ex.Message}";
            Console.Error.WriteLine($"Error loading plant profiles: {message}");
            return (new List<GardenPlantProfile>(), message);
        }
    }
}
