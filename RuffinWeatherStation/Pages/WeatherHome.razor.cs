using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RuffinWeatherStation.Models;
using RuffinWeatherStation.Services;

namespace RuffinWeatherStation.Pages;

public partial class WeatherHome
{
    [Inject] private TemperatureService TemperatureService { get; set; } = default!;
    [Inject] private GardenDataService GardenDataService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private TemperatureMeasurement? measurement;
    private WeatherPrediction? prediction;
    private WeatherAnalysisResult? analysisResult;
    private WeatherAnalysisResult? recentAnalysisResult;
    private List<TemperatureMeasurement>? todaysMeasurements;
    private List<TodayRainfallDataPoint>? rainfallData;
    private WeatherAnalysisResult? recentRainfallAnalysis;
    private string? recentRainfallErrorMessage;
    private HistoricalDailyRecordResponse? historicalDailyRecord;
    private string? historicalDailyErrorMessage;
    private bool isHistoricalDailyLoading = true;
    private DateTime selectedHistoricalDate = DateTime.Today;
    private const string historicalLocation = "backyard";
    private const int recentRainfallDays = 15;
    private double accumulatedRainfall;
    private TimeSpan elapsedRainfallTime = TimeSpan.Zero;
    private DateTime? firstMeasurementTime;
    private int analysisPeriod = 7; // Default to 7-day analysis
    private int analysisHours = 1; // Default to 1-hour analysis for recent data
    private bool showShortTermAnalysis = true; // Default to short-term analysis view
    private readonly int deviceOfflineThresholdMinutes = 10; // Consider device offline after 10 minutes of no updates
    private bool isLoading = true;
    private DateTime? nextCacheRefresh;
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(15); // Estimated cache duration
    private string? userTimeZone; // Store user's timezone
    private NwsAlertSummaryData? homeAlertSummary;
    private List<NwsAlertSnapshotData> attentionAlerts = new();
    private bool isAlertSummaryLoading;
    private string? alertSummaryLoadError;

    protected override async Task OnInitializedAsync()
    {
        // Get user's timezone first.
        await GetUserTimeZone();

        await LoadData();
        await LoadHistoricalDailyRecord();

        // Load the prediction data.
        await LoadPredictionData();

        // Surface high-risk CAP combinations from the latest NWS snapshot on the Home dashboard.
        await LoadHomeAttentionAlerts();

        // Start with short-term (recent) analysis.
        await AnalyzeRecentPeriod(analysisHours);

        // Load dedicated rainfall overview data for garden planning.
        await LoadRecentRainfallAnalysis();

        // Also load long-term analysis in background.
        await AnalyzePeriod(analysisPeriod);
    }

    private async Task LoadHomeAttentionAlerts()
    {
        try
        {
            isAlertSummaryLoading = true;
            alertSummaryLoadError = null;
            attentionAlerts.Clear();

            homeAlertSummary = await GardenDataService.GetAlertsSummaryAsync(days: 7);

            if (homeAlertSummary?.RecentAlerts != null)
            {
                attentionAlerts = homeAlertSummary.RecentAlerts
                    .Where(IsEyebrowRaisingAlert)
                    .OrderByDescending(a => GetSeverityRank(a.Severity))
                    .ThenByDescending(a => a.OnsetUtc ?? a.SentUtc ?? DateTime.MinValue)
                    .Take(4)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            alertSummaryLoadError = $"Unable to load attention alerts: {ex.Message}";
            Console.Error.WriteLine(alertSummaryLoadError);
        }
        finally
        {
            isAlertSummaryLoading = false;
        }
    }

    private async Task GetUserTimeZone()
    {
        try
        {
            userTimeZone = await JSRuntime.InvokeAsync<string>("eval", "Intl.DateTimeFormat().resolvedOptions().timeZone");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error getting user timezone: {ex.Message}");
            userTimeZone = null; // Fallback to UTC
        }
    }

    private DateTime ConvertToUserTimeZone(DateTime utcDateTime)
    {
        if (string.IsNullOrEmpty(userTimeZone))
        {
            // Fallback to local system time if timezone detection failed.
            return utcDateTime.ToLocalTime();
        }

        try
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(userTimeZone);
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZoneInfo);
        }
        catch
        {
            // If timezone conversion fails, fall back to local time.
            return utcDateTime.ToLocalTime();
        }
    }

    private string FormatDateTimeForUser(DateTime utcDateTime)
    {
        var localDateTime = ConvertToUserTimeZone(utcDateTime);
        return localDateTime.ToString("g");
    }

    private string FormatDateTimeForUserWithTimezone(DateTime utcDateTime)
    {
        var localDateTime = ConvertToUserTimeZone(utcDateTime);
        var timeZoneDisplay = string.IsNullOrEmpty(userTimeZone) ? "Local Time" : userTimeZone;
        return $"{localDateTime:g} ({timeZoneDisplay})";
    }

    private async Task LoadPredictionData()
    {
        try
        {
            prediction = await TemperatureService.GetLatestPredictionAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading prediction data: {ex.Message}");
        }
    }

    private async Task LoadData()
    {
        try
        {
            measurement = await TemperatureService.GetLatestMeasurementAsync();
            todaysMeasurements = await TemperatureService.GetTodaysMeasurementsAsync();

            accumulatedRainfall = 0;
            elapsedRainfallTime = TimeSpan.Zero;
            firstMeasurementTime = null;
            rainfallData = new List<TodayRainfallDataPoint>();

            if (todaysMeasurements != null && todaysMeasurements.Any())
            {
                accumulatedRainfall = todaysMeasurements.Sum(m => m.Fields?.Rain ?? 0);
                firstMeasurementTime = todaysMeasurements.Min(m => m.TimestampMs);

                if (firstMeasurementTime.HasValue)
                {
                    elapsedRainfallTime = DateTime.Now - firstMeasurementTime.Value;
                }

                double runningTotal = 0;
                foreach (var entry in todaysMeasurements.OrderBy(m => m.TimestampMs))
                {
                    var increment = entry.Fields?.Rain ?? 0;
                    runningTotal += increment;

                    rainfallData.Add(new TodayRainfallDataPoint
                    {
                        Timestamp = entry.TimestampMs,
                        RainIncrement = increment,
                        AccumulatedRain = runningTotal
                    });
                }

                // Ensure each chart category bucket is unique to prevent duplicate SVG path keys in Radzen column rendering.
                rainfallData = rainfallData
                    .GroupBy(r => r.Timestamp)
                    .OrderBy(g => g.Key)
                    .Select(g => new TodayRainfallDataPoint
                    {
                        Timestamp = g.Key,
                        RainIncrement = g.Sum(x => x.RainIncrement),
                        AccumulatedRain = g.Max(x => x.AccumulatedRain)
                    })
                    .ToList();
            }

            // Update the next cache refresh time estimation.
            nextCacheRefresh = DateTime.Now.Add(cacheDuration);
            isLoading = false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading weather data: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadRecentRainfallAnalysis()
    {
        try
        {
            recentRainfallErrorMessage = null;
            recentRainfallAnalysis = await TemperatureService.GetAnalysisAsync(recentRainfallDays, rainOnly: true);

            if (recentRainfallAnalysis?.DailyRainfall != null)
            {
                recentRainfallAnalysis.DailyRainfall = recentRainfallAnalysis.DailyRainfall
                    .GroupBy(d => d.Timestamp)
                    .OrderBy(g => g.Key)
                    .Select(g => new RainfallDataPoint
                    {
                        Timestamp = g.Key,
                        Total = g.Sum(x => x.Total),
                        MaxRate = g.Max(x => x.MaxRate)
                    })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            recentRainfallErrorMessage = $"Unable to load recent rainfall: {ex.Message}";
            Console.Error.WriteLine(recentRainfallErrorMessage);
        }
    }

    private async Task LoadHistoricalDailyRecord()
    {
        try
        {
            isHistoricalDailyLoading = true;
            historicalDailyErrorMessage = null;

            var (record, errorMessage) = await TemperatureService.GetHistoricalDailyRecordAsync(selectedHistoricalDate, historicalLocation);
            historicalDailyRecord = record;
            historicalDailyErrorMessage = errorMessage;
        }
        catch (Exception ex)
        {
            historicalDailyErrorMessage = $"Unable to load historical daily record: {ex.Message}";
            Console.Error.WriteLine(historicalDailyErrorMessage);
        }
        finally
        {
            isHistoricalDailyLoading = false;
        }
    }

    private async Task OnHistoricalDateChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (DateTime.TryParse(value, out var parsedDate))
        {
            selectedHistoricalDate = parsedDate.Date;
            await LoadHistoricalDailyRecord();
            StateHasChanged();
        }
    }

    // New constants for data age thresholds.
    private readonly int DATA_FRESH_MINUTES = 60; // Data is considered fresh within 1 hour

    // Updated methods to reflect data freshness instead of device online status.
    private bool IsDataStale()
    {
        if (measurement == null)
            return true;

        // Calculate minutes since last update.
        var dataAge = DateTime.Now - measurement.TimestampMs;
        return dataAge.TotalMinutes > DATA_FRESH_MINUTES;
    }

    private bool IsDeviceOffline()
    {
        if (measurement == null)
            return true;

        // Calculate minutes since last update.
        var dataAge = DateTime.Now - measurement.TimestampMs;
        return dataAge.TotalMinutes > deviceOfflineThresholdMinutes;
    }

    private string GetDataAge()
    {
        if (measurement == null)
            return "unknown";

        var age = DateTime.Now - measurement.TimestampMs;
        if (age.TotalDays > 1)
        {
            return $"{Math.Floor(age.TotalDays)} day(s)";
        }

        if (age.TotalHours > 1)
        {
            return $"{Math.Floor(age.TotalHours)} hour(s)";
        }

        return $"{Math.Floor(age.TotalMinutes)} minute(s)";
    }

    private string GetTimeUntilCacheRefresh()
    {
        if (!nextCacheRefresh.HasValue)
            return "unknown";

        var timeLeft = nextCacheRefresh.Value - DateTime.Now;
        if (timeLeft.TotalSeconds <= 0)
            return "any moment";

        if (timeLeft.TotalMinutes < 1)
            return $"{Math.Ceiling(timeLeft.TotalSeconds)} seconds";

        return $"{Math.Ceiling(timeLeft.TotalMinutes)} minutes";
    }

    private void SwitchToLongTermAnalysis()
    {
        showShortTermAnalysis = false;
        StateHasChanged();
    }

    private void SwitchToShortTermAnalysis()
    {
        showShortTermAnalysis = true;
        StateHasChanged();
    }

    private async Task AnalyzePeriod(int days)
    {
        analysisPeriod = days;
        try
        {
            analysisResult = await TemperatureService.GetAnalysisAsync(days);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error analyzing weather over {days} days: {ex.Message}");
        }

        StateHasChanged();
    }

    private async Task AnalyzeRecentPeriod(int hours)
    {
        analysisHours = hours;
        try
        {
            recentAnalysisResult = await TemperatureService.GetRecentAnalysisAsync(hours);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error analyzing recent weather over {hours} hours: {ex.Message}");
        }

        StateHasChanged();
    }

    private string FormatDouble(double? value)
    {
        if (value == null)
            return "N/A";

        return Math.Round(value.Value, 1).ToString("0.0");
    }

    private string FormatTemperatureF(double? tempC)
    {
        if (tempC == null)
            return "N/A";

        double tempF = 32 + (tempC.Value * 9 / 5);
        return Math.Round(tempF, 1).ToString("0.0");
    }

    private string FormatPressureValue(object value)
    {
        if (value is double pressure)
        {
            return pressure.ToString("F3");
        }

        return value?.ToString() ?? "";
    }

    private string FormatTimestamp(object value)
    {
        if (value is DateTime dt)
        {
            // Convert UTC to user's timezone for chart display.
            var localDateTime = ConvertToUserTimeZone(dt);

            if (showShortTermAnalysis)
            {
                // For short-term (hourly) analysis, show hour and minutes.
                return localDateTime.ToString("HH:mm");
            }

            // For long-term analysis, show just the day.
            return localDateTime.ToString("MM/dd");
        }

        return value?.ToString() ?? "";
    }

    private string FormatRainfallTimestamp(object value)
    {
        if (value is DateTime dt)
        {
            return ConvertToUserTimeZone(dt).ToString("HH:mm");
        }

        return value?.ToString() ?? "";
    }

    private string FormatRecentRainfallDay(object value)
    {
        if (value is DateTime dt)
        {
            return ConvertToUserTimeZone(dt).ToString("MM/dd");
        }

        return value?.ToString() ?? "";
    }

    private string FormatHistoricDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "N/A";

        if (DateTime.TryParse(value, out var parsedDate))
            return parsedDate.ToString("MMM d, yyyy");

        return value;
    }

    private string FormatNullableDateTimeForUserWithTimezone(DateTime? value)
    {
        return value.HasValue ? FormatDateTimeForUserWithTimezone(value.Value) : "n/a";
    }

    private bool IsEyebrowRaisingAlert(NwsAlertSnapshotData alert)
    {
        if (!alert.IsActive)
        {
            return false;
        }

        return GetUrgencyRank(alert.Urgency) >= 2 &&
               GetSeverityRank(alert.Severity) >= 2 &&
               GetCertaintyRank(alert.Certainty) >= 1;
    }

    private static int GetUrgencyRank(string urgency)
    {
        return urgency.Trim().ToLowerInvariant() switch
        {
            "immediate" => 4,
            "expected" => 3,
            "future" => 2,
            "past" => 1,
            _ => 0
        };
    }

    private static int GetSeverityRank(string severity)
    {
        return severity.Trim().ToLowerInvariant() switch
        {
            "extreme" => 4,
            "severe" => 3,
            "moderate" => 2,
            "minor" => 1,
            _ => 0
        };
    }

    private static int GetCertaintyRank(string certainty)
    {
        return certainty.Trim().ToLowerInvariant() switch
        {
            "observed" => 3,
            "likely" => 2,
            "possible" => 1,
            _ => 0
        };
    }

    private static string GetHomeSeverityBadgeClass(string severity)
    {
        return GetSeverityRank(severity) switch
        {
            4 => "text-bg-dark",
            3 => "text-bg-danger",
            2 => "text-bg-warning",
            _ => "text-bg-secondary"
        };
    }

    private class TodayRainfallDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double RainIncrement { get; set; }
        public double AccumulatedRain { get; set; }
    }
}
