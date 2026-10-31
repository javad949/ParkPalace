using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ParkPalace.Application.Interfaces;

public class TelephonyAccessScope
{
    public bool HasGlobalAccess { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? VerifiedUserExtension { get; set; }
    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<Guid> SupervisedGroupIds { get; set; } = new();
    public HashSet<string> HubGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class TelephonyConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? Extension { get; set; }
    public HashSet<string> JoinedGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;
}

public interface ITelephonyConnectionTracker
{
    void TrackConnection(string connectionId, Guid? userId, string? username, string? extension, IEnumerable<string> initialGroups);
    void UpdateGroups(string connectionId, IEnumerable<string> currentGroups);
    void UntrackConnection(string connectionId);
    TelephonyConnectionInfo? GetConnection(string connectionId);
    IReadOnlyList<TelephonyConnectionInfo> GetAllConnections();
}

public interface ITelephonyHubGroupManager
{
    Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default);
    Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default);
}

public interface ITelephonyAccessControlService
{
    Task<TelephonyAccessScope> GetUserAccessScopeAsync(Guid? userId, string? username, CancellationToken ct = default);
    bool CanAccessCall(TelephonyAccessScope scope, string? callExtension, string? callerNumber, string? destinationNumber);
    Task<List<string>> GetNotificationTargetGroupsForExtensionAsync(string? extension, CancellationToken ct = default);
    Task SyncActiveConnectionsAsync(CancellationToken ct = default);
}
