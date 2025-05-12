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
            var connectionString = configuration.GetConnectionString("MongoDb");
            var databaseName = configuration.GetValue<string>("DatabaseSettings:DatabaseName");
            
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            
            _measurements = database.GetCollection<TemperatureMeasurement>(
                configuration.GetValue<string>("DatabaseSettings:Collections:Measurements"));
            _hourlyMeasurements = database.GetCollection<HourlyMeasurement>(
                configuration.GetValue<string>("DatabaseSettings:Collections:HourlyMeasurements"));
            _dailyMeasurements = database.GetCollection<DailyMeasurement>(
                configuration.GetValue<string>("DatabaseSettings:Collections:DailyMeasurements"));
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