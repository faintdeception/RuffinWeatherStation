using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RuffinWeatherStation.Models;
using RuffinWeatherStation.Services;
using System.Text.Json;

namespace RuffinWeatherStation.Pages;

public partial class GardenData
{
    private const int recentRainfallDays = 15;
    private const int recentTemperaturePatternDays = 10;
    private const int seasonVisualizationDays = 21;
    private const int dailyMeasurementLookbackDays = 21;
    private const int soonStatusHorizonDays = 30;
    private const double falseSpringWarmDayThresholdC = 20.00;
    private const double falseSpringColdNightThresholdC = 2.22;
    private const double falseSpringSwingThresholdC = 15.56;
    private const double warmStableHighThresholdC = 22.22;
    private const double warmStableLowThresholdC = 10.00;
    private const double coolingPatternTrendThresholdC = -1.39;
    private const double springStabilizingTrendThresholdC = 1.11;
    private const string seasonSignalToggleStorageKey = "gardenData.seasonSignalToggles.v1";

    private bool isLoading = true;
    private string? errorMessage;

    private GardenReferenceData? gardenReference;
    private NwsAlertSummaryData? nwsAlertSummary;
    private NwsForecastData? gardenForecastSummary;
    private WeatherAnalysisResult? rainfallAnalysis;
    private List<DailyMeasurement>? recentDailyMeasurements;
    private List<GardenPlantProfile> plantProfiles = new();
    private List<PlantReadinessCard> plantReadinessCards = new();
    private string? plantProfilesLoadError;
    private List<string> plantProfileWarnings = new();

    private DateOnly seedWindowStart;
    private DateOnly seedWindowEnd;
    private DateOnly hoseWindowStart;
    private DateOnly hoseWindowEnd;
    private int recentFrostDays;
    private string temperatureTrendSummary = "Unknown";
    private bool showDormantCards;
    private List<SeasonTrendPoint> seasonTrendData = new();
    private List<SeasonSignalPoint> seasonWarmSignalPoints = new();
    private List<SeasonSignalPoint> seasonColdSignalPoints = new();
    private List<SeasonSignalPoint> seasonSwingSignalPoints = new();
    private List<SeasonForecastPoint> seasonForecastData = new();
    private WeatherPrediction? seasonForecast;
    private string seasonForecastOverlaySummary = "No forecast overlay available.";
    private int seasonWarmSignalHits;
    private int seasonColdSignalHits;
    private int seasonLargeSwingSignalHits;
    private int seasonSignalWindowDays;
    private bool showWarmSignalSeries = true;
    private bool showColdSignalSeries = true;
    private bool showSwingSignalSeries = true;
    private string seasonRegimeLabel = "Unknown";
    private string seasonRegimeWhy = "Not enough data.";
    private int seasonRegimeConfidence;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            isLoading = true;
            errorMessage = null;

            await LoadSeasonSignalToggleSettingsAsync();

            var rainfallTask = TemperatureService.GetAnalysisAsync(recentRainfallDays, rainOnly: true);
            var dailyTask = TemperatureService.GetDailyMeasurementsAsync(dailyMeasurementLookbackDays);
            var referenceTask = GardenDataService.GetGardenReferenceAsync();
            var plantProfilesTask = GardenPlantProfileService.GetProfilesAsync();
            var alertsTask = GardenDataService.GetAlertsSummaryAsync(days: 7);
            var forecastSummaryTask = GardenDataService.GetForecastSummaryAsync(maxPeriods: 14);
            var forecastTask = TemperatureService.GetLatestPredictionAsync();

            await Task.WhenAll(rainfallTask, dailyTask, referenceTask, plantProfilesTask, alertsTask, forecastSummaryTask, forecastTask);

            rainfallAnalysis = rainfallTask.Result;
            recentDailyMeasurements = dailyTask.Result;
            gardenReference = referenceTask.Result;
            plantProfiles = plantProfilesTask.Result.Profiles;
            plantProfileWarnings = plantProfilesTask.Result.Warnings;
            plantProfilesLoadError = plantProfilesTask.Result.ErrorMessage;
            nwsAlertSummary = alertsTask.Result;
            gardenForecastSummary = forecastSummaryTask.Result;
            seasonForecast = forecastTask.Result;

            NormalizeRainfallChartData();

            if (gardenReference == null)
            {
                errorMessage = "Unable to load garden reference data from the API.";
                return;
            }

            CalculatePlanningWindows();
            CalculateTemperaturePattern();
            CalculateSeasonRegime();
            BuildSeasonForecastOverlay();
            BuildPlantReadinessCards();
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to load garden data: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ToggleWarmSignalSeriesAsync()
    {
        showWarmSignalSeries = !showWarmSignalSeries;
        await SaveSeasonSignalToggleSettingsAsync();
    }

    private async Task ToggleColdSignalSeriesAsync()
    {
        showColdSignalSeries = !showColdSignalSeries;
        await SaveSeasonSignalToggleSettingsAsync();
    }

    private async Task ToggleSwingSignalSeriesAsync()
    {
        showSwingSignalSeries = !showSwingSignalSeries;
        await SaveSeasonSignalToggleSettingsAsync();
    }

    private async Task LoadSeasonSignalToggleSettingsAsync()
    {
        try
        {
            var rawJson = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", seasonSignalToggleStorageKey);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return;
            }

            var settings = JsonSerializer.Deserialize<SeasonSignalToggleSettings>(rawJson);
            if (settings == null)
            {
                return;
            }

            showWarmSignalSeries = settings.ShowWarmSignalSeries;
            showColdSignalSeries = settings.ShowColdSignalSeries;
            showSwingSignalSeries = settings.ShowSwingSignalSeries;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not load season signal toggles from localStorage: {ex.Message}");
        }
    }

    private async Task SaveSeasonSignalToggleSettingsAsync()
    {
        try
        {
            var settings = new SeasonSignalToggleSettings
            {
                ShowWarmSignalSeries = showWarmSignalSeries,
                ShowColdSignalSeries = showColdSignalSeries,
                ShowSwingSignalSeries = showSwingSignalSeries
            };

            var rawJson = JsonSerializer.Serialize(settings);
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", seasonSignalToggleStorageKey, rawJson);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not save season signal toggles to localStorage: {ex.Message}");
        }
    }

    private void CalculatePlanningWindows()
    {
        if (gardenReference == null)
        {
            return;
        }

        seedWindowStart = gardenReference.AverageLastFrostDate.AddDays(-42);
        seedWindowEnd = gardenReference.AverageLastFrostDate.AddDays(-14);

        hoseWindowStart = gardenReference.SeasonStarts.Spring.AddDays(-14);
        hoseWindowEnd = gardenReference.SeasonStarts.Spring.AddDays(14);
    }

    private void CalculateTemperaturePattern()
    {
        if (recentDailyMeasurements == null || recentDailyMeasurements.Count == 0)
        {
            temperatureTrendSummary = "No daily measurements";
            recentFrostDays = 0;
            return;
        }

        var ordered = recentDailyMeasurements
            .OrderBy(d => d.TimestampMs)
            .ToList();

        var recentPatternWindow = ordered
            .TakeLast(recentTemperaturePatternDays)
            .ToList();

        if (recentPatternWindow.Count == 0)
        {
            temperatureTrendSummary = "No daily measurements";
            recentFrostDays = 0;
            return;
        }

        var firstAvg = recentPatternWindow.First().Fields.Temperature.Avg;
        var lastAvg = recentPatternWindow.Last().Fields.Temperature.Avg;
        var trendDelta = lastAvg - firstAvg;

        if (trendDelta > 1.0)
        {
            temperatureTrendSummary = $"Warming ({trendDelta:F1}C)";
        }
        else if (trendDelta < -1.0)
        {
            temperatureTrendSummary = $"Cooling ({Math.Abs(trendDelta):F1}C)";
        }
        else
        {
            temperatureTrendSummary = "Stable";
        }

        recentFrostDays = recentPatternWindow.Count(d => d.Fields.Temperature.Min <= 0);
    }

    private void CalculateSeasonRegime()
    {
        seasonTrendData.Clear();
        seasonWarmSignalPoints.Clear();
        seasonColdSignalPoints.Clear();
        seasonSwingSignalPoints.Clear();

        if (recentDailyMeasurements == null || recentDailyMeasurements.Count == 0)
        {
            seasonRegimeLabel = "Insufficient Data";
            seasonRegimeWhy = "No recent daily measurements available.";
            seasonRegimeConfidence = 0;
            seasonWarmSignalHits = 0;
            seasonColdSignalHits = 0;
            seasonLargeSwingSignalHits = 0;
            seasonSignalWindowDays = 0;
            return;
        }

        var ordered = recentDailyMeasurements
            .OrderBy(d => d.TimestampMs)
            .TakeLast(seasonVisualizationDays)
            .ToList();

        seasonTrendData = new List<SeasonTrendPoint>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var source = ordered[i];
            var averageC = (source.Fields.Temperature.Max + source.Fields.Temperature.Min) / 2.0;

            var rollingWindowStart = Math.Max(0, i - 4);
            var rollingWindow = ordered
                .Skip(rollingWindowStart)
                .Take(i - rollingWindowStart + 1)
                .Select(d => (d.Fields.Temperature.Max + d.Fields.Temperature.Min) / 2.0)
                .ToList();

            var rollingAverageC = rollingWindow.Average();

            seasonTrendData.Add(new SeasonTrendPoint
            {
                Date = source.TimestampMs,
                HighC = source.Fields.Temperature.Max,
                LowC = source.Fields.Temperature.Min,
                AverageC = averageC,
                RollingAverageC = rollingAverageC,
                WarmDayThresholdC = falseSpringWarmDayThresholdC,
                ColdNightThresholdC = falseSpringColdNightThresholdC
            });

            if (source.Fields.Temperature.Max >= falseSpringWarmDayThresholdC)
            {
                seasonWarmSignalPoints.Add(new SeasonSignalPoint
                {
                    Date = source.TimestampMs,
                    ValueC = source.Fields.Temperature.Max
                });
            }

            if (source.Fields.Temperature.Min <= falseSpringColdNightThresholdC)
            {
                seasonColdSignalPoints.Add(new SeasonSignalPoint
                {
                    Date = source.TimestampMs,
                    ValueC = source.Fields.Temperature.Min
                });
            }

            if ((source.Fields.Temperature.Max - source.Fields.Temperature.Min) >= falseSpringSwingThresholdC)
            {
                seasonSwingSignalPoints.Add(new SeasonSignalPoint
                {
                    Date = source.TimestampMs,
                    ValueC = averageC
                });
            }
        }

        if (seasonTrendData.Count < 5)
        {
            seasonRegimeLabel = "Insufficient Data";
            seasonRegimeWhy = "Need at least 5 daily points for stable season detection.";
            seasonRegimeConfidence = 20;
            seasonWarmSignalHits = 0;
            seasonColdSignalHits = 0;
            seasonLargeSwingSignalHits = 0;
            seasonSignalWindowDays = seasonTrendData.Count;
            return;
        }

        var window = seasonTrendData.TakeLast(Math.Min(10, seasonTrendData.Count)).ToList();
        var warmHighDays = window.Count(p => p.HighC >= falseSpringWarmDayThresholdC);
        var coldNightDays = window.Count(p => p.LowC <= falseSpringColdNightThresholdC);
        var largeSwingDays = window.Count(p => (p.HighC - p.LowC) >= falseSpringSwingThresholdC);
        var firstAvg = window.First().AverageC;
        var lastAvg = window.Last().AverageC;
        var avgTrend = lastAvg - firstAvg;
        var warmStableDays = window.Count(p => p.HighC >= warmStableHighThresholdC && p.LowC >= warmStableLowThresholdC);

        seasonWarmSignalHits = warmHighDays;
        seasonColdSignalHits = coldNightDays;
        seasonLargeSwingSignalHits = largeSwingDays;
        seasonSignalWindowDays = window.Count;

        if (warmHighDays >= 3 && coldNightDays >= 3)
        {
            seasonRegimeLabel = "False Spring Signal";
            seasonRegimeConfidence = Math.Min(95, 45 + (warmHighDays * 6) + (coldNightDays * 6) + (largeSwingDays * 4));
            seasonRegimeWhy = $"{warmHighDays} warm day(s) ≥ {falseSpringWarmDayThresholdC:F2}°C (68°F) paired with {coldNightDays} cold night(s) ≤ {falseSpringColdNightThresholdC:F2}°C (36°F) in the last {window.Count} days.";
            return;
        }

        if (warmStableDays >= 4 && coldNightDays == 0)
        {
            seasonRegimeLabel = "Warm Stable";
            seasonRegimeConfidence = Math.Min(92, 50 + (warmStableDays * 8));
            seasonRegimeWhy = $"Sustained warmth: {warmStableDays} day(s) with highs ≥ {warmStableHighThresholdC:F2}°C (72°F) and lows ≥ {warmStableLowThresholdC:F2}°C (50°F).";
            return;
        }

        if (avgTrend <= coolingPatternTrendThresholdC && coldNightDays >= 2)
        {
            seasonRegimeLabel = "Cooling Pattern";
            seasonRegimeConfidence = Math.Min(90, 45 + (coldNightDays * 7) + (int)Math.Abs(avgTrend * 5));
            seasonRegimeWhy = $"Average temperature trend dropped by {Math.Abs(avgTrend):F2}°C ({Math.Abs(ToFahrenheit(avgTrend) - 32):F1}°F) with recurring cold nights.";
            return;
        }

        if (avgTrend >= springStabilizingTrendThresholdC && coldNightDays <= 1)
        {
            seasonRegimeLabel = "Spring Stabilizing";
            seasonRegimeConfidence = Math.Min(88, 40 + (int)(avgTrend * 8) + ((window.Count - coldNightDays) * 2));
            seasonRegimeWhy = $"Average temperature trend rose by {avgTrend:F2}°C ({Math.Abs(ToFahrenheit(avgTrend) - 32):F1}°F) with limited cold-night setbacks.";
            return;
        }

        seasonRegimeLabel = "Transitional";
        seasonRegimeConfidence = 55;
        seasonRegimeWhy = "Mixed daily highs/lows indicate seasonal transition without a dominant regime yet.";
    }

    private void BuildSeasonForecastOverlay()
    {
        seasonForecastData.Clear();
        seasonForecastOverlaySummary = "No forecast overlay available.";

        if (seasonForecast == null || seasonForecast.HasError)
        {
            return;
        }

        var anchor = seasonForecast.CreatedAt == default ? DateTime.Now : seasonForecast.CreatedAt.ToLocalTime();

        if (seasonForecast.Prediction12h?.Temperature != null)
        {
            seasonForecastData.Add(new SeasonForecastPoint
            {
                Date = anchor.AddHours(12),
                ForecastHighC = seasonForecast.Prediction12h.Temperature.Max,
                ForecastLowC = seasonForecast.Prediction12h.Temperature.Min
            });
        }

        if (seasonForecast.Prediction24h?.Temperature != null)
        {
            seasonForecastData.Add(new SeasonForecastPoint
            {
                Date = anchor.AddHours(24),
                ForecastHighC = seasonForecast.Prediction24h.Temperature.Max,
                ForecastLowC = seasonForecast.Prediction24h.Temperature.Min
            });
        }

        if (seasonForecastData.Count == 0)
        {
            return;
        }

        seasonForecastOverlaySummary = $"Generated {FormatUtcDateTime(seasonForecast.CreatedAt)} with confidence {seasonForecast.ConfidencePercentage}.";
    }

    private void BuildPlantReadinessCards()
    {
        plantReadinessCards.Clear();

        if (gardenReference == null || plantProfiles.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var computedCards = new List<PlantReadinessCard>();

        foreach (var profile in plantProfiles)
        {
            var seasonTone = ResolveSeasonTone(profile.Categories);
            var targetTransplantDate = profile.DaysAfterLastFrostToTransplant.HasValue
                ? gardenReference.AverageLastFrostDate.AddDays(profile.DaysAfterLastFrostToTransplant.Value)
                : (DateOnly?)null;

            var indoorStartDate = profile.DaysBeforeLastFrostToStartIndoors.HasValue
                ? gardenReference.AverageLastFrostDate.AddDays(-profile.DaysBeforeLastFrostToStartIndoors.Value)
                : (DateOnly?)null;

            var nightThresholdC = profile.MinNightTempC ?? 10.00;
            var useCoolingLogic = ShouldUseCoolingLogic(profile);

            var nightStreak = CalculateNightStreak(nightThresholdC, useCoolingLogic);
            var requiredNights = Math.Max(0, profile.RequiredConsecutiveNights);
            var leadDays = Math.Max(0, profile.LeadDays ?? 14);
            var statusLeadDays = Math.Min(soonStatusHorizonDays, leadDays);

            var primaryWindow = EvaluateMonthDayWindow(today, profile.WindowStartMonthDay, profile.WindowEndMonthDay, statusLeadDays);
            var secondaryWindow = EvaluateMonthDayWindow(today, profile.SecondaryWindowStartMonthDay, profile.SecondaryWindowEndMonthDay, statusLeadDays);
            var hasWindow = primaryWindow.HasWindow || secondaryWindow.HasWindow;
            var isInWindow = primaryWindow.IsInWindow || secondaryWindow.IsInWindow;
            var isWindowSoon = !isInWindow && (primaryWindow.IsWindowSoon || secondaryWindow.IsWindowSoon);
            var daysUntilWindowStart = GetEarliestDaysUntil(primaryWindow.DaysUntilStart, secondaryWindow.DaysUntilStart);
            var activeWindowLabel = ResolveWindowLabel(
                primaryWindow.IsInWindow,
                secondaryWindow.IsInWindow,
                profile.WindowStartMonthDay,
                profile.SecondaryWindowStartMonthDay);
            var upcomingWindowLabel = ResolveWindowLabel(
                primaryWindow.IsWindowSoon,
                secondaryWindow.IsWindowSoon,
                profile.WindowStartMonthDay,
                profile.SecondaryWindowStartMonthDay,
                primaryWindow.DaysUntilStart,
                secondaryWindow.DaysUntilStart);
            var (hasLatestPlantDate, isPastLatestPlantDate, daysUntilLatestPlantDate) = EvaluateLatestPlantDate(today, profile.LatestPlantMonthDay);
            var isPastPlantingWindow = !hasWindow && hasLatestPlantDate && isPastLatestPlantDate && !profile.SupportsSuccessionPlanting;

            var hasDateRule = targetTransplantDate.HasValue;
            var targetDateValue = targetTransplantDate.GetValueOrDefault();
            var daysUntilTargetDate = hasDateRule ? targetDateValue.DayNumber - today.DayNumber : (int?)null;

            var isDateReady = !hasDateRule || today >= targetDateValue;
            var isDateSoon = hasDateRule && !isDateReady && daysUntilTargetDate.HasValue && daysUntilTargetDate.Value <= soonStatusHorizonDays;
            var isStreakReady = nightStreak >= requiredNights;

            var isRelevantNow = (hasWindow ? (isInWindow || isWindowSoon) : (isDateReady || isDateSoon)) && !isPastPlantingWindow;
            var isActionNow = isRelevantNow && isStreakReady && (!hasWindow || isInWindow) && isDateReady;
            var hasTimingRule = hasWindow || hasDateRule || hasLatestPlantDate;
            var streakSupportsSoon = !hasTimingRule && IsNearStreak(requiredNights, nightStreak);
            var isSoon = !isActionNow && !isPastPlantingWindow && (isRelevantNow || streakSupportsSoon);

            var statusLabel = isActionNow ? "Action now" : isSoon ? "Soon" : "Dormant";
            var badgeClass = GetStatusBadgeClass(statusLabel, seasonTone);

            var ruleExplanation = BuildRuleExplanation(requiredNights, nightStreak, useCoolingLogic, nightThresholdC, profile.SupportsSuccessionPlanting);
            var timingExplanation = BuildTimingExplanation(hasWindow, isInWindow, isWindowSoon, daysUntilWindowStart, activeWindowLabel, upcomingWindowLabel, hasDateRule, daysUntilTargetDate, hasLatestPlantDate, isPastPlantingWindow, daysUntilLatestPlantDate);

            var guidance = BuildGuidance(statusLabel, targetTransplantDate, requiredNights, nightStreak, useCoolingLogic, nightThresholdC, timingExplanation, profile.SupportsSuccessionPlanting, isPastPlantingWindow);
            var actionTypeLabel = ResolveActionTypeLabel(profile.ActionType, useCoolingLogic, hasWindow, isInWindow, isWindowSoon);

            var harvestLeadDays = Math.Max(0, profile.HarvestLeadDays ?? leadDays);
            var harvestStatusLeadDays = Math.Min(soonStatusHorizonDays, harvestLeadDays);
            var (hasHarvestWindow, isHarvestInWindow, isHarvestSoon, harvestDaysUntilStart) = EvaluateMonthDayWindow(today, profile.HarvestWindowStartMonthDay, profile.HarvestWindowEndMonthDay, harvestStatusLeadDays);

            if (hasHarvestWindow)
            {
                var harvestStatus = isHarvestInWindow ? "Action now" : isHarvestSoon ? "Soon" : "Dormant";
                var harvestTiming = BuildHarvestTimingExplanation(isHarvestInWindow, isHarvestSoon, harvestDaysUntilStart);
                var harvestRank = GetStatusRank(harvestStatus);
                var primaryRank = GetStatusRank(statusLabel);

                if (harvestRank < primaryRank)
                {
                    statusLabel = harvestStatus;
                    badgeClass = GetStatusBadgeClass(statusLabel, seasonTone);
                    actionTypeLabel = "Harvest";
                    ruleExplanation = "Harvest window rule is driving this card.";
                    timingExplanation = harvestTiming;
                    guidance = harvestStatus == "Action now"
                        ? "Harvest window is open now. Prioritize harvest quality and preservation steps."
                        : $"Harvest window is approaching. {harvestTiming}";
                }
                else if (harvestRank <= 1 && primaryRank <= 1 && !actionTypeLabel.Equals("Harvest", StringComparison.OrdinalIgnoreCase))
                {
                    actionTypeLabel = $"{actionTypeLabel} + Harvest";
                    timingExplanation = $"{timingExplanation} Harvest note: {harvestTiming}";
                    guidance = "Plant and harvest signals overlap. Stage tasks across the week to avoid crop stress.";
                }
            }

            computedCards.Add(new PlantReadinessCard
            {
                SeasonHeaderClass = ResolveSeasonHeaderClass(profile.Categories),
                SeasonLabel = ResolveSeasonLabel(profile.Categories),
                SeasonEmoji = ResolveSeasonEmoji(profile.Categories),
                SeasonTone = seasonTone,
                DisplayName = profile.DisplayName,
                CategoriesLabel = FormatCategories(profile.Categories),
                ActionTypeLabel = actionTypeLabel,
                RequiredConsecutiveNights = requiredNights,
                CurrentNightStreak = nightStreak,
                NightTempThresholdC = nightThresholdC,
                SupportsSuccessionPlanting = profile.SupportsSuccessionPlanting,
                StreakOperatorLabel = useCoolingLogic ? "≤" : "≥",
                TargetTransplantDate = targetTransplantDate,
                IndoorStartDate = indoorStartDate,
                StatusLabel = statusLabel,
                BadgeClass = badgeClass,
                RuleExplanation = ruleExplanation,
                TimingExplanation = timingExplanation,
                Guidance = guidance,
                Notes = profile.Notes
            });
        }

        plantReadinessCards = computedCards
            .OrderBy(c => GetStatusRank(c.StatusLabel))
            .ThenBy(c => c.DisplayName)
            .ToList();
    }

    private IEnumerable<PlantReadinessCard> GetVisiblePlantReadinessCards()
    {
        return showDormantCards
            ? plantReadinessCards
            : plantReadinessCards.Where(c => !c.StatusLabel.Equals("Dormant", StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<PlantReadinessCard> GetActionNowCards()
    {
        return GetVisiblePlantReadinessCards()
            .Where(c => c.StatusLabel.Equals("Action now", StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<PlantReadinessCard> GetSoonCards()
    {
        return GetVisiblePlantReadinessCards()
            .Where(c => c.StatusLabel.Equals("Soon", StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<PlantReadinessCard> GetDormantCards()
    {
        return GetVisiblePlantReadinessCards()
            .Where(c => c.StatusLabel.Equals("Dormant", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveActionTypeLabel(string? actionType, bool useCoolingLogic, bool hasWindow, bool isInWindow, bool isWindowSoon)
    {
        var normalizedAction = string.IsNullOrWhiteSpace(actionType)
            ? "plant"
            : actionType.Trim().ToLowerInvariant();

        // For cooling/fall-planting profiles, pre-window reminders are usually buying actions.
        if (normalizedAction == "plant" && useCoolingLogic && hasWindow && isWindowSoon && !isInWindow)
        {
            return "Buy";
        }

        return normalizedAction switch
        {
            "buy" => "Buy",
            "harvest" => "Harvest",
            "prep" => "Prep",
            _ => "Plant"
        };
    }

    private static string ResolveSeasonHeaderClass(List<string>? categories)
    {
        return ResolveSeasonTone(categories) switch
        {
            "warm" => "season-warm",
            "cool" => "season-cool",
            "fall" => "season-fall",
            _ => "season-general"
        };
    }

    private static string ResolveSeasonLabel(List<string>? categories)
    {
        if (ContainsCategory(categories, "warm season"))
        {
            return "Warm Season";
        }

        if (ContainsCategory(categories, "cool season"))
        {
            return "Cool Season";
        }

        if (ContainsCategory(categories, "bulb") || ContainsCategory(categories, "perennial"))
        {
            return "Fall Planting";
        }

        return "General";
    }

    private static string ResolveSeasonEmoji(List<string>? categories)
    {
        if (ContainsCategory(categories, "warm season"))
        {
            return "☀️";
        }

        if (ContainsCategory(categories, "cool season"))
        {
            return "🥬";
        }

        if (ContainsCategory(categories, "bulb") || ContainsCategory(categories, "perennial"))
        {
            return "🍂";
        }

        return "🌿";
    }

    private static string ResolveSeasonTone(List<string>? categories)
    {
        if (ContainsCategory(categories, "warm season"))
        {
            return "warm";
        }

        if (ContainsCategory(categories, "cool season"))
        {
            return "cool";
        }

        if (ContainsCategory(categories, "bulb") || ContainsCategory(categories, "perennial"))
        {
            return "fall";
        }

        return "general";
    }

    private static bool ContainsCategory(List<string>? categories, string value)
    {
        return categories?.Any(c => c.Contains(value, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool ShouldUseCoolingLogic(GardenPlantProfile profile)
    {
        // Cooling logic is for fall-planted bulb workflows, not all perennials.
        if (ContainsCategory(profile.Categories, "warm season") || ContainsCategory(profile.Categories, "cool season"))
        {
            return false;
        }

        return ContainsCategory(profile.Categories, "bulb");
    }

    private int CalculateNightStreak(double thresholdC, bool useCoolingLogic)
    {
        if (recentDailyMeasurements == null || recentDailyMeasurements.Count == 0)
        {
            return 0;
        }

        var streak = 0;
        foreach (var day in recentDailyMeasurements.OrderByDescending(d => d.TimestampMs))
        {
            var minC = day.Fields.Temperature.Min;
            var matches = useCoolingLogic ? minC <= thresholdC : minC >= thresholdC;
            if (matches)
            {
                streak++;
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private static string BuildGuidance(string statusLabel, DateOnly? targetDate, int requiredNights, int currentNights, bool useCoolingLogic, double thresholdC, string timingExplanation, bool supportsSuccessionPlanting, bool isPastPlantingWindow)
    {
        var directionText = useCoolingLogic ? "at or below" : "at or above";
        var streakDisplay = FormatCurrentNightsForGuidance(currentNights, requiredNights, supportsSuccessionPlanting);

        if (isPastPlantingWindow)
        {
            return "Primary planting window has closed for this season.";
        }

        if (statusLabel.Equals("Action now", StringComparison.OrdinalIgnoreCase))
        {
            return "Conditions look favorable to act now.";
        }

        if (statusLabel.Equals("Soon", StringComparison.OrdinalIgnoreCase))
        {
            return $"Monitor this plant closely. Need {requiredNights}-night streak {directionText} {FormatTemperaturePair(thresholdC, "F2", "F0")} (currently {streakDisplay}). {timingExplanation}";
        }

        return $"Outside active window right now. Next target timing is around {FormatGuidanceDate(targetDate)}.";
    }

    private static (bool HasWindow, bool IsInWindow, bool IsWindowSoon, int? DaysUntilStart) EvaluateMonthDayWindow(DateOnly today, string? startMonthDay, string? endMonthDay, int leadDays)
    {
        if (!TryParseMonthDay(startMonthDay, today.Year, out var start) || !TryParseMonthDay(endMonthDay, today.Year, out var end))
        {
            return (false, false, false, null);
        }

        var inWindow = IsDateWithinRange(today, start, end);
        if (inWindow)
        {
            return (true, true, false, 0);
        }

        var nextStart = GetNextOccurrence(today, startMonthDay!);
        var daysUntil = nextStart.DayNumber - today.DayNumber;
        var isSoon = daysUntil >= 0 && daysUntil <= leadDays;
        return (true, false, isSoon, daysUntil);
    }

    private static DateOnly GetNextOccurrence(DateOnly today, string monthDay)
    {
        if (!TryParseMonthDay(monthDay, today.Year, out var thisYearDate))
        {
            return today;
        }

        if (thisYearDate >= today)
        {
            return thisYearDate;
        }

        return TryParseMonthDay(monthDay, today.Year + 1, out var nextYearDate) ? nextYearDate : today;
    }

    private static bool IsDateWithinRange(DateOnly date, DateOnly start, DateOnly end)
    {
        if (start <= end)
        {
            return date >= start && date <= end;
        }

        return date >= start || date <= end;
    }

    private static bool TryParseMonthDay(string? monthDay, int year, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(monthDay))
        {
            return false;
        }

        var composed = $"{year}-{monthDay}";
        return DateOnly.TryParseExact(composed, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }

    private static bool IsNearStreak(int requiredNights, int currentNights)
    {
        if (requiredNights <= 0)
        {
            return true;
        }

        return currentNights >= Math.Max(0, requiredNights - 2);
    }

    private static int GetStatusRank(string status)
    {
        return status switch
        {
            "Action now" => 0,
            "Soon" => 1,
            _ => 2
        };
    }

    private static string GetStatusBadgeClass(string status, string seasonTone)
    {
        var statusClass = status switch
        {
            "Action now" => "status-action",
            "Soon" => "status-soon",
            _ => "status-dormant"
        };

        return $"{statusClass} season-tone-{seasonTone}";
    }

    private static string BuildRuleExplanation(int requiredNights, int currentNights, bool useCoolingLogic, double thresholdC, bool supportsSuccessionPlanting)
    {
        var operatorText = useCoolingLogic ? "≤" : "≥";
        var streakDisplay = FormatNightStreakForDisplay(currentNights, requiredNights, supportsSuccessionPlanting);
        return $"Night streak {streakDisplay} where night min {operatorText} {FormatTemperaturePair(thresholdC, "F2", "F0")}.";
    }

    private static string BuildTimingExplanation(bool hasWindow, bool isInWindow, bool isWindowSoon, int? daysUntilWindowStart, string? activeWindowLabel, string? upcomingWindowLabel, bool hasDateRule, int? daysUntilTargetDate, bool hasLatestPlantDate, bool isPastPlantingWindow, int? daysUntilLatestPlantDate)
    {
        if (isPastPlantingWindow)
        {
            return "Primary planting window has closed for this season.";
        }

        if (hasWindow)
        {
            if (isInWindow)
            {
                if (!string.IsNullOrWhiteSpace(activeWindowLabel))
                {
                    return $"Inside {activeWindowLabel}.";
                }

                return "Inside configured seasonal window.";
            }

            if (isWindowSoon && daysUntilWindowStart.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(upcomingWindowLabel))
                {
                    return $"{upcomingWindowLabel} opens in {daysUntilWindowStart.Value} day(s).";
                }

                return $"Window opens in {daysUntilWindowStart.Value} day(s).";
            }

            if (daysUntilWindowStart.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(upcomingWindowLabel))
                {
                    return $"{upcomingWindowLabel} opens in {daysUntilWindowStart.Value} day(s).";
                }

                return $"Window opens in {daysUntilWindowStart.Value} day(s).";
            }
        }

        if (hasLatestPlantDate && daysUntilLatestPlantDate.HasValue)
        {
            if (daysUntilLatestPlantDate.Value == 0)
            {
                return "Last recommended planting day is today.";
            }

            return $"Primary planting window closes in {daysUntilLatestPlantDate.Value} day(s).";
        }

        if (hasDateRule && daysUntilTargetDate.HasValue)
        {
            if (daysUntilTargetDate.Value <= 0)
            {
                return "Date target has opened.";
            }

            return $"Date target opens in {daysUntilTargetDate.Value} day(s).";
        }

        return "No strict date window configured.";
    }

    private static (bool HasLatestDate, bool IsPastLatestDate, int? DaysUntilLatestDate) EvaluateLatestPlantDate(DateOnly today, string? latestPlantMonthDay)
    {
        if (!TryParseMonthDay(latestPlantMonthDay, today.Year, out var latestDate))
        {
            return (false, false, null);
        }

        if (today <= latestDate)
        {
            return (true, false, latestDate.DayNumber - today.DayNumber);
        }

        return (true, true, null);
    }

    private static int? GetEarliestDaysUntil(int? firstDaysUntil, int? secondDaysUntil)
    {
        if (!firstDaysUntil.HasValue)
        {
            return secondDaysUntil;
        }

        if (!secondDaysUntil.HasValue)
        {
            return firstDaysUntil;
        }

        return Math.Min(firstDaysUntil.Value, secondDaysUntil.Value);
    }

    private static string? ResolveWindowLabel(bool primaryState, bool secondaryState, string? primaryWindowStartMonthDay, string? secondaryWindowStartMonthDay, int? primaryDaysUntil = null, int? secondaryDaysUntil = null)
    {
        if (primaryState && !secondaryState)
        {
            return GetSeasonLabel(primaryWindowStartMonthDay);
        }

        if (secondaryState && !primaryState)
        {
            return GetSeasonLabel(secondaryWindowStartMonthDay);
        }

        if (primaryState && secondaryState)
        {
            if (primaryDaysUntil.HasValue && secondaryDaysUntil.HasValue)
            {
                return primaryDaysUntil.Value <= secondaryDaysUntil.Value
                    ? GetSeasonLabel(primaryWindowStartMonthDay)
                    : GetSeasonLabel(secondaryWindowStartMonthDay);
            }

            return "Seasonal window";
        }

        return null;
    }

    private static string GetSeasonLabel(string? startMonthDay)
    {
        if (!TryGetMonthFromMonthDay(startMonthDay, out var month))
        {
            return "Seasonal window";
        }

        return month >= 7 ? "Fall window" : "Spring window";
    }

    private static bool TryGetMonthFromMonthDay(string? monthDay, out int month)
    {
        month = 0;
        if (string.IsNullOrWhiteSpace(monthDay))
        {
            return false;
        }

        var parts = monthDay.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        return int.TryParse(parts[0], out month);
    }

    private static string FormatNightStreakForDisplay(int currentNights, int requiredNights, bool supportsSuccessionPlanting)
    {
        if (requiredNights <= 0 || supportsSuccessionPlanting || currentNights <= requiredNights)
        {
            return $"{currentNights}/{requiredNights}";
        }

        return $"{requiredNights}+/{requiredNights}";
    }

    private static string FormatCurrentNightsForGuidance(int currentNights, int requiredNights, bool supportsSuccessionPlanting)
    {
        if (requiredNights <= 0 || supportsSuccessionPlanting || currentNights <= requiredNights)
        {
            return currentNights.ToString();
        }

        return $"{requiredNights}+";
    }

    private static string BuildHarvestTimingExplanation(bool isInWindow, bool isSoon, int? daysUntilStart)
    {
        if (isInWindow)
        {
            return "Inside configured harvest window.";
        }

        if (isSoon && daysUntilStart.HasValue)
        {
            return $"Harvest window opens in {daysUntilStart.Value} day(s).";
        }

        if (daysUntilStart.HasValue)
        {
            return $"Harvest window opens in {daysUntilStart.Value} day(s).";
        }

        return "Harvest window timing not configured.";
    }

    private static int GetNightStreakPercent(int currentNights, int requiredNights)
    {
        if (requiredNights <= 0)
        {
            return 100;
        }

        var rawPercent = (currentNights * 100.0) / requiredNights;
        return (int)Math.Clamp(Math.Round(rawPercent), 0, 100);
    }

    private static string GetNightStreakProgressClass(string status)
    {
        return status switch
        {
            "Action now" => "bg-success",
            "Soon" => "bg-warning",
            _ => "bg-secondary"
        };
    }

    private string GetChartRenderKey(string chartName)
    {
        // Work around Radzen path-key collisions during unrelated rerenders.
        return $"{chartName}-{showDormantCards}";
    }

    private string RainChartRenderKey => GetChartRenderKey("rain");
    private string SeasonChartRenderKey => GetChartRenderKey("season");

    private static string FormatGuidanceDate(DateOnly? date)
    {
        return date.HasValue ? date.Value.ToString("MMM d, yyyy") : "the recommended window";
    }

    private static string FormatCategories(List<string>? categories)
    {
        if (categories == null || categories.Count == 0)
        {
            return "Uncategorized";
        }

        return string.Join(", ", categories.Select(c => c.Trim()).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static double ToFahrenheit(double celsius)
    {
        return 32 + (celsius * 9 / 5);
    }

    private static string FormatTemperaturePair(double celsius, string celsiusFormat, string fahrenheitFormat)
    {
        var fahrenheit = ToFahrenheit(celsius);
        return $"{celsius.ToString(celsiusFormat)}°C ({fahrenheit.ToString(fahrenheitFormat)}°F)";
    }

    private static string FormatDateLabel(object value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("MM-dd");
        }

        return string.Empty;
    }

    private static double ConvertMillimetersToInches(double millimeters)
    {
        return millimeters / 25.4;
    }

    private static string FormatRainfallMillimetersAndInches(double millimeters)
    {
        return $"{millimeters:F1} mm ({ConvertMillimetersToInches(millimeters):F2} in)";
    }

    private static string GetRainIntensityLabel(WeatherAnalysisResult? analysis)
    {
        var peakDailyMm = analysis?.DailyRainfall?.Any() == true
            ? analysis.DailyRainfall!.Max(d => d.Total)
            : 0;

        if (peakDailyMm >= 30)
        {
            return "Torrential";
        }

        if (peakDailyMm >= 15)
        {
            return "Heavy";
        }

        if (peakDailyMm >= 5)
        {
            return "Moderate";
        }

        if (peakDailyMm > 0)
        {
            return "Light";
        }

        return "Dry";
    }

    private static string GetRainIntensityBadgeClass(WeatherAnalysisResult? analysis)
    {
        return GetRainIntensityLabel(analysis) switch
        {
            "Torrential" => "text-bg-primary",
            "Heavy" => "text-bg-info",
            "Moderate" => "text-bg-success",
            "Light" => "text-bg-secondary",
            _ => "text-bg-light text-dark border"
        };
    }

    private void NormalizeRainfallChartData()
    {
        if (rainfallAnalysis?.DailyRainfall == null)
        {
            return;
        }

        var normalized = rainfallAnalysis.DailyRainfall
            .GroupBy(d => DateOnly.FromDateTime(d.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new RainfallDataPoint
            {
                Timestamp = g.Min(x => x.Timestamp),
                Total = g.Sum(x => x.Total),
                MaxRate = g.Max(x => x.MaxRate)
            })
            .ToList();

        rainfallAnalysis.DailyRainfall = normalized;
        rainfallAnalysis.TotalRainfall = normalized.Sum(d => d.Total);
        rainfallAnalysis.RainyDaysCount = normalized.Count(d => d.Total > 0.1);
        rainfallAnalysis.AverageDailyRainfall = normalized.Any() ? normalized.Average(d => d.Total) : 0;
        rainfallAnalysis.MaxDailyRainfall = normalized.Any() ? normalized.Max(d => d.Total) : 0;

        var longestDryStreak = 0;
        var currentDryStreak = 0;
        foreach (var day in normalized)
        {
            if (day.Total <= 0.1)
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

        rainfallAnalysis.LongestDrySpellDays = longestDryStreak;
    }

    private static string FormatSeasonDateLabel(object value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("MM-dd");
        }

        return string.Empty;
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("MMM d, yyyy");
    }

    private static string FormatNullableDate(DateOnly? value)
    {
        return value.HasValue ? FormatDate(value.Value) : "n/a";
    }

    private static string FormatUtcDateTime(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm 'UTC'") : "n/a";
    }

    private static string GetPriorityBadgeClass(string priority)
    {
        if (priority.Equals("High", StringComparison.OrdinalIgnoreCase))
        {
            return "text-bg-danger";
        }

        if (priority.Equals("Medium", StringComparison.OrdinalIgnoreCase))
        {
            return "text-bg-warning";
        }

        return "text-bg-secondary";
    }

    private static string GetSeasonRegimeBadgeClass(string regime)
    {
        return regime switch
        {
            "False Spring Signal" => "text-bg-danger",
            "Spring Stabilizing" => "text-bg-success",
            "Cooling Pattern" => "text-bg-primary",
            "Warm Stable" => "text-bg-warning",
            "Insufficient Data" => "text-bg-secondary",
            _ => "text-bg-secondary"
        };
    }

    private class PlantReadinessCard
    {
        public string SeasonHeaderClass { get; set; } = string.Empty;
        public string SeasonLabel { get; set; } = string.Empty;
        public string SeasonEmoji { get; set; } = string.Empty;
        public string SeasonTone { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CategoriesLabel { get; set; } = string.Empty;
        public string ActionTypeLabel { get; set; } = string.Empty;
        public int RequiredConsecutiveNights { get; set; }
        public int CurrentNightStreak { get; set; }
        public double NightTempThresholdC { get; set; }
        public bool SupportsSuccessionPlanting { get; set; }
        public double NightTempThresholdF => ToFahrenheit(NightTempThresholdC);
        public string StreakOperatorLabel { get; set; } = string.Empty;
        public DateOnly? TargetTransplantDate { get; set; }
        public DateOnly? IndoorStartDate { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = string.Empty;
        public string RuleExplanation { get; set; } = string.Empty;
        public string TimingExplanation { get; set; } = string.Empty;
        public string Guidance { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    private class SeasonTrendPoint
    {
        public DateTime Date { get; set; }
        public double HighC { get; set; }
        public double LowC { get; set; }
        public double AverageC { get; set; }
        public double RollingAverageC { get; set; }
        public double WarmDayThresholdC { get; set; }
        public double ColdNightThresholdC { get; set; }
    }

    private class SeasonSignalPoint
    {
        public DateTime Date { get; set; }
        public double ValueC { get; set; }
    }

    private class SeasonForecastPoint
    {
        public DateTime Date { get; set; }
        public double ForecastHighC { get; set; }
        public double ForecastLowC { get; set; }
    }

    private class SeasonSignalToggleSettings
    {
        public bool ShowWarmSignalSeries { get; set; } = true;
        public bool ShowColdSignalSeries { get; set; } = true;
        public bool ShowSwingSignalSeries { get; set; } = true;
    }
}
