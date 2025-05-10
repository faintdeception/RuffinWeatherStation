using Microsoft.AspNetCore.Mvc;
using RuffinWeatherStation.Api.Models;
using RuffinWeatherStation.Api.Services;
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
    }
}