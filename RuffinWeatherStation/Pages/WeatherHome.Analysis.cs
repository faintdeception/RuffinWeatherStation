namespace RuffinWeatherStation.Pages;

public partial class WeatherHome
{
    private void SwitchToLongTermAnalysis()
    {
        showShortTermAnalysis = false;
        StateHasChanged();
    }

    private void SwitchToShortTermAnalysis()
    {
        showShortTermAnalysis = true;
        StateHasChanged();
    }

    private async Task AnalyzePeriod(int days)
    {
        analysisPeriod = days;
        try
        {
            analysisResult = await TemperatureService.GetAnalysisAsync(days);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error analyzing weather over {days} days: {ex.Message}");
        }

        StateHasChanged();
    }

    private async Task AnalyzeRecentPeriod(int hours)
    {
        analysisHours = hours;
        try
        {
            recentAnalysisResult = await TemperatureService.GetRecentAnalysisAsync(hours);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error analyzing recent weather over {hours} hours: {ex.Message}");
        }

        StateHasChanged();
    }
}
