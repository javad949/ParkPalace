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

public class SupplierInvoiceService : ISupplierInvoiceService
{
    private readonly ICrmDbContext _dbContext;

    public SupplierInvoiceService(ICrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<SupplierInvoiceDto>> GetInvoicesAsync(int page, int pageSize, Guid? supplierId, string? status, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _dbContext.SupplierInvoices
            .Include(si => si.Supplier)
            .Include(si => si.PurchaseOrder)
            .Include(si => si.Lines)
                .ThenInclude(l => l.Material)
            .Include(si => si.Lines)
                .ThenInclude(l => l.Unit)
            .AsNoTracking()
            .AsQueryable();

        if (supplierId.HasValue && supplierId.Value != Guid.Empty)
        {
            query = query.Where(si => si.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SupplierInvoiceStatus>(status, true, out var stEnum))
        {
            query = query.Where(si => si.Status == stEnum);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(si => si.InvoiceDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SupplierInvoiceDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<SupplierInvoiceDto?> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var si = await _dbContext.SupplierInvoices
            .Include(s => s.Supplier)
            .Include(s => s.PurchaseOrder)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Material)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Unit)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return si != null ? MapToDto(si) : null;
    }

    public async Task<SupplierInvoiceDto> CreateInvoiceAsync(CreateSupplierInvoiceRequest req, string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.InvoiceNumber))
        {
            throw new InvalidOperationException("شماره فاکتور تأمین‌کننده الزامی است.");
        }

        if (req.Lines == null || req.Lines.Count == 0)
        {
            throw new InvalidOperationException("حداقل یک سطر برای ثبت فاکتور تأمین‌کننده الزامی است.");
        }

        // Duplicate Invoice Detection: Same supplier + invoice number
        var isDuplicate = await _dbContext.SupplierInvoices.AnyAsync(
            si => si.SupplierId == req.SupplierId && si.InvoiceNumber == req.InvoiceNumber.Trim(),
            ct);

        if (isDuplicate)
        {
            throw new InvalidOperationException($"فاکتور تکراری: فاکتوری با شماره '{req.InvoiceNumber}' قبلاً برای این تأمین‌کننده ثبت شده است.");
        }

        var supplier = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == req.SupplierId, ct);
        if (supplier == null)
        {
            throw new InvalidOperationException("تأمین‌کننده موردنظر یافت نشد.");
        }

        PurchaseOrder? po = null;
        if (req.PurchaseOrderId.HasValue && req.PurchaseOrderId.Value != Guid.Empty)
        {
            po = await _dbContext.PurchaseOrders
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == req.PurchaseOrderId.Value, ct);
        }

        var invoice = new SupplierInvoice
        {
            InvoiceNumber = req.InvoiceNumber.Trim(),
            SupplierId = req.SupplierId,
            PurchaseOrderId = req.PurchaseOrderId,
            InvoiceDateUtc = req.InvoiceDateUtc,
            DueDateUtc = req.DueDateUtc,
            ReceivedDateUtc = DateTime.UtcNow,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "IRR" : req.Currency,
            ExchangeRate = 1.0m,
            TaxTotal = req.TaxTotal,
            DiscountTotal = req.DiscountTotal,
            Status = SupplierInvoiceStatus.PendingMatching,
            Notes = req.Notes,
            CreatedBy = username,
            CreatedAtUtc = DateTime.UtcNow
        };

        decimal subTotal = 0m;

        foreach (var lReq in req.Lines)
        {
            var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == lReq.MaterialId, ct);
            if (material == null)
            {
                throw new InvalidOperationException($"ماده اولیه با شناسه {lReq.MaterialId} یافت نشد.");
            }

            var unitId = lReq.UnitId ?? material.BaseUnitId;
            decimal lineSub = lReq.InvoicedQuantity * lReq.UnitPrice;
            decimal lineTotal = lineSub + lReq.TaxAmount - lReq.DiscountAmount;
            subTotal += lineSub;

            var invLine = new SupplierInvoiceLine
            {
                PurchaseOrderLineId = lReq.PurchaseOrderLineId,
                GoodsReceiptLineId = lReq.GoodsReceiptLineId,
                MaterialId = lReq.MaterialId,
                InvoicedQuantity = lReq.InvoicedQuantity,
                UnitId = unitId,
                UnitPrice = lReq.UnitPrice,
                TaxAmount = lReq.TaxAmount,
                DiscountAmount = lReq.DiscountAmount,
                LineTotal = lineTotal,
                Description = lReq.Description
            };

            invoice.Lines.Add(invLine);

            // Update PO line invoiced quantity if linked
            if (po != null && lReq.PurchaseOrderLineId.HasValue)
            {
                var poLine = po.Lines.FirstOrDefault(l => l.Id == lReq.PurchaseOrderLineId.Value);
                if (poLine != null)
                {
                    poLine.InvoicedQuantity += lReq.InvoicedQuantity;
                }
            }
        }

        invoice.SubTotal = subTotal;
        invoice.TotalAmount = subTotal + req.TaxTotal - req.DiscountTotal;

        _dbContext.SupplierInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetInvoiceByIdAsync(invoice.Id, ct))!;
    }

    private static SupplierInvoiceDto MapToDto(SupplierInvoice si)
    {
        return new SupplierInvoiceDto
        {
            Id = si.Id,
            InvoiceNumber = si.InvoiceNumber,
            SupplierId = si.SupplierId,
            SupplierName = si.Supplier?.DisplayName ?? "-",
            PurchaseOrderId = si.PurchaseOrderId,
            OrderNumber = si.PurchaseOrder?.OrderNumber,
            InvoiceDateUtc = si.InvoiceDateUtc,
            DueDateUtc = si.DueDateUtc,
            ReceivedDateUtc = si.ReceivedDateUtc,
            Currency = si.Currency,
            SubTotal = si.SubTotal,
            TaxTotal = si.TaxTotal,
            DiscountTotal = si.DiscountTotal,
            TotalAmount = si.TotalAmount,
            Status = si.Status.ToString(),
            RegisteredInSepidar = si.RegisteredInSepidar,
            SepidarInvoiceRef = si.SepidarInvoiceRef,
            SepidarVoucherNumber = si.SepidarVoucherNumber,
            AttachmentFileName = si.AttachmentFileName,
            Notes = si.Notes,
            Lines = si.Lines.Select(l => new SupplierInvoiceLineDto
            {
                Id = l.Id,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                GoodsReceiptLineId = l.GoodsReceiptLineId,
                MaterialId = l.MaterialId,
                MaterialCode = l.Material?.InternalCode ?? "-",
                MaterialName = l.Material?.Name ?? "-",
                InvoicedQuantity = l.InvoicedQuantity,
                UnitName = l.Unit?.Name ?? "کیلوگرم",
                UnitCode = l.Unit?.Code ?? "KG",
                UnitPrice = l.UnitPrice,
                TaxAmount = l.TaxAmount,
                DiscountAmount = l.DiscountAmount,
                LineTotal = l.LineTotal,
                Description = l.Description
            }).ToList()
        };
    }
}
