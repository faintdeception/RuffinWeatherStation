using MongoDB.Driver;
using RuffinWeatherStation.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RuffinWeatherStation.Api.Services
{
    public class WeatherService
    {
        private readonly IMongoCollection<TemperatureMeasurement> _measurements;
        private readonly IMongoCollection<HourlyMeasurement> _hourlyMeasurements;
        private readonly IMongoCollection<DailyMeasurement> _dailyMeasurements;

        public WeatherService(IConfiguration configuration)
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("weather_data");
            _measurements = database.GetCollection<TemperatureMeasurement>("measurements");
            _hourlyMeasurements = database.GetCollection<HourlyMeasurement>("hourly_measurements");
            _dailyMeasurements = database.GetCollection<DailyMeasurement>("daily_measurements");
        }

        public async Task<TemperatureMeasurement> GetLatestMeasurementAsync()
        {
            return await _measurements.Find(_ => true)
                .SortByDescending(m => m.TimestampMs)
                .FirstOrDefaultAsync();
        }

        public async Task<List<TemperatureMeasurement>> GetRecentMeasurementsAsync(int count = 25)
        {
            return await _measurements.Find(_ => true)
                .SortByDescending(m => m.TimestampMs)
                .Limit(count)
                .ToListAsync();
        }
        
        public async Task<List<HourlyMeasurement>> GetHourlyMeasurementsAsync(DateTime? startDate = null)
        {
            var filter = Builders<HourlyMeasurement>.Filter.Empty;
            
            if (startDate.HasValue)
            {
                // Filter for measurements on or after the start date
                filter = Builders<HourlyMeasurement>.Filter.Gte(m => m.TimestampMs, startDate.Value);
            }
            
            return await _hourlyMeasurements.Find(filter)
                .SortBy(m => m.TimestampMs)
                .ToListAsync();
        }
        
        public async Task<List<DailyMeasurement>> GetDailyMeasurementsAsync(DateTime? startDate = null)
        {
            var filter = Builders<DailyMeasurement>.Filter.Empty;
            
            if (startDate.HasValue)
            {
                // Filter for measurements on or after the start date
                filter = Builders<DailyMeasurement>.Filter.Gte(m => m.TimestampMs, startDate.Value);
            }
            
            return await _dailyMeasurements.Find(filter)
                .SortBy(m => m.TimestampMs)
                .ToListAsync();
        }
    }
}