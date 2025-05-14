using Microsoft.AspNetCore.Mvc;
using RuffinWeatherStation.Api.Models;
using RuffinWeatherStation.Api.Services;
using System;
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
        public async Task<ActionResult<IEnumerable<TemperatureMeasurement>>> GetRecent([FromQuery] int count = 25)
        {
            var measurements = await _weatherService.GetRecentMeasurementsAsync(count);
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
        public async Task<ActionResult<IEnumerable<DailyMeasurement>>> GetDailyMeasurements([FromQuery] string startDate = null)
        {
            DateTime? date = null;
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedDate))
            {
                date = parsedDate;
            }
            
            var measurements = await _weatherService.GetDailyMeasurementsAsync(date);
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
    }
}