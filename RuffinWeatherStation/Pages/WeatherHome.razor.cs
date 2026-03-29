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
    private List<DataPoint> todayLightLevels = new();
    private DateTime? approximateSunriseUtc;
    private DateTime? approximateSunsetUtc;
    private DateTime? daylightSnapshotFetchedAtUtc;
    private string daylightSnapshotLocation = string.Empty;
    private bool usesPriorDaySnapshotForDaylight;

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
}
