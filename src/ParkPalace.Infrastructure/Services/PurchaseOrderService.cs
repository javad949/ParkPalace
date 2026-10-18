using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ICrmDbContext _dbContext;

    public PurchaseOrderService(ICrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<PurchaseOrderDto>> GetOrdersAsync(int page, int pageSize, string? status, string? search, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _dbContext.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .Include(po => po.Lines)
                .ThenInclude(l => l.Material)
            .Include(po => po.Lines)
                .ThenInclude(l => l.Unit)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PurchaseOrderStatus>(status, true, out var stEnum))
        {
            query = query.Where(po => po.Status == stEnum);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(po => po.OrderNumber.ToLower().Contains(s) ||
                                      (po.Supplier != null && po.Supplier.DisplayName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(po => po.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PurchaseOrderDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<PurchaseOrderDto?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Material)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Unit)
            .Include(p => p.Audits)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return po != null ? MapToDto(po) : null;
    }

    public async Task<PurchaseOrderDto> CreateOrderAsync(CreatePurchaseOrderRequest req, string username, CancellationToken ct = default)
    {
        if (req.Lines == null || req.Lines.Count == 0)
        {
            throw new InvalidOperationException("حداقل یک سطر برای ایجاد سفارش خرید الزامی است.");
        }

        var supplier = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == req.SupplierId, ct);
        if (supplier == null)
        {
            throw new InvalidOperationException("تأمین‌کننده انتخاب‌شده در پایگاه داده یافت نشد.");
        }

        var warehouseId = req.WarehouseId;
        if (warehouseId == null || warehouseId == Guid.Empty)
        {
            var defaultWh = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN", ct);
            warehouseId = defaultWh?.Id ?? Guid.Empty;
        }

        var countToday = await _dbContext.PurchaseOrders.CountAsync(ct) + 1;
        var orderNumber = $"PO-{DateTime.UtcNow:yyyyMM}-{countToday:D4}";

        var po = new PurchaseOrder
        {
            OrderNumber = orderNumber,
            SupplierId = req.SupplierId,
            WarehouseId = warehouseId.Value,
            Currency = "IRR",
            ExchangeRate = 1.0m,
            ExpectedDeliveryDateUtc = req.ExpectedDeliveryDateUtc,
            PaymentTerms = req.PaymentTerms,
            DeliveryTerms = req.DeliveryTerms,
            Notes = req.Notes,
            Status = PurchaseOrderStatus.Draft,
            CreatedBy = username,
            CreatedAtUtc = DateTime.UtcNow
        };

        decimal subTotal = 0m;
        decimal taxTotal = 0m;
        decimal discountTotal = 0m;

        int lineNo = 1;
        foreach (var lReq in req.Lines)
        {
            var material = await _dbContext.Materials
                .Include(m => m.BaseUnit)
                .FirstOrDefaultAsync(m => m.Id == lReq.MaterialId, ct);

            if (material == null)
            {
                throw new InvalidOperationException($"ماده اولیه با شناسه {lReq.MaterialId} یافت نشد.");
            }

            var unitId = lReq.UnitId ?? material.BaseUnitId;
            var unit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == unitId, ct);

            decimal lineSub = lReq.OrderedQuantity * lReq.UnitPrice;
            decimal lineTax = lineSub * lReq.TaxRate;
            decimal lineDiscount = lReq.DiscountAmount;
            decimal lineTotal = lineSub + lineTax - lineDiscount;

            subTotal += lineSub;
            taxTotal += lineTax;
            discountTotal += lineDiscount;

            var poLine = new PurchaseOrderLine
            {
                LineNumber = lineNo++,
                MaterialId = lReq.MaterialId,
                OrderedQuantity = lReq.OrderedQuantity,
                UnitId = unitId,
                UnitPrice = lReq.UnitPrice,
                TaxRate = lReq.TaxRate,
                TaxAmount = lineTax,
                DiscountAmount = lineDiscount,
                LineTotal = lineTotal,
                Notes = lReq.Notes,
                ReceivedQuantity = 0m,
                InvoicedQuantity = 0m
            };

            po.Lines.Add(poLine);
        }

        po.SubTotal = subTotal;
        po.TaxTotal = taxTotal;
        po.DiscountTotal = discountTotal;
        po.GrandTotal = subTotal + taxTotal - discountTotal;

        // Direct PO issuance by Procurement without manager approval (SRS FR-05)
        if (req.DirectIssue)
        {
            po.Status = PurchaseOrderStatus.Issued;
            po.IssuedByUsername = username;
            po.IssuedAtUtc = DateTime.UtcNow;
            po.ConcurrencyToken = Guid.NewGuid().ToString();

            foreach (var line in po.Lines)
            {
                var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == line.MaterialId, ct);
                var unit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == line.UnitId, ct);

                line.MaterialCodeSnapshot = material?.InternalCode ?? "UNKNOWN";
                line.MaterialNameSnapshot = material?.Name ?? "UNKNOWN";
                line.UnitCodeSnapshot = unit?.Code ?? "KG";
                line.UnitNameSnapshot = unit?.Name ?? "کیلوگرم";
                line.SupplierNameSnapshot = supplier.DisplayName;
            }

            po.Audits.Add(new PurchaseOrderAudit
            {
                PurchaseOrderId = po.Id,
                Action = "DirectlyIssuedByProcurement",
                PerformedBy = username,
                TimestampUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    po.OrderNumber,
                    po.GrandTotal,
                    Note = "Direct PO issued by Procurement without manager approval"
                })
            });
        }
        else
        {
            po.Audits.Add(new PurchaseOrderAudit
            {
                PurchaseOrderId = po.Id,
                Action = "Created",
                PerformedBy = username,
                TimestampUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new { po.OrderNumber, po.GrandTotal, LineCount = po.Lines.Count })
            });
        }

        _dbContext.PurchaseOrders.Add(po);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetOrderByIdAsync(po.Id, ct))!;
    }

    public async Task<PurchaseOrderDto> IssueOrderAsync(Guid id, string username, string concurrencyToken, CancellationToken ct = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Material)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Unit)
            .Include(p => p.Audits)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (po == null)
        {
            throw new InvalidOperationException("سفارش خرید موردنظر یافت نشد.");
        }

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException($"تنها سفارشات در وضعیت پیش‌نویس قابل صدور هستند. وضعیت فعلی: {po.Status}");
        }

        if (!string.Equals(po.ConcurrencyToken, concurrencyToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new DbUpdateConcurrencyException("اطلاعات سفارش توسط کاربر دیگری به‌روزرسانی شده است. لطفاً صفحه را تازه‌سازی کنید.");
        }

        // Direct PO issuance: freeze immutable snapshots of prices, quantities, and descriptions
        po.Status = PurchaseOrderStatus.Issued;
        po.IssuedByUsername = username;
        po.IssuedAtUtc = DateTime.UtcNow;
        po.UpdatedAtUtc = DateTime.UtcNow;
        po.UpdatedBy = username;
        po.ConcurrencyToken = Guid.NewGuid().ToString();

        foreach (var line in po.Lines)
        {
            line.MaterialCodeSnapshot = line.Material?.InternalCode ?? "UNKNOWN";
            line.MaterialNameSnapshot = line.Material?.Name ?? "UNKNOWN";
            line.UnitCodeSnapshot = line.Unit?.Code ?? "KG";
            line.UnitNameSnapshot = line.Unit?.Name ?? "کیلوگرم";
            line.SupplierNameSnapshot = po.Supplier?.DisplayName ?? "UNKNOWN";
        }

        po.Audits.Add(new PurchaseOrderAudit
        {
            PurchaseOrderId = po.Id,
            Action = "DirectlyIssuedByProcurement",
            PerformedBy = username,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                po.OrderNumber,
                po.GrandTotal,
                Note = "Direct PO issued by Procurement without manager approval"
            })
        });

        await _dbContext.SaveChangesAsync(ct);
        return (await GetOrderByIdAsync(po.Id, ct))!;
    }

    public async Task<PurchaseOrderDto> CancelOrderAsync(Guid id, CancelPurchaseOrderRequest req, string username, CancellationToken ct = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Receipts)
            .Include(p => p.Audits)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (po == null)
        {
            throw new InvalidOperationException("سفارش خرید موردنظر یافت نشد.");
        }

        if (po.Status == PurchaseOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("سفارش پیش از این ابطال شده است.");
        }

        if (!string.Equals(po.ConcurrencyToken, req.ConcurrencyToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new DbUpdateConcurrencyException("تداخل همزمانی: سفارش همزمان توسط کاربر دیگری تغییر یافته است.");
        }

        // Cancellation rules: check if any receipts were accepted
        var acceptedReceiptsCount = po.Receipts.Count(r => r.Status == GoodsReceiptStatus.Accepted || r.Status == GoodsReceiptStatus.PartiallyAccepted);
        if (acceptedReceiptsCount > 0)
        {
            throw new InvalidOperationException("به دلیل وجود رسید انبار پذیرفته‌شده برای این سفارش، امکان ابطال مستقیم سفارش خرید وجود ندارد.");
        }

        po.Status = PurchaseOrderStatus.Cancelled;
        po.CancelledBy = username;
        po.CancellationReason = req.Reason;
        po.CancelledAtUtc = DateTime.UtcNow;
        po.UpdatedAtUtc = DateTime.UtcNow;
        po.UpdatedBy = username;
        po.ConcurrencyToken = Guid.NewGuid().ToString();

        po.Audits.Add(new PurchaseOrderAudit
        {
            PurchaseOrderId = po.Id,
            Action = "Cancelled",
            PerformedBy = username,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Reason = req.Reason })
        });

        await _dbContext.SaveChangesAsync(ct);
        return (await GetOrderByIdAsync(po.Id, ct))!;
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po)
    {
        return new PurchaseOrderDto
        {
            Id = po.Id,
            OrderNumber = po.OrderNumber,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.DisplayName ?? "-",
            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse?.Name ?? "-",
            Status = po.Status.ToString(),
            Currency = po.Currency,
            ExchangeRate = po.ExchangeRate,
            SubTotal = po.SubTotal,
            TaxTotal = po.TaxTotal,
            DiscountTotal = po.DiscountTotal,
            GrandTotal = po.GrandTotal,
            IssuedByUsername = po.IssuedByUsername,
            IssuedAtUtc = po.IssuedAtUtc,
            ExpectedDeliveryDateUtc = po.ExpectedDeliveryDateUtc,
            PaymentTerms = po.PaymentTerms,
            DeliveryTerms = po.DeliveryTerms,
            Notes = po.Notes,
            ConcurrencyToken = po.ConcurrencyToken,
            CreatedAtUtc = po.CreatedAtUtc,
            CreatedBy = po.CreatedBy,
            LineCount = po.Lines.Count,
            TotalOrderedQuantity = po.Lines.Sum(l => l.OrderedQuantity),
            TotalReceivedQuantity = po.Lines.Sum(l => l.ReceivedQuantity),
            TotalInvoicedQuantity = po.Lines.Sum(l => l.InvoicedQuantity),
            Lines = po.Lines.Select(l => new PurchaseOrderLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                MaterialId = l.MaterialId,
                MaterialCode = l.Material?.InternalCode ?? l.MaterialCodeSnapshot ?? "-",
                MaterialName = l.Material?.Name ?? l.MaterialNameSnapshot ?? "-",
                OrderedQuantity = l.OrderedQuantity,
                UnitId = l.UnitId,
                UnitName = l.Unit?.Name ?? l.UnitNameSnapshot ?? "کیلوگرم",
                UnitCode = l.Unit?.Code ?? l.UnitCodeSnapshot ?? "KG",
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                TaxAmount = l.TaxAmount,
                DiscountAmount = l.DiscountAmount,
                LineTotal = l.LineTotal,
                ReceivedQuantity = l.ReceivedQuantity,
                InvoicedQuantity = l.InvoicedQuantity,
                MaterialCodeSnapshot = l.MaterialCodeSnapshot,
                MaterialNameSnapshot = l.MaterialNameSnapshot,
                UnitCodeSnapshot = l.UnitCodeSnapshot,
                UnitNameSnapshot = l.UnitNameSnapshot,
                SupplierNameSnapshot = l.SupplierNameSnapshot,
                Notes = l.Notes
            }).OrderBy(l => l.LineNumber).ToList(),
            Audits = po.Audits.Select(a => new PurchaseOrderAuditDto
            {
                Id = a.Id,
                Action = a.Action,
                PerformedBy = a.PerformedBy,
                TimestampUtc = a.TimestampUtc,
                DetailsJson = a.DetailsJson
            }).OrderByDescending(a => a.TimestampUtc).ToList()
        };
    }
}
