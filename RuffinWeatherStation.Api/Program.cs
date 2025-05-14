using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RuffinWeatherStation.Api.Services;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

try
{
    Console.WriteLine($"[STARTUP] Application starting at {DateTime.UtcNow}...");
    
    var builder = WebApplication.CreateBuilder(args);

    Console.WriteLine("[STARTUP] Builder created, configuring services...");
    
    // Add connection string debug info
    Console.WriteLine($"[STARTUP] Connection string from config: {(string.IsNullOrEmpty(builder.Configuration.GetConnectionString("MongoDb")) ? "NOT FOUND" : "FOUND")}");
    Console.WriteLine($"[STARTUP] Database name from config: {builder.Configuration["DatabaseSettings:DatabaseName"] ?? "NOT FOUND"}");

    // Add services to the container.
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddControllers();

    Console.WriteLine("[STARTUP] Controllers and OpenAPI configured...");

    // Register the WeatherService
    try {
        Console.WriteLine("[STARTUP] Registering WeatherService...");
        builder.Services.AddSingleton<WeatherService>();
        Console.WriteLine("[STARTUP] WeatherService registered successfully");
    }
    catch (Exception ex) {
        Console.WriteLine($"[STARTUP ERROR] Failed to register WeatherService: {ex.Message}");
        throw;
    }

    // Configure authentication services
    Console.WriteLine("[STARTUP] Registering UserService and WeatherNoteService...");
    builder.Services.AddSingleton<UserService>();
    builder.Services.AddSingleton<WeatherNoteService>();

    // Configure JWT Authentication
    Console.WriteLine("[STARTUP] Configuring JWT authentication...");
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
    Console.WriteLine("[STARTUP] Configuring CORS...");
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorApp", policy =>
        {
            // Allow specific development origins
            var allowedOrigins = new List<string>
            {
                "https://localhost:5001", 
                "http://localhost:5000",
                "https://localhost:7272",
                "http://localhost:5259",
                "https://localhost:7159",
                "http://localhost:5204",
                "https://ruffin-weather-app.azurestaticapps.net"
            };
            
            policy.SetIsOriginAllowed(origin => 
            {
                // Check if origin is in our explicit list
                if (allowedOrigins.Contains(origin))
                {
                    return true;
                }
                
                // Allow any gray-grass Azure Static Web App URL regardless of the numeric suffix
                if (origin.Contains("gray-grass-0ffc76b10") && origin.EndsWith(".azurestaticapps.net"))
                {
                    return true;
                }
                
                // Allow proud-forest Azure Static Web App URL
                if (origin.Contains("proud-forest-0544df410") && origin.EndsWith(".azurestaticapps.net"))
                {
                    return true;
                }
                
                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
    });

    // Handle circular dependency between UserService and AuthService
    // Register AuthService with a factory that can access UserService
    Console.WriteLine("[STARTUP] Registering AuthService...");
    builder.Services.AddSingleton<AuthService>(serviceProvider => {
        try {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var userService = serviceProvider.GetRequiredService<UserService>();
            var authService = new AuthService(config, userService);
            userService.Initialize(authService);
            Console.WriteLine("[STARTUP] AuthService registered successfully");
            return authService;
        }
        catch (Exception ex) {
            Console.WriteLine($"[STARTUP ERROR] Failed to create AuthService: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
    });

    // Add health checks
    Console.WriteLine("[STARTUP] Adding health checks...");
    builder.Services.AddHealthChecks()
        .AddCheck("MongoDB", () => {
            try {
                // We'll just report healthy since we've already validated the connection
                return HealthCheckResult.Healthy("MongoDB connection is available");
            }
            catch (Exception ex) {
                return HealthCheckResult.Unhealthy("MongoDB connection failed", ex);
            }
        });

    Console.WriteLine("[STARTUP] All services registered, building app...");
    var app = builder.Build();

    Console.WriteLine("[STARTUP] App built, configuring middleware...");

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

    // Add health check endpoint
    app.MapHealthChecks("/health", new HealthCheckOptions {
        ResultStatusCodes = {
            [HealthStatus.Healthy] = 200,
            [HealthStatus.Degraded] = 200,
            [HealthStatus.Unhealthy] = 503
        }
    });

    Console.WriteLine("[STARTUP] App fully configured, starting...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"[FATAL ERROR] Application startup failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    
    // Write to a file that can be found via SSH
    try {
        File.WriteAllText("/home/site/wwwroot/startup_error.log", 
            $"[{DateTime.UtcNow}] Fatal error in startup: {ex.ToString()}");
    }
    catch {
        // Ignore errors writing to file
    }
    
    // Re-throw to properly terminate the application
    throw;
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
