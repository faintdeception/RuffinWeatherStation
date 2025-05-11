using Microsoft.AspNetCore.Mvc;
using RuffinWeatherStation.Api.Models;
using RuffinWeatherStation.Api.Services;
using System.Threading.Tasks;

namespace RuffinWeatherStation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            var response = _authService.Authenticate(request);
            
            if (response == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            return Ok(response);
        }
    }
}