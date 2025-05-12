param(
    [string]$resourceGroupName = "RuffinWeatherStation-rg",
    [string]$location = "eastus",
    [string]$apiAppName = "ruffin-weather-api",
    [string]$webAppName = "ruffin-weather-app",
    [string]$appServicePlanName = "ruffin-weather-plan"
)

# Login to Azure (uncomment if not already logged in)
# az login

# Create a resource group if it doesn't exist
Write-Output "Creating resource group $resourceGroupName..."
az group create --name $resourceGroupName --location $location

# Create App Service Plan
Write-Output "Creating App Service Plan..."
az appservice plan create --name $appServicePlanName --resource-group $resourceGroupName --sku B1 --is-linux

# Create Web App for API
Write-Output "Creating Web App for API..."
az webapp create --name $apiAppName --resource-group $resourceGroupName --plan $appServicePlanName --runtime "DOTNET:9.0"

# Create Static Web App for Blazor Client
Write-Output "Creating Static Web App for Blazor client..."
az staticwebapp create --name $webAppName --resource-group $resourceGroupName --location $location --sku Free

# Configure API App Settings
Write-Output "Configuring API App Settings..."
az webapp config appsettings set --name $apiAppName --resource-group $resourceGroupName --settings "ASPNETCORE_ENVIRONMENT=Production"

# Get the Static Web App deployment token
$staticWebAppToken = az staticwebapp secrets list --name $webAppName --resource-group $resourceGroupName --query "properties.apiKey" -o tsv
Write-Output "Static Web App deployment token retrieved."

# Output deployment information
Write-Output "Azure resources created successfully!"
Write-Output "API URL: https://$apiAppName.azurewebsites.net"
Write-Output "Web App URL: https://<static-web-app-url>"
Write-Output "To deploy the API, run: dotnet publish -c Release && az webapp deployment source config-zip -g $resourceGroupName -n $apiAppName --src <path-to-published-zip>"
Write-Output "For the Static Web App, use the deployment token with GitHub Actions or Azure DevOps"
Write-Output "Deployment token: $staticWebAppToken"