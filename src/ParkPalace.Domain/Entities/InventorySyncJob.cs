using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public enum SyncJobType
{
    Full = 1,
    Incremental = 2,
    Manual = 3
}

public enum SyncJobStatus
{
    Pending = 1,
    Running = 2,
    Success = 3,
    Partial = 4,
    Failed = 5
}

public class InventorySyncJob : BaseEntity
{
    public SyncJobType JobType { get; set; } = SyncJobType.Incremental;
    public SyncJobStatus Status { get; set; } = SyncJobStatus.Pending;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public int ItemsProcessed { get; set; }
    public int MovementsProcessed { get; set; }
    public int SnapshotsUpdated { get; set; }

    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string? TriggeredBy { get; set; } // "SystemScheduler" or username
}
