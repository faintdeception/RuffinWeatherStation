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

    public GardenController(IOptions<GardenSettings> gardenSettings, WeatherService weatherService)
    {
        _gardenSettings = gardenSettings.Value;
        _weatherService = weatherService;
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
