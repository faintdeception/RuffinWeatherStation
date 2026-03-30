using System.Globalization;
using RuffinWeatherStation.Models;

namespace RuffinWeatherStation.Utilities;

public static class WeatherDisplayHelpers
{
    public static bool IsEyebrowRaisingAlert(NwsAlertSnapshotData alert)
    {
        if (!alert.IsActive)
        {
            return false;
        }

        return GetUrgencyRank(alert.Urgency) >= 2 &&
               GetSeverityRank(alert.Severity) >= 2 &&
               GetCertaintyRank(alert.Certainty) >= 1;
    }

    public static int GetUrgencyRank(string urgency)
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

    public static int GetSeverityRank(string severity)
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

    public static int GetCertaintyRank(string certainty)
    {
        return certainty.Trim().ToLowerInvariant() switch
        {
            "observed" => 3,
            "likely" => 2,
            "possible" => 1,
            _ => 0
        };
    }

    public static string GetHomeSeverityBadgeClass(string severity)
    {
        return GetSeverityRank(severity) switch
        {
            4 => "text-bg-dark",
            3 => "text-bg-danger",
            2 => "text-bg-warning",
            _ => "text-bg-secondary"
        };
    }

    public static double GetLatestWindSpeed(double? windSpeed)
    {
        return Math.Max(windSpeed ?? 0, 0);
    }

    public static double NormalizeWindDirection(double rawDirection)
    {
        var normalized = rawDirection % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    public static string GetWindDirectionCardinalLabel(string? currentCardinal, double normalizedDirection)
    {
        if (!string.IsNullOrWhiteSpace(currentCardinal))
            return currentCardinal;

        string[] cardinalDirections = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
        var index = (int)Math.Round(normalizedDirection / 22.5, MidpointRounding.AwayFromZero) % cardinalDirections.Length;
        return cardinalDirections[index];
    }

    public static string GetWindRosePetalPoints(double speed)
    {
        const double center = 110;
        const double minRadius = 20;
        const double maxRadius = 90;
        const double referenceSpeed = 25; // m/s cap for petal scaling

        // Ease-out scaling creates stronger visual separation for light-to-moderate gusts.
        var normalizedSpeed = Math.Clamp(speed / referenceSpeed, 0, 1);
        normalizedSpeed = Math.Pow(normalizedSpeed, 0.75);
        var radius = minRadius + (normalizedSpeed * (maxRadius - minRadius));
        var shoulderOffset = 9 + (normalizedSpeed * 6);
        var shoulderY = center - radius + 14;
        var tipY = center - radius;

        return string.Create(
            provider: CultureInfo.InvariantCulture,
            $"{center:0.0},{center:0.0} {center - shoulderOffset:0.0},{shoulderY:0.0} {center:0.0},{tipY:0.0} {center + shoulderOffset:0.0},{shoulderY:0.0}");
    }

    public static string GetWindIntensityClass(double speed)
    {
        return speed switch
        {
            < 2 => "wind-intensity-calm",
            < 5 => "wind-intensity-breezy",
            < 9 => "wind-intensity-fresh",
            < 14 => "wind-intensity-strong",
            _ => "wind-intensity-severe"
        };
    }

    public static double GetWindPetalStrokeWidth(double speed)
    {
        if (speed < 2)
        {
            return 1.0;
        }

        if (speed < 5)
        {
            return 1.3;
        }

        if (speed < 9)
        {
            return 1.7;
        }

        if (speed < 14)
        {
            return 2.1;
        }

        return 2.5;
    }
}
