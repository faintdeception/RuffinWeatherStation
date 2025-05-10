using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace RuffinWeatherStation.Api.Models
{
    public class TemperatureMeasurement
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        // Keeping this for backward compatibility with older documents
        [BsonElement("timestamp")]
        public long? TimestampNanoseconds { get; set; }

        // Use the new date-formatted timestamp
        [BsonElement("timestamp_ms")]
        public DateTime TimestampMs { get; set; }

        [BsonIgnore]
        public DateTime Timestamp 
        { 
            get 
            {
                // Prefer the new timestamp_ms field if available
                return TimestampMs.ToLocalTime();
            }
        }

        [BsonElement("fields")]
        public WeatherFields Fields { get; set; }
        
        [BsonElement("tags")]
        public WeatherTags Tags { get; set; }
    }

    public class WeatherFields
    {
        [BsonElement("device_temperature")]
        public double DeviceTemperature { get; set; }

        [BsonElement("temperature")]
        public double Temperature { get; set; }

        [BsonElement("humidity")]
        public double Humidity { get; set; }

        [BsonElement("dewpoint")]
        public double Dewpoint { get; set; }

        [BsonElement("lux")]
        public double Lux { get; set; }

        [BsonElement("pressure")]
        public double Pressure { get; set; }

        [BsonElement("wind_speed")]
        public double WindSpeed { get; set; }

        [BsonElement("rain")]
        public double Rain { get; set; }

        [BsonElement("wind_direction")]
        public double WindDirection { get; set; }  // Changed from int to double

        [BsonElement("wind_direction_cardinal")]
        public string WindDirectionCardinal { get; set; }
    }

    public class WeatherTags
    {
        [BsonElement("location")]
        public string Location { get; set; }

        [BsonElement("sensor_type")]
        public string SensorType { get; set; }
    }
}