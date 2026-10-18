using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class AuditEvent : BaseEntity
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public Guid? ActorId { get; set; }

    public string ActorUsername { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? DetailsJson { get; set; }

    public string? IpAddress { get; set; }

    public bool Succeeded { get; set; } = true;
}
