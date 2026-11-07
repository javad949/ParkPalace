using ParkPalace.Application.DTOs;
using ParkPalace.Application.Financial;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Infrastructure.Services;

/// <summary>
/// Composes CRM ops KPIs with optional Sepidar finance figures.
/// Never invents Sepidar money; remaps live trade AR/AP to Unverified (NeedsValidation).
/// </summary>
public class ExecutiveDashboardService : IExecutiveDashboardService
{
    private const string ValidationCauseVoucherState =
        "وضعیت سند (ACC.Voucher.State) فیلتر نشده و قرارداد حسابداری واحد مالی امضا نشده است.";

    private readonly IReportingService _reporting;
    private readonly IFinancialDashboardService _finance;

    public ExecutiveDashboardService(IReportingService reporting, IFinancialDashboardService finance)
    {
        _reporting = reporting;
        _finance = finance;
    }

    public async Task<ExecutiveDashboardDto> GetAsync(ExecutiveDashboardQuery query, bool includeFinance, CancellationToken ct = default)
    {
        var filters = new ReportFilterParams
        {
            StartDateUtc = query.FromUtc,
            EndDateUtc = query.ToUtc,
            SearchTerm = query.Search,
            Page = 1,
            PageSize = 25
        };

        var procurement = await _reporting.GetProcurementSummaryAsync(filters, ct);
        var inventory = await _reporting.GetInventoryHealthAsync(filters, ct);
        var financeRec = await _reporting.GetFinanceReconciliationReportAsync(filters, ct);
        var telephony = await _reporting.GetTelephonyActivityReportAsync(filters, ct);
        var suppliers = await _reporting.GetSupplierPerformanceAsync(filters, ct);
        var materials = await _reporting.GetMaterialConsumptionAsync(filters, ct);
        var managerDash = await _reporting.GetRoleDashboardAsync("Manager", filters, ct);
        var procurementDash = await _reporting.GetRoleDashboardAsync("Procurement", filters, ct);

        var overduePoCount = procurementDash.ActionItems
            .Count(a => string.Equals(a.TargetView, "orders", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(a.Severity, "Critical", StringComparison.OrdinalIgnoreCase));

        var dto = new ExecutiveDashboardDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Freshness = new ExecutiveFreshnessDto
            {
                OpsFreshnessBadge = managerDash.FreshnessBadge,
                CompletenessFlags = new List<string> { "ops-crm-live" }
            },
            Slicers = BuildSlicers(includeFinance: false),
            Cards = BuildOpsCards(procurement, inventory, financeRec, telephony, overduePoCount),
            ActionItems = MergeActions(procurementDash.ActionItems, managerDash.ActionItems),
            TopSuppliersOps = suppliers.Take(5).ToList(),
            CriticalMaterials = materials
                .Where(m => m.InventoryRiskStatus is "Critical" or "Warning")
                .Take(8)
                .ToList(),
            DrillLinks = new ExecutiveDrillLinksDto()
        };

        if (!includeFinance)
        {
            dto.FinanceAccessNote =
                "ارقام مطالبات/بدهی سپیدار فقط با مجوز صریح Finance.PartyLedgerView نمایش داده می‌شود. نقش Admin فنی به‌تنهایی کافی نیست؛ هدر X-Active-Role نقش تخصیص‌نیافته را فعال نمی‌کند.";
            AppendBlockedFinanceStubs(dto.Cards);
            return dto;
        }

        var fin = await _finance.GetDashboardAsync(new FinancialDashboardQuery
        {
            FiscalYear = query.FiscalYear,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            // PartyCategory must NOT alter AR/AP totals — finance service applies it only to NeedsReview.
            PartyCategory = query.PartyCategory,
            Search = query.Search,
            FollowUpOwner = query.FollowUpOwner
        }, ct);

        var hasPrevious = fin.PreviousPeriodFromUtc.HasValue && fin.PreviousPeriodToUtc.HasValue;
        dto.Comparison = new ExecutiveComparisonWindowDto
        {
            CurrentFromUtc = fin.CurrentPeriodFromUtc,
            CurrentToUtc = fin.CurrentPeriodToUtc,
            PreviousFromUtc = fin.PreviousPeriodFromUtc,
            PreviousToUtc = fin.PreviousPeriodToUtc,
            StatusNote = fin.ComparisonStatus,
            HasEquivalentPreviousPeriod = hasPrevious
                && (fin.PreviousReceivableNet.HasValue || fin.PreviousPayableNet.HasValue)
        };

        dto.Slicers = BuildSlicers(includeFinance: true, fin);
        dto.Freshness.LastSuccessfulReadUtc = fin.LastSuccessfulFinancialReadUtc;
        dto.Freshness.FiscalYearLabel = fin.FiscalYears.FirstOrDefault(y =>
                y.FiscalYearId == (query.FiscalYear ?? fin.FiscalYears.FirstOrDefault()?.FiscalYearId))?.Title
            ?? fin.CatalogFiscalYear?.Title;
        dto.Freshness.FinanceFreshness = fin.Freshness;
        dto.Freshness.FinanceFreshnessDetail = fin.FreshnessDetail;
        dto.Freshness.CompletenessFlags.Add(fin.SepidarReadAttempted ? "sepidar-read-attempted" : "sepidar-read-skipped");
        if (string.Equals(fin.Freshness, "LiveRead", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fin.Freshness, "Live", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fin.Freshness, "Verified", StringComparison.OrdinalIgnoreCase))
            dto.Freshness.CompletenessFlags.Add("finance-live");
        else
            dto.Freshness.CompletenessFlags.Add("finance-incomplete-or-unverified");

        foreach (var card in MapFinanceCards(fin, dto.Comparison))
            dto.Cards.Add(card);

        AppendBlockedFinanceStubs(dto.Cards, alreadyPresent: true);

        dto.MonthlyTrend = fin.Monthly ?? new List<DashboardMonthlyDto>();
        dto.TopDebtors = fin.TopParties
            .Where(p => string.Equals(p.Side, TradeAccountClassifier.Receivable, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Net)
            .Take(8)
            .ToList();
        dto.TopCreditors = fin.TopParties
            .Where(p => string.Equals(p.Side, TradeAccountClassifier.Payable, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Net)
            .Take(8)
            .ToList();
        if (dto.TopDebtors.Count == 0 && dto.TopCreditors.Count == 0)
            dto.TopDebtors = fin.TopParties.OrderByDescending(p => Math.Abs(p.Net)).Take(8).ToList();
        dto.NeedsReview = fin.NeedsReview;
        dto.OpenFollowUps = fin.OpenFollowUps;

        return dto;
    }

    private static List<ExecutiveKpiCardDto> BuildOpsCards(
        ProcurementSummaryReportDto procurement,
        InventoryHealthReportDto inventory,
        FinanceReconciliationReportDto financeRec,
        TelephonyActivityReportDto telephony,
        int overduePoCount)
    {
        return new List<ExecutiveKpiCardDto>
        {
            new()
            {
                ContractId = "ED-01",
                Code = "overdue-po",
                Title = "سفارش‌های خرید سررسیدگذشته",
                NumericValue = overduePoCount,
                Unit = "سفارش",
                Status = FinancialFigureStatus.Verified,
                Reason = "Issued/PartiallyReceived با ExpectedDeliveryDate گذشته (فهرست اقدام Procurement)",
                Source = "CRM.PurchaseOrders",
                RequiredPermission = "Reports.Procurement|Manager",
                DrillTarget = "orders",
                DrillQuery = "filter=overdue"
            },
            new()
            {
                ContractId = "ED-01b",
                Code = "active-po",
                Title = "سفارش‌های خرید فعال",
                NumericValue = procurement.ActiveOrdersCount,
                Unit = "سفارش",
                Status = FinancialFigureStatus.Verified,
                Reason = "Issued / PartiallyReceived از CRM PurchaseOrders",
                Source = "CRM.PurchaseOrders",
                RequiredPermission = "Reports.Procurement|Manager",
                DrillTarget = "orders",
                DrillQuery = "filter=active"
            },
            new()
            {
                ContractId = "ED-02",
                Code = "critical-stock",
                Title = "کسری مواد (Critical)",
                NumericValue = inventory.CriticalStockCount,
                Unit = "SKU",
                Status = FinancialFigureStatus.Verified,
                Reason = "موجودی CRM؛ ردیف‌های بدون SafetyStock در فهرست با NeedsValidation جدا می‌شوند",
                Source = "CRM.Inventory",
                RequiredPermission = "Reports.Warehouse|Manager",
                DrillTarget = "warehouse",
                DrillQuery = "filter=critical"
            },
            new()
            {
                ContractId = "ED-03",
                Code = "open-match-exceptions",
                Title = "مغایرت فاکتور باز",
                NumericValue = financeRec.OpenExceptionsCount,
                Unit = "مورد",
                Status = FinancialFigureStatus.Verified,
                Reason = "تطبیق سه‌طرفه CRM؛ IsResolved=false",
                Source = "CRM.Matching",
                RequiredPermission = "Reports.Finance|Manager",
                DrillTarget = "finance",
                DrillQuery = "filter=open-exceptions"
            },
            new()
            {
                ContractId = "ED-04",
                Code = "overdue-followups",
                Title = "پیگیری‌های سررسیدگذشته",
                NumericValue = telephony.OverdueFollowUpsCount,
                Unit = "مورد",
                Status = FinancialFigureStatus.Verified,
                Reason = "CallNotes با FollowUpDue گذشته",
                Source = "CRM.CallNotes",
                RequiredPermission = "Reports.View",
                DrillTarget = "calls",
                DrillQuery = "filter=overdue-followups"
            },
            new()
            {
                ContractId = "ED-OPS-PO-VOLUME",
                Code = "po-volume-crm",
                Title = "حجم سفارش خرید (CRM)",
                Amount = procurement.TotalPurchaseVolume,
                Unit = "ریال",
                Status = FinancialFigureStatus.Verified,
                Reason = "جمع مبلغ سفارش‌های خرید CRM — این رقم فروش/خرید سپیدار نیست",
                Source = "CRM.PurchaseOrders",
                RequiredPermission = "Reports.Procurement|Manager",
                DrillTarget = "orders"
            }
        };
    }

    private static IEnumerable<ExecutiveKpiCardDto> MapFinanceCards(
        FinancialDashboardDto fin,
        ExecutiveComparisonWindowDto comparison)
    {
        foreach (var c in fin.Cards)
        {
            var contractId = c.Code switch
            {
                "receivables" => "ED-06",
                "payables" => "ED-07",
                "receipts" => "ED-13",
                "payments" => "ED-14",
                "overdue" => "ED-15",
                _ => "ED-FIN-" + c.Code
            };

            var status = c.Status;
            var reason = c.Reason;
            string? validationCause = null;
            decimal? previous = null;

            if (c.Code is "receivables" or "payables")
            {
                previous = c.Code == "receivables" ? fin.PreviousReceivableNet : fin.PreviousPayableNet;
                if (c.Status is FinancialFigureStatus.Verified or FinancialFigureStatus.Zero
                    && c.Amount.HasValue)
                {
                    status = FinancialFigureStatus.Unverified;
                    validationCause = ValidationCauseVoucherState;
                    reason = "نیازمند اعتبارسنجی: " + reason + " " + validationCause;
                }
            }

            var amount = NullAmountForNonNumericStatus(status, c.Amount);
            yield return new ExecutiveKpiCardDto
            {
                ContractId = contractId,
                Code = c.Code,
                Title = c.Title,
                Amount = amount,
                NumericValue = null,
                Status = status,
                Reason = reason,
                Source = status == FinancialFigureStatus.Blocked
                    ? "N/A"
                    : "Sepidar.ACC.VoucherItem (read-only)",
                RequiredPermission = "Finance.PartyLedgerView",
                DrillTarget = "financeDashboard",
                DrillQuery = c.Code is "receivables" or "payables" ? $"side={c.Code}" : null,
                PreviousPeriodAmount = comparison.HasEquivalentPreviousPeriod ? previous : null,
                PreviousPeriodNote = comparison.HasEquivalentPreviousPeriod
                    ? $"بازه معادل: {FormatUtc(comparison.PreviousFromUtc)} تا {FormatUtc(comparison.PreviousToUtc)}"
                    : (fin.ComparisonStatus ?? "بازه معادل سال قبل موجود نیست؛ مقایسه نمایش داده نمی‌شود."),
                LastSuccessfulReadUtc = fin.LastSuccessfulFinancialReadUtc,
                ValidationCause = validationCause
            };
        }

        yield return BlockedCard("ED-11", "sepidar-sales", "فروش (سپیدار)",
            "قرارداد و نگاشت قطعی نوع سند فروش سپیدار تأیید نشده است.");
        yield return BlockedCard("ED-12", "sepidar-purchases", "خرید (سپیدار)",
            "خرید پولی سپیدار مسدود است؛ حجم سفارش CRM جداگانه در کارت عملیاتی آمده است.");

        // ED-08: only when equivalent previous window + at least one previous figure exists.
        if (comparison.HasEquivalentPreviousPeriod)
        {
            yield return new ExecutiveKpiCardDto
            {
                ContractId = "ED-08",
                Code = "yoy-trade",
                Title = "مقایسه بازه معادل سال قبل (AR/AP)",
                Amount = null,
                NumericValue = null,
                Status = FinancialFigureStatus.Unverified,
                Reason = "نیازمند اعتبارسنجی. مطالبات سال قبل: "
                    + (fin.PreviousReceivableNet?.ToString("N0") ?? "—")
                    + "؛ بدهی سال قبل: "
                    + (fin.PreviousPayableNet?.ToString("N0") ?? "—")
                    + ". " + (comparison.StatusNote ?? ""),
                Source = "Sepidar fiscal equivalent window",
                RequiredPermission = "Finance.PartyLedgerView",
                DrillTarget = "financeDashboard",
                PreviousPeriodAmount = fin.PreviousReceivableNet,
                PreviousPeriodNote =
                    $"{FormatUtc(comparison.PreviousFromUtc)} تا {FormatUtc(comparison.PreviousToUtc)}",
                LastSuccessfulReadUtc = fin.LastSuccessfulFinancialReadUtc,
                ValidationCause = ValidationCauseVoucherState
            };
        }
        else
        {
            yield return new ExecutiveKpiCardDto
            {
                ContractId = "ED-08",
                Code = "yoy-trade",
                Title = "مقایسه بازه معادل سال قبل (AR/AP)",
                Amount = null,
                NumericValue = null,
                Status = FinancialFigureStatus.Blocked,
                Reason = fin.ComparisonStatus
                    ?? "بازه معادل سال قبل تأیید نشده؛ مقایسه نمایش داده نمی‌شود و رقم صفر ساخته نشده است.",
                Source = "N/A",
                RequiredPermission = "Finance.PartyLedgerView",
                DrillTarget = "financeDashboard",
                LastSuccessfulReadUtc = fin.LastSuccessfulFinancialReadUtc
            };
        }
    }

    private static decimal? NullAmountForNonNumericStatus(FinancialFigureStatus status, decimal? amount)
    {
        if (status is FinancialFigureStatus.Blocked
            or FinancialFigureStatus.Unavailable
            or FinancialFigureStatus.NoData)
            return null;
        return amount;
    }

    private static string FormatUtc(DateTime? utc) =>
        utc.HasValue ? utc.Value.ToString("yyyy-MM-dd") : "—";

    private static ExecutiveKpiCardDto BlockedCard(string contractId, string code, string title, string reason) => new()
    {
        ContractId = contractId,
        Code = code,
        Title = title,
        Amount = null,
        NumericValue = null,
        Status = FinancialFigureStatus.Blocked,
        Reason = reason,
        Source = "N/A",
        RequiredPermission = "Finance.PartyLedgerView",
        DrillTarget = "financeDashboard"
    };

    private static void AppendBlockedFinanceStubs(List<ExecutiveKpiCardDto> cards, bool alreadyPresent = false)
    {
        void Ensure(string contractId, string code, string title, string reason)
        {
            if (cards.Any(c => c.Code == code || c.ContractId == contractId)) return;
            cards.Add(BlockedCard(contractId, code, title, reason));
        }

        if (!alreadyPresent)
        {
            Ensure("ED-06", "receivables", "مطالبات تجاری", "بدون Finance.PartyLedgerView رقم سپیدار نمایش داده نمی‌شود.");
            Ensure("ED-07", "payables", "بدهی تجاری", "بدون Finance.PartyLedgerView رقم سپیدار نمایش داده نمی‌شود.");
            Ensure("ED-13", "receipts", "دریافت‌ها", "مسدود — سند دریافت نقد تأیید نشده است.");
            Ensure("ED-14", "payments", "پرداخت‌ها", "مسدود — سند پرداخت نقد تأیید نشده است.");
            Ensure("ED-15", "overdue", "مطالبات سررسیدگذشته", "مسدود — ستون سررسید تأیید نشده است.");
            Ensure("ED-08", "yoy-trade", "مقایسه بازه معادل سال قبل", "بدون مجوز مالی مقایسه نمایش داده نمی‌شود.");
        }

        Ensure("ED-11", "sepidar-sales", "فروش (سپیدار)", "مسدود — نگاشت فروش تأیید نشده است.");
        Ensure("ED-12", "sepidar-purchases", "خرید (سپیدار)", "مسدود — خرید پولی سپیدار تأیید نشده است.");
    }

    private static ExecutiveSlicersDto BuildSlicers(bool includeFinance, FinancialDashboardDto? fin = null)
    {
        var hasYears = includeFinance && fin?.FiscalYears is { Count: > 0 };
        var items = new List<ExecutiveSlicerStateDto>
        {
            new()
            {
                Key = "fiscalYear",
                Label = "سال مالی",
                Enabled = hasYears,
                AppliesTo = "finance-window",
                DisabledReason = includeFinance
                    ? (hasYears ? null : "سال مالی زنده در دسترس نیست")
                    : "فقط با مجوز Finance.PartyLedgerView"
            },
            new()
            {
                Key = "dateRange",
                Label = "بازه تاریخ شمسی",
                Enabled = true,
                AppliesTo = includeFinance ? "ops-date+finance-window" : "ops-date",
                DisabledReason = null
            },
            new()
            {
                Key = "partySearch",
                Label = "جستجوی شخص",
                Enabled = includeFinance,
                AppliesTo = "finance-search+needs-review",
                DisabledReason = includeFinance ? null : "جستجوی طرف‌حساب مالی فقط با مجوز دفتر"
            },
            new()
            {
                Key = "partyType",
                Label = "نوع شخص",
                Enabled = includeFinance,
                AppliesTo = "needs-review-only",
                DisabledReason = includeFinance
                    ? null
                    : "فقط فهرست بازبینی CRM؛ روی جمع AR/AP سپیدار اعمال نمی‌شود"
            },
            new()
            {
                Key = "followUpOwner",
                Label = "مسئول پیگیری",
                Enabled = includeFinance,
                AppliesTo = "crm-followups",
                DisabledReason = includeFinance ? null : "فقط با مجوز مالی"
            },
            new()
            {
                Key = "orgUnit",
                Label = "واحد سازمانی",
                Enabled = false,
                AppliesTo = "none",
                DisabledReason = "نگاشت معتبر واحد سازمانی به اسناد مالی سپیدار در کد فعلی نیست"
            },
            new()
            {
                Key = "itemGroup",
                Label = "گروه کالا",
                Enabled = false,
                AppliesTo = "none",
                DisabledReason = "روی AR/AP سپیدار اعمال نمی‌شود؛ مصرف مواد از مسیر گزارش انبار"
            },
            new()
            {
                Key = "status",
                Label = "وضعیت سفارش/مغایرت",
                Enabled = false,
                AppliesTo = "none",
                DisabledReason = "اسلایسر وضعیت سراسری روی این API پیاده‌سازی نشده؛ فیلتر از کارت اقدام به صفحه مقصد می‌رود"
            }
        };

        return new ExecutiveSlicersDto
        {
            FiscalYears = fin?.FiscalYears ?? new List<DashboardFiscalYearDto>(),
            ActiveFiscalYearId = fin?.CatalogFiscalYear?.FiscalYearId
                ?? fin?.FiscalYears.FirstOrDefault()?.FiscalYearId,
            Items = items
        };
    }

    private static List<DashboardActionItemDto> MergeActions(
        IEnumerable<DashboardActionItemDto> a,
        IEnumerable<DashboardActionItemDto> b)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<DashboardActionItemDto>();
        foreach (var item in a.Concat(b))
        {
            var key = item.Id + "|" + item.Title;
            if (!seen.Add(key)) continue;
            list.Add(item);
            if (list.Count >= 20) break;
        }
        return list;
    }
}
