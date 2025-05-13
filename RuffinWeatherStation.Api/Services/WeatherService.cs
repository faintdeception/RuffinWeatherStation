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
            try
            {
                Console.WriteLine("[WEATHER SERVICE] Initializing WeatherService...");
                
                var connectionString = configuration.GetConnectionString("MongoDb");
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("[WEATHER SERVICE ERROR] MongoDB connection string is null or empty!");
                    throw new InvalidOperationException("MongoDB connection string not found in configuration");
                }
                
                Console.WriteLine($"[WEATHER SERVICE] Connection string found with length: {connectionString.Length}");
                
                var databaseName = configuration.GetValue<string>("DatabaseSettings:DatabaseName");
                if (string.IsNullOrEmpty(databaseName))
                {
                    Console.WriteLine("[WEATHER SERVICE ERROR] DatabaseName is null or empty!");
                    throw new InvalidOperationException("DatabaseSettings:DatabaseName not found in configuration");
                }
                
                Console.WriteLine($"[WEATHER SERVICE] DatabaseName: {databaseName}");
                
                // Log collection names for debugging
                var measurementsCollection = configuration.GetValue<string>("DatabaseSettings:Collections:Measurements");
                var hourlyCollection = configuration.GetValue<string>("DatabaseSettings:Collections:HourlyMeasurements");
                var dailyCollection = configuration.GetValue<string>("DatabaseSettings:Collections:DailyMeasurements");
                
                Console.WriteLine($"[WEATHER SERVICE] Collections: Measurements={measurementsCollection}, Hourly={hourlyCollection}, Daily={dailyCollection}");
                
                Console.WriteLine("[WEATHER SERVICE] Creating MongoDB client...");
                var client = new MongoClient(connectionString);
                
                Console.WriteLine("[WEATHER SERVICE] Getting database reference...");
                var database = client.GetDatabase(databaseName);
                
                Console.WriteLine("[WEATHER SERVICE] Accessing collections...");
                _measurements = database.GetCollection<TemperatureMeasurement>(measurementsCollection);
                _hourlyMeasurements = database.GetCollection<HourlyMeasurement>(hourlyCollection);
                _dailyMeasurements = database.GetCollection<DailyMeasurement>(dailyCollection);
                
                Console.WriteLine("[WEATHER SERVICE] Successfully initialized WeatherService");
                
                // Simple ping test to validate connection
                try
                {
                    Console.WriteLine("[WEATHER SERVICE] Testing database connection with ping...");
                    var ping = database.RunCommand<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
                    Console.WriteLine("[WEATHER SERVICE] Ping successful, connection established");
                }
                catch (Exception pingEx)
                {
                    Console.WriteLine($"[WEATHER SERVICE ERROR] Database ping failed: {pingEx.Message}");
                    throw new Exception("MongoDB connection test failed", pingEx);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEATHER SERVICE ERROR] Error initializing WeatherService: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw; // Re-throw to properly fail application startup
            }
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