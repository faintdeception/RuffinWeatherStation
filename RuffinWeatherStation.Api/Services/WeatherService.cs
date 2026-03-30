using MongoDB.Driver;
using MongoDB.Bson;
using RuffinWeatherStation.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RuffinWeatherStation.Api.Services
{
    public class WeatherService
    {
        private readonly IMongoCollection<TemperatureMeasurement> _measurements;
        private readonly IMongoCollection<HourlyMeasurement> _hourlyMeasurements;
        private readonly IMongoCollection<DailyMeasurement> _dailyMeasurements;
        private readonly IMongoCollection<HistoricalDailyRecord> _dailyDateRecords;
        private readonly IMongoCollection<AllTimeRecordDocument> _allTimeRecords;
        private readonly IMongoCollection<WeatherPrediction> _predictions;
        private readonly IMongoCollection<BsonDocument> _nwsSnapshots;
        private readonly NwsIconCacheService _nwsIconCacheService;

        public WeatherService(IConfiguration configuration, NwsIconCacheService nwsIconCacheService)
        {
            try
            {
                Console.WriteLine("[WEATHER SERVICE] Initializing WeatherService...");
            _nwsIconCacheService = nwsIconCacheService;
                
                var connectionString = configuration.GetConnectionString("MongoDb");
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("[WEATHER SERVICE ERROR] MongoDB connection string is null or empty!");
                    throw new InvalidOperationException("MongoDB connection string not found in configuration");
                }
                
                // Safely display part of the connection string for debugging
                if (connectionString.Length > 20)
                {
                    string maskedConnectionString = connectionString;
                    // If it contains a password, mask it
                    if (maskedConnectionString.Contains("@"))
                    {
                        var parts = maskedConnectionString.Split('@');
                        var credentialPart = parts[0];
                        var hostPart = parts[1];
                        
                        if (credentialPart.Contains(':'))
                        {
                            var userPass = credentialPart.Split(':');
                            var username = userPass[0];
                            maskedConnectionString = $"{username}:****@{hostPart}";
                        }
                    }
                    
                    Console.WriteLine($"[WEATHER SERVICE] Connection string found, begins with: {maskedConnectionString.Substring(0, 15)}...");
                    Console.WriteLine($"[WEATHER SERVICE] Connection string length: {connectionString}");
                }
                
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
                var dailyDateRecordsCollection = configuration.GetValue<string>("DatabaseSettings:Collections:DailyDateRecords") ?? "daily_date_records";
                var allTimeRecordsCollection = configuration.GetValue<string>("DatabaseSettings:Collections:Records") ?? "records";
                var predictionsCollection = configuration.GetValue<string>("DatabaseSettings:Collections:Predictions") ?? "weather_predictions";
                var nwsSnapshotsCollection = configuration.GetValue<string>("DatabaseSettings:Collections:NwsSnapshots") ?? "nws_snapshots";
                
                Console.WriteLine($"[WEATHER SERVICE] Collections: Measurements={measurementsCollection}, Hourly={hourlyCollection}, Daily={dailyCollection}, DailyDateRecords={dailyDateRecordsCollection}, Records={allTimeRecordsCollection}, Predictions={predictionsCollection}, NwsSnapshots={nwsSnapshotsCollection}");
                
                Console.WriteLine("[WEATHER SERVICE] Creating MongoDB client...");
                var client = new MongoClient(connectionString);
                
                Console.WriteLine("[WEATHER SERVICE] Getting database reference...");
                var database = client.GetDatabase(databaseName);
                
                Console.WriteLine("[WEATHER SERVICE] Accessing collections...");
                _measurements = database.GetCollection<TemperatureMeasurement>(measurementsCollection);
                _hourlyMeasurements = database.GetCollection<HourlyMeasurement>(hourlyCollection);
                _dailyMeasurements = database.GetCollection<DailyMeasurement>(dailyCollection);
                _dailyDateRecords = database.GetCollection<HistoricalDailyRecord>(dailyDateRecordsCollection);
                _allTimeRecords = database.GetCollection<AllTimeRecordDocument>(allTimeRecordsCollection);
                _predictions = database.GetCollection<WeatherPrediction>(predictionsCollection);
                _nwsSnapshots = database.GetCollection<BsonDocument>(nwsSnapshotsCollection);
                
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

        public async Task<List<TemperatureMeasurement>> GetRecentMeasurementsAsync(int count = 25, DateTime? sinceUtc = null)
        {
            var filter = sinceUtc.HasValue
                ? Builders<TemperatureMeasurement>.Filter.Gte(m => m.TimestampMs, sinceUtc.Value)
                : Builders<TemperatureMeasurement>.Filter.Empty;

            return await _measurements.Find(filter)
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
        
        public async Task<List<DailyMeasurement>> GetDailyMeasurementsAsync(DateTime? startDate = null, bool rainOnly = false)
        {
            // Keep unbounded requests from scanning legacy data that may have schema drift.
            var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-120);
            var filters = new List<FilterDefinition<DailyMeasurement>>();

            // Use explicit field paths because stored documents use snake_case keys.
            filters.Add(Builders<DailyMeasurement>.Filter.Gte("timestamp_ms", effectiveStartDate));

            if (rainOnly)
            {
                // Match the MongoDB query shape: { "fields.rain.sum": { $gt: 0 } }
                filters.Add(Builders<DailyMeasurement>.Filter.Gt("fields.rain.sum", 0));
            }

            var filter = Builders<DailyMeasurement>.Filter.And(filters);
            
            return await _dailyMeasurements.Find(filter)
                .Sort(Builders<DailyMeasurement>.Sort.Ascending("timestamp_ms"))
                .ToListAsync();
        }

        public async Task<HistoricalDailyRecordResponse> GetHistoricalDailyRecordAsync(DateTime date, string location)
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var monthDay = date.ToString("MM-dd");

            var filter = Builders<HistoricalDailyRecord>.Filter.And(
                Builders<HistoricalDailyRecord>.Filter.Eq(r => r.MonthDay, monthDay),
                Builders<HistoricalDailyRecord>.Filter.Eq(r => r.Location, normalizedLocation)
            );

            var record = await _dailyDateRecords.Find(filter).FirstOrDefaultAsync();

            if (record == null)
            {
                return new HistoricalDailyRecordResponse
                {
                    HasData = false,
                    RequestedDate = date.ToString("yyyy-MM-dd"),
                    MonthDay = monthDay,
                    Location = normalizedLocation
                };
            }

            return new HistoricalDailyRecordResponse
            {
                HasData = true,
                RequestedDate = date.ToString("yyyy-MM-dd"),
                MonthDay = record.MonthDay,
                Location = record.Location,
                High = record.High,
                Low = record.Low,
                UpdatedAt = record.UpdatedAt
            };
        }

        public async Task<AllTimeRecordsResponse> GetAllTimeHighlightsAsync(string location)
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var dailyRainfallFieldCandidates = new[] { "rain_daily_total", "daily_rainfall", "rainfall", "rain", "rain_sum", "precipitation" };
            var fields = new[] { "temperature", "wind_speed" }
                .Concat(dailyRainfallFieldCandidates)
                .ToArray();

            var filter = Builders<AllTimeRecordDocument>.Filter.And(
                Builders<AllTimeRecordDocument>.Filter.Eq(r => r.Location, normalizedLocation),
                Builders<AllTimeRecordDocument>.Filter.Eq(r => r.RecordType, "highest"),
                Builders<AllTimeRecordDocument>.Filter.In(r => r.Field, fields)
            );

            var records = await _allTimeRecords.Find(filter).ToListAsync();

            AllTimeRecordEntry? ToEntry(string field)
            {
                var record = records.FirstOrDefault(r => r.Field == field);
                if (record == null)
                {
                    return null;
                }

                return new AllTimeRecordEntry
                {
                    Field = record.Field,
                    Value = record.Value,
                    Date = record.Date,
                    Timestamp = record.Timestamp
                };
            }

            AllTimeRecordEntry? ToFirstAvailableEntry(IEnumerable<string> fieldCandidates)
            {
                foreach (var field in fieldCandidates)
                {
                    var entry = ToEntry(field);
                    if (entry != null)
                    {
                        return entry;
                    }
                }

                return null;
            }

            var temperature = ToEntry("temperature");
            var windSpeed = ToEntry("wind_speed");
            var dailyRainfall = ToFirstAvailableEntry(dailyRainfallFieldCandidates);

            return new AllTimeRecordsResponse
            {
                HasData = temperature != null || windSpeed != null || dailyRainfall != null,
                Location = normalizedLocation,
                Temperature = temperature,
                WindSpeed = windSpeed,
                DailyRainfall = dailyRainfall
            };
        }

        public async Task<WeatherPrediction> GetLatestPredictionAsync()
        {
            try
            {
                return await _predictions.Find(_ => true)
                    .SortByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEATHER SERVICE ERROR] Error fetching latest prediction: {ex.Message}");
                return null;
            }
        }

        public async Task<List<WeatherPrediction>> GetRecentPredictionsAsync(int count = 5)
        {
            try
            {
                return await _predictions.Find(_ => true)
                    .SortByDescending(p => p.CreatedAt)
                    .Limit(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEATHER SERVICE ERROR] Error fetching recent predictions: {ex.Message}");
                return new List<WeatherPrediction>();
            }
        }

        public async Task<NwsAlertSummaryResponse> GetNwsAlertSummaryAsync(int days = 7, string location = "backyard")
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var lookbackDays = Math.Clamp(days, 1, 30);
            var nowUtc = DateTime.UtcNow;
            var snapshotWindowStartUtc = nowUtc.AddHours(-36);

            try
            {
                var locationFilter = Builders<BsonDocument>.Filter.Eq("location", normalizedLocation);
                var locationSnapshots = await _nwsSnapshots
                    .Find(locationFilter)
                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                    .Limit(240)
                    .ToListAsync();

                var latestSnapshot = SelectLatestSnapshotInWindow(locationSnapshots, snapshotWindowStartUtc);

                if (latestSnapshot == null)
                {
                    var fallbackSnapshots = await _nwsSnapshots
                        .Find(Builders<BsonDocument>.Filter.Empty)
                        .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                        .Limit(240)
                        .ToListAsync();

                    latestSnapshot = SelectLatestSnapshotInWindow(fallbackSnapshots, snapshotWindowStartUtc);
                }

                if (latestSnapshot == null)
                {
                    return new NwsAlertSummaryResponse
                    {
                        Location = normalizedLocation,
                        LookbackDays = lookbackDays,
                        GeneratedAtUtc = DateTime.UtcNow,
                        TotalSnapshots = 0
                    };
                }

                var snapshotFetchedAt = GetDateTimeUtc(latestSnapshot, "created_at", "fetched_at", "timestamp", "nws_data.created_at", "nws_data.fetched_at");
                var snapshotLocation = NormalizeValue(GetString(latestSnapshot, "location", "nws_data.location")) ?? normalizedLocation;
                var snapshotReportDate = GetSnapshotReportDate(latestSnapshot);
                var usesPriorDaySnapshot = snapshotReportDate.HasValue && snapshotReportDate.Value < DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var (sunriseUtc, sunsetUtc) = GetDaylightWindowUtc(latestSnapshot);

                var alerts = new List<NwsAlertSnapshotSummary>();

                foreach (var alertDoc in ExpandAlertDocuments(latestSnapshot))
                {
                    var sent = GetDateTimeUtc(alertDoc, "sent", "timestamp", "created_at", "properties.sent", "properties.effective");
                    var onset = GetDateTimeUtc(alertDoc, "onset", "properties.onset", "effective", "properties.effective");
                    var expires = GetDateTimeUtc(alertDoc, "expires", "ends", "expiration", "properties.expires", "properties.ends");

                    var rawEvent = NormalizeValue(GetString(alertDoc, "event", "properties.event", "title"));
                    var headline = NormalizeValue(GetString(alertDoc, "headline", "properties.headline", "title")) ?? string.Empty;
                    var description = NormalizeValue(GetString(alertDoc, "description", "properties.description")) ?? string.Empty;
                    var instruction = NormalizeValue(GetString(alertDoc, "instruction", "properties.instruction")) ?? string.Empty;
                    var rawSeverity = NormalizeValue(GetString(alertDoc, "severity", "properties.severity"));
                    var urgency = NormalizeValue(GetString(alertDoc, "urgency", "properties.urgency")) ?? "Unknown";
                    var certainty = NormalizeValue(GetString(alertDoc, "certainty", "properties.certainty")) ?? "Unknown";
                    var alertId = NormalizeValue(GetString(alertDoc, "id", "properties.id", "alert_id")) ?? string.Empty;
                    var sourceUrl = NormalizeValue(GetString(alertDoc, "@id", "properties.@id", "properties.url", "url", "links.self", "links.web")) ?? string.Empty;

                    var eventName = rawEvent;
                    if (string.IsNullOrWhiteSpace(eventName) && !string.IsNullOrWhiteSpace(headline))
                    {
                        eventName = headline.Length > 80 ? headline[..80] + "..." : headline;
                    }

                    var severity = InferSeverity(rawSeverity, eventName, urgency, certainty);
                    var status = NormalizeValue(GetString(alertDoc, "status", "properties.status")) ?? string.Empty;

                    // Skip records that do not carry real alert content.
                    if (string.IsNullOrWhiteSpace(eventName) && string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(description))
                    {
                        continue;
                    }

                    var isActive = IsAlertActive(status, expires);

                    alerts.Add(new NwsAlertSnapshotSummary
                    {
                        AlertId = alertId,
                        Event = string.IsNullOrWhiteSpace(eventName) ? "Unspecified Event" : eventName,
                        Urgency = urgency,
                        Severity = string.IsNullOrWhiteSpace(severity) ? "Info" : severity,
                        Certainty = certainty,
                        Headline = headline,
                        Description = description,
                        Instruction = instruction,
                        SourceUrl = sourceUrl,
                        OnsetUtc = onset,
                        SentUtc = sent,
                        ExpiresUtc = expires,
                        IsActive = isActive
                    });
                }

                var severeLevels = new[] { "severe", "extreme" };
                var severeCount = alerts.Count(a => severeLevels.Contains(a.Severity, StringComparer.OrdinalIgnoreCase));
                var recommendations = BuildMitigationRecommendations(alerts);
                var isSnapshotExpired = alerts.Count > 0 && alerts.All(a => !a.IsActive);

                return new NwsAlertSummaryResponse
                {
                    Location = normalizedLocation,
                    LookbackDays = lookbackDays,
                    GeneratedAtUtc = DateTime.UtcNow,
                    TotalSnapshots = 1,
                    SnapshotFetchedAtUtc = snapshotFetchedAt,
                    IsSnapshotExpired = isSnapshotExpired,
                    ActiveAlerts = alerts.Count(a => a.IsActive),
                    ExpiredAlerts = alerts.Count(a => !a.IsActive),
                    SevereOrExtremeAlerts = severeCount,
                    ApproximateSunriseUtc = sunriseUtc,
                    ApproximateSunsetUtc = sunsetUtc,
                    DaylightSnapshotFetchedAtUtc = snapshotFetchedAt,
                    DaylightSnapshotLocation = snapshotLocation,
                    UsesPriorDaySnapshotForDaylight = usesPriorDaySnapshot,
                    RecentAlerts = alerts
                        .OrderByDescending(a => a.OnsetUtc ?? a.SentUtc ?? DateTime.MinValue)
                        .ToList(),
                    MitigationRecommendations = recommendations
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEATHER SERVICE ERROR] Error building NWS alert summary: {ex.Message}");
                return new NwsAlertSummaryResponse
                {
                    Location = normalizedLocation,
                    LookbackDays = lookbackDays,
                    GeneratedAtUtc = DateTime.UtcNow
                };
            }
        }

        public async Task<NwsForecastResponse> GetNwsForecastSummaryAsync(string location = "backyard", int maxPeriods = 12)
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "backyard" : location.Trim();
            var cappedPeriods = Math.Clamp(maxPeriods, 2, 20);
            var nowUtc = DateTime.UtcNow;
            var snapshotWindowStartUtc = nowUtc.AddHours(-48);

            try
            {
                var locationFilter = Builders<BsonDocument>.Filter.Eq("location", normalizedLocation);
                var locationSnapshots = await _nwsSnapshots
                    .Find(locationFilter)
                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                    .Limit(240)
                    .ToListAsync();

                var latestSnapshot = SelectLatestSnapshotInWindow(locationSnapshots, snapshotWindowStartUtc);

                if (latestSnapshot == null)
                {
                    var fallbackSnapshots = await _nwsSnapshots
                        .Find(Builders<BsonDocument>.Filter.Empty)
                        .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                        .Limit(240)
                        .ToListAsync();

                    latestSnapshot = SelectLatestSnapshotInWindow(fallbackSnapshots, snapshotWindowStartUtc);
                }

                if (latestSnapshot == null)
                {
                    return new NwsForecastResponse
                    {
                        Location = normalizedLocation,
                        GeneratedAtUtc = DateTime.UtcNow,
                        HasData = false
                    };
                }

                var snapshotFetchedAt = GetDateTimeUtc(latestSnapshot, "created_at", "fetched_at", "timestamp", "nws_data.created_at", "nws_data.fetched_at");

                var periodDocs = ExpandForecastPeriodDocuments(latestSnapshot).ToList();
                var periodTasks = periodDocs.Select(BuildForecastPeriodSummaryAsync).ToList();
                var periods = (await Task.WhenAll(periodTasks))
                    .Where(p => !string.IsNullOrWhiteSpace(p.ShortForecast) || !string.IsNullOrWhiteSpace(p.DetailedForecast) || p.StartTimeUtc.HasValue)
                    .OrderBy(p => p.StartTimeUtc ?? DateTime.MaxValue)
                    .Take(cappedPeriods)
                    .ToList();

                return new NwsForecastResponse
                {
                    Location = normalizedLocation,
                    GeneratedAtUtc = DateTime.UtcNow,
                    SnapshotFetchedAtUtc = snapshotFetchedAt,
                    HasData = periods.Count > 0,
                    Periods = periods
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEATHER SERVICE ERROR] Error building NWS forecast summary: {ex.Message}");
                return new NwsForecastResponse
                {
                    Location = normalizedLocation,
                    GeneratedAtUtc = DateTime.UtcNow,
                    HasData = false
                };
            }
        }

        private async Task<NwsForecastPeriodSummary> BuildForecastPeriodSummaryAsync(BsonDocument periodDoc)
        {
            var windSpeedText = NormalizeValue(GetString(periodDoc, "windSpeed", "wind_speed", "properties.windSpeed")) ?? string.Empty;
            var sourceTemperature = TryGetDouble(periodDoc, "temperature", "temp", "temperature.value", "properties.temperature");
            var sourceTemperatureUnit = NormalizeValue(GetString(periodDoc, "temperatureUnit", "temperature_unit", "temp_unit", "properties.temperatureUnit")) ?? "F";
            var sourceIconUrl = NormalizeValue(GetString(periodDoc, "icon", "iconUrl", "properties.icon"));

            return new NwsForecastPeriodSummary
            {
                Name = NormalizeValue(GetString(periodDoc, "name", "period_name", "title")) ?? "Forecast Period",
                StartTimeUtc = GetDateTimeUtc(periodDoc, "startTime", "start_time", "start", "properties.startTime"),
                EndTimeUtc = GetDateTimeUtc(periodDoc, "endTime", "end_time", "end", "properties.endTime"),
                IsDaytime = TryGetBool(periodDoc, "isDaytime", "is_daytime", "daytime", "properties.isDaytime"),
                Temperature = sourceTemperature,
                CelsiusTemperature = ConvertToCelsius(sourceTemperature, sourceTemperatureUnit),
                TemperatureUnit = sourceTemperatureUnit,
                WindSpeedText = windSpeedText,
                WindSpeedMphMax = ParseWindSpeedMaxMph(windSpeedText),
                WindDirection = NormalizeValue(GetString(periodDoc, "windDirection", "wind_direction", "properties.windDirection")) ?? string.Empty,
                PrecipitationChancePercent = TryGetDouble(periodDoc, "probabilityOfPrecipitation.value", "pop", "precipitationChance", "precipitation_probability", "properties.probabilityOfPrecipitation.value"),
                IconUrl = await _nwsIconCacheService.GetCachedIconUrlAsync(sourceIconUrl),
                ShortForecast = NormalizeValue(GetString(periodDoc, "shortForecast", "short_forecast", "summary", "properties.shortForecast")) ?? string.Empty,
                DetailedForecast = NormalizeValue(GetString(periodDoc, "detailedForecast", "detailed_forecast", "description", "properties.detailedForecast")) ?? string.Empty
            };
        }

        private static string? GetString(BsonDocument doc, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (TryGetBsonValue(doc, candidate, out var value) && value != null && !value.IsBsonNull)
                {
                    if (value.IsString)
                    {
                        return value.AsString;
                    }

                    if (value is BsonArray array)
                    {
                        var first = array.FirstOrDefault(v => v != null && !v.IsBsonNull && v.IsString);
                        if (first != null && first.IsString)
                        {
                            return first.AsString;
                        }
                    }

                    return value.ToString();
                }
            }

            return null;
        }

        private static DateTime? GetDateTimeUtc(BsonDocument doc, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!TryGetBsonValue(doc, candidate, out var value) || value == null || value.IsBsonNull)
                {
                    continue;
                }

                if (value.IsValidDateTime)
                {
                    return value.ToUniversalTime();
                }

                if ((value.IsInt64 || value.IsInt32 || value.IsDouble) && TryParseEpochToUtc(value, out var epochDate))
                {
                    return epochDate;
                }

                if (value.IsString && DateTimeOffset.TryParse(value.AsString, out var parsedOffset))
                {
                    return parsedOffset.UtcDateTime;
                }

                if (value.IsString && DateTime.TryParse(value.AsString, out var parsed))
                {
                    return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
                }
            }

            return null;
        }

        private static bool TryParseEpochToUtc(BsonValue value, out DateTime utcDate)
        {
            utcDate = DateTime.MinValue;

            double numeric = value.IsInt64 ? value.AsInt64 : value.IsInt32 ? value.AsInt32 : value.AsDouble;

            try
            {
                // Heuristic: values above 1e12 are milliseconds, otherwise seconds.
                utcDate = numeric > 1_000_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds((long)numeric).UtcDateTime
                    : DateTimeOffset.FromUnixTimeSeconds((long)numeric).UtcDateTime;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static BsonDocument? SelectLatestSnapshotInWindow(IEnumerable<BsonDocument> snapshots, DateTime windowStartUtc)
        {
            foreach (var snapshot in snapshots)
            {
                var fetchedAtUtc = GetDateTimeUtc(snapshot, "created_at", "fetched_at", "timestamp", "nws_data.created_at", "nws_data.fetched_at")
                    ?? GetObjectIdUtc(snapshot);

                if (!fetchedAtUtc.HasValue)
                {
                    continue;
                }

                if (fetchedAtUtc.Value >= windowStartUtc)
                {
                    return snapshot;
                }
            }

            return null;
        }

        private static DateTime? GetObjectIdUtc(BsonDocument snapshot)
        {
            if (!snapshot.TryGetValue("_id", out var idValue))
            {
                return null;
            }

            return idValue.IsObjectId ? idValue.AsObjectId.CreationTime.ToUniversalTime() : null;
        }

        private static (DateTime? SunriseUtc, DateTime? SunsetUtc) GetDaylightWindowUtc(BsonDocument snapshot)
        {
            var sunriseUtc = GetDateTimeUtc(snapshot,
                "nws_data.forecast.sunrise",
                "forecast.sunrise",
                "sunrise");

            var sunsetUtc = GetDateTimeUtc(snapshot,
                "nws_data.forecast.sunset",
                "forecast.sunset",
                "sunset");

            return (sunriseUtc, sunsetUtc);
        }

        private static DateOnly? GetSnapshotReportDate(BsonDocument snapshot)
        {
            var reportDateRaw = NormalizeValue(GetString(snapshot, "report_date", "nws_data.report_date"));
            if (string.IsNullOrWhiteSpace(reportDateRaw))
            {
                return null;
            }

            return DateOnly.TryParse(reportDateRaw, out var reportDate) ? reportDate : null;
        }

        private static IEnumerable<BsonDocument> ExpandAlertDocuments(BsonDocument snapshot)
        {
            yield return snapshot;

            if (snapshot.TryGetValue("features", out var featuresValue) && featuresValue is BsonArray features)
            {
                foreach (var feature in features.OfType<BsonDocument>())
                {
                    yield return feature;
                }
            }

            if (snapshot.TryGetValue("alerts", out var alertsValue) && alertsValue is BsonArray alerts)
            {
                foreach (var alert in alerts.OfType<BsonDocument>())
                {
                    yield return alert;
                }
            }

            // Support persisted snapshots that wrap NWS payload under `nws_data`.
            if (TryGetBsonValue(snapshot, "nws_data.features", out var nestedFeaturesValue) && nestedFeaturesValue is BsonArray nestedFeatures)
            {
                foreach (var feature in nestedFeatures.OfType<BsonDocument>())
                {
                    yield return feature;
                }
            }

            if (TryGetBsonValue(snapshot, "nws_data.alerts", out var nestedAlertsValue) && nestedAlertsValue is BsonArray nestedAlerts)
            {
                foreach (var alert in nestedAlerts.OfType<BsonDocument>())
                {
                    yield return alert;
                }
            }
        }

        private static IEnumerable<BsonDocument> ExpandForecastPeriodDocuments(BsonDocument snapshot)
        {
            IEnumerable<BsonDocument> FromPath(string path)
            {
                if (TryGetBsonValue(snapshot, path, out var value) && value is BsonArray periods)
                {
                    return periods.OfType<BsonDocument>();
                }

                return Enumerable.Empty<BsonDocument>();
            }

            return FromPath("forecast.periods")
                .Concat(FromPath("nws_data.forecast.periods"))
                .Concat(FromPath("periods"))
                .Concat(FromPath("nws_data.periods"));
        }

        private static bool TryGetBool(BsonDocument doc, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!TryGetBsonValue(doc, candidate, out var value) || value == null || value.IsBsonNull)
                {
                    continue;
                }

                if (value.IsBoolean)
                {
                    return value.AsBoolean;
                }

                if (value.IsString && bool.TryParse(value.AsString, out var parsedBool))
                {
                    return parsedBool;
                }
            }

            return false;
        }

        private static double? TryGetDouble(BsonDocument doc, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!TryGetBsonValue(doc, candidate, out var value) || value == null || value.IsBsonNull)
                {
                    continue;
                }

                if (value.IsDouble)
                {
                    return value.AsDouble;
                }

                if (value.IsInt32)
                {
                    return value.AsInt32;
                }

                if (value.IsInt64)
                {
                    return value.AsInt64;
                }

                if (value.IsDecimal128)
                {
                    return (double)value.AsDecimal128;
                }

                if (value.IsString && double.TryParse(value.AsString, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static double? ParseWindSpeedMaxMph(string windSpeedText)
        {
            if (string.IsNullOrWhiteSpace(windSpeedText))
            {
                return null;
            }

            var numericMatches = Regex.Matches(windSpeedText, "\\d+(\\.\\d+)?");
            if (numericMatches.Count == 0)
            {
                return null;
            }

            var values = numericMatches
                .Select(m => double.TryParse(m.Value, out var parsed) ? parsed : double.NaN)
                .Where(v => !double.IsNaN(v))
                .ToList();

            if (values.Count == 0)
            {
                return null;
            }

            return values.Max();
        }

        private static double? ConvertToCelsius(double? temperature, string? unit)
        {
            if (!temperature.HasValue)
            {
                return null;
            }

            if (string.Equals(unit, "C", StringComparison.OrdinalIgnoreCase))
            {
                return temperature.Value;
            }

            if (string.Equals(unit, "F", StringComparison.OrdinalIgnoreCase))
            {
                return (temperature.Value - 32) * 5 / 9;
            }

            // If unit is unknown, preserve value as-is to avoid dropping data.
            return temperature.Value;
        }

        private static string? NormalizeValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            if (normalized.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("n/a", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized;
        }

        private static string? InferSeverity(string? severity, string? eventName, string? urgency, string? certainty)
        {
            if (!string.IsNullOrWhiteSpace(severity))
            {
                return severity;
            }

            var eventLower = (eventName ?? string.Empty).ToLowerInvariant();
            if (eventLower.Contains("warning"))
            {
                return "Severe";
            }

            if (eventLower.Contains("watch"))
            {
                return "Moderate";
            }

            if (eventLower.Contains("advisory"))
            {
                return "Minor";
            }

            if (string.Equals(urgency, "Immediate", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(certainty, "Observed", StringComparison.OrdinalIgnoreCase))
            {
                return "Severe";
            }

            return null;
        }

        private static bool IsAlertActive(string status, DateTime? expiresUtc)
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();

            if (normalizedStatus is "expired" or "cancelled" or "canceled")
            {
                return false;
            }

            if (normalizedStatus is "active" or "actual")
            {
                return true;
            }

            // Without explicit status, require a valid future expiry to consider the alert active.
            return expiresUtc.HasValue && expiresUtc.Value >= DateTime.UtcNow;
        }

        private static List<NwsMitigationRecommendation> BuildMitigationRecommendations(List<NwsAlertSnapshotSummary> alerts)
        {
            var activeAlerts = alerts.Where(a => a.IsActive).ToList();
            var recommendations = new List<NwsMitigationRecommendation>();

            if (activeAlerts.Count == 0)
            {
                return recommendations;
            }

            void AddRecommendation(string category, string priority, string guidance)
            {
                if (recommendations.Any(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                                             r.Guidance.Equals(guidance, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                recommendations.Add(new NwsMitigationRecommendation
                {
                    Category = category,
                    Priority = priority,
                    Guidance = guidance
                });
            }

            foreach (var alert in activeAlerts)
            {
                var text = $"{alert.Event} {alert.Headline} {alert.Description}".ToLowerInvariant();
                var severe = alert.Severity.Equals("severe", StringComparison.OrdinalIgnoreCase) ||
                             alert.Severity.Equals("extreme", StringComparison.OrdinalIgnoreCase);

                if (text.Contains("wildfire") || text.Contains("red flag") || text.Contains("smoke"))
                {
                    AddRecommendation("Wildfire", severe ? "High" : "Medium", "Move flammables away from structures, stage hoses, and keep an evacuation-ready tool kit by exits.");
                    AddRecommendation("Wildfire", severe ? "High" : "Medium", "Pause outdoor burning and mowing during peak wind periods; water vulnerable beds early in the day.");
                }

                if (text.Contains("freeze") || text.Contains("frost"))
                {
                    AddRecommendation("Freeze", "High", "Cover sensitive plants before sunset and disconnect/protect exposed hose bibs and irrigation lines.");
                }

                if (text.Contains("wind") || text.Contains("gale") || text.Contains("thunderstorm"))
                {
                    AddRecommendation("Wind/Storm", severe ? "High" : "Medium", "Secure lightweight pots, trellises, and garden furniture; delay foliar sprays ahead of gusty periods.");
                }

                if (text.Contains("flood") || text.Contains("flash") || text.Contains("heavy rain"))
                {
                    AddRecommendation("Flooding", "High", "Clear drainage paths and avoid watering until soil infiltration recovers to reduce root stress.");
                }

                if (text.Contains("heat") || text.Contains("excessive heat"))
                {
                    AddRecommendation("Heat", severe ? "High" : "Medium", "Prioritize deep early-morning watering, add temporary shade cloth, and postpone transplanting in peak heat windows.");
                }
            }

            if (recommendations.Count == 0)
            {
                AddRecommendation("General", "Medium", "Review active alerts and postpone non-essential garden work until hazards clear.");
            }

            return recommendations.Take(6).ToList();
        }

        private static bool TryGetBsonValue(BsonDocument doc, string path, out BsonValue value)
        {
            value = BsonNull.Value;
            var current = (BsonValue)doc;

            foreach (var segment in path.Split('.'))
            {
                if (current is BsonDocument currentDoc && currentDoc.TryGetValue(segment, out var next))
                {
                    current = next;
                }
                else
                {
                    return false;
                }
            }

            value = current;
            return true;
        }
    }
}