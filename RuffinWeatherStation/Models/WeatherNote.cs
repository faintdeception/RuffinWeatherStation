using System;
using System.ComponentModel.DataAnnotations;

namespace RuffinWeatherStation.Models
{
    public class WeatherNote
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public required string Content { get; set; }
        public required string UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class WeatherNoteRequest
    {
        [Required]
        public DateTime Date { get; set; }
        
        [Required]
        [MinLength(1, ErrorMessage = "Content cannot be empty")]
        public required string Content { get; set; }
    }
}