using Microsoft.IdentityModel.Tokens;
using RuffinWeatherStation.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RuffinWeatherStation.Api.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        private readonly UserService _userService;

        public AuthService(IConfiguration configuration, UserService userService)
        {
            _configuration = configuration;
            _userService = userService;
        }

        public LoginResponse? Authenticate(LoginRequest loginRequest)
        {
            var user = _userService.GetByUsername(loginRequest.Username);
            
            if (user == null || !VerifyPassword(loginRequest.Password, user.PasswordHash))
            {
                return null;
            }

            // Update last login time
            user.LastLogin = DateTime.UtcNow;
            _userService.Update(user);
            
            // Generate JWT token
            var token = GenerateJwtToken(user);
            
            return new LoginResponse
            {
                Username = user.Username,
                Token = token.tokenString,
                Expiration = token.validTo
            };
        }

        private (string tokenString, DateTime validTo) GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "defaultDevelopmentKey12345678901234567890");
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return (tokenHandler.WriteToken(token), tokenDescriptor.Expires.Value);
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}