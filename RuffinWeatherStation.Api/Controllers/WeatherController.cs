using Microsoft.AspNetCore.Mvc;
using RuffinWeatherStation.Api.Models;
using RuffinWeatherStation.Api.Services;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RuffinWeatherStation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weatherService;

        public WeatherController(WeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        [HttpGet("latest")]
        public async Task<ActionResult<TemperatureMeasurement>> GetLatest()
        {
            var measurement = await _weatherService.GetLatestMeasurementAsync();
            if (measurement == null)
            {
                return NotFound();
            }
            return measurement;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<TemperatureMeasurement>>> GetRecent(
            [FromQuery] int count = 25,
            [FromQuery] DateTime? sinceUtc = null)
        {
            var measurements = await _weatherService.GetRecentMeasurementsAsync(count, sinceUtc);
            return measurements;
        }

        [HttpGet("hourly")]
        public async Task<ActionResult<IEnumerable<HourlyMeasurement>>> GetHourlyMeasurements([FromQuery] string startDate = null)
        {
            DateTime? date = null;
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedDate))
            {
                date = parsedDate;
            }
            
            var measurements = await _weatherService.GetHourlyMeasurementsAsync(date);
            return measurements;
        }

        [HttpGet("daily")]
        public async Task<ActionResult<IEnumerable<DailyMeasurement>>> GetDailyMeasurements(
            [FromQuery] string startDate = null,
            [FromQuery] bool rainOnly = false)
        {
            DateTime? date = null;
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedDate))
            {
                date = parsedDate;
            }
            
            var measurements = await _weatherService.GetDailyMeasurementsAsync(date, rainOnly);
            return measurements;
        }

        [HttpGet("prediction/latest")]
        public async Task<ActionResult<WeatherPrediction>> GetLatestPrediction()
        {
            var prediction = await _weatherService.GetLatestPredictionAsync();
            if (prediction == null)
            {
                return NotFound();
            }
            return prediction;
        }

        [HttpGet("prediction/recent")]
        public async Task<ActionResult<IEnumerable<WeatherPrediction>>> GetRecentPredictions([FromQuery] int count = 5)
        {
            var predictions = await _weatherService.GetRecentPredictionsAsync(count);
            return predictions;
        }

        [HttpGet("historical-daily")]
        public async Task<ActionResult<HistoricalDailyRecordResponse>> GetHistoricalDaily(
            [FromQuery] string date,
            [FromQuery] string? location = "backyard")
        {
            if (string.IsNullOrWhiteSpace(date) ||
                !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return BadRequest(new { message = "Invalid date format. Expected yyyy-MM-dd." });
            }

            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var record = await _weatherService.GetHistoricalDailyRecordAsync(parsedDate, normalizedLocation);
            return Ok(record);
        }

        [HttpGet("records/highlights")]
        public async Task<ActionResult<AllTimeRecordsResponse>> GetAllTimeHighlights([FromQuery] string? location = "backyard")
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var highlights = await _weatherService.GetAllTimeHighlightsAsync(normalizedLocation);
            return Ok(highlights);
        }
    }
}