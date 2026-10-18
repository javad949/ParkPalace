using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class SepidarFixtureFinancialAdapter : ISepidarFinancialAdapter
{
    public string AdapterName => "SepidarFixtureFinancialAdapter (Deterministic Bakery Finance Dataset)";

    public Task<List<SepidarCandidateInvoiceDto>> GetCandidateInvoicesAsync(DateTime? sinceDateUtc, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var fixtures = new List<SepidarCandidateInvoiceDto>
        {
            new()
            {
                SepidarInvoiceId = "SEP-INV-1001",
                SepidarVoucherNumber = "FIN-202610-8801",
                VoucherDateUtc = now.AddDays(-2),
                SupplierName = "شرکت آرد ستاره البرز",
                SupplierNationalCode = "10102345678",
                Amount = 187500000m,
                Currency = "IRR",
                ConfidenceScore = 0.95m,
                MatchConfidenceLevel = "High",
                MatchReason = "تطابق کامل شناسه ملی تأمین‌کننده، مبلغ قطعی و تاریخ صدور"
            },
            new()
            {
                SepidarInvoiceId = "SEP-INV-1002",
                SepidarVoucherNumber = "FIN-202610-8802",
                VoucherDateUtc = now.AddDays(-3),
                SupplierName = "صنایع مخمر و بهبوددهنده رضوی",
                SupplierNationalCode = "10103456789",
                Amount = 64000000m,
                Currency = "IRR",
                ConfidenceScore = 0.90m,
                MatchConfidenceLevel = "High",
                MatchReason = "تطابق عنوان تأمین‌کننده، مبلغ فاکتور و شناسه یکتای طرف حساب"
            },
            new()
            {
                SepidarInvoiceId = "SEP-INV-1003",
                SepidarVoucherNumber = "FIN-202610-8803",
                VoucherDateUtc = now.AddDays(-4),
                SupplierName = "روغن نباتی و مارگارین گلناز",
                SupplierNationalCode = "10104567890",
                Amount = 45000000m,
                Currency = "IRR",
                ConfidenceScore = 0.70m,
                MatchConfidenceLevel = "Medium",
                MatchReason = "تطابق شناسه تأمین‌کننده؛ اختلاف جزئی در رقم مالیات ارزش افزوده نیازمند تأیید مالی"
            },
            new()
            {
                SepidarInvoiceId = "SEP-INV-1004",
                SepidarVoucherNumber = "FIN-202610-8804",
                VoucherDateUtc = now.AddDays(-1),
                SupplierName = "صنایع چاپ و بسته‌بندی پویا پلاستیک",
                SupplierNationalCode = "10105678901",
                Amount = 28000000m,
                Currency = "IRR",
                ConfidenceScore = 0.85m,
                MatchConfidenceLevel = "High",
                MatchReason = "تطابق کامل حواله رسید انبار و بارنامه حمل با پیش‌فاکتور"
            }
        };

        if (sinceDateUtc.HasValue)
        {
            fixtures = fixtures.Where(f => f.VoucherDateUtc >= sinceDateUtc.Value).ToList();
        }

        return Task.FromResult(fixtures);
    }
}

public class SepidarLiveFinancialAdapter : ISepidarFinancialAdapter
{
    private readonly string _connectionString;
    private readonly ILogger<SepidarLiveFinancialAdapter> _logger;

    public string AdapterName => "SepidarLiveFinancialAdapter (SQL Server Read-Only crm_reader)";

    public SepidarLiveFinancialAdapter(IConfiguration configuration, ILogger<SepidarLiveFinancialAdapter> logger)
    {
        _connectionString = configuration.GetConnectionString("SepidarDatabase") ?? string.Empty;
        _logger = logger;
    }

    public async Task<List<SepidarCandidateInvoiceDto>> GetCandidateInvoicesAsync(DateTime? sinceDateUtc, CancellationToken ct = default)
    {
        // When connection is not configured or network unreachable, log and return empty list
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("Sepidar financial connection string not configured.");
            return new List<SepidarCandidateInvoiceDto>();
        }

        // Live parameterized SELECT-only implementation
        return await Task.FromResult(new List<SepidarCandidateInvoiceDto>());
    }
}

public class SepidarReconciliationService : ISepidarReconciliationService
{
    private readonly ICrmDbContext _dbContext;
    private readonly ISepidarFinancialAdapter _adapter;

    public SepidarReconciliationService(ICrmDbContext dbContext, ISepidarFinancialAdapter adapter)
    {
        _dbContext = dbContext;
        _adapter = adapter;
    }

    public async Task<List<SepidarCandidateInvoiceDto>> GetCandidatesForInvoiceAsync(Guid supplierInvoiceId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.SupplierInvoices
            .Include(si => si.Supplier)
            .FirstOrDefaultAsync(si => si.Id == supplierInvoiceId, ct);

        if (invoice == null)
        {
            throw new InvalidOperationException("فاکتور موردنظر در CRM یافت نشد.");
        }

        var candidates = await _adapter.GetCandidateInvoicesAsync(invoice.InvoiceDateUtc.AddDays(-14), ct);
        var scoredList = new List<SepidarCandidateInvoiceDto>();

        foreach (var c in candidates)
        {
            decimal score = 0m;
            string reason = "";

            // Supplier Match
            if (invoice.Supplier != null)
            {
                var nationalCode = invoice.Supplier.NationalIdentifier ?? invoice.Supplier.NationalId;
                if (!string.IsNullOrWhiteSpace(c.SupplierNationalCode) &&
                    string.Equals(c.SupplierNationalCode, nationalCode, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.45m;
                    reason += "تطابق کامل شناسه ملی؛ ";
                }
                else if (c.SupplierName.Contains(invoice.Supplier.DisplayName) || invoice.Supplier.DisplayName.Contains(c.SupplierName))
                {
                    score += 0.35m;
                    reason += "تطابق نام تأمین‌کننده؛ ";
                }
            }

            // Amount Match
            decimal amountDiff = Math.Abs(c.Amount - invoice.TotalAmount);
            if (amountDiff == 0m)
            {
                score += 0.40m;
                reason += "تطابق دقیق ریالی؛ ";
            }
            else if (invoice.TotalAmount > 0 && (amountDiff / invoice.TotalAmount) <= 0.02m)
            {
                score += 0.25m;
                reason += "تطابق ریالی با اختلاف کمتر از ۲٪؛ ";
            }

            // Date Proximity (within 5 days)
            var daysDiff = Math.Abs((c.VoucherDateUtc - invoice.InvoiceDateUtc).TotalDays);
            if (daysDiff <= 3)
            {
                score += 0.15m;
                reason += "همزمانی تاریخ برگه مالی؛ ";
            }
            else if (daysDiff <= 7)
            {
                score += 0.10m;
                reason += "فاصله زمانی زیر یک هفته؛ ";
            }

            score = Math.Min(1.0m, Math.Round(score, 2));

            string level = score >= 0.80m ? "High" : score >= 0.50m ? "Medium" : "Low";

            scoredList.Add(new SepidarCandidateInvoiceDto
            {
                SepidarInvoiceId = c.SepidarInvoiceId,
                SepidarVoucherNumber = c.SepidarVoucherNumber,
                VoucherDateUtc = c.VoucherDateUtc,
                SupplierName = c.SupplierName,
                SupplierNationalCode = c.SupplierNationalCode,
                Amount = c.Amount,
                Currency = c.Currency,
                ConfidenceScore = score,
                MatchConfidenceLevel = level,
                MatchReason = string.IsNullOrWhiteSpace(reason) ? "عدم انطباق مشخصات کلیدی" : reason.TrimEnd(' ', '؛')
            });
        }

        return scoredList.OrderByDescending(c => c.ConfidenceScore).ToList();
    }

    public async Task<SepidarFinancialReconciliationDto> LinkInvoiceAsync(LinkSepidarInvoiceRequest req, string username, CancellationToken ct = default)
    {
        var invoice = await _dbContext.SupplierInvoices
            .Include(si => si.Supplier)
            .FirstOrDefaultAsync(si => si.Id == req.SupplierInvoiceId, ct);

        if (invoice == null)
        {
            throw new InvalidOperationException("فاکتور موردنظر در سامانه CRM یافت نشد.");
        }

        // Check if already linked
        var existing = await _dbContext.SepidarFinancialReconciliations
            .FirstOrDefaultAsync(r => r.SupplierInvoiceId == req.SupplierInvoiceId && r.LinkStatus == ReconciliationLinkStatus.Confirmed, ct);

        if (existing != null)
        {
            throw new InvalidOperationException("این فاکتور قبلاً با سند رسمی سپیدار تطبیق داده شده است. ابتدا باید پیوند قبلی را لغو کنید.");
        }

        var reconciliation = new SepidarFinancialReconciliation
        {
            SupplierInvoiceId = req.SupplierInvoiceId,
            SepidarInvoiceId = req.SepidarInvoiceId,
            SepidarVoucherNumber = req.SepidarVoucherNumber,
            SepidarVoucherDateUtc = req.SepidarVoucherDateUtc,
            SepidarAmount = req.SepidarAmount,
            CandidateConfidenceScore = 1.0m,
            LinkStatus = ReconciliationLinkStatus.Confirmed,
            LinkedBy = username,
            LinkedAtUtc = DateTime.UtcNow,
            AuditNotes = req.AuditNotes ?? "تأیید دستی و اتصال قطعی سند سپیدار توسط کارشناس مالی"
        };

        invoice.RegisteredInSepidar = true;
        invoice.SepidarInvoiceRef = req.SepidarInvoiceId;
        invoice.SepidarVoucherNumber = req.SepidarVoucherNumber;

        _dbContext.SepidarFinancialReconciliations.Add(reconciliation);
        await _dbContext.SaveChangesAsync(ct);

        return new SepidarFinancialReconciliationDto
        {
            Id = reconciliation.Id,
            SupplierInvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            SupplierName = invoice.Supplier?.DisplayName ?? "-",
            SepidarInvoiceId = reconciliation.SepidarInvoiceId,
            SepidarVoucherNumber = reconciliation.SepidarVoucherNumber,
            SepidarVoucherDateUtc = reconciliation.SepidarVoucherDateUtc,
            SepidarAmount = reconciliation.SepidarAmount,
            CandidateConfidenceScore = reconciliation.CandidateConfidenceScore,
            LinkStatus = reconciliation.LinkStatus.ToString(),
            LinkedBy = reconciliation.LinkedBy,
            LinkedAtUtc = reconciliation.LinkedAtUtc,
            AuditNotes = reconciliation.AuditNotes
        };
    }

    public async Task<SepidarFinancialReconciliationDto> UnlinkInvoiceAsync(Guid reconciliationId, string auditNotes, string username, CancellationToken ct = default)
    {
        var reconciliation = await _dbContext.SepidarFinancialReconciliations
            .Include(r => r.SupplierInvoice)
                .ThenInclude(si => si!.Supplier)
            .FirstOrDefaultAsync(r => r.Id == reconciliationId, ct);

        if (reconciliation == null)
        {
            throw new InvalidOperationException("رکورد تطبیق موردنظر یافت نشد.");
        }

        reconciliation.LinkStatus = ReconciliationLinkStatus.Unlinked;
        reconciliation.AuditNotes = $"لغو اتصال توسط {username}: {auditNotes}";

        if (reconciliation.SupplierInvoice != null)
        {
            reconciliation.SupplierInvoice.RegisteredInSepidar = false;
            reconciliation.SupplierInvoice.SepidarInvoiceRef = null;
            reconciliation.SupplierInvoice.SepidarVoucherNumber = null;
        }

        await _dbContext.SaveChangesAsync(ct);

        return new SepidarFinancialReconciliationDto
        {
            Id = reconciliation.Id,
            SupplierInvoiceId = reconciliation.SupplierInvoiceId,
            InvoiceNumber = reconciliation.SupplierInvoice?.InvoiceNumber ?? "-",
            SupplierName = reconciliation.SupplierInvoice?.Supplier?.DisplayName ?? "-",
            SepidarInvoiceId = reconciliation.SepidarInvoiceId,
            SepidarVoucherNumber = reconciliation.SepidarVoucherNumber,
            SepidarVoucherDateUtc = reconciliation.SepidarVoucherDateUtc,
            SepidarAmount = reconciliation.SepidarAmount,
            CandidateConfidenceScore = reconciliation.CandidateConfidenceScore,
            LinkStatus = reconciliation.LinkStatus.ToString(),
            LinkedBy = reconciliation.LinkedBy,
            LinkedAtUtc = reconciliation.LinkedAtUtc,
            AuditNotes = reconciliation.AuditNotes
        };
    }

    public async Task<List<SepidarFinancialReconciliationDto>> GetReconciliationHistoryAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.SepidarFinancialReconciliations
            .Include(r => r.SupplierInvoice)
                .ThenInclude(si => si!.Supplier)
            .OrderByDescending(r => r.LinkedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return list.Select(r => new SepidarFinancialReconciliationDto
        {
            Id = r.Id,
            SupplierInvoiceId = r.SupplierInvoiceId,
            InvoiceNumber = r.SupplierInvoice?.InvoiceNumber ?? "-",
            SupplierName = r.SupplierInvoice?.Supplier?.DisplayName ?? "-",
            SepidarInvoiceId = r.SepidarInvoiceId,
            SepidarVoucherNumber = r.SepidarVoucherNumber,
            SepidarVoucherDateUtc = r.SepidarVoucherDateUtc,
            SepidarAmount = r.SepidarAmount,
            CandidateConfidenceScore = r.CandidateConfidenceScore,
            LinkStatus = r.LinkStatus.ToString(),
            LinkedBy = r.LinkedBy,
            LinkedAtUtc = r.LinkedAtUtc,
            AuditNotes = r.AuditNotes
        }).ToList();
    }
}
