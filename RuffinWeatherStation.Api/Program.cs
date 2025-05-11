using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RuffinWeatherStation.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Register the WeatherService
builder.Services.AddSingleton<WeatherService>();

// Configure authentication services
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<WeatherNoteService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                builder.Configuration["Jwt:Key"] ?? "defaultDevelopmentKey12345678901234567890")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

// Configure authorization
builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5001", 
                "http://localhost:5000",
                "https://localhost:7272",
                "http://localhost:5259",
                "https://localhost:7159",  // Added potential Blazor app port
                "http://localhost:5204")   // Added potential Blazor app port
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Handle circular dependency between UserService and AuthService
// Register AuthService with a factory that can access UserService
builder.Services.AddSingleton<AuthService>(serviceProvider => {
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var userService = serviceProvider.GetRequiredService<UserService>();
    var authService = new AuthService(config, userService);
    userService.Initialize(authService);
    return authService;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorApp");

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
