using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Infrastructure.Services.Telephony;

public class TelephonyConnectionTracker : ITelephonyConnectionTracker
{
    private readonly ConcurrentDictionary<string, TelephonyConnectionInfo> _connections = new();

    public void TrackConnection(string connectionId, Guid? userId, string? username, string? extension, IEnumerable<string> initialGroups)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;

        var groups = new HashSet<string>(initialGroups ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var info = new TelephonyConnectionInfo
        {
            ConnectionId = connectionId,
            UserId = userId,
            Username = username,
            Extension = extension,
            JoinedGroups = groups,
            ConnectedAtUtc = DateTime.UtcNow
        };

        _connections.AddOrUpdate(connectionId, info, (_, _) => info);
    }

    public void UpdateGroups(string connectionId, IEnumerable<string> currentGroups)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;

        if (_connections.TryGetValue(connectionId, out var info))
        {
            info.JoinedGroups = new HashSet<string>(currentGroups ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public void UntrackConnection(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;
        _connections.TryRemove(connectionId, out _);
    }

    public TelephonyConnectionInfo? GetConnection(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return null;
        _connections.TryGetValue(connectionId, out var info);
        return info;
    }

    public IReadOnlyList<TelephonyConnectionInfo> GetAllConnections()
    {
        return _connections.Values.ToList();
    }
}
