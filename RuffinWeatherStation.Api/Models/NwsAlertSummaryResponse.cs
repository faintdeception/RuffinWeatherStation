namespace RuffinWeatherStation.Api.Models;

public class NwsAlertSummaryResponse
{
    public string Location { get; set; } = "backyard";
    public int LookbackDays { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalSnapshots { get; set; }
    public DateTime? SnapshotFetchedAtUtc { get; set; }
    public bool IsSnapshotExpired { get; set; }
    public int ActiveAlerts { get; set; }
    public int ExpiredAlerts { get; set; }
    public int SevereOrExtremeAlerts { get; set; }
    public DateTime? ApproximateSunriseUtc { get; set; }
    public DateTime? ApproximateSunsetUtc { get; set; }
    public DateTime? DaylightSnapshotFetchedAtUtc { get; set; }
    public string DaylightSnapshotLocation { get; set; } = string.Empty;
    public bool UsesPriorDaySnapshotForDaylight { get; set; }
    public List<NwsAlertSnapshotSummary> RecentAlerts { get; set; } = new();
    public List<NwsMitigationRecommendation> MitigationRecommendations { get; set; } = new();
}

public class NwsAlertSnapshotSummary
{
    public string AlertId { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Certainty { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime? OnsetUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public bool IsActive { get; set; }
}

public class NwsMitigationRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
}
