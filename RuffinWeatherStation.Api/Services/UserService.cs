using RuffinWeatherStation.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RuffinWeatherStation.Api.Services
{
    public class UserService
    {
        // For simplicity, we'll store users in memory
        // In a production app, you would use a database
        private static List<User> _users = new List<User>();
        private AuthService? _authService;

        // Constructor with circular dependency handling
        public UserService()
        {
            // Add a default admin user if the list is empty
            if (_users.Count == 0)
            {
                // Password will be hashed in the Initialize method
                _users.Add(new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "", // Will be set in Initialize
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Method to initialize with AuthService (to avoid circular dependency)
        public void Initialize(AuthService authService)
        {
            _authService = authService;
            
            // Set password hash for admin if not set
            var admin = _users.FirstOrDefault(u => u.Username == "admin");
            if (admin != null && string.IsNullOrEmpty(admin.PasswordHash))
            {
                admin.PasswordHash = _authService.HashPassword("Admin123!"); 
            }
        }

        public User? GetByUsername(string username)
        {
            return _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public User? GetById(int id)
        {
            return _users.FirstOrDefault(u => u.Id == id);
        }

        public void Update(User user)
        {
            // Find and update the user
            var existingUser = _users.FirstOrDefault(u => u.Id == user.Id);
            if (existingUser != null)
            {
                int index = _users.IndexOf(existingUser);
                _users[index] = user;
            }
        }

        public User Create(string username, string password)
        {
            if (_authService == null)
            {
                throw new InvalidOperationException("AuthService not initialized");
            }
            
            var user = new User
            {
                Id = _users.Any() ? _users.Max(u => u.Id) + 1 : 1,
                Username = username,
                PasswordHash = _authService.HashPassword(password),
                CreatedAt = DateTime.UtcNow
            };

            _users.Add(user);
            return user;
        }
    }
}