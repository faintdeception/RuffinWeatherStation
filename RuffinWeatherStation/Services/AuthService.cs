using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace RuffinWeatherStation.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private const string TokenKey = "auth_token";
        private const string UsernameKey = "username";
        private const string ExpirationKey = "token_expiration";

        public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetToken();
            return !string.IsNullOrEmpty(token);
        }

        public bool IsAuthenticated => Task.Run(async () => await IsAuthenticatedAsync()).Result;

        public async Task<bool> Login(string username, string password)
        {
            var request = new
            {
                Username = username,
                Password = password
            };

            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
            
            if (!response.IsSuccessStatusCode)
                return false;

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (loginResponse == null)
                return false;

            // Store token in local storage
            await SaveToken(loginResponse.Token, loginResponse.Username, loginResponse.Expiration);
            
            return true;
        }

        public async Task Logout()
        {
            await RemoveToken();
        }

        public async Task<string> GetUsername()
        {
            return await GetLocalStorageItem(UsernameKey) ?? string.Empty;
        }

        public async Task<string> GetToken()
        {
            return await GetLocalStorageItem(TokenKey) ?? string.Empty;
        }

        public async Task AddAuthHeader(HttpClient client)
        {
            var token = await GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        private async Task<string> GetLocalStorageItem(string key)
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
        }

        private async Task SaveToken(string token, string username, DateTime expiration)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UsernameKey, username);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ExpirationKey, expiration.ToString("o"));
        }

        private async Task RemoveToken()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UsernameKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ExpirationKey);
        }
    }

    public class LoginResponse
    {
        public required string Token { get; set; }
        public required string Username { get; set; }
        public DateTime Expiration { get; set; }
    }
}