using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using RoleConst = ParkPalace.Domain.Constants.Roles;
using PermConst = ParkPalace.Domain.Constants.Permissions;

namespace ParkPalace.Infrastructure.Identity;

public class DevCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICrmDbContext _dbContext;

    private bool _initialized;
    private Guid? _userId;
    private string _username = "sahar\\dev_admin";
    private string _displayName = "مدیر ارشد سیستم (توسعه)";
    private string _adSid = "S-1-5-21-DEV-ADMIN-SID";
    private string _activeRole = RoleConst.Admin;
    private List<string> _roles = new() { RoleConst.Admin };
    private List<string> _availableRoles = new() { RoleConst.Admin, RoleConst.Procurement, RoleConst.Manager, RoleConst.Warehouse, RoleConst.Finance };
    private List<string> _permissions = new(PermConst.All);
    private bool _isAuthenticated = true;

    public DevCurrentUserService(IHttpContextAccessor httpContextAccessor, ICrmDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        // Check for development unauthenticated override
        if (httpContext.Request.Headers.TryGetValue("X-Dev-Unauthenticated", out _))
        {
            _isAuthenticated = false;
            _username = "Anonymous";
            _displayName = "کاربر ناشناس";
            _adSid = "";
            _activeRole = "None";
            _roles.Clear();
            _availableRoles.Clear();
            _permissions.Clear();
            return;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Dev-User", out var customUser) && !string.IsNullOrWhiteSpace(customUser))
        {
            _username = customUser.ToString().Trim();
            _displayName = _username;
        }

        var accountName = _username.Contains('\\') ? _username.Split('\\')[1] : _username;
        var saharDomainUser = $"SAHAR\\{accountName}";
        var crmDomainUser = $"CRM\\{accountName}";

        // Check if user exists in DB
        var user = _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefault(u => 
                u.Username == _username || 
                u.Username == accountName ||
                u.Username == saharDomainUser ||
                u.Username == crmDomainUser);

        List<string> assignedRoles;
        if (user != null && user.IsEnabled)
        {
            _userId = user.Id;
            _displayName = user.DisplayName;
            _adSid = user.AdObjectSid ?? "S-1-5-21-DEV-USER";
            assignedRoles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        }
        else
        {
            // Simulated user identity based on username conventions
            var normalizedUser = accountName.ToLowerInvariant();
            if (normalizedUser == "warehouse")
            {
                assignedRoles = new List<string> { RoleConst.Warehouse };
                _displayName = "سرپرست انبار (توسعه)";
            }
            else if (normalizedUser == "procurement" || normalizedUser == "procurement_lead" || normalizedUser == "procurement_user")
            {
                assignedRoles = new List<string> { RoleConst.Procurement };
                _displayName = "کارشناس تدارکات (توسعه)";
            }
            else if (normalizedUser == "finance")
            {
                assignedRoles = new List<string> { RoleConst.Finance };
                _displayName = "کارشناس حسابداری (توسعه)";
            }
            else if (normalizedUser == "manager")
            {
                assignedRoles = new List<string> { RoleConst.Manager };
                _displayName = "مدیر ناظر بازرگانی (توسعه)";
            }
            else if (normalizedUser == "unauthorized" || normalizedUser == "guest" || normalizedUser == "none")
            {
                assignedRoles = new List<string>();
                _displayName = "کاربر فاقد مجوز (توسعه)";
            }
            else
            {
                // Default to Admin
                assignedRoles = new List<string> { RoleConst.Admin, RoleConst.Manager };
                _displayName = "مدیر ارشد سیستم (توسعه)";
            }
        }

        var isSuperAdmin = assignedRoles.Contains(RoleConst.Admin);
        if (isSuperAdmin)
        {
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
            _availableRoles = assignedRoles.ToList();
        }

        // Determine requested active role (via X-Active-Role or X-Dev-Role)
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
            activeRole = assignedRoles.Contains(RoleConst.Admin)
                ? RoleConst.Admin
                : (assignedRoles.FirstOrDefault() ?? "None");
        }

        _activeRole = activeRole;
        _roles = activeRole == "None" ? new List<string>() : new List<string> { activeRole };

        // Determine permissions for the active role
        _permissions.Clear();
        if (activeRole == RoleConst.Admin)
        {
            _permissions.AddRange(PermConst.All);
        }
        else
        {
            if (activeRole == RoleConst.Procurement)
            {
                _permissions.AddRange(new[] {
                    PermConst.MaterialsView, PermConst.MaterialsManage,
                    PermConst.SuppliersView, PermConst.SuppliersManage,
                    PermConst.SuppliersPricesView, PermConst.SuppliersPricesManage,
                    PermConst.OrdersView, PermConst.OrdersCreate, PermConst.OrdersIssue, PermConst.OrdersCancel,
                    PermConst.QuotesView, PermConst.QuotesManage,
                    PermConst.WarehouseView, PermConst.ReceiptsView, 
                    PermConst.CallsView, PermConst.CallsManage, PermConst.CallsRecordingsListen,
                    PermConst.InventoryView, PermConst.RecommendationsView, PermConst.PoliciesManage,
                    PermConst.InvoicesView, PermConst.MatchingView,
                    PermConst.ReportsView, PermConst.ReportsProcurement, PermConst.ReportsExport
                });
            }
            else if (activeRole == RoleConst.Warehouse)
            {
                _permissions.AddRange(new[] {
                    PermConst.WarehouseView, PermConst.WarehouseReceive,
                    PermConst.ReceiptsView, PermConst.ReceiptsManage,
                    PermConst.MaterialsView, PermConst.OrdersView,
                    PermConst.InventoryView,
                    PermConst.ReportsView, PermConst.ReportsWarehouse, PermConst.ReportsExport
                });
            }
            else if (activeRole == RoleConst.Finance)
            {
                _permissions.AddRange(new[] {
                    PermConst.FinanceView, PermConst.FinanceReconcile,
                    PermConst.InvoicesView, PermConst.InvoicesManage,
                    PermConst.MatchingView, PermConst.MatchingResolve,
                    PermConst.OrdersView, PermConst.ReceiptsView, PermConst.SuppliersView,
                    PermConst.SuppliersPricesView, PermConst.InventoryView,
                    PermConst.ReportsView, PermConst.ReportsFinance, PermConst.ReportsExport
                });
            }
            else if (activeRole == RoleConst.Manager)
            {
                _permissions.AddRange(new[] {
                    PermConst.MaterialsView, PermConst.SuppliersView,
                    PermConst.SuppliersPricesView,
                    PermConst.OrdersView, PermConst.QuotesView, PermConst.WarehouseView,
                    PermConst.ReceiptsView, PermConst.InvoicesView, PermConst.MatchingView,
                    PermConst.FinanceView, PermConst.CallsView, PermConst.CallsSupervise, PermConst.CallsRecordingsListen,
                    PermConst.InventoryView, PermConst.RecommendationsView,
                    PermConst.ReportsView, PermConst.ReportsManager, PermConst.ReportsProcurement, PermConst.ReportsWarehouse, PermConst.ReportsFinance, PermConst.ReportsExport
                });
            }
        }
    }

    public Guid? UserId { get { EnsureInitialized(); return _userId; } }
    public string? Username { get { EnsureInitialized(); return _username; } }
    public string? DisplayName { get { EnsureInitialized(); return _displayName; } }
    public string? AdSid { get { EnsureInitialized(); return _adSid; } }
    public string ActiveRole { get { EnsureInitialized(); return _activeRole; } }
    public IReadOnlyList<string> Roles { get { EnsureInitialized(); return _roles; } }
    public IReadOnlyList<string> AvailableRoles { get { EnsureInitialized(); return _availableRoles; } }
    public IReadOnlyList<string> Permissions { get { EnsureInitialized(); return _permissions; } }
    public bool IsAuthenticated { get { EnsureInitialized(); return _isAuthenticated; } }

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
