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

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly ICrmDbContext _dbContext;

    public GoodsReceiptService(ICrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<GoodsReceiptDto>> GetReceiptsAsync(int page, int pageSize, Guid? poId, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _dbContext.GoodsReceipts
            .Include(gr => gr.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(gr => gr.Warehouse)
            .Include(gr => gr.Lines)
                .ThenInclude(l => l.Material)
            .Include(gr => gr.Lines)
                .ThenInclude(l => l.Unit)
            .AsNoTracking()
            .AsQueryable();

        if (poId.HasValue && poId.Value != Guid.Empty)
        {
            query = query.Where(gr => gr.PurchaseOrderId == poId.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(gr => gr.ReceiptDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<GoodsReceiptDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<GoodsReceiptDto?> GetReceiptByIdAsync(Guid id, CancellationToken ct = default)
    {
        var gr = await _dbContext.GoodsReceipts
            .Include(g => g.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.Lines)
                .ThenInclude(l => l.Material)
            .Include(g => g.Lines)
                .ThenInclude(l => l.Unit)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        return gr != null ? MapToDto(gr) : null;
    }

    public async Task<GoodsReceiptDto> CreateReceiptAsync(CreateGoodsReceiptRequest req, string username, CancellationToken ct = default)
    {
        if (req.Lines == null || req.Lines.Count == 0)
        {
            throw new InvalidOperationException("حداقل یک سطر برای ثبت رسید انبار الزامی است.");
        }

        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Receipts)
            .FirstOrDefaultAsync(p => p.Id == req.PurchaseOrderId, ct);

        if (po == null)
        {
            throw new InvalidOperationException("سفارش خرید موردنظر یافت نشد.");
        }

        if (po.Status != PurchaseOrderStatus.Issued && po.Status != PurchaseOrderStatus.PartiallyReceived)
        {
            throw new InvalidOperationException($"رسید انبار تنها برای سفارشات در وضعیت 'صادرشده' یا 'تحویل جزئی' قابل ثبت است. وضعیت فعلی: {po.Status}");
        }

        var warehouseId = req.WarehouseId ?? po.WarehouseId;
        var countToday = await _dbContext.GoodsReceipts.CountAsync(ct) + 1;
        var receiptNumber = $"GRN-{DateTime.UtcNow:yyyyMM}-{countToday:D4}";

        var gr = new GoodsReceipt
        {
            ReceiptNumber = receiptNumber,
            PurchaseOrderId = po.Id,
            WarehouseId = warehouseId,
            ReceivedByUsername = username,
            ReceiptDateUtc = DateTime.UtcNow,
            CarrierName = req.CarrierName,
            DeliveryNoteNumber = req.DeliveryNoteNumber,
            VehiclePlateNumber = req.VehiclePlateNumber,
            SepidarMovementRef = req.SepidarMovementRef,
            Notes = req.Notes,
            Status = GoodsReceiptStatus.Accepted
        };

        foreach (var lReq in req.Lines)
        {
            var poLine = po.Lines.FirstOrDefault(l => l.Id == lReq.PurchaseOrderLineId);
            if (poLine == null)
            {
                throw new InvalidOperationException($"سطر سفارش با شناسه {lReq.PurchaseOrderLineId} در این سفارش خرید یافت نشد.");
            }

            // Over-receipt protection: allowable delivery cannot exceed 105% of ordered quantity
            decimal maxAllowable = poLine.OrderedQuantity * 1.05m;
            decimal newCumulative = poLine.ReceivedQuantity + lReq.AcceptedQuantity;
            if (newCumulative > maxAllowable)
            {
                throw new InvalidOperationException($"کسری/مازاد غیرمجاز: مقدار تجمعی دریافتی ({newCumulative}) از سقف مجاز سفارش ({maxAllowable}) فراتر است.");
            }

            Enum.TryParse<QcStatus>(lReq.QcStatus, true, out var qcEnum);

            var grLine = new GoodsReceiptLine
            {
                PurchaseOrderLineId = poLine.Id,
                MaterialId = poLine.MaterialId,
                DeliveredQuantity = lReq.DeliveredQuantity,
                AcceptedQuantity = lReq.AcceptedQuantity,
                RejectedQuantity = lReq.RejectedQuantity,
                UnitId = poLine.UnitId,
                QcStatus = qcEnum,
                RejectionReason = lReq.RejectionReason,
                BatchNumber = lReq.BatchNumber,
                ExpiryDateUtc = lReq.ExpiryDateUtc,
                StorageLocation = lReq.StorageLocation,
                Notes = lReq.Notes
            };

            gr.Lines.Add(grLine);

            // Update PO Line Received Quantity
            poLine.ReceivedQuantity += lReq.AcceptedQuantity;
        }

        // Recalculate PO overall status
        bool allFullyReceived = po.Lines.All(l => l.ReceivedQuantity >= l.OrderedQuantity);
        bool anyReceived = po.Lines.Any(l => l.ReceivedQuantity > 0m);

        if (allFullyReceived)
        {
            po.Status = PurchaseOrderStatus.FullyReceived;
        }
        else if (anyReceived)
        {
            po.Status = PurchaseOrderStatus.PartiallyReceived;
        }

        _dbContext.GoodsReceipts.Add(gr);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetReceiptByIdAsync(gr.Id, ct))!;
    }

    private static GoodsReceiptDto MapToDto(GoodsReceipt gr)
    {
        return new GoodsReceiptDto
        {
            Id = gr.Id,
            ReceiptNumber = gr.ReceiptNumber,
            PurchaseOrderId = gr.PurchaseOrderId,
            OrderNumber = gr.PurchaseOrder?.OrderNumber ?? "-",
            SupplierName = gr.PurchaseOrder?.Supplier?.DisplayName ?? "-",
            WarehouseId = gr.WarehouseId,
            WarehouseName = gr.Warehouse?.Name ?? "-",
            ReceivedByUsername = gr.ReceivedByUsername,
            ReceiptDateUtc = gr.ReceiptDateUtc,
            CarrierName = gr.CarrierName,
            DeliveryNoteNumber = gr.DeliveryNoteNumber,
            VehiclePlateNumber = gr.VehiclePlateNumber,
            Status = gr.Status.ToString(),
            SepidarMovementRef = gr.SepidarMovementRef,
            Notes = gr.Notes,
            Lines = gr.Lines.Select(l => new GoodsReceiptLineDto
            {
                Id = l.Id,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                MaterialId = l.MaterialId,
                MaterialCode = l.Material?.InternalCode ?? "-",
                MaterialName = l.Material?.Name ?? "-",
                DeliveredQuantity = l.DeliveredQuantity,
                AcceptedQuantity = l.AcceptedQuantity,
                RejectedQuantity = l.RejectedQuantity,
                UnitName = l.Unit?.Name ?? "کیلوگرم",
                UnitCode = l.Unit?.Code ?? "KG",
                QcStatus = l.QcStatus.ToString(),
                RejectionReason = l.RejectionReason,
                BatchNumber = l.BatchNumber,
                ExpiryDateUtc = l.ExpiryDateUtc,
                StorageLocation = l.StorageLocation,
                Notes = l.Notes
            }).ToList()
        };
    }
}
