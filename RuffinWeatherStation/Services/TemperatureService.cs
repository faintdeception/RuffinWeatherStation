using RuffinWeatherStation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace RuffinWeatherStation.Services
{
    public class TemperatureService
    {
        private readonly HttpClient _httpClient;
        // Removed hardcoded API URL - will use HttpClient's BaseAddress instead

        public TemperatureService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<TemperatureMeasurement?> GetLatestMeasurementAsync()
        {
            try
            {
                // Use relative URL - HttpClient.BaseAddress will be prepended
                return await _httpClient.GetFromJsonAsync<TemperatureMeasurement>("api/weather/latest");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching latest measurement: {ex.Message}");
                return null;
            }
        }

        public async Task<List<TemperatureMeasurement>?> GetRecentMeasurementsAsync(int count = 25)
        {
            try
            {
                // Use relative URL - HttpClient.BaseAddress will be prepended
                return await _httpClient.GetFromJsonAsync<List<TemperatureMeasurement>>($"api/weather/recent?count={count}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching recent measurements: {ex.Message}");
                return null;
            }
        }
        
        public async Task<List<TemperatureMeasurement>?> GetTodaysMeasurementsAsync()
        {
            try
            {
                // Get today's date in local time
                DateTime today = DateTime.Now.Date;
                
                // For now, we'll use the recent endpoint with a larger count
                // and filter client-side to get today's data
                var recentMeasurements = await GetRecentMeasurementsAsync(100);
                
                if (recentMeasurements == null)
                    return null;
                    
                // Filter measurements to get only those from today
                return recentMeasurements
                    .Where(m => m.TimestampMs.Date == today)
                    .OrderBy(m => m.TimestampMs)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching today's measurements: {ex.Message}");
                return null;
            }
        }
    }
}