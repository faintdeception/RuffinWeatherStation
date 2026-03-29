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
}
