using FluentAssertions;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Financial;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class ExecutiveDashboardAccessTests
{
    [Fact]
    public void Ops_Allows_OrganizationManager_And_ReportPerms_Without_Ledger()
    {
        var org = new StubUser(true, new[] { Roles.OrganizationManager }, new[] { Permissions.OrgReportsView });
        ExecutiveDashboardAccess.CanViewOps(org).Should().BeTrue();
        ExecutiveDashboardAccess.CanViewFinanceFigures(org).Should().BeFalse();
    }

    [Fact]
    public void FinanceFigures_Require_PartyLedgerView_Not_Admin_Alone()
    {
        var admin = new StubUser(true, new[] { Roles.Admin }, new[] { Permissions.AdminUsers, Permissions.ReportsAdmin });
        ExecutiveDashboardAccess.CanViewOps(admin).Should().BeTrue();
        ExecutiveDashboardAccess.CanViewFinanceFigures(admin).Should().BeFalse();

        // Spoofed Finance role string without PartyLedgerView still cannot open figures.
        var spoofedFinanceRole = new StubUser(true, new[] { Roles.Finance }, new[] { Permissions.FinanceView, Permissions.ReportsFinance });
        ExecutiveDashboardAccess.CanViewFinanceFigures(spoofedFinanceRole).Should().BeFalse();

        var ledger = new StubUser(true, new[] { Roles.Finance }, new[] { Permissions.FinancePartyLedgerView });
        ExecutiveDashboardAccess.CanViewFinanceFigures(ledger).Should().BeTrue();
    }

    [Fact]
    public async Task Service_WithoutFinance_DoesNotExposeSepidarAmounts_AndBlocksSalesPurchase()
    {
        var reporting = new FakeReporting();
        var finance = new FakeFinance();
        var svc = new ExecutiveDashboardService(reporting, finance);

        var dto = await svc.GetAsync(new ExecutiveDashboardQuery(), includeFinance: false);

        finance.Called.Should().BeFalse();
        dto.Cards.Should().Contain(c => c.ContractId == "ED-01" && c.NumericValue == 2); // Critical overdue actions
        dto.Cards.Should().Contain(c => c.ContractId == "ED-01b" && c.NumericValue == 3);
        dto.Cards.Should().Contain(c => c.ContractId == "ED-06" && c.Amount == null && c.NumericValue == null && c.Status == FinancialFigureStatus.Blocked);
        dto.Cards.Should().Contain(c => c.ContractId == "ED-11" && c.Amount == null && c.Status == FinancialFigureStatus.Blocked);
        dto.Cards.Should().Contain(c => c.ContractId == "ED-12" && c.Amount == null);
        dto.Slicers.Items.Should().Contain(s => s.Key == "status" && s.Enabled == false);
        dto.Slicers.Items.Should().Contain(s => s.Key == "orgUnit" && s.Enabled == false);
        dto.Slicers.Items.Should().Contain(s => s.Key == "partyType" && s.Enabled == false);
        dto.FinanceAccessNote.Should().NotBeNullOrWhiteSpace();
        dto.TopDebtors.Should().BeEmpty();
    }

    [Fact]
    public async Task Service_WithFinance_RemapsVerified_AddsValidationMeta_BlocksYoYWithoutPreviousWindow()
    {
        var reporting = new FakeReporting();
        var finance = new FakeFinance
        {
            Dashboard = new FinancialDashboardDto
            {
                Freshness = "LiveRead",
                FreshnessDetail = "test",
                LastSuccessfulFinancialReadUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                ComparisonStatus = "بازه معادل سال قبل موجود نیست.",
                Cards = new List<FinancialFigureDto>
                {
                    new() { Code = "receivables", Title = "مطالبات", Amount = 1000m, Status = FinancialFigureStatus.Verified, Reason = "live" },
                    new() { Code = "payables", Title = "بدهی", Amount = 500m, Status = FinancialFigureStatus.Verified, Reason = "live" },
                    new() { Code = "receipts", Title = "دریافت", Amount = 0m, Status = FinancialFigureStatus.Blocked, Reason = "blocked" },
                    new() { Code = "payments", Title = "پرداخت", Amount = 0m, Status = FinancialFigureStatus.Blocked, Reason = "blocked" },
                    new() { Code = "overdue", Title = "سررسید", Amount = 0m, Status = FinancialFigureStatus.Blocked, Reason = "blocked" }
                },
                TopParties = new List<DashboardPartyBalanceDto>
                {
                    new() { Code = "1", Title = "A", Net = 10, Side = TradeAccountClassifier.Receivable },
                    new() { Code = "2", Title = "B", Net = 20, Side = TradeAccountClassifier.Payable }
                }
            }
        };
        var svc = new ExecutiveDashboardService(reporting, finance);

        var dto = await svc.GetAsync(new ExecutiveDashboardQuery(), includeFinance: true);

        finance.Called.Should().BeTrue();
        var ar = dto.Cards.Should().ContainSingle(c => c.ContractId == "ED-06").Subject;
        ar.Amount.Should().Be(1000m);
        ar.Status.Should().Be(FinancialFigureStatus.Unverified);
        ar.ValidationCause.Should().NotBeNullOrWhiteSpace();
        ar.LastSuccessfulReadUtc.Should().Be(finance.Dashboard.LastSuccessfulFinancialReadUtc);
        ar.Source.Should().Contain("Sepidar");

        dto.Cards.Where(c => c.Status == FinancialFigureStatus.Blocked)
            .Should().OnlyContain(c => c.Amount == null && c.NumericValue == null);

        dto.Cards.Should().Contain(c => c.ContractId == "ED-08" && c.Status == FinancialFigureStatus.Blocked && c.Amount == null);
        dto.Comparison.HasEquivalentPreviousPeriod.Should().BeFalse();
        dto.Slicers.Items.Should().Contain(s => s.Key == "partyType" && s.AppliesTo == "needs-review-only");
        dto.TopDebtors.Should().ContainSingle(p => p.Code == "1");
        dto.TopCreditors.Should().ContainSingle(p => p.Code == "2");
    }

    [Fact]
    public async Task Service_YoY_OnlyWhenEquivalentPreviousPeriodExists()
    {
        var reporting = new FakeReporting();
        var finance = new FakeFinance
        {
            Dashboard = new FinancialDashboardDto
            {
                Freshness = "LiveRead",
                PreviousPeriodFromUtc = new DateTime(2025, 3, 21),
                PreviousPeriodToUtc = new DateTime(2025, 11, 1),
                CurrentPeriodFromUtc = new DateTime(2026, 3, 21),
                CurrentPeriodToUtc = new DateTime(2026, 11, 1),
                PreviousReceivableNet = 800m,
                PreviousPayableNet = 400m,
                ComparisonStatus = "بازه معادل از FMK.FiscalYear",
                Cards = new List<FinancialFigureDto>
                {
                    new() { Code = "receivables", Title = "مطالبات", Amount = 1000m, Status = FinancialFigureStatus.Verified, Reason = "live" },
                    new() { Code = "payables", Title = "بدهی", Amount = 500m, Status = FinancialFigureStatus.Verified, Reason = "live" },
                    new() { Code = "receipts", Title = "دریافت", Amount = null, Status = FinancialFigureStatus.Blocked, Reason = "blocked" },
                    new() { Code = "payments", Title = "پرداخت", Amount = null, Status = FinancialFigureStatus.Blocked, Reason = "blocked" },
                    new() { Code = "overdue", Title = "سررسید", Amount = null, Status = FinancialFigureStatus.Blocked, Reason = "blocked" }
                }
            }
        };
        var svc = new ExecutiveDashboardService(reporting, finance);
        var dto = await svc.GetAsync(new ExecutiveDashboardQuery(), includeFinance: true);

        dto.Comparison.HasEquivalentPreviousPeriod.Should().BeTrue();
        var yoy = dto.Cards.Should().ContainSingle(c => c.ContractId == "ED-08").Subject;
        yoy.Status.Should().Be(FinancialFigureStatus.Unverified);
        yoy.PreviousPeriodAmount.Should().Be(800m);
        dto.Cards.Should().Contain(c => c.ContractId == "ED-06" && c.PreviousPeriodAmount == 800m);
        dto.Cards.Should().Contain(c => c.ContractId == "ED-07" && c.PreviousPeriodAmount == 400m);
    }

    private sealed class FakeFinance : IFinancialDashboardService
    {
        public bool Called { get; private set; }
        public FinancialDashboardDto Dashboard { get; set; } = new();

        public Task<FinancialDashboardDto> GetDashboardAsync(FinancialDashboardQuery query, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(Dashboard);
        }

        public Task<PartyLedgerDto?> GetPartyLedgerAsync(Guid partyId, bool includeDocuments, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<PartyLedgerDto?>(null);
    }

    private sealed class FakeReporting : IReportingService
    {
        public Task<ProcurementSummaryReportDto> GetProcurementSummaryAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProcurementSummaryReportDto { ActiveOrdersCount = 3, TotalPurchaseVolume = 1000m });

        public Task<List<PurchaseOrderTrendItemDto>> GetPurchaseOrderTrendsAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PurchaseOrderTrendItemDto>());

        public Task<List<SupplierPerformanceReportItemDto>> GetSupplierPerformanceAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SupplierPerformanceReportItemDto>());

        public Task<List<MaterialConsumptionReportItemDto>> GetMaterialConsumptionAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<MaterialConsumptionReportItemDto>());

        public Task<InventoryHealthReportDto> GetInventoryHealthAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new InventoryHealthReportDto { CriticalStockCount = 2 });

        public Task<FinanceReconciliationReportDto> GetFinanceReconciliationReportAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new FinanceReconciliationReportDto { OpenExceptionsCount = 1 });

        public Task<TelephonyActivityReportDto> GetTelephonyActivityReportAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new TelephonyActivityReportDto { OverdueFollowUpsCount = 4 });

        public Task<IntegrationHealthReportDto> GetIntegrationHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new IntegrationHealthReportDto());

        public Task<RoleDashboardDto> GetRoleDashboardAsync(string roleName, ReportFilterParams filters, CancellationToken cancellationToken = default)
        {
            var actions = string.Equals(roleName, "Procurement", StringComparison.OrdinalIgnoreCase)
                ? new List<DashboardActionItemDto>
                {
                    new() { Id = "po1", Title = "سفارش خرید با تأخیر: A", Severity = "Critical", TargetView = "orders" },
                    new() { Id = "po2", Title = "سفارش خرید با تأخیر: B", Severity = "Critical", TargetView = "orders" },
                    new() { Id = "po3", Title = "عادی", Severity = "Normal", TargetView = "orders" }
                }
                : new List<DashboardActionItemDto>
                {
                    new() { Id = "1", Title = "تست اقدام", Severity = "Warning", TargetView = "finance" }
                };

            return Task.FromResult(new RoleDashboardDto { Role = roleName, ActionItems = actions });
        }
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
