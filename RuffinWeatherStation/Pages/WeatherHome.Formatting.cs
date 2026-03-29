using Microsoft.JSInterop;
using RuffinWeatherStation.Utilities;

namespace RuffinWeatherStation.Pages;

public partial class WeatherHome
{
    // Data is considered fresh within 1 hour.
    private readonly int DATA_FRESH_MINUTES = 60;

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

    private DateTime? ConvertToUserTimeZoneNullable(DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue)
        {
            return null;
        }

        return ConvertToUserTimeZone(utcDateTime.Value);
    }

    private bool IsDataStale()
    {
        if (measurement == null)
            return true;

        var dataAge = DateTime.Now - measurement.TimestampMs;
        return dataAge.TotalMinutes > DATA_FRESH_MINUTES;
    }

    private bool IsDeviceOffline()
    {
        if (measurement == null)
            return true;

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
            var localDateTime = ConvertToUserTimeZone(dt);

            if (showShortTermAnalysis)
            {
                return localDateTime.ToString("HH:mm");
            }

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

    private string FormatLuxAxisValue(object value)
    {
        if (value is double lux)
        {
            if (lux >= 1000)
            {
                return $"{lux / 1000:0.#}k";
            }

            return lux.ToString("0");
        }

        return value?.ToString() ?? "";
    }

    private string FormatLuxValue(double value)
    {
        if (value >= 1000)
        {
            return $"{value:0,0}";
        }

        return value.ToString("0");
    }

    private string FormatDaylightTime(DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue)
        {
            return "n/a";
        }

        return ConvertToUserTimeZone(utcDateTime.Value).ToString("h:mm tt");
    }

    private string BuildDaylightSnapshotLabel()
    {
        if (!daylightSnapshotFetchedAtUtc.HasValue)
        {
            return "No recent NWS daylight snapshot available (36-hour window).";
        }

        var sourceLocation = string.IsNullOrWhiteSpace(daylightSnapshotLocation) ? "backyard" : daylightSnapshotLocation;
        var timingLabel = usesPriorDaySnapshotForDaylight ? "Prior-day snapshot" : "Latest snapshot";
        return $"{timingLabel} from {sourceLocation} at {FormatDateTimeForUserWithTimezone(daylightSnapshotFetchedAtUtc.Value)}";
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

    private static string GetHomeSeverityBadgeClass(string severity)
    {
        return WeatherDisplayHelpers.GetHomeSeverityBadgeClass(severity);
    }

    private static double ConvertMillimetersToInches(double millimeters)
    {
        return millimeters / 25.4;
    }

    private string FormatRainfallMillimetersAndInches(double millimeters)
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
}
