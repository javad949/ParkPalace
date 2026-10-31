using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Api.Hubs;

public class TelephonyHub : Hub<ITelephonyClient>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ITelephonyAccessControlService _accessControlService;
    private readonly ITelephonyConnectionTracker _connectionTracker;
    private readonly ILogger<TelephonyHub> _logger;

    public TelephonyHub(
        ICurrentUserService currentUserService,
        ITelephonyAccessControlService accessControlService,
        ITelephonyConnectionTracker connectionTracker,
        ILogger<TelephonyHub> logger)
    {
        _currentUserService = currentUserService;
        _accessControlService = accessControlService;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    public async Task RegisterExtension(string? extension)
    {
        var username = _currentUserService.Username;
        var userId = _currentUserService.UserId;

        // 1. Resolve server-side verified access scope
        var scope = await _accessControlService.GetUserAccessScopeAsync(userId, username);

        // 2. Validate client-supplied parameter
        var requestedExt = extension?.Trim();
        if (!string.IsNullOrEmpty(requestedExt))
        {
            if (!scope.HasGlobalAccess)
            {
                if (string.IsNullOrEmpty(scope.VerifiedUserExtension) ||
                    !string.Equals(scope.VerifiedUserExtension, requestedExt, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Security rejection: User {Username} attempted to register unassigned extension {RequestedExt} (Verified: {VerifiedExt})",
                        username, requestedExt, scope.VerifiedUserExtension);
                    throw new HubException("شما مجاز به ثبت این شماره داخلی نیستید.");
                }
            }
        }

        // 3. Clear existing groups on this connection to prevent privilege retention
        var existingConn = _connectionTracker.GetConnection(Context.ConnectionId);
        if (existingConn != null)
        {
            foreach (var grp in existingConn.JoinedGroups)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, grp);
            }
        }

        // 4. Join only authorized groups determined by server-side scope
        foreach (var grp in scope.HubGroups)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, grp);
        }

        // 5. Track connection for dynamic revocation and lifecycle tracking
        _connectionTracker.TrackConnection(
            Context.ConnectionId,
            userId,
            username,
            scope.VerifiedUserExtension,
            scope.HubGroups);

        _logger.LogInformation("Connection {ConnectionId} for user {Username} registered with groups: {Groups}",
            Context.ConnectionId, username, string.Join(", ", scope.HubGroups));
    }

    public async Task UnregisterExtension(string? extension)
    {
        var existingConn = _connectionTracker.GetConnection(Context.ConnectionId);
        if (existingConn != null)
        {
            foreach (var grp in existingConn.JoinedGroups)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, grp);
            }
        }
        _connectionTracker.UntrackConnection(Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionTracker.UntrackConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
