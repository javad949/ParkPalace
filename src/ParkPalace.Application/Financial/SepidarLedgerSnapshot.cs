namespace ParkPalace.Application.Financial;

public sealed class SepidarLedgerSnapshot
{
    public bool Connected { get; init; }
    public string? Failure { get; init; }
    public DateTime ReadAtUtc { get; init; }
    public List<SepidarFiscalYearRow> Years { get; init; } = new();
    public SepidarFiscalYearRow? Selected { get; init; }
    public SepidarFiscalYearRow? Previous { get; init; }
    public DateTime? CurrentFrom { get; init; }
    public DateTime? CurrentTo { get; init; }
    public DateTime? PreviousFrom { get; init; }
    public DateTime? PreviousTo { get; init; }
    public string ComparisonNote { get; init; } = string.Empty;
    public List<SepidarAccountScope> Accounts { get; init; } = new();
    public bool FiguresRead { get; init; }
    public decimal ReceivableNet { get; init; }
    public decimal PayableNet { get; init; }
    public decimal? PreviousReceivableNet { get; init; }
    public decimal? PreviousPayableNet { get; init; }
    public List<SepidarMonthlyNet> Monthly { get; init; } = new();
    public List<SepidarPartyNet> TopParties { get; init; } = new();
}

public sealed class SepidarFiscalYearRow
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime Start { get; init; }
    public DateTime EndInclusive { get; init; }
}

public sealed class SepidarAccountScope
{
    public int AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
}

public sealed class SepidarMonthlyNet
{
    public DateTime MonthStart { get; init; }
    public string Side { get; init; } = string.Empty;
    public decimal Net { get; init; }
    public bool PreviousPeriod { get; init; }
}

public sealed class SepidarPartyNet
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal Net { get; init; }
    public string Side { get; init; } = string.Empty;
}

public sealed class SepidarPartyLedgerResult
{
    public bool Connected { get; init; }
    public bool Found { get; init; }
    public bool CodeMismatch { get; init; }
    public string? Failure { get; init; }
    public string? ResolvedCode { get; init; }
    public string? ResolvedTitle { get; init; }
    public bool HasDetailLink { get; init; }
    public decimal OpeningNet { get; init; }
    public decimal PeriodDebit { get; init; }
    public decimal PeriodCredit { get; init; }
    public int TotalLines { get; init; }
    public List<SepidarPartyLine> Lines { get; init; } = new();
}

public sealed class SepidarPartyLine
{
    public string Reference { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Number { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Side { get; init; } = string.Empty;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal RunningNet { get; init; }
}
