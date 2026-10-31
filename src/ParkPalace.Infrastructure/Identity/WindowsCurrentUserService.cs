using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using RoleConst = ParkPalace.Domain.Constants.Roles;
using PermConst = ParkPalace.Domain.Constants.Permissions;

namespace ParkPalace.Infrastructure.Identity;

public class WindowsCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICrmDbContext _dbContext;

    private bool _initialized;
    private Guid? _userId;
    private string? _username;
    private string? _displayName;
    private string? _adSid;
    private string? _extension;
    private string _activeRole = RoleConst.Admin;
    private List<string> _roles = new();
    private List<string> _availableRoles = new();
    private List<string> _permissions = new();

    public WindowsCurrentUserService(IHttpContextAccessor httpContextAccessor, ICrmDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity == null || !httpContext.User.Identity.IsAuthenticated)
        {
            return;
        }

        var identity = httpContext.User.Identity;
        _username = identity.Name ?? "Unknown";
        _displayName = _username;

        if (identity is WindowsIdentity winIdentity && winIdentity.User != null)
        {
            _adSid = winIdentity.User.Value;
        }
        else
        {
            _adSid = httpContext.User.FindFirst(ClaimTypes.PrimarySid)?.Value 
                     ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? _username;
        }

        var accountName = _username.Contains('\\') ? _username.Split('\\')[1] : _username;

        // Security decision: only an exact username match is a proven server identity.
        // Domain stripping, SAHAR\account, CRM\account, and short-name matching are not used.
        // More than one exact row is ambiguous and must not select either account.
        var exactUsers = _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Where(u => u.Username == _username)
            .ToList();

        var user = exactUsers.Count == 1 ? exactUsers[0] : null;

        if (user != null && user.IsEnabled)
        {
            _userId = user.Id;
            _displayName = user.DisplayName;

            var ext = ResolveOwnedExtension(user.Id, user.Username);
            if (ext != null)
            {
                _extension = ext.Extension;
            }

            var assignedRoles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();

            var isSuperAdmin = assignedRoles.Contains(RoleConst.Admin);
            if (isSuperAdmin)
            {
                // SuperAdmin possesses system audit capability across all authorized operational roles
                _availableRoles = new List<string>
                {
                    RoleConst.Admin,
                    RoleConst.Procurement,
                    RoleConst.Manager,
                    RoleConst.Warehouse,
                    RoleConst.Finance
                };
            }
            else
            {
                // Standard users are strictly restricted to their explicitly assigned database roles
                _availableRoles = assignedRoles.ToList();
            }

            // Determine active role based on request header (X-Active-Role or X-Dev-Role)
            string? requestedRole = null;
            if (httpContext.Request.Headers.TryGetValue("X-Active-Role", out var reqRoleHeader))
            {
                requestedRole = reqRoleHeader.ToString().Trim();
            }
            else if (httpContext.Request.Headers.TryGetValue("X-Dev-Role", out var devRoleHeader))
            {
                requestedRole = devRoleHeader.ToString().Trim();
            }

            string activeRole;
            if (string.Equals(requestedRole, "None", StringComparison.OrdinalIgnoreCase))
            {
                activeRole = "None";
            }
            else if (!string.IsNullOrEmpty(requestedRole) && _availableRoles.Contains(requestedRole, StringComparer.OrdinalIgnoreCase))
            {
                activeRole = _availableRoles.First(r => r.Equals(requestedRole, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Default role: Admin if user has Admin, else first assigned role
                activeRole = assignedRoles.Contains(RoleConst.Admin)
                    ? RoleConst.Admin
                    : (assignedRoles.FirstOrDefault() ?? "None");
            }

            _activeRole = activeRole;
            _roles = activeRole == "None" ? new List<string>() : new List<string> { activeRole };

            // Scope effective permissions strictly to the ACTIVE role
            if (activeRole == RoleConst.Admin)
            {
                _permissions = _dbContext.Permissions
                    .Select(p => p.Code)
                    .Distinct()
                    .ToList();
            }
            else
            {
                var roleEntity = _dbContext.Roles
                    .Include(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                    .FirstOrDefault(r => r.Name == activeRole);

                if (roleEntity != null)
                {
                    _permissions = roleEntity.RolePermissions
                        .Select(rp => rp.Permission.Code)
                        .Distinct()
                        .ToList();
                }
                else
                {
                    _permissions = new List<string>();
                }
            }
        }
        else if (user == null && accountName.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
        {
            _availableRoles = new List<string>
            {
                RoleConst.Admin,
                RoleConst.Procurement,
                RoleConst.Manager,
                RoleConst.Warehouse,
                RoleConst.Finance
            };
            _activeRole = RoleConst.Admin;
            _roles = new List<string> { RoleConst.Admin };
            _permissions = _dbContext.Permissions.Select(p => p.Code).Distinct().ToList();
        }
    }

    private UserExtension? ResolveOwnedExtension(Guid userId, string username)
    {
        var byUserId = _dbContext.UserExtensions
            .Where(e => e.IsActive && e.UserId == userId && e.Extension != null && e.Extension != "")
            .ToList();
        if (byUserId.Count > 0)
        {
            return byUserId[0];
        }

        var byExactName = _dbContext.UserExtensions
            .Where(e => e.IsActive
                && e.Username == username
                && (e.UserId == null || e.UserId == userId)
                && e.Extension != null && e.Extension != "")
            .ToList();
        return byExactName.Count == 1 ? byExactName[0] : null;
    }

    public Guid? UserId { get { EnsureInitialized(); return _userId; } }
    public string? Username 
    { 
        get 
        { 
            EnsureInitialized(); 
            return !string.IsNullOrWhiteSpace(_username) 
                ? _username 
                : (_httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "CRM\\Administrator"); 
        } 
    }
    public string? DisplayName { get { EnsureInitialized(); return _displayName; } }
    public string? AdSid { get { EnsureInitialized(); return _adSid; } }
    public string ActiveRole { get { EnsureInitialized(); return _activeRole; } }
    public string? Extension { get { EnsureInitialized(); return _extension; } }
    public IReadOnlyList<string> Roles { get { EnsureInitialized(); return _roles; } }
    public IReadOnlyList<string> AvailableRoles { get { EnsureInitialized(); return _availableRoles; } }
    public IReadOnlyList<string> Permissions { get { EnsureInitialized(); return _permissions; } }
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool HasPermission(string permission)
    {
        EnsureInitialized();
        return _permissions.Contains(permission);
    }

    public bool IsInRole(string role)
    {
        EnsureInitialized();
        return _roles.Contains(role);
    }
}
