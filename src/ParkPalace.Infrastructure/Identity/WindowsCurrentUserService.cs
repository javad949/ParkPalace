using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

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
    private List<string> _roles = new();
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

        // Correlate with CRM AppUser table
        var user = _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefault(u => u.AdObjectSid == _adSid || u.Username == _username);

        if (user != null && user.IsEnabled)
        {
            _userId = user.Id;
            _displayName = user.DisplayName;
            _roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
            _permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToList();
        }
    }

    public Guid? UserId { get { EnsureInitialized(); return _userId; } }
    public string? Username { get { EnsureInitialized(); return _username; } }
    public string? DisplayName { get { EnsureInitialized(); return _displayName; } }
    public string? AdSid { get { EnsureInitialized(); return _adSid; } }
    public IReadOnlyList<string> Roles { get { EnsureInitialized(); return _roles; } }
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
