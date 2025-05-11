using RuffinWeatherStation.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RuffinWeatherStation.Api.Services
{
    public class WeatherNoteService
    {
        // For simplicity, we'll store notes in memory
        // In a production app, you would use a database
        private static List<WeatherNote> _notes = new List<WeatherNote>();

        public WeatherNoteService()
        {
        }

        public WeatherNote? GetByDate(DateTime date)
        {
            return _notes.FirstOrDefault(n => n.Date.Date == date.Date);
        }

        public List<WeatherNote> GetByDateRange(DateTime startDate, DateTime endDate)
        {
            return _notes.Where(n => n.Date.Date >= startDate.Date && n.Date.Date <= endDate.Date)
                         .OrderBy(n => n.Date)
                         .ToList();
        }

        public WeatherNote? GetById(int id)
        {
            return _notes.FirstOrDefault(n => n.Id == id);
        }

        public WeatherNote Create(WeatherNoteRequest request, string userId)
        {
            // Check if note already exists for the date
            var existingNote = GetByDate(request.Date);
            if (existingNote != null)
            {
                throw new InvalidOperationException($"Note already exists for date {request.Date.ToShortDateString()}");
            }

            var note = new WeatherNote
            {
                Id = _notes.Any() ? _notes.Max(n => n.Id) + 1 : 1,
                Date = request.Date.Date, // Store only the date part
                Content = request.Content,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _notes.Add(note);
            return note;
        }

        public WeatherNote Update(int id, WeatherNoteRequest request, string userId)
        {
            var existingNote = _notes.FirstOrDefault(n => n.Id == id);
            if (existingNote == null)
            {
                throw new KeyNotFoundException($"Note with id {id} not found");
            }

            // Update only the content (date remains the same)
            existingNote.Content = request.Content;
            existingNote.UpdatedAt = DateTime.UtcNow;

            return existingNote;
        }

        public bool Delete(int id)
        {
            var noteToRemove = _notes.FirstOrDefault(n => n.Id == id);
            if (noteToRemove != null)
            {
                _notes.Remove(noteToRemove);
                return true;
            }
            return false;
        }
    }
}