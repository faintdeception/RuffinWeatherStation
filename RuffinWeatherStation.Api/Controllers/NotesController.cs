using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuffinWeatherStation.Api.Models;
using RuffinWeatherStation.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace RuffinWeatherStation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotesController : ControllerBase
    {
        private readonly WeatherNoteService _noteService;

        public NotesController(WeatherNoteService noteService)
        {
            _noteService = noteService;
        }

        [HttpGet("{date}")]
        public ActionResult<WeatherNote> GetByDate(DateTime date)
        {
            var note = _noteService.GetByDate(date);
            if (note == null)
            {
                return NotFound();
            }

            return Ok(note);
        }

        [HttpGet]
        public ActionResult<IEnumerable<WeatherNote>> GetByDateRange(
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate)
        {
            // Default to current month if no dates provided
            startDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            endDate ??= startDate.Value.AddMonths(1).AddDays(-1);

            // Limit the range to avoid excessive data
            if ((endDate.Value - startDate.Value).TotalDays > 366)
            {
                return BadRequest(new { message = "Date range cannot exceed 1 year" });
            }

            var notes = _noteService.GetByDateRange(startDate.Value, endDate.Value);
            return Ok(notes);
        }

        [HttpPost]
        [Authorize]
        public ActionResult<WeatherNote> Create([FromBody] WeatherNoteRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var note = _noteService.Create(request, userId);
                return CreatedAtAction(nameof(GetByDate), new { date = note.Date }, note);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public ActionResult<WeatherNote> Update(int id, [FromBody] WeatherNoteRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
                var note = _noteService.Update(id, request, userId);
                return Ok(note);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public ActionResult Delete(int id)
        {
            var success = _noteService.Delete(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}