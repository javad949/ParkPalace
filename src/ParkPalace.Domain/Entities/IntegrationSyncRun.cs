using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class IntegrationSyncRun : BaseEntity
{
    public string SourceSystem { get; set; } = "Sepidar";

    public string TargetEntity { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, InProgress, Succeeded, Failed, Disabled

    public int RecordsIngested { get; set; }

    public string? ErrorMessage { get; set; }

    public string? WatermarkToken { get; set; }
}
