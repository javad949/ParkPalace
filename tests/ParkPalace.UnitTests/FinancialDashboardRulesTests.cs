using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Financial;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class FinancialDashboardRulesTests
{
    [Fact]
    public void EquivalentPeriod_UsesSameElapsedSpan_AndDoesNotInventASumOfBalances()
    {
        var currentStart = new DateTime(2026, 3, 21);
        var currentEnd = new DateTime(2027, 3, 21);
        var previousStart = new DateTime(2025, 3, 21);
        var previousEnd = new DateTime(2026, 3, 21);
        var asOf = new DateTime(2026, 11, 5);

        var window = FiscalPeriodComparison.CompareElapsed(currentStart, currentEnd, previousStart, previousEnd, asOf);

        window.CurrentFrom.Should().Be(currentStart);
        window.CurrentTo.Should().Be(asOf);
        window.PreviousFrom.Should().Be(previousStart);
        (window.PreviousTo - window.PreviousFrom).Should().Be(window.Elapsed);
        (window.CurrentTo - window.CurrentFrom).Should().Be(window.Elapsed);
        window.Elapsed.Should().Be(asOf - currentStart);
    }

    [Fact]
    public void EquivalentPeriod_DoesNotRunPastThePreviousYearEnd()
    {
        var window = FiscalPeriodComparison.CompareElapsed(
            new DateTime(2026, 3, 21),
            new DateTime(2027, 3, 21),
            new DateTime(2025, 3, 21),
            new DateTime(2025, 6, 1),
            new DateTime(2026, 11, 5));

        window.PreviousTo.Should().Be(new DateTime(2025, 6, 1));
    }

    [Fact]
    public void Access_PartyLedgerRequiresDedicatedPermission_NotViewOrReconcileOrAdmin()
    {
        var managerReportOnly = new StubUser(true, new[] { Roles.Manager }, new[] { Permissions.ReportsManager });
        FinancialAccess.CanViewPartyLedger(managerReportOnly).Should().BeFalse();
        FinancialAccess.CanViewTotals(managerReportOnly).Should().BeFalse();
        FinancialAccess.CanViewDocumentLines(managerReportOnly).Should().BeFalse();

        var financeViewOnly = new StubUser(true, new[] { Roles.Finance }, new[] { Permissions.FinanceView });
        FinancialAccess.CanViewPartyLedger(financeViewOnly).Should().BeFalse();
        FinancialAccess.CanViewFinanceOverview(financeViewOnly).Should().BeTrue();

        var reconcileOnly = new StubUser(true, new[] { Roles.Finance }, new[] { Permissions.FinanceReconcile });
        FinancialAccess.CanViewPartyLedger(reconcileOnly).Should().BeFalse();
        FinancialAccess.CanReconcile(reconcileOnly).Should().BeTrue();

        var adminOnly = new StubUser(true, new[] { Roles.Admin }, new[] { Permissions.AdminUsers, Permissions.AdminSettings });
        FinancialAccess.CanViewPartyLedger(adminOnly).Should().BeFalse();

        var ledger = new StubUser(true, new[] { Roles.Finance }, new[] { Permissions.FinancePartyLedgerView });
        FinancialAccess.CanViewPartyLedger(ledger).Should().BeTrue();
        FinancialAccess.CanViewTotals(ledger).Should().BeTrue();
        FinancialAccess.CanViewDocumentLines(ledger).Should().BeTrue();

        var warehouse = new StubUser(true, new[] { Roles.Warehouse }, new[] { Permissions.WarehouseView });
        FinancialAccess.CanViewTotals(warehouse).Should().BeFalse();
        FinancialAccess.CanViewDocumentLines(warehouse).Should().BeFalse();
    }

    [Fact]
    public async Task Dashboard_DoesNotPresentBlockedMetricsAsZero_AndListsUnmappedParties()
    {
        await using var db = NewDb();
        db.Parties.Add(new Party { DisplayName = "بدون نگاشت", Category = PartyCategory.Supplier, IsActive = true });
        db.Parties.Add(new Party { DisplayName = "نگاشت‌شده", Category = PartyCategory.Customer, IsActive = true, SepidarPartyRef = "55", SepidarCode = "55" });
        var shared = Guid.NewGuid().ToString();
        db.Parties.Add(new Party { DisplayName = "تکرار الف", IsActive = true, SepidarPartyRef = shared, SepidarCode = "9" });
        db.Parties.Add(new Party { DisplayName = "تکرار ب", IsActive = true, SepidarPartyRef = shared, SepidarCode = "8" });
        await db.SaveChangesAsync();

        var service = new FinancialDashboardService(db, new FakeAdapter("SepidarLiveFinancialAdapter", connected: false, available: true, balance: 10m));
        var dashboard = await service.GetDashboardAsync(new FinancialDashboardQuery());

        dashboard.SepidarReadAttempted.Should().BeFalse();
        dashboard.Freshness.Should().Be("NotVerified");
        dashboard.LastSuccessfulFinancialReadUtc.Should().BeNull();
        dashboard.Cards.Should().OnlyContain(c => c.Amount == null && c.Status == FinancialFigureStatus.Blocked);
        dashboard.Cards.Should().NotContain(c => c.Status == FinancialFigureStatus.Zero);
        dashboard.DueDates.Status.Should().Be(FinancialFigureStatus.Blocked);
        dashboard.LargestBalances.Amount.Should().BeNull();
        dashboard.UnmappedPartyCount.Should().Be(1);
        dashboard.NeedsReview.Should().Contain(x => x.DisplayName == "بدون نگاشت");
        dashboard.NeedsReview.Should().Contain(x => x.DisplayName == "تکرار الف");
        dashboard.NeedsReview.Should().NotContain(x => x.DisplayName == "نگاشت‌شده");
        dashboard.CatalogFiscalYear!.LiveRead.Should().BeFalse();
        dashboard.CatalogFiscalYear.FiscalYearId.Should().Be(10);
    }

    [Fact]
    public void Classifier_KeepsTradeTitles_AndDropsTaxPayrollAndDeposits()
    {
        TradeAccountClassifier.Side("حسابهای دریافتنی تجاری سایر اشخاص").Should().Be(TradeAccountClassifier.Receivable);
        TradeAccountClassifier.Side("اسناد پرداختنی تجاری").Should().Be(TradeAccountClassifier.Payable);
        TradeAccountClassifier.Side("سایر حسابهای دریافتنی - مالیات بر ارزش افزوده").Should().BeNull();
        TradeAccountClassifier.Side("سپرده بیمه پرداختنی").Should().BeNull();
        TradeAccountClassifier.Side("پیش پرداخت خرید").Should().BeNull();
    }

    [Fact]
    public async Task Dashboard_ShowsLiveTradeNets_AndLeavesCashAndDueDatesBlocked()
    {
        await using var db = NewDb();
        db.Parties.Add(new Party { DisplayName = "آرد", IsActive = true, SepidarCode = "1010", SepidarPartyRef = "5" });
        db.Parties.Add(new Party { DisplayName = "تکرار ۱", IsActive = true, SepidarCode = "2020" });
        db.Parties.Add(new Party { DisplayName = "تکرار ۲", IsActive = true, SepidarCode = "2020" });
        await db.SaveChangesAsync();

        var service = new FinancialDashboardService(
            db,
            new FakeAdapter("SepidarFixtureFinancialAdapter", connected: true, available: true, balance: 999m),
            new FakeLedger());
        var dashboard = await service.GetDashboardAsync(new FinancialDashboardQuery());

        dashboard.SepidarReadAttempted.Should().BeTrue();
        dashboard.Freshness.Should().Be("LiveRead");
        dashboard.Cards.Single(c => c.Code == "receivables").Amount.Should().Be(1500m);
        dashboard.Cards.Single(c => c.Code == "receivables").Status.Should().Be(FinancialFigureStatus.Verified);
        dashboard.Cards.Single(c => c.Code == "payables").Amount.Should().Be(400m);
        dashboard.Cards.Single(c => c.Code == "receipts").Amount.Should().BeNull();
        dashboard.Cards.Single(c => c.Code == "receipts").Status.Should().Be(FinancialFigureStatus.Blocked);
        dashboard.Cards.Single(c => c.Code == "overdue").Status.Should().Be(FinancialFigureStatus.Blocked);
        dashboard.Cards.Should().NotContain(c => c.Amount == 999m);
        dashboard.CatalogFiscalYear!.LiveRead.Should().BeTrue();
        dashboard.CatalogFiscalYear.Title.Should().Be("1405");
        dashboard.CurrentPeriodFromUtc.Should().Be(new DateTime(2026, 3, 21));
        dashboard.PreviousPeriodFromUtc.Should().Be(new DateTime(2025, 3, 21));
        dashboard.AccountScope.Should().Contain(a => a.Code == "1015" && a.Side == TradeAccountClassifier.Receivable);
        dashboard.TopParties.Should().Contain(p => p.Code == "1010" && p.CrmName == "آرد");
        dashboard.TopParties.Should().Contain(p => p.Code == "2020" && p.PartyId == null);
        dashboard.Monthly.Should().Contain(m => m.Side == TradeAccountClassifier.Receivable && m.Net == 100m && !m.PreviousPeriod);
    }

    [Fact]
    public async Task Dashboard_ConnectionFailureIsNotAZeroBalance()
    {
        await using var db = NewDb();
        var service = new FinancialDashboardService(
            db,
            new FakeAdapter("SepidarLiveFinancialAdapter", connected: false, available: false, balance: 0m),
            new FakeLedger { Connected = false });
        var dashboard = await service.GetDashboardAsync(new FinancialDashboardQuery());

        dashboard.Freshness.Should().Be("Unavailable");
        dashboard.Cards.Single(c => c.Code == "receivables").Amount.Should().BeNull();
        dashboard.Cards.Single(c => c.Code == "receivables").Status.Should().Be(FinancialFigureStatus.Unavailable);
        dashboard.Cards.Should().NotContain(c => c.Status == FinancialFigureStatus.Zero);
    }

    [Fact]
    public async Task Dashboard_Search_ReadsLiveSepidarHits_AndLinksUniqueCrmParty()
    {
        await using var db = NewDb();
        db.Parties.Add(new Party { DisplayName = "افق روشن", Category = PartyCategory.Supplier, IsActive = true });
        db.Parties.Add(new Party { DisplayName = "دیگر", IsActive = true, SepidarPartyRef = "1", SepidarCode = "1", SepidarMappingVerified = true });
        await db.SaveChangesAsync();

        var service = new FinancialDashboardService(
            db,
            new FakeAdapter("SepidarLiveFinancialAdapter", connected: true, available: true, balance: 0m),
            new FakeLedger(),
            new FakePartyLookup());
        var dashboard = await service.GetDashboardAsync(new FinancialDashboardQuery { Search = "افق روشن" });

        dashboard.NeedsReview.Should().ContainSingle(x => x.DisplayName == "افق روشن");
        dashboard.SepidarSearchHits.Should().ContainSingle(h => h.Code == "10749" && h.Title.Contains("افق روشن"));
        dashboard.SepidarSearchHits[0].PartyId.Should().NotBeNull();
        dashboard.SepidarSearchHits[0].CrmName.Should().Be("افق روشن");
        dashboard.SepidarSearchHits[0].MappingVerified.Should().BeFalse();
        dashboard.ActiveFilters.Should().Contain(f => f.Key == "search");
    }

    private sealed class FakePartyLookup : ISepidarPartyLookup
    {
        public Task<SepidarCodeLookupDto> FindByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(new SepidarCodeLookupDto { Connected = true, Unique = true, Code = code, PartyRef = "857", Title = "توسعه رايانه افق روشن شركت" });

        public Task<SepidarPartySearchResultDto> SearchAsync(string? search, CancellationToken ct = default)
            => Task.FromResult(new SepidarPartySearchResultDto
            {
                Connected = true,
                Message = "از سپیدار 1 مورد خوانده شد.",
                Hits =
                {
                    new SepidarPartySearchHitDto
                    {
                        Code = "10749",
                        Title = "توسعه رايانه افق روشن شركت",
                        PartyRef = "857"
                    }
                }
            });

        public Task<PagedResult<SepidarPartyListRow>> ListPagedAsync(DirectoryListQuery query, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<SepidarPartyListRow> { Items = new List<SepidarPartyListRow>(), TotalCount = 0, PageNumber = 1, PageSize = query.PageSize });
    }

    [Fact]
    public async Task PartyLedger_UsesReaderTotals_AndIgnoresFixtureAmounts()
    {
        await using var db = NewDb();
        var party = new Party { DisplayName = "آرد نمونه", IsActive = true, SepidarPartyRef = "1001", SepidarCode = "1001" };
        db.Parties.Add(party);
        await db.SaveChangesAsync();

        var service = new FinancialDashboardService(
            db,
            new FakeAdapter("SepidarFixtureFinancialAdapter", connected: true, available: true, balance: 187500000m),
            new FakeLedger());
        var ledger = await service.GetPartyLedgerAsync(party.Id, includeDocuments: true, page: 1, pageSize: 20);

        ledger!.ClosingBalance.Amount.Should().Be(12m);
        ledger.LinesStatus.Should().Be(FinancialFigureStatus.Verified);
        ledger.Lines.Should().ContainSingle();
        ledger.Lines[0].RunningBalance.Should().Be(12m);
        ledger.ClosingBalance.Amount.Should().NotBe(187500000m);
    }

    [Fact]
    public async Task PartyLedger_IgnoresFixtureAmounts_AndConnectionFailureIsNotZero()
    {
        await using var db = NewDb();
        var party = new Party { DisplayName = "آرد نمونه", IsActive = true, SepidarPartyRef = "1001", SepidarCode = "1001" };
        db.Parties.Add(party);
        db.PartyContacts.Add(new PartyContact { PartyId = party.Id, PersonName = "نماینده", IsActive = true, IsPrimary = true });
        var call = new CallRecord
        {
            UniqueId = "1.1",
            LinkedId = "1.1",
            CallerNumber = "021",
            DestinationNumber = "224",
            PartyId = party.Id,
            StartTimeUtc = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc)
        };
        db.CallRecords.Add(call);
        db.CallNotes.Add(new CallNote
        {
            CallRecordId = call.Id,
            Username = "finance",
            NoteText = "پیگیری مانده",
            FollowUpRequired = true,
            FollowUpAssignedTo = "واحد مالی",
            FollowUpStatus = FollowUpStatus.Pending
        });
        await db.SaveChangesAsync();

        var fixture = new FinancialDashboardService(db, new FakeAdapter("SepidarFixtureFinancialAdapter", connected: true, available: true, balance: 187500000m));
        var hidden = await fixture.GetPartyLedgerAsync(party.Id, includeDocuments: true, page: 1, pageSize: 20);
        hidden!.LinesStatus.Should().Be(FinancialFigureStatus.Unverified);
        hidden.ClosingBalance.Amount.Should().BeNull();
        hidden.Lines.Should().BeEmpty();
        hidden.TotalLineCount.Should().BeNull();
        hidden.Contacts.Should().ContainSingle(c => c.PersonName == "نماینده");
        hidden.Calls.Should().ContainSingle();
        hidden.FollowUps.Should().ContainSingle(f => f.Owner == "واحد مالی");

        var liveDown = new FinancialDashboardService(db, new FakeAdapter("SepidarLiveFinancialAdapter", connected: false, available: false, balance: 0m));
        var failed = await liveDown.GetPartyLedgerAsync(party.Id, includeDocuments: true, page: 1, pageSize: 20);
        failed!.LinesStatus.Should().Be(FinancialFigureStatus.Unavailable);
        failed.OpeningBalance.Amount.Should().BeNull();
        failed.OpeningBalance.Status.Should().NotBe(FinancialFigureStatus.Zero);
    }

    private static CrmDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CrmDbContext(options);
    }

    private sealed class FakeAdapter : ISepidarFinancialAdapter
    {
        private readonly bool _connected;
        private readonly bool _available;
        private readonly decimal _balance;

        public FakeAdapter(string name, bool connected, bool available, decimal balance)
        {
            AdapterName = name;
            _connected = connected;
            _available = available;
            _balance = balance;
        }

        public string AdapterName { get; }

        public Task<List<SepidarCandidateInvoiceDto>> GetCandidateInvoicesAsync(DateTime? sinceDateUtc, CancellationToken ct = default)
            => Task.FromResult(new List<SepidarCandidateInvoiceDto>());

        public Task<SepidarPartyFinancialHistoryDto> GetPartyFinancialHistoryAsync(string sepidarPartyRef, string? sepidarCode, int? fiscalYear = null, CancellationToken ct = default)
            => Task.FromResult(new SepidarPartyFinancialHistoryDto
            {
                IsConnected = _connected,
                IsAvailable = _available,
                OpeningBalance = _balance,
                Balance = _balance,
                StatusMessage = _connected ? "unavailable contract" : "connection failed"
            });
    }

    private sealed class FakeLedger : ISepidarLedgerReader
    {
        public bool Connected { get; init; } = true;

        public Task<SepidarLedgerSnapshot> ReadAsync(int? fiscalYear, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            if (!Connected)
            {
                return Task.FromResult(new SepidarLedgerSnapshot
                {
                    Connected = false,
                    Failure = "ارتباط برقرار نشد."
                });
            }

            return Task.FromResult(new SepidarLedgerSnapshot
            {
                Connected = true,
                FiguresRead = true,
                Selected = new SepidarFiscalYearRow
                {
                    Id = 10,
                    Title = "1405",
                    Start = new DateTime(2026, 3, 21),
                    EndInclusive = new DateTime(2027, 3, 20)
                },
                Previous = new SepidarFiscalYearRow
                {
                    Id = 9,
                    Title = "1404",
                    Start = new DateTime(2025, 3, 21),
                    EndInclusive = new DateTime(2026, 3, 20)
                },
                CurrentFrom = new DateTime(2026, 3, 21),
                CurrentTo = new DateTime(2026, 11, 5),
                PreviousFrom = new DateTime(2025, 3, 21),
                PreviousTo = new DateTime(2025, 11, 5),
                ComparisonNote = "بازه معادل",
                ReceivableNet = 1500m,
                PayableNet = 400m,
                PreviousReceivableNet = 900m,
                PreviousPayableNet = 300m,
                Accounts = new List<SepidarAccountScope>
                {
                    new() { AccountId = 1, Code = "1015", Title = "حسابهای دریافتنی تجاری", Side = TradeAccountClassifier.Receivable }
                },
                Monthly = new List<SepidarMonthlyNet>
                {
                    new() { MonthStart = new DateTime(2026, 4, 1), Side = TradeAccountClassifier.Receivable, Net = 100m }
                },
                TopParties = new List<SepidarPartyNet>
                {
                    new() { Code = "1010", Title = "آرد سپیدار", Net = 1500m, Side = TradeAccountClassifier.Receivable },
                    new() { Code = "2020", Title = "مبهم", Net = 50m, Side = TradeAccountClassifier.Payable }
                }
            });
        }

        public Task<SepidarPartyLedgerResult> ReadPartyAsync(string sepidarPartyRef, string? sepidarCode, bool includeLines, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new SepidarPartyLedgerResult
            {
                Connected = true,
                Found = true,
                HasDetailLink = true,
                OpeningNet = 10m,
                PeriodDebit = 5m,
                PeriodCredit = 3m,
                TotalLines = 1,
                Lines = includeLines
                    ? new List<SepidarPartyLine>
                    {
                        new() { Reference = "9", Date = new DateTime(2026, 4, 1), Number = "100", Debit = 5m, Credit = 3m, RunningNet = 12m, Side = TradeAccountClassifier.Receivable }
                    }
                    : new List<SepidarPartyLine>()
            });
    }

    private sealed class StubUser : ICurrentUserService
    {
        public StubUser(bool authenticated, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
        {
            IsAuthenticated = authenticated;
            Roles = roles;
            Permissions = permissions;
        }

        public Guid? UserId => Guid.NewGuid();
        public string? Username => "test";
        public string? DisplayName => "test";
        public string? AdSid => null;
        public string ActiveRole => Roles.FirstOrDefault() ?? "";
        public string? Extension => null;
        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<string> AvailableRoles => Roles;
        public IReadOnlyList<string> Permissions { get; }
        public bool IsAuthenticated { get; }
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
