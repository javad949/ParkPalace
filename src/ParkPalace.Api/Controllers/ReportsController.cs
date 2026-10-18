using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IExportService _exportService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportingService reportingService,
        IExportService exportService,
        ICurrentUserService currentUserService,
        ILogger<ReportsController> logger)
    {
        _reportingService = reportingService;
        _exportService = exportService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) ||
               _currentUserService.Permissions.Contains(permission);
    }

    private ObjectResult Forbidden(string detail = "شما مجوز دسترسی به این گزارش را ندارید.")
    {
        return StatusCode(403, new ProblemDetails
        {
            Title = "دسترسی غیرمجاز",
            Detail = detail,
            Status = 403
        });
    }

    [HttpGet("procurement-summary")]
    public async Task<ActionResult<ProcurementSummaryReportDto>> GetProcurementSummary([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsProcurement) && !HasPermission(Permissions.ReportsView))
            return Forbidden("شما مجوز مشاهده گزارشات خلاصه تدارکات را ندارید.");

        var result = await _reportingService.GetProcurementSummaryAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("purchase-order-trends")]
    public async Task<ActionResult<List<PurchaseOrderTrendItemDto>>> GetPurchaseOrderTrends([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsView))
            return Forbidden("شما مجوز مشاهده روندهای خرید را ندارید.");

        var result = await _reportingService.GetPurchaseOrderTrendsAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("supplier-performance")]
    public async Task<ActionResult<List<SupplierPerformanceReportItemDto>>> GetSupplierPerformance([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsView) && !HasPermission(Permissions.SuppliersView))
            return Forbidden("شما مجوز مشاهده عملکرد تأمین‌کنندگان را ندارید.");

        var result = await _reportingService.GetSupplierPerformanceAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("material-consumption")]
    public async Task<ActionResult<List<MaterialConsumptionReportItemDto>>> GetMaterialConsumption([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsWarehouse) && !HasPermission(Permissions.ReportsView) && !HasPermission(Permissions.InventoryView))
            return Forbidden("شما مجوز مشاهده مصرف کالاها را ندارید.");

        var result = await _reportingService.GetMaterialConsumptionAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("inventory-health")]
    public async Task<ActionResult<InventoryHealthReportDto>> GetInventoryHealth([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsWarehouse) && !HasPermission(Permissions.ReportsView) && !HasPermission(Permissions.InventoryView))
            return Forbidden("شما مجوز مشاهده سلامت انبار را ندارید.");

        var result = await _reportingService.GetInventoryHealthAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("finance-reconciliation")]
    public async Task<ActionResult<FinanceReconciliationReportDto>> GetFinanceReconciliation([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsFinance) && !HasPermission(Permissions.ReportsView) && !HasPermission(Permissions.FinanceView))
            return Forbidden("شما مجوز مشاهده گزارشات انطباق مالی را ندارید.");

        var result = await _reportingService.GetFinanceReconciliationReportAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("telephony-activity")]
    public async Task<ActionResult<TelephonyActivityReportDto>> GetTelephonyActivity([FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.CallsView) && !HasPermission(Permissions.ReportsView))
            return Forbidden("شما مجوز مشاهده آمار تماس‌ها را ندارید.");

        var result = await _reportingService.GetTelephonyActivityReportAsync(filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("integration-health")]
    public async Task<ActionResult<IntegrationHealthReportDto>> GetIntegrationHealth(CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsAdmin) && !HasPermission(Permissions.AdminSettings) && !HasPermission(Permissions.ReportsView))
            return Forbidden("شما مجوز مشاهده سلامت اتصالات سیستم را ندارید.");

        var result = await _reportingService.GetIntegrationHealthAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("role-dashboard/{role}")]
    public async Task<ActionResult<RoleDashboardDto>> GetRoleDashboard(string role, [FromQuery] ReportFilterParams filters, CancellationToken cancellationToken)
    {
        var targetRole = role.Trim();
        var userRoles = _currentUserService.Roles;
        var isAdmin = userRoles.Contains(Roles.Admin);
        var isManager = userRoles.Contains(Roles.Manager);

        bool isAllowed = isAdmin;

        if (string.Equals(targetRole, Roles.Procurement, StringComparison.OrdinalIgnoreCase))
        {
            isAllowed = isAllowed || isManager || userRoles.Contains(Roles.Procurement) || HasPermission(Permissions.ReportsProcurement);
        }
        else if (string.Equals(targetRole, Roles.Warehouse, StringComparison.OrdinalIgnoreCase))
        {
            isAllowed = isAllowed || isManager || userRoles.Contains(Roles.Warehouse) || HasPermission(Permissions.ReportsWarehouse);
        }
        else if (string.Equals(targetRole, Roles.Finance, StringComparison.OrdinalIgnoreCase))
        {
            isAllowed = isAllowed || isManager || userRoles.Contains(Roles.Finance) || HasPermission(Permissions.ReportsFinance);
        }
        else if (string.Equals(targetRole, Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            isAllowed = isAllowed || isManager || HasPermission(Permissions.ReportsManager);
        }
        else if (string.Equals(targetRole, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            isAllowed = isAllowed || HasPermission(Permissions.ReportsAdmin);
        }
        else
        {
            isAllowed = isAllowed || HasPermission(Permissions.ReportsView);
        }

        if (!isAllowed)
            return Forbidden($"شما مجوز مشاهده داشبورد اختصاصی نقش '{targetRole}' را ندارید.");

        var result = await _reportingService.GetRoleDashboardAsync(targetRole, filters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportReportGet([FromQuery] ExportRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsExport))
            return Forbidden("شما مجوز خروجی داده‌های گزارشات را ندارید.");

        var username = _currentUserService.Username ?? "Unknown";
        var result = await _exportService.ExportReportAsync(request, username, cancellationToken);

        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportReportPost([FromBody] ExportRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.ReportsExport))
            return Forbidden("شما مجوز خروجی داده‌های گزارشات را ندارید.");

        var username = _currentUserService.Username ?? "Unknown";
        var result = await _exportService.ExportReportAsync(request, username, cancellationToken);

        return File(result.FileBytes, result.ContentType, result.FileName);
    }
}
