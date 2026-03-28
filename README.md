# Ruffin Weather Station

## Configuration and Sensitive Data Handling

### Connection Strings
To protect sensitive connection strings and secrets, this project uses the following approach:

1. **For Development:**
   - Copy `appsettings.template.json` to `appsettings.Development.json`
   - Fill in your actual connection strings and secrets in `appsettings.Development.json`
   - This file is excluded from git via `.gitignore`

2. **For Production:**
   - In Azure, use environment variables or Azure Key Vault to store connection strings
   - These can be configured in the App Service configuration

### MongoDB to CosmosDB Migration
When moving from MongoDB to CosmosDB:

1. Use the CosmosDB MongoDB API for compatibility
2. Update the `ConnectionStrings:CosmosDb` setting in your configuration
3. For local development, you can use the CosmosDB Emulator:
   ```
   docker run -p 8081:8081 -p 10251-10254:10251-10254 -m 3g --name=cosmosdb-emulator mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
   ```

## Getting Started

1. Clone the repository
2. Configure your database connection strings as described above
3. Run the API project: `dotnet run --project RuffinWeatherStation.Api`
4. Run the Blazor WebAssembly project: `dotnet run --project RuffinWeatherStation`

## Garden Data

The app now includes a `Garden Data` page in the left navigation. This page provides:

- Recent rainfall context for planning.
- Seasonal start reference dates (spring/summer/fall/winter).
- Average last frost reference date resolved by the API.
- Plant readiness guidance from JSON profiles.
- NWS weather-risk snapshot summaries from `nws_snapshots`.
- Mitigation playbook recommendations generated from active alert categories.
- Drill-down alert links when source URLs are present in snapshots.

### API Endpoint

- `GET /api/garden/reference`
- Optional query: `year` (for example: `/api/garden/reference?year=2026`)
- `GET /api/garden/alerts-summary`
- Optional query: `days`, `location` (for example: `/api/garden/alerts-summary?days=7&location=backyard`)

### Frost Configuration (Environment Variables)

Set these on the API host to override defaults:

- `GardenSettings__AverageLastFrostMonthDay` (format: `MM-dd`, example: `04-20`)
- `GardenSettings__LocationLabel` (example: `front-yard`)

Defaults are defined in `RuffinWeatherStation.Api/appsettings.json`.

### Plant Profile Data Source

`Garden Data` now reads plant profiles from:

- `RuffinWeatherStation/wwwroot/data/garden-plants.json`

Each profile supports temperature-streak and frost-offset fields for readiness guidance cards. Current logic uses recent daily air-temperature averages as a proxy for soil-warmth streaks.

## SPA Routing (Refresh on Deep Links)

This Blazor WebAssembly app uses client-side routing. To avoid `404` responses when refreshing on routes like `/garden-data` or `/notes`, Azure Static Web Apps must rewrite non-file paths to `index.html`.

This is configured in:

- `RuffinWeatherStation/wwwroot/staticwebapp.config.json`