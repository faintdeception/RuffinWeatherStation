namespace RuffinWeatherStation.Models;

public class NwsAlertSummaryData
{
    public string Location { get; set; } = "backyard";
    public int LookbackDays { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalSnapshots { get; set; }
    public int ActiveAlerts { get; set; }
    public int ExpiredAlerts { get; set; }
    public int SevereOrExtremeAlerts { get; set; }
    public List<NwsAlertSnapshotData> RecentAlerts { get; set; } = new();
}

public class NwsAlertSnapshotData
{
    public string Event { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public DateTime? SentUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public bool IsActive { get; set; }
}
