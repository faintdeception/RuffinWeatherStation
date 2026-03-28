using RuffinWeatherStation.Models;
using System.Net.Http.Json;

namespace RuffinWeatherStation.Services;

public class GardenPlantProfileService
{
    private readonly HttpClient _httpClient;

    public GardenPlantProfileService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<GardenPlantProfile>> GetProfilesAsync()
    {
        try
        {
            var profiles = await _httpClient.GetFromJsonAsync<List<GardenPlantProfile>>("data/garden-plants.json");
            return profiles ?? new List<GardenPlantProfile>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading plant profiles: {ex.Message}");
            return new List<GardenPlantProfile>();
        }
    }
}
