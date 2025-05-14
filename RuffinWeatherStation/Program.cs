using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using RuffinWeatherStation;
using RuffinWeatherStation.Services;
using Microsoft.Extensions.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load configuration from appsettings.json
builder.Services.AddScoped(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiBaseUrl = config.GetSection("ApiSettings:BaseUrl").Value ?? builder.HostEnvironment.BaseAddress;
    
    // Configure HttpClient with compression headers
    var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
    httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("br"));
    
    return httpClient;
});

// Register services
// Note: TemperatureService constructor now requires IJSRuntime - this is automatically injected
builder.Services.AddScoped<TemperatureService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WeatherNoteService>();

// Add Radzen services
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

await builder.Build().RunAsync();
