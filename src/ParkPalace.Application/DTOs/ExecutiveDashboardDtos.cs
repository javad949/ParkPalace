using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.DTOs;

/// <summary>Query for the executive decision dashboard (ops + optional finance).</summary>
public class ExecutiveDashboardQuery
{
    public int? FiscalYear { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public PartyCategory? PartyCategory { get; set; }
    public string? Search { get; set; }
    public string? FollowUpOwner { get; set; }
}

public class ExecutiveDashboardDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public ExecutiveViewerDto Viewer { get; set; } = new();
    public ExecutiveSlicersDto Slicers { get; set; } = new();
    public ExecutiveFreshnessDto Freshness { get; set; } = new();
    public List<ExecutiveKpiCardDto> Cards { get; set; } = new();
    public List<DashboardActionItemDto> ActionItems { get; set; } = new();
    public List<DashboardMonthlyDto> MonthlyTrend { get; set; } = new();
    public List<DashboardPartyBalanceDto> TopDebtors { get; set; } = new();
    public List<DashboardPartyBalanceDto> TopCreditors { get; set; } = new();
    public List<PartyReviewItemDto> NeedsReview { get; set; } = new();
    public List<CrmFollowUpItemDto> OpenFollowUps { get; set; } = new();
    public List<SupplierPerformanceReportItemDto> TopSuppliersOps { get; set; } = new();
    public List<MaterialConsumptionReportItemDto> CriticalMaterials { get; set; } = new();
    public ExecutiveDrillLinksDto DrillLinks { get; set; } = new();
    public ExecutiveComparisonWindowDto Comparison { get; set; } = new();
    public string? FinanceAccessNote { get; set; }
}

public class ExecutiveViewerDto
{
    public bool HasPartyLedgerView { get; set; }
    public bool CanViewOps { get; set; }
    public List<string> Roles { get; set; } = new();
    public string PermissionsNote { get; set; } = string.Empty;
}

public class ExecutiveSlicersDto
{
    public List<DashboardFiscalYearDto> FiscalYears { get; set; } = new();
    public int? ActiveFiscalYearId { get; set; }
    public List<ExecutiveSlicerStateDto> Items { get; set; } = new();
}

public class ExecutiveSlicerStateDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? DisabledReason { get; set; }
    /// <summary>Which KPI surfaces this slicer actually filters, e.g. finance-window, needs-review-only, ops-date.</summary>
    public string? AppliesTo { get; set; }
}

public class ExecutiveFreshnessDto
{
    public DateTime? LastSuccessfulReadUtc { get; set; }
    public string? FiscalYearLabel { get; set; }
    public string FinanceFreshness { get; set; } = string.Empty;
    public string FinanceFreshnessDetail { get; set; } = string.Empty;
    public List<string> CompletenessFlags { get; set; } = new();
    public string OpsFreshnessBadge { get; set; } = "داده‌های زنده CRM";
}

public class ExecutiveKpiCardDto
{
    public string ContractId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public double? NumericValue { get; set; }
    public string? Unit { get; set; }
    public FinancialFigureStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public string? DrillTarget { get; set; }
    /// <summary>Optional query fragment for filtered drill (e.g. status=Issued).</summary>
    public string? DrillQuery { get; set; }
    public decimal? PreviousPeriodAmount { get; set; }
    public string? PreviousPeriodNote { get; set; }
    public DateTime? LastSuccessfulReadUtc { get; set; }
    public string? ValidationCause { get; set; }
}

public class ExecutiveDrillLinksDto
{
    public string FinanceDashboard { get; set; } = "financeDashboard";
    public string Orders { get; set; } = "orders";
    public string Warehouse { get; set; } = "warehouse";
    public string FinanceMatching { get; set; } = "finance";
    public string Calls { get; set; } = "calls";
    public string PartyLedgerTemplate { get; set; } = "/api/v1/finance/dashboard/parties/{partyId}/ledger";
}

public class ExecutiveComparisonWindowDto
{
    public DateTime? CurrentFromUtc { get; set; }
    public DateTime? CurrentToUtc { get; set; }
    public DateTime? PreviousFromUtc { get; set; }
    public DateTime? PreviousToUtc { get; set; }
    public string? StatusNote { get; set; }
    public bool HasEquivalentPreviousPeriod { get; set; }
}
