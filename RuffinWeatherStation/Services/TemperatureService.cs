using RuffinWeatherStation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.JSInterop;
using RuffinWeatherStation.Models.JsonConverters;

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
        private const int HISTORICAL_DAILY_CACHE_MINUTES = 60;
        private const int ALL_TIME_RECORDS_CACHE_MINUTES = 120;
        private const int MAX_LOCAL_STORAGE_JSON_LENGTH = 350_000;
        private const int MAX_RECENT_MEASUREMENTS_PERSIST_COUNT = 250;
        private bool _recentMeasurementsCacheCleanupAttempted;

        public TemperatureService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        // Helper methods for caching
        private async Task<T?> GetCachedDataAsync<T>(
            string cacheKey,
            int cacheMinutes,
            Func<Task<T?>> fetchFunction,
            bool allowPersistentCache = true)
        {
            // First try memory cache
            if (_memoryCache.TryGetValue(cacheKey, out var cachedData) && 
                (DateTime.Now - cachedData.Timestamp).TotalMinutes < cacheMinutes)
            {
                Console.WriteLine($"Cache hit for {cacheKey} - returning memory cached data");
                return (T)cachedData.Data;
            }
            
            // Then try local storage
            if (allowPersistentCache)
            {
                try 
                {
                    var storedData = await LoadFromLocalStorageAsync<CacheEntry<T>>(cacheKey);
                    if (storedData != null && 
                        storedData.Data != null &&
                        (DateTime.Now - storedData.Timestamp).TotalMinutes < cacheMinutes)
                    {
                        Console.WriteLine($"Cache hit for {cacheKey} - returning localStorage data");
                        
                        // Update memory cache
                        _memoryCache[cacheKey] = (storedData.Data!, storedData.Timestamp);
                        
                        return storedData.Data;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading from localStorage: {ex.Message}");
                    // Continue to fetch from API if localStorage fails
                }
            }
            
            // If not in cache or expired, fetch new data
            Console.WriteLine($"Cache miss for {cacheKey} - fetching from API");
            var data = await fetchFunction();
            
            if (data != null)
            {
                // Update memory cache
                _memoryCache[cacheKey] = (data, DateTime.Now);
                
                // Update localStorage cache
                if (allowPersistentCache)
                {
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
            }
            
            return data;
        }
        
        private async Task SaveToLocalStorageAsync<T>(string key, T data)
        {
            var serialized = JsonSerializer.Serialize(data);

            // Avoid pushing very large blobs into localStorage; browsers enforce small per-origin quotas.
            if (serialized.Length > MAX_LOCAL_STORAGE_JSON_LENGTH)
            {
                Console.WriteLine($"Skipping localStorage for {key}: payload size {serialized.Length} exceeds threshold {MAX_LOCAL_STORAGE_JSON_LENGTH}.");
                return;
            }

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, serialized);
        }

        private async Task<T?> LoadFromLocalStorageAsync<T>(string key) where T : class
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            return json == null ? null : JsonSerializer.Deserialize<T>(json);
        }

        private async Task EnsureRecentMeasurementsCacheCleanupAsync()
        {
            if (_recentMeasurementsCacheCleanupAttempted)
            {
                return;
            }

            _recentMeasurementsCacheCleanupAttempted = true;

            try
            {
                var removedCount = await _jsRuntime.InvokeAsync<int>(
                    "ruffinWeatherStorage.cleanupRecentMeasurementsCache",
                    MAX_RECENT_MEASUREMENTS_PERSIST_COUNT,
                    MAX_LOCAL_STORAGE_JSON_LENGTH);

                if (removedCount > 0)
                {
                    Console.WriteLine($"Cleaned up {removedCount} stale recent measurement cache entries from localStorage.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error cleaning recent measurement cache: {ex.Message}");
            }
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

        public async Task<List<TemperatureMeasurement>?> GetRecentMeasurementsAsync(int count = 25, DateTime? sinceUtc = null)
        {
            await EnsureRecentMeasurementsCacheCleanupAsync();

            var allowPersistentCache = !sinceUtc.HasValue && count <= MAX_RECENT_MEASUREMENTS_PERSIST_COUNT;

            return await GetCachedDataAsync<List<TemperatureMeasurement>>(
                $"recent_measurements_{count}_{sinceUtc?.ToUniversalTime().ToString("yyyyMMddHHmm") ?? "none"}", 
                RECENT_CACHE_MINUTES,
                async () => {
                    try
                    {
                        var endpoint = $"api/weather/recent?count={count}";
                        if (sinceUtc.HasValue)
                        {
                            endpoint += $"&sinceUtc={Uri.EscapeDataString(sinceUtc.Value.ToUniversalTime().ToString("o"))}";
                        }

                        return await _httpClient.GetFromJsonAsync<List<TemperatureMeasurement>>(endpoint);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching recent measurements: {ex.Message}");
                        return null;
                    }
                },
                allowPersistentCache);
        }
        
        public async Task<List<TemperatureMeasurement>?> GetTodaysMeasurementsAsync()
        {
            try
            {
                var todayUtc = DateTime.UtcNow.Date;

                // Request a generous cap and let the API filter by UTC day start.
                var recentMeasurements = await GetRecentMeasurementsAsync(2000, todayUtc);
                
                if (recentMeasurements == null)
                    return null;
                    
                // Re-apply filter client-side as a safety net and normalize ordering.
                return recentMeasurements
                    .Where(m => m.TimestampMs >= todayUtc)
                    .OrderBy(m => m.TimestampMs)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching today's measurements: {ex.Message}");
                return null;
            }
        }

        public async Task<List<DataPoint>> GetRecentLightLevelsAsync(int hours = 1)
        {
            try
            {
                int normalizedHours = Math.Clamp(hours, 1, 24);
                int sampleCount = Math.Max(50, normalizedHours * 30);
                DateTime sinceUtc = DateTime.UtcNow.AddHours(-normalizedHours);

                var recentMeasurements = await GetRecentMeasurementsAsync(sampleCount, sinceUtc);
                if (recentMeasurements == null || !recentMeasurements.Any())
                {
                    return new List<DataPoint>();
                }

                return recentMeasurements
                    .Where(m => m.TimestampMs >= sinceUtc)
                    .Where(m => m.Fields != null)
                    .GroupBy(m => m.TimestampMs)
                    .OrderBy(g => g.Key)
                    .Select(g => new DataPoint
                    {
                        Timestamp = g.Key,
                        Value = g.Average(m => m.Fields?.Lux ?? 0)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching recent light levels: {ex.Message}");
                return new List<DataPoint>();
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

        public async Task<List<DailyMeasurement>?> GetDailyMeasurementsAsync(int days = 7, bool rainOnly = false)
        {
            return await GetCachedDataAsync<List<DailyMeasurement>>(
                $"daily_measurements_{days}_{(rainOnly ? "rain_only" : "all")}", 
                DAILY_CACHE_MINUTES,
                async () => {
                    try
                    {
                        DateTime startDate = DateTime.UtcNow.AddDays(-days);
                        string formattedDate = startDate.ToString("yyyy-MM-dd");
                        string rainOnlyQuery = rainOnly ? "&rainOnly=true" : string.Empty;

                        return await _httpClient.GetFromJsonAsync<List<DailyMeasurement>>($"api/weather/daily?startDate={formattedDate}{rainOnlyQuery}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error fetching daily measurements: {ex.Message}");
                        return null;
                    }
                });
        }

        public async Task<WeatherAnalysisResult> AnalyzeWeatherTrendsAsync(int days = 7, bool rainOnly = false)
        {
            var result = new WeatherAnalysisResult();
            
            try
            {
                // Get daily and hourly measurements
                var dailyData = await GetDailyMeasurementsAsync(days, rainOnly);
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
                    result.AverageDailyRainfall = dailyData.Average(d => d.Fields.Rain.Sum);
                    result.MaxDailyRainfall = dailyData.Max(d => d.Fields.Rain.Sum);
                    
                    // Set the analysis period
                    result.StartDate = dailyData.Min(d => d.TimestampMs);
                    result.EndDate = dailyData.Max(d => d.TimestampMs);
                    
                    var orderedDaily = dailyData.OrderBy(d => d.TimestampMs).ToList();

                    // Calculate trends - is temperature rising or falling?
                    var firstDayAvg = orderedDaily.First().Fields.Temperature.Avg;
                    var lastDayAvg = orderedDaily.Last().Fields.Temperature.Avg;
                    result.TemperatureTrend = lastDayAvg - firstDayAvg;
                    
                    // Calculate pressure trend
                    var firstDayPressure = orderedDaily.First().Fields.Pressure.Avg;
                    var lastDayPressure = orderedDaily.Last().Fields.Pressure.Avg;
                    result.PressureTrend = lastDayPressure - firstDayPressure;

                    // Calculate rainfall trend information
                    var firstDayRain = orderedDaily.First().Fields.Rain.Sum;
                    var lastDayRain = orderedDaily.Last().Fields.Rain.Sum;
                    result.RainTrend = lastDayRain - firstDayRain;

                    int currentDryStreak = 0;
                    int longestDryStreak = 0;
                    foreach (var day in orderedDaily)
                    {
                        if (day.Fields.Rain.Sum <= 0.1)
                        {
                            currentDryStreak++;
                            if (currentDryStreak > longestDryStreak)
                            {
                                longestDryStreak = currentDryStreak;
                            }
                        }
                        else
                        {
                            currentDryStreak = 0;
                        }
                    }

                    result.LongestDrySpellDays = longestDryStreak;
                    result.DailyRainfall = orderedDaily
                        .Select(d => new RainfallDataPoint
                        {
                            Timestamp = d.TimestampMs,
                            Total = d.Fields.Rain.Sum,
                            MaxRate = d.Fields.Rain.Max
                        })
                        .ToList();
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

        public async Task<(HistoricalDailyRecordResponse? Record, string? ErrorMessage)> GetHistoricalDailyRecordAsync(DateTime date, string location = "backyard")
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var formattedDate = date.ToString("yyyy-MM-dd");
            var cacheKey = $"historical_daily_{formattedDate}_{normalizedLocation}";

            try
            {
                var record = await GetCachedDataAsync<HistoricalDailyRecordResponse>(
                    cacheKey,
                    HISTORICAL_DAILY_CACHE_MINUTES,
                    async () => await _httpClient.GetFromJsonAsync<HistoricalDailyRecordResponse>(
                        $"api/weather/historical-daily?date={formattedDate}&location={Uri.EscapeDataString(normalizedLocation)}"));

                return (record, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching historical daily record for {formattedDate}: {ex.Message}");
                return (null, ex.Message);
            }
        }

        public async Task<(AllTimeRecordsResponse? Records, string? ErrorMessage)> GetAllTimeRecordsAsync(string location = "backyard")
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var cacheKey = $"all_time_records_{normalizedLocation}";

            try
            {
                var records = await GetCachedDataAsync<AllTimeRecordsResponse>(
                    cacheKey,
                    ALL_TIME_RECORDS_CACHE_MINUTES,
                    async () => await _httpClient.GetFromJsonAsync<AllTimeRecordsResponse>(
                        $"api/weather/records/highlights?location={Uri.EscapeDataString(normalizedLocation)}"));

                return (records, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching all-time records for {normalizedLocation}: {ex.Message}");
                return (null, ex.Message);
            }
        }

        public async Task<WeatherAnalysisResult> GetRecentAnalysisAsync(int hours)
        {
            // Call the existing method that already implements this functionality
            return await AnalyzeRecentMeasurementsAsync(hours);
        }

        public async Task<WeatherAnalysisResult> GetAnalysisAsync(int days, bool rainOnly = false)
        {
            // Call the existing method that already implements this functionality
            return await AnalyzeWeatherTrendsAsync(days, rainOnly);
        }

        public async Task<WeatherPrediction?> GetLatestPredictionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/weather/prediction/latest");
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"WeatherPrediction JSON: {json}");

                return JsonSerializer.Deserialize<WeatherPrediction>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new DateTimeConverter() }
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching latest prediction: {ex.Message}");
                return null;
            }
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
        public double AverageDailyRainfall { get; set; }
        public double MaxDailyRainfall { get; set; }
        public double RainTrend { get; set; }
        public int LongestDrySpellDays { get; set; }
        
        // Analysis period
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        // Chart data
        public List<TemperatureDataPoint>? HourlyTemperatures { get; set; }
        public List<DataPoint>? HourlyPressures { get; set; }
        public List<RainfallDataPoint>? DailyRainfall { get; set; }
        
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

    public class RainfallDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Total { get; set; }
        public double MaxRate { get; set; }
    }
}