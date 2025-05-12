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