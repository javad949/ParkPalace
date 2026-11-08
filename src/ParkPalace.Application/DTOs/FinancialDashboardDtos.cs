using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.DTOs;

public enum FinancialFigureStatus
{
    Verified = 1,
    Unverified = 2,
    Blocked = 3,
    Unavailable = 4,
    NoData = 5,
    Zero = 6
}

public class FinancialFigureDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public FinancialFigureStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class FinancialDashboardQuery
{
    public int? FiscalYear { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public PartyCategory? PartyCategory { get; set; }
    public string? Search { get; set; }
    public string? FollowUpOwner { get; set; }
}

public class ActiveFilterDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class VerifiedFiscalYearDto
{
    public int FiscalYearId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public bool LiveRead { get; set; }
}

public class PartyReviewItemDto
{
    public Guid PartyId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string? SepidarCode { get; set; }
    public string? SepidarPartyRef { get; set; }
}

public class CrmFollowUpItemDto
{
    public Guid NoteId { get; set; }
    public Guid? PartyId { get; set; }
    public string? PartyName { get; set; }
    public string? Owner { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public string NoteText { get; set; } = string.Empty;
    public string Source { get; set; } = "CRM";
}

public class FinancialDashboardDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public bool SepidarReadAttempted { get; set; }
    public string Freshness { get; set; } = string.Empty;
    public string FreshnessDetail { get; set; } = string.Empty;
    public DateTime? LastSuccessfulFinancialReadUtc { get; set; }
    public VerifiedFiscalYearDto? CatalogFiscalYear { get; set; }
    public string? ComparisonStatus { get; set; }
    public DateTime? CurrentPeriodFromUtc { get; set; }
    public DateTime? CurrentPeriodToUtc { get; set; }
    public DateTime? PreviousPeriodFromUtc { get; set; }
    public DateTime? PreviousPeriodToUtc { get; set; }
    /// <summary>Trade AR net for the equivalent previous fiscal window; null if window unavailable.</summary>
    public decimal? PreviousReceivableNet { get; set; }
    /// <summary>Trade AP net for the equivalent previous fiscal window; null if window unavailable.</summary>
    public decimal? PreviousPayableNet { get; set; }
    public List<DashboardFiscalYearDto> FiscalYears { get; set; } = new();
    public List<DashboardAccountScopeDto> AccountScope { get; set; } = new();
    public List<FinancialFigureDto> Cards { get; set; } = new();
    public List<FinancialFigureDto> MonthlyComparison { get; set; } = new();
    public List<DashboardMonthlyDto> Monthly { get; set; } = new();
    public FinancialFigureDto DueDates { get; set; } = new();
    public FinancialFigureDto LargestBalances { get; set; } = new();
    public List<DashboardPartyBalanceDto> TopParties { get; set; } = new();
    public List<DashboardSepidarSearchHitDto> SepidarSearchHits { get; set; } = new();
    public string? SepidarSearchMessage { get; set; }
    public List<PartyReviewItemDto> NeedsReview { get; set; } = new();
    public bool ReviewSampleCapped { get; set; }
    public int UnmappedPartyCount { get; set; }
    public List<CrmFollowUpItemDto> OpenFollowUps { get; set; } = new();
    public List<ActiveFilterDto> ActiveFilters { get; set; } = new();
}

public class DashboardSepidarSearchHitDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? PartyRef { get; set; }
    public Guid? PartyId { get; set; }
    public string? CrmName { get; set; }
    public bool MappingVerified { get; set; }
}

public class DashboardFiscalYearDto
{
    public int FiscalYearId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class DashboardAccountScopeDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
}

public class DashboardMonthlyDto
{
    public DateTime MonthStart { get; set; }
    public string Side { get; set; } = string.Empty;
    public decimal Net { get; set; }
    public bool PreviousPeriod { get; set; }
}

public class DashboardPartyBalanceDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Net { get; set; }
    public string Side { get; set; } = string.Empty;
    public Guid? PartyId { get; set; }
    public string? CrmName { get; set; }
}

public class SepidarPartyFinancialMovementDto
{
    public string VoucherItemId { get; set; } = string.Empty;
    public DateTime VoucherDateUtc { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal CumulativeBalance { get; set; }
}

public class SepidarPartyFinancialHistoryDto
{
    public bool IsConnected { get; set; }
    public bool IsAvailable { get; set; }
    public string? StatusMessage { get; set; }
    public string? UnverifiedDependencyDetails { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
    public List<SepidarPartyFinancialMovementDto> Movements { get; set; } = new();
}

public class PartyLedgerLineDto
{
    public string? SepidarReference { get; set; }
    public DateTime? DateUtc { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentType { get; set; }
    public string? Description { get; set; }
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
    public decimal? RunningBalance { get; set; }
}

public class PartyCrmContactDto
{
    public Guid Id { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? Position { get; set; }
}

public class PartyCrmCallDto
{
    public Guid Id { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public string CallerNumber { get; set; } = string.Empty;
    public string DestinationNumber { get; set; } = string.Empty;
    public string? CallerName { get; set; }
}

public class PartyLedgerDto
{
    public Guid PartyId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? SepidarPartyRef { get; set; }
    public string? SepidarCode { get; set; }
    public bool MappingVerified { get; set; }
    public bool DocumentsAuthorized { get; set; }
    public FinancialFigureDto OpeningBalance { get; set; } = new();
    public FinancialFigureDto TotalDebit { get; set; } = new();
    public FinancialFigureDto TotalCredit { get; set; } = new();
    public FinancialFigureDto Receipts { get; set; } = new();
    public FinancialFigureDto Payments { get; set; } = new();
    public FinancialFigureDto ClosingBalance { get; set; } = new();
    public FinancialFigureStatus LinesStatus { get; set; }
    public string LinesReason { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? TotalLineCount { get; set; }
    public List<PartyLedgerLineDto> Lines { get; set; } = new();
    public List<PartyCrmContactDto> Contacts { get; set; } = new();
    public List<PartyCrmCallDto> Calls { get; set; } = new();
    public List<CrmFollowUpItemDto> FollowUps { get; set; } = new();
}
