using System.Net.Http.Json;
using System.Text.Json;
using RuffinWeatherStation.Models;

namespace RuffinWeatherStation.Services
{
    public class WeatherNoteService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;

        public WeatherNoteService(HttpClient httpClient, AuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        public async Task<(WeatherNote? Note, bool NotFound, string? ErrorMessage)> GetNoteByDate(DateTime date)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/notes/{date:yyyy-MM-dd}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 - No note exists for this date
                    return (null, true, null);
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    return (null, false, $"API error: {response.StatusCode}");
                }
                
                var note = await response.Content.ReadFromJsonAsync<WeatherNote>();
                return (note, false, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching note for date {date:yyyy-MM-dd}: {ex.Message}");
                return (null, false, ex.Message);
            }
        }

        public async Task<List<WeatherNote>> GetNotesByDateRange(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<WeatherNote>>(
                    $"api/notes?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}") ?? new List<WeatherNote>();
            }
            catch
            {
                return new List<WeatherNote>();
            }
        }

        public async Task<WeatherNote?> CreateNote(WeatherNoteRequest request)
        {
            await _authService.AddAuthHeader(_httpClient);

            var response = await _httpClient.PostAsJsonAsync("api/notes", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WeatherNote>();
            }
            
            return null;
        }

        public async Task<WeatherNote?> UpdateNote(int id, WeatherNoteRequest request)
        {
            await _authService.AddAuthHeader(_httpClient);

            var response = await _httpClient.PutAsJsonAsync($"api/notes/{id}", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WeatherNote>();
            }
            
            return null;
        }

        public async Task<bool> DeleteNote(int id)
        {
            await _authService.AddAuthHeader(_httpClient);

            var response = await _httpClient.DeleteAsync($"api/notes/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}