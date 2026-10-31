using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Telephony;

public class TelephonyAccessControlService : ITelephonyAccessControlService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelephonyConnectionTracker _connectionTracker;
    private readonly ITelephonyHubGroupManager? _hubGroupManager;
    private readonly ILogger<TelephonyAccessControlService> _logger;

    public TelephonyAccessControlService(
        IServiceScopeFactory scopeFactory,
        ITelephonyConnectionTracker connectionTracker,
        ILogger<TelephonyAccessControlService> logger,
        ITelephonyHubGroupManager? hubGroupManager = null)
    {
        _scopeFactory = scopeFactory;
        _connectionTracker = connectionTracker;
        _logger = logger;
        _hubGroupManager = hubGroupManager;
    }

    public async Task<TelephonyAccessScope> GetUserAccessScopeAsync(Guid? userId, string? username, CancellationToken ct = default)
    {
        var scope = new TelephonyAccessScope
        {
            UserId = userId,
            Username = username
        };

        var cleanUser = username?.Trim();
        if ((userId == null || userId == Guid.Empty) && string.IsNullOrEmpty(cleanUser))
        {
            _logger.LogWarning("Access scope denied: anonymous or empty user identity.");
            return scope; // Empty scope: no extensions, no hub groups, no global access
        }

        using var serviceScope = _scopeFactory.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<ICrmDbContext>();

        AppUser? userEntity = null;

        // 1. Resolve user entity with strict, unambiguous identity
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            // Point 1: When a valid UserId is provided, retrieve user EXCLUSIVELY by that UserId.
            // Under NO circumstance should an OR condition with Username select another user.
            userEntity = await dbContext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

            // If a username was also supplied alongside UserId, verify they do not conflict.
            // If they conflict, reject as an ambiguous/conflicting identity.
            if (userEntity != null && !string.IsNullOrEmpty(cleanUser))
            {
                if (!string.Equals(userEntity.Username, cleanUser, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Access scope denied: identity conflict between provided UserId ({UserId} -> '{ActualUser}') and Username ('{SuppliedUser}').",
                        userId.Value, userEntity.Username, cleanUser);
                    return scope; // Empty scope: conflicting identity!
                }
            }
        }
        else if (!string.IsNullOrEmpty(cleanUser))
        {
            // Point 2: If UserId is not provided, accept ONLY exact, unambiguous server identity.
            // Domain stripping, short name matching, and EndsWith("\\" + account) are strictly prohibited.
            var matchingUsers = await dbContext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .Where(u => u.Username == cleanUser)
                .ToListAsync(ct);

            if (matchingUsers.Count == 1)
            {
                userEntity = matchingUsers[0];
            }
            else if (matchingUsers.Count > 1)
            {
                _logger.LogWarning("Access scope denied: ambiguous identity for username '{Username}' ({Count} matching records found).",
                    cleanUser, matchingUsers.Count);
                return scope; // Empty scope: ambiguous identity!
            }
        }

        // Point 2 & 4: Disabled user or invalid/non-existent identity receives empty scope BEFORE extracting extension or supervised groups.
        // Zero side-channels to retain access!
        if (userEntity == null || !userEntity.IsEnabled)
        {
            _logger.LogWarning("Access scope denied: user '{Username}' (Id: {UserId}) is invalid, not found, or disabled.", cleanUser, userId);
            return scope; // Empty scope: AllowedExtensions empty, HubGroups empty, HasGlobalAccess = false
        }

        scope.UserId = userEntity.Id;
        scope.Username = userEntity.Username;

        // Point 5 POLICY: HasGlobalAccess is granted via: (Roles.Admin) OR (Permissions.CallsSupervise).
        // This is a disjunctive policy (OR):
        //   - Either the user is in role 'Admin' (Roles.Admin)
        //   - OR the user possesses the explicit permission 'Calls.Supervise' (Permissions.CallsSupervise).
        // It is NOT a conjunctive requirement (NOT Admin AND CallsSupervise).
        // No other roles or permissions grant global telephony supervision without an explicit policy update.
        bool hasAdminRole = userEntity.UserRoles.Any(ur =>
            string.Equals(ur.Role.Name, Roles.Admin, StringComparison.OrdinalIgnoreCase));

        bool hasSupervisePerm = userEntity.UserRoles.Any(ur =>
            ur.Role.RolePermissions.Any(rp =>
                string.Equals(rp.Permission.Code, Permissions.CallsSupervise, StringComparison.OrdinalIgnoreCase)));

        scope.HasGlobalAccess = hasAdminRole || hasSupervisePerm;

        // 2. Point 3: Resolve verified extension assigned in UserExtensions (must be IsActive == true)
        // PRIORITY 1: Explicit mapping by UserId
        UserExtension? userExt = null;
        var extsByUserId = await dbContext.UserExtensions
            .AsNoTracking()
            .Where(e => e.IsActive && e.UserId == userEntity.Id && !string.IsNullOrWhiteSpace(e.Extension))
            .ToListAsync(ct);

        if (extsByUserId.Count > 0)
        {
            userExt = extsByUserId[0];
            if (extsByUserId.Count > 1)
            {
                _logger.LogWarning("User {UserId} has {Count} active extensions mapped; using extension {Ext}.",
                    userEntity.Id, extsByUserId.Count, userExt.Extension);
            }
        }
        else if (!string.IsNullOrEmpty(userEntity.Username))
        {
            // PRIORITY 2: Legacy fallback by EXACT username, only if unambiguous and non-conflicting.
            // Must NOT conflict with another user's UserId.
            var extsByUsername = await dbContext.UserExtensions
                .AsNoTracking()
                .Where(e => e.IsActive &&
                            e.Username == userEntity.Username &&
                            (e.UserId == null || e.UserId == userEntity.Id) &&
                            !string.IsNullOrWhiteSpace(e.Extension))
                .ToListAsync(ct);

            if (extsByUsername.Count == 1)
            {
                userExt = extsByUsername[0];
            }
            else if (extsByUsername.Count > 1)
            {
                _logger.LogWarning("Ambiguous extension fallback for username '{Username}': found {Count} matching records.",
                    userEntity.Username, extsByUsername.Count);
                // Ambiguous: leave userExt = null
            }
        }

        string? verifiedExt = userExt?.Extension?.Trim();
        scope.VerifiedUserExtension = verifiedExt;

        if (!string.IsNullOrEmpty(verifiedExt))
        {
            scope.AllowedExtensions.Add(verifiedExt);
            scope.HubGroups.Add($"ext_{verifiedExt}");
        }

        // 3. Find groups where user is designated supervisor
        // CRITICAL: ONLY active groups (IsActive == true) grant supervision!
        var supervisedGroups = await dbContext.TelephonyGroups
            .Include(g => g.Members)
            .AsNoTracking()
            .Where(g => g.IsActive && (
                g.SupervisorUserId == userEntity.Id ||
                (!string.IsNullOrEmpty(verifiedExt) && g.SupervisorExtension == verifiedExt) ||
                g.Members.Any(m => (m.UserId == userEntity.Id && m.IsSupervisor) ||
                                   (!string.IsNullOrEmpty(verifiedExt) && m.Extension == verifiedExt && m.IsSupervisor))
            ))
            .ToListAsync(ct);

        foreach (var group in supervisedGroups)
        {
            scope.SupervisedGroupIds.Add(group.Id);
            scope.HubGroups.Add($"group_sup_{group.Id}");

            if (!string.IsNullOrWhiteSpace(group.SupervisorExtension))
            {
                scope.AllowedExtensions.Add(group.SupervisorExtension.Trim());
            }

            foreach (var member in group.Members)
            {
                if (!string.IsNullOrWhiteSpace(member.Extension))
                {
                    scope.AllowedExtensions.Add(member.Extension.Trim());
                }
            }
        }

        // 4. Global admin / supervisor joins global supervisory group
        if (scope.HasGlobalAccess)
        {
            scope.HubGroups.Add("global_supervisors");
        }

        return scope;
    }

    public bool CanAccessCall(TelephonyAccessScope scope, string? callExtension, string? callerNumber, string? destinationNumber)
    {
        if (scope == null) return false;
        if (scope.HasGlobalAccess) return true;
        if (scope.AllowedExtensions.Count == 0) return false;

        if (!string.IsNullOrWhiteSpace(callExtension) && scope.AllowedExtensions.Contains(callExtension.Trim()))
            return true;

        if (!string.IsNullOrWhiteSpace(callerNumber) && scope.AllowedExtensions.Contains(callerNumber.Trim()))
            return true;

        if (!string.IsNullOrWhiteSpace(destinationNumber) && scope.AllowedExtensions.Contains(destinationNumber.Trim()))
            return true;

        return false;
    }

    public async Task<List<string>> GetNotificationTargetGroupsForExtensionAsync(string? extension, CancellationToken ct = default)
    {
        var targetGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Global supervisors (admins) always receive notifications
        targetGroups.Add("global_supervisors");

        if (string.IsNullOrWhiteSpace(extension))
        {
            return targetGroups.ToList();
        }

        var cleanExt = extension.Trim();

        // 1. Direct extension recipient group
        targetGroups.Add($"ext_{cleanExt}");

        // 2. Supervisors of any active group containing cleanExt as member or supervisor
        using var serviceScope = _scopeFactory.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<ICrmDbContext>();

        var relevantGroupIds = await dbContext.TelephonyGroups
            .AsNoTracking()
            .Where(g => g.IsActive && (
                g.SupervisorExtension == cleanExt ||
                g.Members.Any(m => m.Extension == cleanExt)
            ))
            .Select(g => g.Id)
            .ToListAsync(ct);

        foreach (var groupId in relevantGroupIds)
        {
            targetGroups.Add($"group_sup_{groupId}");
        }

        return targetGroups.ToList();
    }

    public async Task SyncActiveConnectionsAsync(CancellationToken ct = default)
    {
        if (_hubGroupManager == null)
        {
            _logger.LogDebug("SyncActiveConnectionsAsync skipped: no ITelephonyHubGroupManager registered.");
            return;
        }

        var connections = _connectionTracker.GetAllConnections();
        if (connections.Count == 0) return;

        _logger.LogInformation("Syncing {Count} active telephony SignalR connections...", connections.Count);

        foreach (var conn in connections)
        {
            try
            {
                var newScope = await GetUserAccessScopeAsync(conn.UserId, conn.Username, ct);
                var desiredGroups = newScope.HubGroups;
                var currentGroups = conn.JoinedGroups;

                var toRemove = currentGroups.Except(desiredGroups, StringComparer.OrdinalIgnoreCase).ToList();
                var toAdd = desiredGroups.Except(currentGroups, StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var rem in toRemove)
                {
                    await _hubGroupManager.RemoveFromGroupAsync(conn.ConnectionId, rem, ct);
                    _logger.LogInformation("Revoked group {Group} from connection {ConnId} for user {Username}",
                        rem, conn.ConnectionId, conn.Username);
                }

                foreach (var add in toAdd)
                {
                    await _hubGroupManager.AddToGroupAsync(conn.ConnectionId, add, ct);
                    _logger.LogInformation("Granted group {Group} to connection {ConnId} for user {Username}",
                        add, conn.ConnectionId, conn.Username);
                }

                _connectionTracker.UpdateGroups(conn.ConnectionId, desiredGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing connection {ConnId} for user {Username}", conn.ConnectionId, conn.Username);
            }
        }
    }
}
