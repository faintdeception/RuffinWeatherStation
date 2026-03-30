using Microsoft.AspNetCore.Components;
using RuffinWeatherStation.Models;
using RuffinWeatherStation.Services;
using RuffinWeatherStation.Utilities;

namespace RuffinWeatherStation.Pages;

public partial class WeatherHome
{
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
                    .Where(WeatherDisplayHelpers.IsEyebrowRaisingAlert)
                    .OrderByDescending(a => WeatherDisplayHelpers.GetSeverityRank(a.Severity))
                    .ThenByDescending(a => a.OnsetUtc ?? a.SentUtc ?? DateTime.MinValue)
                    .Take(4)
                    .ToList();
            }

            UpdateDaylightCardData();
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

    private async Task LoadHomeForecastSummary()
    {
        try
        {
            isForecastLoading = true;
            forecastLoadError = null;
            homeForecastSummary = await GardenDataService.GetForecastSummaryAsync(maxPeriods: 4);
        }
        catch (Exception ex)
        {
            forecastLoadError = $"Unable to load NWS forecast summary: {ex.Message}";
            Console.Error.WriteLine(forecastLoadError);
        }
        finally
        {
            isForecastLoading = false;
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

                    UpdateDaylightCardData();

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

    private void UpdateDaylightCardData()
    {
        todayLightLevels = todaysMeasurements?
            .Where(m => m.Fields != null)
            .GroupBy(m => m.TimestampMs)
            .OrderBy(g => g.Key)
            .Select(g => new DataPoint
            {
                Timestamp = g.Key,
                Value = g.Average(m => m.Fields?.Lux ?? 0)
            })
            .ToList() ?? new List<DataPoint>();

        approximateSunriseUtc = homeAlertSummary?.ApproximateSunriseUtc;
        approximateSunsetUtc = homeAlertSummary?.ApproximateSunsetUtc;
        daylightSnapshotFetchedAtUtc = homeAlertSummary?.DaylightSnapshotFetchedAtUtc;
        daylightSnapshotLocation = homeAlertSummary?.DaylightSnapshotLocation ?? string.Empty;
        usesPriorDaySnapshotForDaylight = homeAlertSummary?.UsesPriorDaySnapshotForDaylight ?? false;
    }

    private async Task LoadRecentRainfallAnalysis()
    {
        try
        {
            recentRainfallErrorMessage = null;
            recentRainfallAnalysis = await TemperatureService.GetAnalysisAsync(recentRainfallDays, rainOnly: true);

            if (recentRainfallAnalysis?.DailyRainfall != null)
            {
                // Normalize to one point per displayed day so chart rendering stays deterministic.
                var normalizedDailyRainfall = recentRainfallAnalysis.DailyRainfall
                    .GroupBy(d => DateOnly.FromDateTime(ConvertToUserTimeZone(d.Timestamp)))
                    .OrderBy(g => g.Key)
                    .Select(g => new RainfallDataPoint
                    {
                        Timestamp = g.Min(x => x.Timestamp),
                        Total = g.Sum(x => x.Total),
                        MaxRate = g.Max(x => x.MaxRate)
                    })
                    .ToList();

                recentRainfallAnalysis.DailyRainfall = normalizedDailyRainfall;
                recentRainfallAnalysis.TotalRainfall = normalizedDailyRainfall.Sum(d => d.Total);
                recentRainfallAnalysis.RainyDaysCount = normalizedDailyRainfall.Count(d => d.Total > 0.1);
                recentRainfallAnalysis.AverageDailyRainfall = normalizedDailyRainfall.Any()
                    ? normalizedDailyRainfall.Average(d => d.Total)
                    : 0;
                recentRainfallAnalysis.MaxDailyRainfall = normalizedDailyRainfall.Any()
                    ? normalizedDailyRainfall.Max(d => d.Total)
                    : 0;

                int currentDryStreak = 0;
                int longestDryStreak = 0;
                foreach (var day in normalizedDailyRainfall)
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

                recentRainfallAnalysis.LongestDrySpellDays = longestDryStreak;
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
}
