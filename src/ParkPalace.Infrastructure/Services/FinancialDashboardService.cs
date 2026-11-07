using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Financial;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class FinancialDashboardService : IFinancialDashboardService
{
    private const int ReviewSampleLimit = 100;
    private readonly ICrmDbContext _db;
    private readonly ISepidarFinancialAdapter _sepidar;
    private readonly ISepidarLedgerReader? _ledger;
    private readonly ISepidarPartyLookup? _partyLookup;

    public FinancialDashboardService(
        ICrmDbContext db,
        ISepidarFinancialAdapter sepidar,
        ISepidarLedgerReader? ledger = null,
        ISepidarPartyLookup? partyLookup = null)
    {
        _db = db;
        _sepidar = sepidar;
        _ledger = ledger;
        _partyLookup = partyLookup;
    }

    public async Task<FinancialDashboardDto> GetDashboardAsync(FinancialDashboardQuery query, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var lastRead = await _db.IntegrationSyncRuns.AsNoTracking()
            .Where(r => r.SourceSystem == "Sepidar" && r.TargetEntity == "FinancialLedger" && r.Status == "Succeeded")
            .OrderByDescending(r => r.CompletedAtUtc)
            .Select(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        var catalog = VerifiedSepidarCatalog.FiscalYear1405;
        FiscalComparisonWindow? window = null;
        string comparisonStatus = "مقایسه با سال مالی قبل مسدود است؛ مرز تاریخ سال قبل در شواهد تأییدشده ثبت نشده است.";
        if (query.FiscalYear is null or 1405)
        {
            comparisonStatus = "سال ۱۴۰۵ در کاتالوگ شواهد تأیید شده است، اما تاریخ شروع و پایان سال قبل خوانده نشده است. بازهٔ معادل ساخته نمی‌شود.";
        }

        var dto = new FinancialDashboardDto
        {
            GeneratedAtUtc = now,
            SepidarReadAttempted = false,
            Freshness = "NotVerified",
            FreshnessDetail = "خواندن تجمیعی سپیدار انجام نشد. قرارداد اسناد حسابداری (وضعیت قطعی، ارز، منطقه زمانی و سررسید) هنوز تأیید نشده است. این حالت با «قطع ارتباط» یا «مانده صفر» یکی نیست.",
            LastSuccessfulFinancialReadUtc = lastRead,
            CatalogFiscalYear = new VerifiedFiscalYearDto
            {
                FiscalYearId = catalog.FiscalYearId,
                Title = catalog.Title,
                StartDate = catalog.Start,
                EndDate = catalog.End,
                Evidence = "docs/sepidar_source_contract_and_unverified_assumptions.md و docs/runs/sepidar_sample_phase2_20261027_174332/01_fiscal_years.csv",
                LiveRead = false
            },
            ComparisonStatus = comparisonStatus,
            CurrentPeriodFromUtc = window?.CurrentFrom,
            CurrentPeriodToUtc = window?.CurrentTo,
            PreviousPeriodFromUtc = window?.PreviousFrom,
            PreviousPeriodToUtc = window?.PreviousTo,
            Cards = BlockedCards(),
            MonthlyComparison = new List<FinancialFigureDto>
            {
                Blocked("monthly-comparison", "مقایسه ماهانه سال جاری و سال قبل",
                    "نمودار ماهانه رسم نمی‌شود. هیچ سطر ACC.Voucher در نمونه‌برداری خوانده نشده و جمع ماهانه قابل تأیید نیست.")
            },
            DueDates = Blocked("due-dates", "سررسید",
                "ستون سررسید در متادیتای تأییدشده ACC.Voucher وجود ندارد. GNR.Reminder.DueDate هم به اسناد حسابداری وصل نشده است."),
            LargestBalances = Blocked("largest-balances", "بزرگ‌ترین مانده‌ها",
                "مانده طرف‌حساب‌ها تأیید نشده است؛ فهرست خالی به معنی مانده صفر نیست."),
            ActiveFilters = DescribeFilters(query)
        };

        if (_ledger != null)
            await ApplyLiveLedgerAsync(dto, query, ct);

        dto.UnmappedPartyCount = await _db.Parties.CountAsync(
            p => p.IsActive && (p.SepidarPartyRef == null || p.SepidarPartyRef == ""), ct);

        var reviewQuery = _db.Parties.AsNoTracking().Where(p => p.IsActive);
        if (query.PartyCategory.HasValue)
            reviewQuery = reviewQuery.Where(p => p.Category == query.PartyCategory.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            reviewQuery = reviewQuery.Where(p =>
                p.DisplayName.Contains(term)
                || (p.SepidarCode != null && p.SepidarCode.Contains(term))
                || (p.SepidarPartyRef != null && p.SepidarPartyRef.Contains(term))
                || (p.SepidarTitle != null && p.SepidarTitle.Contains(term)));
        }

        var sample = await reviewQuery
            .OrderBy(p => p.DisplayName)
            .Select(p => new PartyProbe(p.Id, p.DisplayName, p.Category, p.SepidarCode, p.SepidarPartyRef, p.SepidarMappingVerified))
            .Take(ReviewSampleLimit + 1)
            .ToListAsync(ct);
        dto.ReviewSampleCapped = sample.Count > ReviewSampleLimit;
        if (dto.ReviewSampleCapped) sample = sample.Take(ReviewSampleLimit).ToList();

        var duplicateRefs = sample
            .Where(p => !string.IsNullOrWhiteSpace(p.SepidarPartyRef))
            .GroupBy(p => p.SepidarPartyRef!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateCodes = sample
            .Where(p => !string.IsNullOrWhiteSpace(p.SepidarCode))
            .GroupBy(p => p.SepidarCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        dto.NeedsReview = sample
            .Select(p => MapReview(p, duplicateRefs, duplicateCodes))
            .Where(x => x != null)
            .Cast<PartyReviewItemDto>()
            .Take(50)
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.Search) && _partyLookup != null)
            await ApplySepidarSearchAsync(dto, query.Search.Trim(), sample, ct);
        else if (!string.IsNullOrWhiteSpace(query.Search) && _partyLookup == null)
            dto.SepidarSearchMessage = "جستجوی زنده سپیدار در این اجرا در دسترس نیست.";

        var followUps = _db.CallNotes.AsNoTracking()
            .Where(n => n.FollowUpRequired && n.FollowUpStatus == FollowUpStatus.Pending);
        if (!string.IsNullOrWhiteSpace(query.FollowUpOwner))
        {
            var owner = query.FollowUpOwner.Trim();
            followUps = followUps.Where(n => n.FollowUpAssignedTo != null && n.FollowUpAssignedTo.Contains(owner));
        }

        var noteRows = await followUps
            .OrderBy(n => n.FollowUpDueDateUtc)
            .Take(30)
            .Select(n => new { n.Id, n.CallRecordId, n.FollowUpAssignedTo, n.FollowUpDueDateUtc, n.NoteText })
            .ToListAsync(ct);
        var callIds = noteRows.Select(n => n.CallRecordId).Distinct().ToList();
        var callParties = await _db.CallRecords.AsNoTracking()
            .Where(c => callIds.Contains(c.Id))
            .Select(c => new { c.Id, c.PartyId, PartyName = c.Party != null ? c.Party.DisplayName : null })
            .ToListAsync(ct);
        var byCall = callParties.ToDictionary(c => c.Id);

        dto.OpenFollowUps = noteRows.Select(n =>
        {
            byCall.TryGetValue(n.CallRecordId, out var call);
            return new CrmFollowUpItemDto
            {
                NoteId = n.Id,
                PartyId = call?.PartyId,
                PartyName = call?.PartyName,
                Owner = n.FollowUpAssignedTo,
                DueDateUtc = n.FollowUpDueDateUtc,
                NoteText = n.NoteText,
                Source = "CRM"
            };
        }).ToList();

        return dto;
    }

    public async Task<PartyLedgerDto?> GetPartyLedgerAsync(Guid partyId, bool includeDocuments, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;

        var party = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partyId, ct);
        if (party == null) return null;

        var mappingVerified = !string.IsNullOrWhiteSpace(party.SepidarPartyRef);
        var ledger = new PartyLedgerDto
        {
            PartyId = party.Id,
            DisplayName = party.DisplayName,
            SepidarPartyRef = party.SepidarPartyRef,
            SepidarCode = party.SepidarCode,
            MappingVerified = mappingVerified,
            DocumentsAuthorized = includeDocuments,
            Page = page,
            PageSize = pageSize,
            TotalLineCount = null,
            LinesStatus = FinancialFigureStatus.Unverified,
            LinesReason = "ریز اسناد حسابداری تأیید نشده است. فهرست خالی به معنی «بدون سند» نیست."
        };

        ledger.OpeningBalance = Blocked("opening-balance", "مانده اول دوره", ledger.LinesReason);
        ledger.TotalDebit = Blocked("total-debit", "جمع بدهکار", ledger.LinesReason);
        ledger.TotalCredit = Blocked("total-credit", "جمع بستانکار", ledger.LinesReason);
        ledger.Receipts = Blocked("receipts", "دریافت‌ها", "رسید انبار با دریافت وجه یکی دانسته نشده است.");
        ledger.Payments = Blocked("payments", "پرداخت‌ها", "سند پرداخت تأییدشده در شواهد موجود نیست.");
        ledger.ClosingBalance = Blocked("closing-balance", "مانده پایان", ledger.LinesReason);

        if (_ledger != null && mappingVerified)
        {
            await ApplyPartyLedgerAsync(ledger, party, includeDocuments, page, pageSize, ct);
        }

        var liveReader = _sepidar.AdapterName.Contains("Live", StringComparison.OrdinalIgnoreCase);
        if (_ledger != null && !mappingVerified && includeDocuments)
        {
            ledger.LinesStatus = FinancialFigureStatus.NoData;
            ledger.LinesReason = "این طرف‌حساب شناسه SepidarPartyRef ندارد. کد تفصیلی جایگزین شناسه نمی‌شود و مانده‌ای خوانده نشد.";
        }
        else if (_ledger != null && !mappingVerified)
        {
            ledger.LinesStatus = FinancialFigureStatus.Blocked;
            ledger.LinesReason = "ریز اسناد برای این درخواست باز نشده است.";
        }
        else if (_ledger != null)
        {
            // ApplyPartyLedgerAsync already set the figures. Fixture amounts stay unused.
        }
        else if (includeDocuments && mappingVerified && !liveReader)
        {
            ledger.LinesStatus = FinancialFigureStatus.Unverified;
            ledger.LinesReason = "آداپتر جاری سپیدار زنده نیست. اعداد آزمایشی به‌عنوان مانده یا گردش تأییدشده نشان داده نمی‌شوند.";
        }
        else if (includeDocuments && mappingVerified)
        {
            var history = await _sepidar.GetPartyFinancialHistoryAsync(party.SepidarPartyRef!.Trim(), party.SepidarCode, null, ct);
            if (!history.IsConnected)
            {
                ledger.LinesStatus = FinancialFigureStatus.Unavailable;
                ledger.LinesReason = history.StatusMessage ?? "ارتباط با سپیدار برقرار نشد. مانده قبلیِ تأییدشده‌ای برای نمایش وجود ندارد.";
            }
            else if (!history.IsAvailable)
            {
                ledger.LinesStatus = FinancialFigureStatus.Unverified;
                ledger.LinesReason = history.StatusMessage
                    ?? history.UnverifiedDependencyDetails
                    ?? "تاریخچه مالی این طرف‌حساب هنوز قابل تأیید نیست.";
            }
            else
            {
                ledger.LinesStatus = FinancialFigureStatus.Verified;
                ledger.LinesReason = "آداپتر سپیدار این سابقه را در دسترس اعلام کرد.";
                ledger.OpeningBalance = VerifiedAmount("opening-balance", "مانده اول دوره", history.OpeningBalance);
                ledger.TotalDebit = VerifiedAmount("total-debit", "جمع بدهکار", history.TotalDebit);
                ledger.TotalCredit = VerifiedAmount("total-credit", "جمع بستانکار", history.TotalCredit);
                ledger.ClosingBalance = VerifiedAmount("closing-balance", "مانده پایان", history.Balance);
                ledger.TotalLineCount = history.Movements.Count;
                ledger.Lines = history.Movements
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new PartyLedgerLineDto
                    {
                        SepidarReference = m.VoucherItemId,
                        DateUtc = m.VoucherDateUtc,
                        DocumentNumber = m.VoucherNumber,
                        Description = m.Description,
                        Debit = m.Debit,
                        Credit = m.Credit,
                        RunningBalance = m.CumulativeBalance
                    }).ToList();
            }
        }
        else if (includeDocuments && !mappingVerified)
        {
            ledger.LinesStatus = FinancialFigureStatus.NoData;
            ledger.LinesReason = "این طرف‌حساب شناسه SepidarPartyRef ندارد. کد تفصیلی جایگزین شناسه نمی‌شود و مانده‌ای خوانده نشد.";
        }
        else
        {
            ledger.LinesStatus = FinancialFigureStatus.Blocked;
            ledger.LinesReason = "ریز اسناد برای این درخواست باز نشده است.";
        }

        ledger.Contacts = await _db.PartyContacts.AsNoTracking()
            .Where(c => c.PartyId == partyId && c.IsActive)
            .OrderByDescending(c => c.IsPrimary)
            .Take(20)
            .Select(c => new PartyCrmContactDto { Id = c.Id, PersonName = c.PersonName, Position = c.Position })
            .ToListAsync(ct);

        ledger.Calls = await _db.CallRecords.AsNoTracking()
            .Where(c => c.PartyId == partyId)
            .OrderByDescending(c => c.StartTimeUtc)
            .Take(20)
            .Select(c => new PartyCrmCallDto
            {
                Id = c.Id,
                StartTimeUtc = c.StartTimeUtc,
                CallerNumber = c.CallerNumber,
                DestinationNumber = c.DestinationNumber,
                CallerName = c.CallerName
            }).ToListAsync(ct);

        var partyCallIds = ledger.Calls.Select(c => c.Id).ToList();
        ledger.FollowUps = await _db.CallNotes.AsNoTracking()
            .Where(n => partyCallIds.Contains(n.CallRecordId) && n.FollowUpRequired)
            .OrderBy(n => n.FollowUpDueDateUtc)
            .Take(20)
            .Select(n => new CrmFollowUpItemDto
            {
                NoteId = n.Id,
                PartyId = partyId,
                PartyName = party.DisplayName,
                Owner = n.FollowUpAssignedTo,
                DueDateUtc = n.FollowUpDueDateUtc,
                NoteText = n.NoteText,
                Source = "CRM"
            }).ToListAsync(ct);

        return ledger;
    }

    private async Task ApplyLiveLedgerAsync(FinancialDashboardDto dto, FinancialDashboardQuery query, CancellationToken ct)
    {
        var snap = await _ledger!.ReadAsync(query.FiscalYear, query.FromUtc, query.ToUtc, ct);
        dto.SepidarReadAttempted = true;
        dto.FiscalYears = snap.Years.Select(y => new DashboardFiscalYearDto
        {
            FiscalYearId = y.Id,
            Title = y.Title,
            StartDate = y.Start,
            EndDate = y.EndInclusive
        }).ToList();
        dto.ComparisonStatus = snap.ComparisonNote;
        dto.CurrentPeriodFromUtc = snap.CurrentFrom;
        dto.CurrentPeriodToUtc = snap.CurrentTo;
        dto.PreviousPeriodFromUtc = snap.PreviousFrom;
        dto.PreviousPeriodToUtc = snap.PreviousTo;

        if (!snap.Connected)
        {
            dto.Freshness = "Unavailable";
            dto.FreshnessDetail = snap.Failure ?? "ارتباط با سپیدار برقرار نشد.";
            dto.Cards = new List<FinancialFigureDto>
            {
                Unavailable("receivables", "مطالبات تجاری", dto.FreshnessDetail),
                Unavailable("payables", "بدهی‌های تجاری", dto.FreshnessDetail),
                Blocked("receipts", "دریافت‌ها", "رسید انبار دریافت وجه نیست و سند دریافت نقد جداگانه تأیید نشده است."),
                Blocked("payments", "پرداخت‌ها", "سند پرداخت نقد جدا از گردش حساب پرداختنی تأیید نشده است."),
                Blocked("overdue", "سررسیدگذشته", "روی سند حسابداری ستون سررسید تأییدشده‌ای نیست.")
            };
            dto.MonthlyComparison = new List<FinancialFigureDto>
            {
                Unavailable("monthly-comparison", "مقایسه ماهانه", dto.FreshnessDetail)
            };
            dto.LargestBalances = Unavailable("largest-balances", "بزرگ‌ترین مانده‌ها", dto.FreshnessDetail);
            return;
        }

        if (snap.Selected != null)
        {
            dto.CatalogFiscalYear = new VerifiedFiscalYearDto
            {
                FiscalYearId = snap.Selected.Id,
                Title = snap.Selected.Title,
                StartDate = snap.Selected.Start,
                EndDate = snap.Selected.EndInclusive,
                Evidence = "FMK.FiscalYear در خواندن زندهٔ فقط‌خواندنی.",
                LiveRead = true
            };
        }

        dto.AccountScope = snap.Accounts.Select(a => new DashboardAccountScopeDto
        {
            Code = a.Code,
            Title = a.Title,
            Side = a.Side
        }).ToList();

        if (!snap.FiguresRead)
        {
            dto.Freshness = "NoData";
            dto.FreshnessDetail = "ارتباط برقرار شد، اما معین دریافتنی یا پرداختنیِ تجاری در درخت حساب این شرکت پیدا نشد. رقم صفر ساخته نشده است.";
            dto.Cards = new List<FinancialFigureDto>
            {
                NoData("receivables", "مطالبات تجاری", dto.FreshnessDetail),
                NoData("payables", "بدهی‌های تجاری", dto.FreshnessDetail),
                Blocked("receipts", "دریافت‌ها", "سند دریافت نقد در این خواندن تعریف نشده است."),
                Blocked("payments", "پرداخت‌ها", "سند پرداخت نقد در این خواندن تعریف نشده است."),
                Blocked("overdue", "سررسیدگذشته", "روی سند حسابداری ستون سررسید تأییدشده‌ای نیست.")
            };
            return;
        }

        dto.Freshness = "LiveRead";
        dto.FreshnessDetail = TradeAccountClassifier.Definition;
        var accountNote = AccountNote(snap.Accounts);
        dto.PreviousReceivableNet = snap.PreviousReceivableNet;
        dto.PreviousPayableNet = snap.PreviousPayableNet;
        dto.Cards = new List<FinancialFigureDto>
        {
            Money("receivables", "مطالبات تجاری", snap.ReceivableNet, PreviousText(snap.PreviousReceivableNet, "مطالبات") + accountNote),
            Money("payables", "بدهی‌های تجاری", snap.PayableNet, PreviousText(snap.PreviousPayableNet, "بدهی") + accountNote),
            Blocked("receipts", "دریافت‌ها", "گردش نقد و بانک با مانده حساب دریافتنی یکی نیست و جداگانه خوانده نشده است."),
            Blocked("payments", "پرداخت‌ها", "گردش نقد و بانک با مانده حساب پرداختنی یکی نیست و جداگانه خوانده نشده است."),
            Blocked("overdue", "سررسیدگذشته", "روی ACC.Voucher ستون سررسید نیست. این کارت صفر نشان داده نمی‌شود.")
        };
        dto.Monthly = snap.Monthly.Select(m => new DashboardMonthlyDto
        {
            MonthStart = m.MonthStart,
            Side = m.Side,
            Net = m.Net,
            PreviousPeriod = m.PreviousPeriod
        }).ToList();
        dto.MonthlyComparison = new List<FinancialFigureDto>
        {
            new()
            {
                Code = "monthly-comparison",
                Title = "مقایسه ماهانه",
                Status = dto.Monthly.Count == 0 ? FinancialFigureStatus.Zero : FinancialFigureStatus.Verified,
                Amount = null,
                Reason = snap.ComparisonNote
            }
        };
        dto.DueDates = Blocked("due-dates", "سررسید", "سررسید در سند حسابداری تأیید نشده است.");
        dto.LargestBalances = new FinancialFigureDto
        {
            Code = "largest-balances",
            Title = "بزرگ‌ترین مانده‌ها",
            Amount = null,
            Status = FinancialFigureStatus.Verified,
            Reason = "ده تفصیلی با بیشترین ماندهٔ مثبت در همین بازه و روی همین معین‌ها."
        };

        var codes = snap.TopParties.Select(p => p.Code).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var crm = await _db.Parties.AsNoTracking()
            .Where(p => p.IsActive && p.SepidarCode != null && codes.Contains(p.SepidarCode))
            .Select(p => new { p.Id, p.DisplayName, p.SepidarCode })
            .ToListAsync(ct);
        var top = snap.TopParties.Select(row =>
        {
            var matches = crm.Where(c => string.Equals(c.SepidarCode, row.Code, StringComparison.OrdinalIgnoreCase)).ToList();
            var unique = matches.Count == 1 ? matches[0] : null;
            return new DashboardPartyBalanceDto
            {
                Code = row.Code,
                Title = row.Title,
                Net = row.Net,
                Side = row.Side,
                PartyId = unique?.Id,
                CrmName = unique?.DisplayName
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            top = top.Where(row =>
                row.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (row.CrmName != null && row.CrmName.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        dto.TopParties = top;
    }

    private async Task ApplySepidarSearchAsync(
        FinancialDashboardDto dto,
        string term,
        List<PartyProbe> crmSample,
        CancellationToken ct)
    {
        var result = await _partyLookup!.SearchAsync(term, ct);
        dto.SepidarSearchMessage = result.Message;
        if (!result.Connected || result.Hits.Count == 0)
        {
            dto.SepidarSearchHits = new List<DashboardSepidarSearchHitDto>();
            return;
        }

        var codes = result.Hits.Select(h => h.Code).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var refs = result.Hits.Select(h => h.PartyRef).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var crm = await _db.Parties.AsNoTracking()
            .Where(p => p.IsActive && (
                (p.SepidarCode != null && codes.Contains(p.SepidarCode))
                || (p.SepidarPartyRef != null && refs.Contains(p.SepidarPartyRef))
                || p.DisplayName.Contains(term)))
            .Select(p => new { p.Id, p.DisplayName, p.SepidarCode, p.SepidarPartyRef, p.SepidarMappingVerified })
            .ToListAsync(ct);

        dto.SepidarSearchHits = result.Hits.Select(hit =>
        {
            var byRef = !string.IsNullOrWhiteSpace(hit.PartyRef)
                ? crm.Where(c => string.Equals(c.SepidarPartyRef, hit.PartyRef, StringComparison.OrdinalIgnoreCase)).ToList()
                : new();
            var byCode = crm.Where(c => string.Equals(c.SepidarCode, hit.Code, StringComparison.OrdinalIgnoreCase)).ToList();
            var unique = byRef.Count == 1 ? byRef[0]
                : byCode.Count == 1 ? byCode[0]
                : null;

            if (unique == null)
            {
                var byName = crmSample
                    .Where(c => c.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(hit.Title) && hit.Title.Contains(c.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (byName.Count == 1)
                {
                    var probe = byName[0];
                    return new DashboardSepidarSearchHitDto
                    {
                        Code = hit.Code,
                        Title = hit.Title,
                        PartyRef = hit.PartyRef,
                        PartyId = probe.Id,
                        CrmName = probe.DisplayName,
                        MappingVerified = probe.MappingVerified
                    };
                }
            }

            return new DashboardSepidarSearchHitDto
            {
                Code = hit.Code,
                Title = hit.Title,
                PartyRef = hit.PartyRef,
                PartyId = unique?.Id,
                CrmName = unique?.DisplayName,
                MappingVerified = unique?.SepidarMappingVerified == true
            };
        }).ToList();
    }

    private async Task ApplyPartyLedgerAsync(PartyLedgerDto ledger, Party party, bool includeDocuments, int page, int pageSize, CancellationToken ct)
    {
        var result = await _ledger!.ReadPartyAsync(party.SepidarPartyRef!.Trim(), party.SepidarCode, includeDocuments, page, pageSize, ct);
        if (!result.Connected)
        {
            ledger.LinesStatus = FinancialFigureStatus.Unavailable;
            ledger.LinesReason = result.Failure ?? "ارتباط با سپیدار برقرار نشد.";
            return;
        }
        if (result.CodeMismatch || !result.HasDetailLink)
        {
            ledger.LinesStatus = result.Found ? FinancialFigureStatus.Unverified : FinancialFigureStatus.NoData;
            ledger.LinesReason = result.Failure ?? "گردش این طرف‌حساب خوانده نشد.";
            return;
        }

        var closing = result.OpeningNet + result.PeriodDebit - result.PeriodCredit;
        ledger.OpeningBalance = Money("opening-balance", "مانده اول دوره", result.OpeningNet, TradeAccountClassifier.Definition);
        ledger.TotalDebit = Money("total-debit", "جمع بدهکار", result.PeriodDebit, "جمع بدهکار ردیف‌های همین سال روی معین‌های تجاری.");
        ledger.TotalCredit = Money("total-credit", "جمع بستانکار", result.PeriodCredit, "جمع بستانکار ردیف‌های همین سال روی معین‌های تجاری.");
        ledger.ClosingBalance = Money("closing-balance", "مانده پایان", closing, "مانده اول دوره به‌علاوه بدهکار منهای بستانکار همین سال.");
        ledger.TotalLineCount = result.TotalLines;
        ledger.LinesStatus = includeDocuments ? FinancialFigureStatus.Verified : FinancialFigureStatus.Blocked;
        ledger.LinesReason = includeDocuments
            ? TradeAccountClassifier.Definition
            : "ریز اسناد برای این درخواست باز نشده است. جمع‌ها از همان معین‌های تجاری خوانده شده‌اند.";
        ledger.Lines = result.Lines.Select(line => new PartyLedgerLineDto
        {
            SepidarReference = line.Reference,
            DateUtc = line.Date,
            DocumentNumber = line.Number,
            DocumentType = line.Side == TradeAccountClassifier.Payable ? "بدهی" : "مطالبات",
            Description = line.Description,
            Debit = line.Debit,
            Credit = line.Credit,
            RunningBalance = line.RunningNet
        }).ToList();
    }

    private static string AccountNote(IReadOnlyList<SepidarAccountScope> accounts)
    {
        var sample = string.Join("، ", accounts.Take(6).Select(a => $"{a.Code} {a.Title}"));
        var extra = accounts.Count > 6 ? $" و {accounts.Count - 6} معین دیگر" : string.Empty;
        return $" معین‌ها: {sample}{extra}.";
    }

    private static string PreviousText(decimal? amount, string title) =>
        amount.HasValue
            ? $"بازه معادل سال قبل برای {title}: {amount.Value:N0}. "
            : "بازه معادل سال قبل موجود نیست. ";

    private static FinancialFigureDto Money(string code, string title, decimal amount, string reason) => new()
    {
        Code = code,
        Title = title,
        Amount = amount,
        Status = amount == 0 ? FinancialFigureStatus.Zero : FinancialFigureStatus.Verified,
        Reason = reason
    };

    private static FinancialFigureDto Unavailable(string code, string title, string reason) => new()
    {
        Code = code,
        Title = title,
        Amount = null,
        Status = FinancialFigureStatus.Unavailable,
        Reason = reason
    };

    private static FinancialFigureDto NoData(string code, string title, string reason) => new()
    {
        Code = code,
        Title = title,
        Amount = null,
        Status = FinancialFigureStatus.NoData,
        Reason = reason
    };

    private static List<FinancialFigureDto> BlockedCards() => new()
    {
        Blocked("receivables", "مطالبات", "مانده طلب از جمع بدهکار/بستانکار اسناد تأیید نشده است."),
        Blocked("payables", "بدهی‌ها", "مانده بدهی بدون قاعدهٔ تأییدشدهٔ طرف‌حساب قابل ارائه نیست."),
        Blocked("receipts", "دریافت‌ها", "رسید انبار (INV.InventoryReceipt) دریافت وجه نیست و نگاشت نوع ۱ هنوز فرضی است."),
        Blocked("payments", "پرداخت‌ها", "سند پرداخت در شواهد نمونه‌برداری خوانده نشده است."),
        Blocked("overdue", "سررسیدگذشته", "سررسید مالی تأیید نشده است و مقدار صفر نمایش داده نمی‌شود.")
    };

    private static FinancialFigureDto Blocked(string code, string title, string reason) => new()
    {
        Code = code,
        Title = title,
        Amount = null,
        Status = FinancialFigureStatus.Blocked,
        Reason = reason
    };

    private static FinancialFigureDto VerifiedAmount(string code, string title, decimal amount) => new()
    {
        Code = code,
        Title = title,
        Amount = amount,
        Status = amount == 0 ? FinancialFigureStatus.Zero : FinancialFigureStatus.Verified,
        Reason = "مقدار را آداپتر مالی سپیدار در دسترس اعلام کرده است."
    };

    private static PartyReviewItemDto? MapReview(PartyProbe party, HashSet<string> duplicateRefs, HashSet<string> duplicateCodes)
    {
        if (string.IsNullOrWhiteSpace(party.SepidarPartyRef))
        {
            return Item(party, string.IsNullOrWhiteSpace(party.SepidarCode)
                ? "شناسه طرف‌حساب سپیدار ندارد."
                : "کد تفصیلی دارد اما شناسه PartyId سپیدار ندارد. کد جایگزین شناسه نمی‌شود.");
        }
        if (duplicateRefs.Contains(party.SepidarPartyRef.Trim()))
            return Item(party, "شناسه سپیدار روی بیش از یک طرف‌حساب CRM تکرار شده است.");
        if (!string.IsNullOrWhiteSpace(party.SepidarCode) && duplicateCodes.Contains(party.SepidarCode.Trim()))
            return Item(party, "کد تفصیلی روی بیش از یک طرف‌حساب CRM تکرار شده است.");
        return null;
    }

    private static PartyReviewItemDto Item(PartyProbe party, string issue) => new()
    {
        PartyId = party.Id,
        DisplayName = party.DisplayName,
        Category = party.Category.ToString(),
        Issue = issue,
        SepidarCode = party.SepidarCode,
        SepidarPartyRef = party.SepidarPartyRef
    };

    private static List<ActiveFilterDto> DescribeFilters(FinancialDashboardQuery query)
    {
        var filters = new List<ActiveFilterDto>();
        if (query.FiscalYear.HasValue)
            filters.Add(new ActiveFilterDto { Key = "fiscalYear", Label = $"سال مالی {query.FiscalYear}" });
        if (query.FromUtc.HasValue)
            filters.Add(new ActiveFilterDto { Key = "from", Label = $"از {query.FromUtc:yyyy-MM-dd}" });
        if (query.ToUtc.HasValue)
            filters.Add(new ActiveFilterDto { Key = "to", Label = $"تا {query.ToUtc:yyyy-MM-dd}" });
        if (query.PartyCategory.HasValue)
            filters.Add(new ActiveFilterDto { Key = "partyCategory", Label = $"نوع طرف‌حساب {query.PartyCategory}" });
        if (!string.IsNullOrWhiteSpace(query.Search))
            filters.Add(new ActiveFilterDto { Key = "search", Label = $"جستجو: {query.Search.Trim()}" });
        if (!string.IsNullOrWhiteSpace(query.FollowUpOwner))
            filters.Add(new ActiveFilterDto { Key = "followUpOwner", Label = $"مسئول پیگیری: {query.FollowUpOwner.Trim()}" });
        return filters;
    }

    private sealed record PartyProbe(
        Guid Id,
        string DisplayName,
        PartyCategory Category,
        string? SepidarCode,
        string? SepidarPartyRef,
        bool MappingVerified);
}

/// <summary>
/// Fiscal year 1405 was read from FMK.FiscalYear. Dates are catalog evidence, not a live query.
/// </summary>
public static class VerifiedSepidarCatalog
{
    public static readonly (int FiscalYearId, string Title, DateTime Start, DateTime End) FiscalYear1405 =
        (10, "1405", new DateTime(2026, 3, 21), new DateTime(2027, 3, 20));
}
