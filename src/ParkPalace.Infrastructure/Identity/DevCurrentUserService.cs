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
    private string _displayName = "کاربر توسعه دهنده (مدیر)";
    private string _adSid = "S-1-5-21-DEV-ADMIN-SID";
    private List<string> _roles = new() { RoleConst.Admin };
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

        // Check for development override headers
        if (httpContext.Request.Headers.TryGetValue("X-Dev-Unauthenticated", out _))
        {
            _isAuthenticated = false;
            _username = "Anonymous";
            _displayName = "کاربر ناشناس";
            _adSid = "";
            _roles.Clear();
            _permissions.Clear();
            return;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Dev-User", out var customUser))
        {
            _username = customUser.ToString();
            _displayName = _username;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Dev-Role", out var customRole))
        {
            var roleStr = customRole.ToString();
            _roles = roleStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            
            // Map permissions according to requested dev role
            _permissions.Clear();
            if (_roles.Contains(RoleConst.Admin))
            {
                _permissions.AddRange(PermConst.All);
            }
            else
            {
                if (_roles.Contains(RoleConst.Procurement))
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
                if (_roles.Contains(RoleConst.Warehouse))
                {
                    _permissions.AddRange(new[] {
                        PermConst.WarehouseView, PermConst.WarehouseReceive,
                        PermConst.ReceiptsView, PermConst.ReceiptsManage,
                        PermConst.MaterialsView, PermConst.OrdersView,
                        PermConst.InventoryView,
                        PermConst.ReportsView, PermConst.ReportsWarehouse, PermConst.ReportsExport
                    });
                }
                if (_roles.Contains(RoleConst.Finance))
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
                if (_roles.Contains(RoleConst.Manager))
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
    }

    public Guid? UserId { get { EnsureInitialized(); return _userId; } }
    public string? Username { get { EnsureInitialized(); return _username; } }
    public string? DisplayName { get { EnsureInitialized(); return _displayName; } }
    public string? AdSid { get { EnsureInitialized(); return _adSid; } }
    public IReadOnlyList<string> Roles { get { EnsureInitialized(); return _roles; } }
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
