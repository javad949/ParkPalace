using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class ThreeWayMatchingEngine : IThreeWayMatchingEngine
{
    private readonly ICrmDbContext _dbContext;

    public ThreeWayMatchingEngine(ICrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ThreeWayMatchResultDto> EvaluateMatchAsync(Guid invoiceId, string username, decimal tolerancePercent = 1.0m, CancellationToken ct = default)
    {
        var invoice = await _dbContext.SupplierInvoices
            .Include(si => si.Supplier)
            .Include(si => si.Lines)
                .ThenInclude(l => l.Material)
            .Include(si => si.Lines)
                .ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(si => si.Id == invoiceId, ct);

        if (invoice == null)
        {
            throw new InvalidOperationException("فاکتور تأمین‌کننده موردنظر یافت نشد.");
        }

        if (!invoice.PurchaseOrderId.HasValue)
        {
            throw new InvalidOperationException("این فاکتور به هیچ سفارش خریدی متصل نشده است و امکان تطبیق سه‌طرفه خودکار ندارد.");
        }

        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Material)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Unit)
            .Include(p => p.Receipts)
                .ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(p => p.Id == invoice.PurchaseOrderId.Value, ct);

        if (po == null)
        {
            throw new InvalidOperationException("سفارش خرید متصل به این فاکتور در سامانه یافت نشد.");
        }

        // Find relevant Goods Receipt (if any)
        var acceptedReceipts = po.Receipts
            .Where(r => r.Status == GoodsReceiptStatus.Accepted || r.Status == GoodsReceiptStatus.PartiallyAccepted)
            .ToList();

        var latestReceipt = acceptedReceipts.OrderByDescending(r => r.ReceiptDateUtc).FirstOrDefault();

        // Remove previous evaluation result if exists
        var existingResult = await _dbContext.ThreeWayMatchResults
            .Include(r => r.LineVariances)
            .FirstOrDefaultAsync(r => r.SupplierInvoiceId == invoiceId, ct);

        if (existingResult != null)
        {
            _dbContext.ThreeWayMatchResults.Remove(existingResult);
            await _dbContext.SaveChangesAsync(ct);
        }

        var matchResult = new ThreeWayMatchResult
        {
            PurchaseOrderId = po.Id,
            GoodsReceiptId = latestReceipt?.Id,
            SupplierInvoiceId = invoice.Id,
            EvaluatedAtUtc = DateTime.UtcNow,
            EvaluatedBy = username,
            TolerancePercentageApplied = tolerancePercent,
            IsResolved = false
        };

        decimal totalVarianceAmount = 0m;
        bool hasMissingReceipt = acceptedReceipts.Count == 0;
        bool hasPriceMismatch = false;
        bool hasQuantityMismatch = false;
        bool allLinesExact = true;
        bool allLinesWithinTolerance = true;

        foreach (var invLine in invoice.Lines)
        {
            // Find corresponding PO line
            PurchaseOrderLine? poLine = null;
            if (invLine.PurchaseOrderLineId.HasValue)
            {
                poLine = po.Lines.FirstOrDefault(l => l.Id == invLine.PurchaseOrderLineId.Value);
            }
            poLine ??= po.Lines.FirstOrDefault(l => l.MaterialId == invLine.MaterialId);

            decimal poQty = poLine?.OrderedQuantity ?? 0m;
            decimal poPrice = poLine?.UnitPrice ?? 0m;
            decimal poLineTotal = poLine?.LineTotal ?? 0m;

            // Find received quantity across accepted receipts
            decimal receivedQty = 0m;
            if (poLine != null)
            {
                receivedQty = acceptedReceipts
                    .SelectMany(r => r.Lines)
                    .Where(rl => rl.PurchaseOrderLineId == poLine.Id && rl.QcStatus == QcStatus.Passed)
                    .Sum(rl => rl.AcceptedQuantity);
            }

            decimal qtyVariance = invLine.InvoicedQuantity - receivedQty;
            decimal priceVariance = invLine.UnitPrice - poPrice;
            decimal priceVariancePercent = poPrice > 0 ? (priceVariance / poPrice) * 100m : 0m;
            decimal lineTotalVariance = invLine.LineTotal - (receivedQty * poPrice);

            totalVarianceAmount += Math.Abs(lineTotalVariance);

            string mismatchType = "Exact";
            if (receivedQty == 0m)
            {
                mismatchType = "MissingReceipt";
                hasMissingReceipt = true;
                allLinesExact = false;
                allLinesWithinTolerance = false;
            }
            else if (invLine.InvoicedQuantity > receivedQty)
            {
                mismatchType = "QuantityOverage"; // Invoiced more than accepted in warehouse
                hasQuantityMismatch = true;
                allLinesExact = false;
                allLinesWithinTolerance = false;
            }
            else if (invLine.InvoicedQuantity < receivedQty)
            {
                mismatchType = "QuantityShortage"; // Invoiced less than received (partial invoice)
                allLinesExact = false;
            }

            if (priceVariance > 0m)
            {
                if (priceVariancePercent > tolerancePercent)
                {
                    mismatchType = mismatchType == "Exact" ? "PriceIncrease" : $"{mismatchType}_PriceIncrease";
                    hasPriceMismatch = true;
                    allLinesExact = false;
                    allLinesWithinTolerance = false;
                }
                else
                {
                    allLinesExact = false; // within tolerance
                }
            }
            else if (priceVariance < 0m)
            {
                mismatchType = mismatchType == "Exact" ? "PriceDiscount" : $"{mismatchType}_PriceDiscount";
                allLinesExact = false;
            }

            var lineVar = new ThreeWayMatchLineVariance
            {
                PurchaseOrderLineId = poLine?.Id,
                SupplierInvoiceLineId = invLine.Id,
                MaterialId = invLine.MaterialId,
                PoQuantity = poQty,
                ReceivedQuantity = receivedQty,
                InvoicedQuantity = invLine.InvoicedQuantity,
                QuantityVariance = qtyVariance,
                PoUnitPrice = poPrice,
                InvoicedUnitPrice = invLine.UnitPrice,
                PriceVariance = priceVariance,
                PriceVariancePercent = Math.Round(priceVariancePercent, 2),
                PoLineTotal = poLineTotal,
                InvoicedLineTotal = invLine.LineTotal,
                TotalVariance = Math.Round(lineTotalVariance, 2),
                MismatchType = mismatchType
            };

            matchResult.LineVariances.Add(lineVar);
        }

        matchResult.TotalVarianceAmount = Math.Round(totalVarianceAmount, 2);

        // Determine Overall Match Status
        if (hasMissingReceipt)
        {
            matchResult.MatchStatus = ThreeWayMatchStatus.MissingReceipt;
            invoice.Status = SupplierInvoiceStatus.ExceptionPendingResolution;
        }
        else if (hasPriceMismatch)
        {
            matchResult.MatchStatus = ThreeWayMatchStatus.PriceMismatch;
            invoice.Status = SupplierInvoiceStatus.ExceptionPendingResolution;
        }
        else if (hasQuantityMismatch)
        {
            matchResult.MatchStatus = ThreeWayMatchStatus.QuantityMismatch;
            invoice.Status = SupplierInvoiceStatus.ExceptionPendingResolution;
        }
        else if (allLinesExact)
        {
            matchResult.MatchStatus = ThreeWayMatchStatus.ExactMatch;
            matchResult.IsResolved = true;
            matchResult.ResolutionAction = "AutoMatchedExact";
            matchResult.ResolvedAtUtc = DateTime.UtcNow;
            matchResult.ResolvedBy = "System";
            invoice.Status = SupplierInvoiceStatus.Matched;
        }
        else if (allLinesWithinTolerance)
        {
            matchResult.MatchStatus = ThreeWayMatchStatus.WithinTolerance;
            matchResult.IsResolved = true;
            matchResult.ResolutionAction = "AutoMatchedWithinTolerance";
            matchResult.ResolvedAtUtc = DateTime.UtcNow;
            matchResult.ResolvedBy = "System";
            invoice.Status = SupplierInvoiceStatus.Matched;
        }
        else
        {
            matchResult.MatchStatus = ThreeWayMatchStatus.ManualReviewRequired;
            invoice.Status = SupplierInvoiceStatus.ExceptionPendingResolution;
        }

        _dbContext.ThreeWayMatchResults.Add(matchResult);
        await _dbContext.SaveChangesAsync(ct);

        return await GetMatchResultByInvoiceIdAsync(invoice.Id, ct) ?? throw new InvalidOperationException("خطا در بازیابی نتیجه تطبیق");
    }

    public async Task<ThreeWayMatchResultDto?> GetMatchResultByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var res = await _dbContext.ThreeWayMatchResults
            .Include(r => r.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(r => r.GoodsReceipt)
            .Include(r => r.SupplierInvoice)
            .Include(r => r.LineVariances)
                .ThenInclude(v => v.Material)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SupplierInvoiceId == invoiceId, ct);

        return res != null ? MapToDto(res) : null;
    }

    public async Task<List<ThreeWayMatchResultDto>> GetExceptionQueueAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.ThreeWayMatchResults
            .Include(r => r.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(r => r.GoodsReceipt)
            .Include(r => r.SupplierInvoice)
            .Include(r => r.LineVariances)
                .ThenInclude(v => v.Material)
            .Where(r => !r.IsResolved)
            .OrderByDescending(r => r.EvaluatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<ThreeWayMatchResultDto> ResolveMatchAsync(Guid matchResultId, ResolveMatchingRequest req, string username, CancellationToken ct = default)
    {
        var match = await _dbContext.ThreeWayMatchResults
            .Include(r => r.SupplierInvoice)
            .FirstOrDefaultAsync(r => r.Id == matchResultId, ct);

        if (match == null)
        {
            throw new InvalidOperationException("نتیجه تطبیق موردنظر یافت نشد.");
        }

        match.IsResolved = true;
        match.ResolutionAction = req.ResolutionAction;
        match.ResolvedBy = username;
        match.ResolvedAtUtc = DateTime.UtcNow;
        match.ResolutionNotes = req.ResolutionNotes;

        if (match.SupplierInvoice != null)
        {
            if (req.ResolutionAction == "InvoiceRejected" || req.ResolutionAction == "Disputed")
            {
                match.SupplierInvoice.Status = SupplierInvoiceStatus.Disputed;
                match.MatchStatus = ThreeWayMatchStatus.Disputed;
            }
            else
            {
                match.SupplierInvoice.Status = SupplierInvoiceStatus.ApprovedForPayment;
                match.MatchStatus = ThreeWayMatchStatus.ApprovedWithVariance;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return (await GetMatchResultByInvoiceIdAsync(match.SupplierInvoiceId, ct))!;
    }

    private static ThreeWayMatchResultDto MapToDto(ThreeWayMatchResult r)
    {
        return new ThreeWayMatchResultDto
        {
            Id = r.Id,
            PurchaseOrderId = r.PurchaseOrderId,
            OrderNumber = r.PurchaseOrder?.OrderNumber ?? "-",
            GoodsReceiptId = r.GoodsReceiptId,
            ReceiptNumber = r.GoodsReceipt?.ReceiptNumber,
            SupplierInvoiceId = r.SupplierInvoiceId,
            InvoiceNumber = r.SupplierInvoice?.InvoiceNumber ?? "-",
            SupplierName = r.PurchaseOrder?.Supplier?.DisplayName ?? "-",
            EvaluatedAtUtc = r.EvaluatedAtUtc,
            EvaluatedBy = r.EvaluatedBy,
            MatchStatus = r.MatchStatus.ToString(),
            TotalVarianceAmount = r.TotalVarianceAmount,
            TolerancePercentageApplied = r.TolerancePercentageApplied,
            IsResolved = r.IsResolved,
            ResolutionAction = r.ResolutionAction,
            ResolvedBy = r.ResolvedBy,
            ResolvedAtUtc = r.ResolvedAtUtc,
            ResolutionNotes = r.ResolutionNotes,
            LineVariances = r.LineVariances.Select(v => new ThreeWayMatchLineVarianceDto
            {
                Id = v.Id,
                PurchaseOrderLineId = v.PurchaseOrderLineId,
                GoodsReceiptLineId = v.GoodsReceiptLineId,
                SupplierInvoiceLineId = v.SupplierInvoiceLineId,
                MaterialId = v.MaterialId,
                MaterialCode = v.Material?.InternalCode ?? "-",
                MaterialName = v.Material?.Name ?? "-",
                PoQuantity = v.PoQuantity,
                ReceivedQuantity = v.ReceivedQuantity,
                InvoicedQuantity = v.InvoicedQuantity,
                QuantityVariance = v.QuantityVariance,
                PoUnitPrice = v.PoUnitPrice,
                InvoicedUnitPrice = v.InvoicedUnitPrice,
                PriceVariance = v.PriceVariance,
                PriceVariancePercent = v.PriceVariancePercent,
                PoLineTotal = v.PoLineTotal,
                InvoicedLineTotal = v.InvoicedLineTotal,
                TotalVariance = v.TotalVariance,
                MismatchType = v.MismatchType,
                Notes = v.Notes
            }).ToList()
        };
    }
}
