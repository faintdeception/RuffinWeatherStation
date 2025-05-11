using System;
using System.ComponentModel.DataAnnotations;

namespace RuffinWeatherStation.Api.Models
{
    public class WeatherNote
    {
        public int Id { get; set; }
        
        [Required]
        public DateTime Date { get; set; }
        
        [Required]
        public required string Content { get; set; }
        
        public required string UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class WeatherNoteRequest
    {
        [Required]
        public DateTime Date { get; set; }
        
        [Required]
        public required string Content { get; set; }
    }
}