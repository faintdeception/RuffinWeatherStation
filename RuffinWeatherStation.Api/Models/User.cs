using System;
using System.ComponentModel.DataAnnotations;

namespace RuffinWeatherStation.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        public required string Username { get; set; }
        
        [Required]
        public required string PasswordHash { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
    }

    public class LoginRequest
    {
        [Required]
        public required string Username { get; set; }
        
        [Required]
        public required string Password { get; set; }
    }

    public class LoginResponse
    {
        public required string Token { get; set; }
        public required string Username { get; set; }
        public DateTime Expiration { get; set; }
    }
}