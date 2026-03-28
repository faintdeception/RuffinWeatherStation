using RuffinWeatherStation.Models;
using System.Net.Http.Json;

namespace RuffinWeatherStation.Services;

public class GardenDataService
{
    private readonly HttpClient _httpClient;

    public GardenDataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GardenReferenceData?> GetGardenReferenceAsync(int? year = null)
    {
        try
        {
            var query = year.HasValue ? $"?year={year.Value}" : string.Empty;
            return await _httpClient.GetFromJsonAsync<GardenReferenceData>($"api/garden/reference{query}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading garden reference data: {ex.Message}");
            return null;
        }
    }
}
