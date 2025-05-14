using RuffinWeatherStation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.JSInterop;

namespace RuffinWeatherStation.Services
{
    public class TemperatureService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        
        // Cache dictionaries to store API responses
        private Dictionary<string, (object Data, DateTime Timestamp)> _memoryCache = new();
        
        // Cache expiration times (in minutes)
        private const int LATEST_CACHE_MINUTES = 15;
        private const int RECENT_CACHE_MINUTES = 30;
        private const int DAILY_CACHE_MINUTES = 60;
        private const int HOURLY_CACHE_MINUTES = 45;
        private const int PREDICTION_CACHE_MINUTES = 60;

        public TemperatureService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        // Helper methods for caching
        private async Task<T?> GetCachedDataAsync<T>(string cacheKey, int cacheMinutes, Func<Task<T?>> fetchFunction)
        {
            // First try memory cache
            if (_memoryCache.TryGetValue(cacheKey, out var cachedData) && 
                (DateTime.Now - cachedData.Timestamp).TotalMinutes < cacheMinutes)
            {
                Console.WriteLine($"Cache hit for {cacheKey} - returning memory cached data");
                return (T)cachedData.Data;
            }
            
            // Then try local storage
            try 
            {
                var storedData = await LoadFromLocalStorageAsync<CacheEntry<T>>(cacheKey);
                if (storedData != null && 
                    (DateTime.Now - storedData.Timestamp).TotalMinutes < cacheMinutes)
                {
                    Console.WriteLine($"Cache hit for {cacheKey} - returning localStorage data");
                    
                    // Update memory cache
                    _memoryCache[cacheKey] = (storedData.Data, storedData.Timestamp);
                    
                    return storedData.Data;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading from localStorage: {ex.Message}");
                // Continue to fetch from API if localStorage fails
            }
            
            // If not in cache or expired, fetch new data
            Console.WriteLine($"Cache miss for {cacheKey} - fetching from API");
            var data = await fetchFunction();
            
            if (data != null)
            {
                // Update memory cache
                _memoryCache[cacheKey] = (data, DateTime.Now);
                
                // Update localStorage cache
                try 
                {
                    await SaveToLocalStorageAsync(cacheKey, new CacheEntry<T> 
                    { 
                        Data = data, 
                        Timestamp = DateTime.Now 
                    });
                }
                catch (Exception ex) 
                {
                    Console.Error.WriteLine($"Error writing to localStorage: {ex.Message}");
                    // Continue even if localStorage fails
                }
            }
            
            return data;
        }
        
        private async Task SaveToLocalStorageAsync<T>(string key, T data)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, 
                JsonSerializer.Serialize(data));
        }

        private async Task<T?> LoadFromLocalStorageAsync<T>(string key) where T : class
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            return json == null ? null : JsonSerializer.Deserialize<T>(json);
        }

        // Local cache class
        private class CacheEntry<T>
        {
            public T Data { get; set; } = default!;
            public DateTime Timestamp { get; set; }
        }

        // Updated methods with caching
        public async Task<TemperatureMeasurement?> GetLatestMeasurementAsync()
        {
            return await GetCachedDataAsync<TemperatureMeasurement>(
                "latest_measurement", 
                LATEST_CACHE_MINUTES,
                async () => {
                    try
                    {
                        return await _httpClient.GetFromJsonAsync<TemperatureMeasurement>("api/weather/latest");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching latest measurement: {ex.Message}");
                        return null;
                    }
                });
        }

        public async Task<List<TemperatureMeasurement>?> GetRecentMeasurementsAsync(int count = 25)
        {
            return await GetCachedDataAsync<List<TemperatureMeasurement>>(
                $"recent_measurements_{count}", 
                RECENT_CACHE_MINUTES,
                async () => {
                    try
                    {
                        return await _httpClient.GetFromJsonAsync<List<TemperatureMeasurement>>($"api/weather/recent?count={count}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching recent measurements: {ex.Message}");
                        return null;
                    }
                });
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

        public async Task<List<HourlyMeasurement>?> GetHourlyMeasurementsAsync(int days = 1)
        {
            return await GetCachedDataAsync<List<HourlyMeasurement>>(
                $"hourly_measurements_{days}", 
                HOURLY_CACHE_MINUTES,
                async () => {
                    try
                    {
                        DateTime startDate = DateTime.UtcNow.AddDays(-days);
                        string formattedDate = startDate.ToString("yyyy-MM-dd");
                        
                        return await _httpClient.GetFromJsonAsync<List<HourlyMeasurement>>($"api/weather/hourly?startDate={formattedDate}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching hourly measurements: {ex.Message}");
                        return null;
                    }
                });
        }

        public async Task<List<DailyMeasurement>?> GetDailyMeasurementsAsync(int days = 7)
        {
            return await GetCachedDataAsync<List<DailyMeasurement>>(
                $"daily_measurements_{days}", 
                DAILY_CACHE_MINUTES,
                async () => {
                    try
                    {
                        DateTime startDate = DateTime.UtcNow.AddDays(-days);
                        string formattedDate = startDate.ToString("yyyy-MM-dd");
                        
                        return await _httpClient.GetFromJsonAsync<List<DailyMeasurement>>($"api/weather/daily?startDate={formattedDate}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching daily measurements: {ex.Message}");
                        return null;
                    }
                });
        }

        public async Task<WeatherAnalysisResult> AnalyzeWeatherTrendsAsync(int days = 7)
        {
            var result = new WeatherAnalysisResult();
            
            try
            {
                // Get daily and hourly measurements
                var dailyData = await GetDailyMeasurementsAsync(days);
                var hourlyData = await GetHourlyMeasurementsAsync(days);
                
                if (dailyData != null && dailyData.Any())
                {
                    // Calculate temperature trends
                    result.HighestTemperature = dailyData.Max(d => d.Fields.Temperature.Max);
                    result.LowestTemperature = dailyData.Min(d => d.Fields.Temperature.Min);
                    result.AverageTemperature = dailyData.Average(d => d.Fields.Temperature.Avg);
                    
                    // Calculate pressure trends
                    result.HighestPressure = dailyData.Max(d => d.Fields.Pressure.Avg);
                    result.LowestPressure = dailyData.Min(d => d.Fields.Pressure.Avg);
                    result.AveragePressure = dailyData.Average(d => d.Fields.Pressure.Avg);
                    
                    // Calculate rainfall totals
                    result.TotalRainfall = dailyData.Sum(d => d.Fields.Rain.Sum);
                    result.RainyDaysCount = dailyData.Count(d => d.Fields.Rain.Sum > 0.1);
                    
                    // Set the analysis period
                    result.StartDate = dailyData.Min(d => d.TimestampMs);
                    result.EndDate = dailyData.Max(d => d.TimestampMs);
                    
                    // Calculate trends - is temperature rising or falling?
                    var firstDayAvg = dailyData.OrderBy(d => d.TimestampMs).First().Fields.Temperature.Avg;
                    var lastDayAvg = dailyData.OrderBy(d => d.TimestampMs).Last().Fields.Temperature.Avg;
                    result.TemperatureTrend = lastDayAvg - firstDayAvg;
                    
                    // Calculate pressure trend
                    var firstDayPressure = dailyData.OrderBy(d => d.TimestampMs).First().Fields.Pressure.Avg;
                    var lastDayPressure = dailyData.OrderBy(d => d.TimestampMs).Last().Fields.Pressure.Avg;
                    result.PressureTrend = lastDayPressure - firstDayPressure;
                }
                
                if (hourlyData != null && hourlyData.Any())
                {
                    // Extract hourly data points for charts
                    result.HourlyTemperatures = hourlyData
                        .OrderBy(h => h.TimestampMs)
                        .Select(h => new TemperatureDataPoint { 
                            Timestamp = h.TimestampMs, 
                            Temperature = h.Fields.Temperature.Avg,
                            Min = h.Fields.Temperature.Min,
                            Max = h.Fields.Temperature.Max
                        })
                        .ToList();
                        
                    // Extract pressure data
                    result.HourlyPressures = hourlyData
                        .OrderBy(h => h.TimestampMs)
                        .Select(h => new DataPoint { 
                            Timestamp = h.TimestampMs, 
                            Value = h.Fields.Pressure.Avg
                        })
                        .ToList();
                }
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error analyzing weather trends: {ex.Message}");
                result.ErrorMessage = ex.Message;
                result.Success = false;
            }
            
            return result;
        }

        public async Task<WeatherAnalysisResult> AnalyzeRecentMeasurementsAsync(int hours = 1)
        {
            var result = new WeatherAnalysisResult();
            
            try
            {
                // Get more recent measurements than we need to ensure we have enough data
                // We'll request at least 50 measurements, assuming measurements are taken every 2 minutes
                // That gives us approximately ~1.5 hours of data for 2-minute readings
                int sampleCount = Math.Max(50, hours * 30); // For longer periods, request more data
                
                var recentMeasurements = await GetRecentMeasurementsAsync(sampleCount);
                
                if (recentMeasurements == null || !recentMeasurements.Any())
                {
                    result.Success = false;
                    result.ErrorMessage = "No recent measurements available";
                    return result;
                }
                
                // Calculate the time threshold based on requested hours
                DateTime timeThreshold = DateTime.Now.AddHours(-hours);
                
                // Filter measurements to get only those within the time window
                var filteredMeasurements = recentMeasurements
                    .Where(m => m.TimestampMs >= timeThreshold)
                    .OrderBy(m => m.TimestampMs)
                    .ToList();
                
                if (!filteredMeasurements.Any())
                {
                    result.Success = false;
                    result.ErrorMessage = $"No measurements found in the last {hours} hour(s)";
                    return result;
                }
                
                // Calculate temperature stats
                result.HighestTemperature = filteredMeasurements.Max(m => m.Fields?.Temperature ?? 0);
                result.LowestTemperature = filteredMeasurements.Min(m => m.Fields?.Temperature ?? 0);
                result.AverageTemperature = filteredMeasurements.Average(m => m.Fields?.Temperature ?? 0);
                
                // Calculate pressure stats
                result.HighestPressure = filteredMeasurements.Max(m => m.Fields?.Pressure ?? 0);
                result.LowestPressure = filteredMeasurements.Min(m => m.Fields?.Pressure ?? 0);
                result.AveragePressure = filteredMeasurements.Average(m => m.Fields?.Pressure ?? 0);
                
                // Calculate rainfall
                result.TotalRainfall = filteredMeasurements.Sum(m => m.Fields?.Rain ?? 0);
                
                // Set analysis period
                result.StartDate = filteredMeasurements.First().TimestampMs;
                result.EndDate = filteredMeasurements.Last().TimestampMs;
                
                // Calculate trends (is temperature rising or falling in this short period?)
                // We'll use a simple comparison of first vs last reading
                var firstTemp = filteredMeasurements.First().Fields?.Temperature ?? 0;
                var lastTemp = filteredMeasurements.Last().Fields?.Temperature ?? 0;
                result.TemperatureTrend = lastTemp - firstTemp;
                
                // Pressure trend
                var firstPressure = filteredMeasurements.First().Fields?.Pressure ?? 0;
                var lastPressure = filteredMeasurements.Last().Fields?.Pressure ?? 0;
                result.PressureTrend = lastPressure - firstPressure;
                
                // Prepare chart data
                result.HourlyTemperatures = new List<TemperatureDataPoint>();
                result.HourlyPressures = new List<DataPoint>();
                
                foreach (var measurement in filteredMeasurements)
                {
                    // Add temperature data point
                    result.HourlyTemperatures.Add(new TemperatureDataPoint
                    {
                        Timestamp = measurement.TimestampMs,
                        Temperature = measurement.Fields?.Temperature ?? 0,
                        // For raw measurements we don't have min/max, so set them equal to actual value
                        Min = measurement.Fields?.Temperature ?? 0,
                        Max = measurement.Fields?.Temperature ?? 0
                    });
                    
                    // Add pressure data point
                    result.HourlyPressures.Add(new DataPoint
                    {
                        Timestamp = measurement.TimestampMs,
                        Value = measurement.Fields?.Pressure ?? 0
                    });
                }
                
                // Calculate sample rate for display
                TimeSpan timeSpan = result.EndDate - result.StartDate;
                if (filteredMeasurements.Count > 1 && timeSpan.TotalMinutes > 0)
                {
                    double avgMinutesBetweenSamples = timeSpan.TotalMinutes / (filteredMeasurements.Count - 1);
                    result.SampleRate = Math.Round(avgMinutesBetweenSamples, 1);
                }
                
                result.MeasurementCount = filteredMeasurements.Count;
                result.Success = true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error analyzing recent measurements: {ex.Message}");
                result.ErrorMessage = ex.Message;
                result.Success = false;
            }
            
            return result;
        }

        public async Task<(TemperatureMeasurement? Measurement, bool NotFound, string? ErrorMessage)> GetTemperatureMeasurementByDate(DateTime date)
        {
            try
            {
                // Format the date in the required format for the API
                string formattedDate = date.ToString("yyyy-MM-dd");
                
                // Use HttpClient directly instead of GetFromJsonAsync to handle 404 properly
                var response = await _httpClient.GetAsync($"api/weather/date/{formattedDate}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 - No data for this date
                    return (null, true, null);
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    return (null, false, $"API error: {response.StatusCode}");
                }
                
                var measurement = await response.Content.ReadFromJsonAsync<TemperatureMeasurement>();
                return (measurement, false, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching measurement for date {date:yyyy-MM-dd}: {ex.Message}");
                return (null, false, ex.Message);
            }
        }

        public async Task<WeatherAnalysisResult> GetRecentAnalysisAsync(int hours)
        {
            // Call the existing method that already implements this functionality
            return await AnalyzeRecentMeasurementsAsync(hours);
        }

        public async Task<WeatherAnalysisResult> GetAnalysisAsync(int days)
        {
            // Call the existing method that already implements this functionality
            return await AnalyzeWeatherTrendsAsync(days);
        }

        public async Task<WeatherPrediction?> GetLatestPredictionAsync()
        {
            return await GetCachedDataAsync<WeatherPrediction>(
                "latest_prediction", 
                PREDICTION_CACHE_MINUTES,
                async () => {
                    try
                    {
                        return await _httpClient.GetFromJsonAsync<WeatherPrediction>("api/weather/prediction/latest");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching latest prediction: {ex.Message}");
                        return null;
                    }
                });
        }
    }

    // New model classes for analysis
    public class WeatherAnalysisResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Temperature stats
        public double HighestTemperature { get; set; }
        public double LowestTemperature { get; set; }
        public double AverageTemperature { get; set; }
        public double TemperatureTrend { get; set; } // Positive means warming, negative means cooling
        
        // Pressure stats
        public double HighestPressure { get; set; }
        public double LowestPressure { get; set; }
        public double AveragePressure { get; set; }
        public double PressureTrend { get; set; } // Positive means rising, negative means falling
        
        // Rainfall stats
        public double TotalRainfall { get; set; }
        public int RainyDaysCount { get; set; }
        
        // Analysis period
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        // Chart data
        public List<TemperatureDataPoint>? HourlyTemperatures { get; set; }
        public List<DataPoint>? HourlyPressures { get; set; }
        
        // Additional properties for short-term analysis
        public double SampleRate { get; set; }  // Average minutes between samples
        public int MeasurementCount { get; set; }
    }

    public class TemperatureDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Temperature { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        
        // C to F conversions
        public double TemperatureF => 32 + (Temperature * 9 / 5);
        public double MinF => 32 + (Min * 9 / 5);
        public double MaxF => 32 + (Max * 9 / 5);
    }

    public class DataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }
}