using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RuffinWeatherStation.Api.Models;
using RuffinWeatherStation.Api.Services;
using System.Globalization;

namespace RuffinWeatherStation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GardenController : ControllerBase
{
    private static readonly string FrostDateFormat = "MM-dd";
    private readonly GardenSettings _gardenSettings;
    private readonly WeatherService _weatherService;
    private readonly NwsIconCacheService _iconCacheService;

    public GardenController(
        IOptions<GardenSettings> gardenSettings,
        WeatherService weatherService,
        NwsIconCacheService iconCacheService)
    {
        _gardenSettings = gardenSettings.Value;
        _weatherService = weatherService;
        _iconCacheService = iconCacheService;
    }

    [HttpGet("reference")]
    public ActionResult<GardenReferenceResponse> GetReference([FromQuery] int? year = null)
    {
        var targetYear = year.GetValueOrDefault(DateTime.UtcNow.Year);
        if (targetYear < 1900 || targetYear > 3000)
        {
            return BadRequest(new { message = "Year must be between 1900 and 3000." });
        }

        var monthDay = string.IsNullOrWhiteSpace(_gardenSettings.AverageLastFrostMonthDay)
            ? "04-15"
            : _gardenSettings.AverageLastFrostMonthDay.Trim();

        var averageLastFrostDate = ResolveAverageLastFrostDate(monthDay, targetYear);

        var response = new GardenReferenceResponse
        {
            Year = targetYear,
            LocationLabel = string.IsNullOrWhiteSpace(_gardenSettings.LocationLabel) ? "backyard" : _gardenSettings.LocationLabel.Trim(),
            AverageLastFrostMonthDay = monthDay,
            AverageLastFrostDate = averageLastFrostDate,
            SeasonStarts = new GardenSeasonStartDates
            {
                Spring = new DateOnly(targetYear, 3, 20),
                Summer = new DateOnly(targetYear, 6, 21),
                Fall = new DateOnly(targetYear, 9, 22),
                Winter = new DateOnly(targetYear, 12, 21)
            },
            GeneratedAtUtc = DateTime.UtcNow
        };

        return Ok(response);
    }

    [HttpGet("alerts-summary")]
    public async Task<ActionResult<NwsAlertSummaryResponse>> GetAlertsSummary(
        [FromQuery] int days = 7,
        [FromQuery] string? location = null)
    {
        var fallbackLocation = string.IsNullOrWhiteSpace(_gardenSettings.LocationLabel) ? "backyard" : _gardenSettings.LocationLabel.Trim();
        var summary = await _weatherService.GetNwsAlertSummaryAsync(days, location ?? fallbackLocation);
        return Ok(summary);
    }

    [HttpGet("forecast-summary")]
    public async Task<ActionResult<NwsForecastResponse>> GetForecastSummary(
        [FromQuery] int maxPeriods = 12,
        [FromQuery] string? location = null)
    {
        var fallbackLocation = string.IsNullOrWhiteSpace(_gardenSettings.LocationLabel) ? "backyard" : _gardenSettings.LocationLabel.Trim();
        var summary = await _weatherService.GetNwsForecastSummaryAsync(location ?? fallbackLocation, maxPeriods);
        return Ok(summary);
    }

    [HttpGet("icon-cache/{fileName}")]
    public IActionResult GetCachedIcon([FromRoute] string fileName)
    {
        var filePath = _iconCacheService.ResolveCachedIconFilePath(fileName);
        if (filePath == null)
        {
            return NotFound();
        }

        var contentType = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                    ? "image/gif"
                    : fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                        ? "image/webp"
                        : fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                            ? "image/svg+xml"
                            : "application/octet-stream";

        return PhysicalFile(filePath, contentType);
    }

    private static DateOnly ResolveAverageLastFrostDate(string monthDay, int year)
    {
        if (DateTime.TryParseExact(
                monthDay,
                FrostDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return new DateOnly(year, parsed.Month, parsed.Day);
        }

        return new DateOnly(year, 4, 15);
    }
}
