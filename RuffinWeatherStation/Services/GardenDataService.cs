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

    public async Task<NwsAlertSummaryData?> GetAlertsSummaryAsync(int days = 7, string? location = null)
    {
        try
        {
            var queryParts = new List<string> { $"days={days}" };
            if (!string.IsNullOrWhiteSpace(location))
            {
                queryParts.Add($"location={Uri.EscapeDataString(location)}");
            }

            var query = string.Join("&", queryParts);
            return await _httpClient.GetFromJsonAsync<NwsAlertSummaryData>($"api/garden/alerts-summary?{query}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading NWS alert summary: {ex.Message}");
            return null;
        }
    }

    public async Task<NwsForecastData?> GetForecastSummaryAsync(int maxPeriods = 12, string? location = null)
    {
        try
        {
            var queryParts = new List<string> { $"maxPeriods={maxPeriods}" };
            if (!string.IsNullOrWhiteSpace(location))
            {
                queryParts.Add($"location={Uri.EscapeDataString(location)}");
            }

            var query = string.Join("&", queryParts);
            return await _httpClient.GetFromJsonAsync<NwsForecastData>($"api/garden/forecast-summary?{query}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading NWS forecast summary: {ex.Message}");
            return null;
        }
    }
}
