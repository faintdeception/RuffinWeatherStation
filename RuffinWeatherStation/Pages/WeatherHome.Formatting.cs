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
}
