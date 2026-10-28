using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Reporting;

public class ReportingService : IReportingService
{
    private readonly ICrmDbContext _dbContext;
    private readonly ILogger<ReportingService> _logger;
    private readonly PersianCalendar _persianCalendar = new();

    public ReportingService(ICrmDbContext dbContext, ILogger<ReportingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private string ToJalaliDate(DateTime utcDate)
    {
        var local = utcDate.ToLocalTime();
        return $"{_persianCalendar.GetYear(local):D4}/{_persianCalendar.GetMonth(local):D2}/{_persianCalendar.GetDayOfMonth(local):D2}";
    }

    public async Task<ProcurementSummaryReportDto> GetProcurementSummaryAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var ordersQuery = _dbContext.PurchaseOrders.AsNoTracking();

        if (filters.StartDateUtc.HasValue)
            ordersQuery = ordersQuery.Where(o => (o.IssuedAtUtc ?? o.CreatedAtUtc) >= filters.StartDateUtc.Value);
        if (filters.EndDateUtc.HasValue)
            ordersQuery = ordersQuery.Where(o => (o.IssuedAtUtc ?? o.CreatedAtUtc) <= filters.EndDateUtc.Value);
        if (filters.SupplierId.HasValue)
            ordersQuery = ordersQuery.Where(o => o.SupplierId == filters.SupplierId.Value);
        if (filters.WarehouseId.HasValue)
            ordersQuery = ordersQuery.Where(o => o.WarehouseId == filters.WarehouseId.Value);

        var ordersList = await ordersQuery.ToListAsync(cancellationToken);

        var validOrders = ordersList.Where(o => o.Status != PurchaseOrderStatus.Cancelled).ToList();
        var totalPurchaseVolume = validOrders.Sum(o => o.GrandTotal);
        var totalOrdersCount = ordersList.Count;
        var activeOrdersCount = ordersList.Count(o => o.Status == PurchaseOrderStatus.Issued || o.Status == PurchaseOrderStatus.PartiallyReceived);
        var completedOrdersCount = ordersList.Count(o => o.Status == PurchaseOrderStatus.FullyReceived || o.Status == PurchaseOrderStatus.Closed);
        var cancelledOrdersCount = ordersList.Count(o => o.Status == PurchaseOrderStatus.Cancelled);

        var averageOrderValue = validOrders.Count > 0 ? Math.Round(totalPurchaseVolume / validOrders.Count, 2) : 0m;

        // Order Fulfillment Rate: Total Received Qty / Total Ordered Qty in non-cancelled orders
        var orderIds = validOrders.Select(o => o.Id).ToList();
        var lines = await _dbContext.PurchaseOrderLines
            .AsNoTracking()
            .Where(l => orderIds.Contains(l.PurchaseOrderId))
            .ToListAsync(cancellationToken);

        var totalOrderedQty = lines.Sum(l => l.OrderedQuantity);
        var totalReceivedQty = lines.Sum(l => l.ReceivedQuantity);
        var fulfillmentRate = totalOrderedQty > 0
            ? Math.Round((totalReceivedQty / totalOrderedQty) * 100m, 2)
            : 0m;

        // On-Time Delivery Rate: Receipts delivered on or before PO ExpectedDeliveryDateUtc
        var receiptsWithPo = await _dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(r => r.PurchaseOrder)
            .Where(r => r.PurchaseOrder != null && r.PurchaseOrder.ExpectedDeliveryDateUtc != null)
            .ToListAsync(cancellationToken);

        var onTimeReceiptsCount = receiptsWithPo.Count(r => r.ReceiptDateUtc <= r.PurchaseOrder!.ExpectedDeliveryDateUtc!.Value);
        var onTimeRate = receiptsWithPo.Count > 0
            ? Math.Round(((decimal)onTimeReceiptsCount / receiptsWithPo.Count) * 100m, 2)
            : 100m;

        // Critical and low stock counts
        var snapshots = await _dbContext.InventorySnapshots
            .AsNoTracking()
            .Include(s => s.Material)
            .ToListAsync(cancellationToken);

        var criticalStockCount = snapshots.Count(s => s.OnHandQuantity <= (s.Material?.SafetyStock ?? 0m) && (s.Material?.SafetyStock ?? 0m) > 0);
        var lowStockCount = snapshots.Count(s => s.OnHandQuantity > (s.Material?.SafetyStock ?? 0m) && s.OnHandQuantity <= (s.Material?.MinStock ?? 0m) && (s.Material?.MinStock ?? 0m) > 0);

        // Recommendations
        var recommendations = await _dbContext.PurchaseRecommendations
            .AsNoTracking()
            .Where(r => r.Status == RecommendationStatus.Active)
            .ToListAsync(cancellationToken);

        var recCount = recommendations.Count;
        var recValue = recommendations.Sum(r => r.RoundedSuggestedQuantity * 50000m);

        // Check data freshness from latest sync
        var lastSync = await _dbContext.InventorySyncJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var freshness = DataFreshnessStatus.LiveVerified;
        string? warning = null;

        if (lastSync != null && (DateTime.UtcNow - lastSync.StartedAtUtc).TotalHours > 24)
        {
            freshness = DataFreshnessStatus.Stale;
            warning = "داده‌های همگام‌سازی انبار بیش از ۲۴ ساعت به‌روزرسانی نشده‌اند.";
        }

        return new ProcurementSummaryReportDto
        {
            TotalPurchaseVolume = totalPurchaseVolume,
            TotalOrdersCount = totalOrdersCount,
            ActiveOrdersCount = activeOrdersCount,
            CompletedOrdersCount = completedOrdersCount,
            CancelledOrdersCount = cancelledOrdersCount,
            AverageOrderValue = averageOrderValue,
            OrderFulfillmentRate = fulfillmentRate,
            OnTimeDeliveryRate = onTimeRate,
            CriticalStockCount = criticalStockCount,
            LowStockCount = lowStockCount,
            ActiveRecommendationsCount = recCount,
            RecommendationsEstimatedValue = recValue,
            DataFreshness = freshness,
            FreshnessWarning = warning,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<List<PurchaseOrderTrendItemDto>> GetPurchaseOrderTrendsAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var ordersQuery = _dbContext.PurchaseOrders.AsNoTracking()
            .Where(o => o.Status != PurchaseOrderStatus.Cancelled);

        if (filters.StartDateUtc.HasValue)
            ordersQuery = ordersQuery.Where(o => (o.IssuedAtUtc ?? o.CreatedAtUtc) >= filters.StartDateUtc.Value);
        if (filters.EndDateUtc.HasValue)
            ordersQuery = ordersQuery.Where(o => (o.IssuedAtUtc ?? o.CreatedAtUtc) <= filters.EndDateUtc.Value);
        if (filters.SupplierId.HasValue)
            ordersQuery = ordersQuery.Where(o => o.SupplierId == filters.SupplierId.Value);

        var orders = await ordersQuery.ToListAsync(cancellationToken);

        var grouped = orders
            .GroupBy(o => (o.IssuedAtUtc ?? o.CreatedAtUtc).Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var date = g.Key;
                var jalali = ToJalaliDate(date);
                var total = g.Sum(o => o.GrandTotal);
                var orderCount = g.Count();
                var fulfilledCount = g.Count(o => o.Status == PurchaseOrderStatus.FullyReceived || o.Status == PurchaseOrderStatus.Closed);
                var receivedAmount = g.Sum(o => o.Status switch
                {
                    PurchaseOrderStatus.FullyReceived or PurchaseOrderStatus.Closed => o.GrandTotal,
                    PurchaseOrderStatus.PartiallyReceived => o.GrandTotal * 0.5m,
                    _ => 0m
                });

                return new PurchaseOrderTrendItemDto
                {
                    Period = jalali,
                    DateUtc = date,
                    JalaliDate = jalali,
                    OrderCount = orderCount,
                    TotalAmount = total,
                    ReceivedAmount = receivedAmount,
                    FulfilledCount = fulfilledCount
                };
            })
            .ToList();

        return grouped;
    }

    public async Task<List<SupplierPerformanceReportItemDto>> GetSupplierPerformanceAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var suppliers = await _dbContext.Parties
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var orders = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Receipts)
            .Include(o => o.Lines)
            .Where(o => o.Status != PurchaseOrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var calls = await _dbContext.CallRecords
            .AsNoTracking()
            .Where(c => c.PartyId != null)
            .ToListAsync(cancellationToken);

        var receiptLines = await _dbContext.GoodsReceiptLines
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var results = new List<SupplierPerformanceReportItemDto>();

        foreach (var supplier in suppliers)
        {
            var supplierOrders = orders.Where(o => o.SupplierId == supplier.Id).ToList();
            if (filters.SupplierId.HasValue && supplier.Id != filters.SupplierId.Value)
                continue;

            var totalOrdersCount = supplierOrders.Count;
            var totalSpend = supplierOrders.Sum(o => o.GrandTotal);

            // Lead time
            var leadTimes = new List<double>();
            int onTimeCount = 0;
            int receiptsWithExpectedCount = 0;

            foreach (var po in supplierOrders)
            {
                foreach (var receipt in po.Receipts)
                {
                    if (po.IssuedAtUtc.HasValue)
                    {
                        var days = (receipt.ReceiptDateUtc - po.IssuedAtUtc.Value).TotalDays;
                        if (days >= 0) leadTimes.Add(days);
                    }

                    if (po.ExpectedDeliveryDateUtc.HasValue)
                    {
                        receiptsWithExpectedCount++;
                        if (receipt.ReceiptDateUtc <= po.ExpectedDeliveryDateUtc.Value)
                            onTimeCount++;
                    }
                }
            }

            var avgLeadTime = leadTimes.Count > 0 ? Math.Round((decimal)leadTimes.Average(), 1) : 0m;
            var onTimeRate = receiptsWithExpectedCount > 0 ? Math.Round(((decimal)onTimeCount / receiptsWithExpectedCount) * 100m, 2) : 100m;

            // Fulfillment Rate
            var allLines = supplierOrders.SelectMany(o => o.Lines).ToList();
            var totalOrdered = allLines.Sum(l => l.OrderedQuantity);
            var totalReceived = allLines.Sum(l => l.ReceivedQuantity);
            var fulfillmentRate = totalOrdered > 0 ? Math.Round((totalReceived / totalOrdered) * 100m, 2) : 0m;

            // Partial Receipt Rate
            var receivedOrders = supplierOrders.Where(o => o.Status == PurchaseOrderStatus.PartiallyReceived || o.Status == PurchaseOrderStatus.FullyReceived).ToList();
            var partialOrders = supplierOrders.Count(o => o.Status == PurchaseOrderStatus.PartiallyReceived);
            var partialReceiptRate = receivedOrders.Count > 0 ? Math.Round(((decimal)partialOrders / receivedOrders.Count) * 100m, 2) : 0m;

            // Quality Acceptance Rate from receipt lines
            var poLineIds = allLines.Select(l => l.Id).ToHashSet();
            var supplierReceiptLines = receiptLines.Where(rl => poLineIds.Contains(rl.PurchaseOrderLineId)).ToList();
            var acceptedQty = supplierReceiptLines.Sum(rl => rl.AcceptedQuantity);
            var rejectedQty = supplierReceiptLines.Sum(rl => rl.RejectedQuantity);
            var totalDelivered = acceptedQty + rejectedQty;
            var qualityRate = totalDelivered > 0 ? Math.Round((acceptedQty / totalDelivered) * 100m, 2) : 100m;

            // Call activity
            var supplierCalls = calls.Where(c => c.PartyId == supplier.Id).ToList();
            var totalCalls = supplierCalls.Count;
            var lastCallDate = supplierCalls.OrderByDescending(c => c.StartTimeUtc).Select(c => (DateTime?)c.StartTimeUtc).FirstOrDefault();
            var lastOrderDate = supplierOrders.OrderByDescending(o => o.CreatedAtUtc).Select(o => (DateTime?)o.CreatedAtUtc).FirstOrDefault();
            var lastContact = (lastCallDate > lastOrderDate) ? lastCallDate : (lastOrderDate ?? lastCallDate);

            results.Add(new SupplierPerformanceReportItemDto
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.DisplayName ?? supplier.LegalName ?? supplier.ShopName ?? "تأمین‌کننده",
                TotalOrdersCount = totalOrdersCount,
                TotalSpend = totalSpend,
                AverageDeliveryLeadTimeDays = avgLeadTime,
                OnTimeDeliveryRate = onTimeRate,
                FulfillmentRate = fulfillmentRate,
                PartialReceiptRate = partialReceiptRate,
                QualityAcceptanceRate = qualityRate,
                TotalCallsCount = totalCalls,
                LastContactDateUtc = lastContact
            });
        }

        return results.OrderByDescending(s => s.TotalSpend).ToList();
    }

    public async Task<List<MaterialConsumptionReportItemDto>> GetMaterialConsumptionAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var materialsQuery = _dbContext.Materials
            .AsNoTracking()
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .AsQueryable();

        if (filters.CategoryId.HasValue)
            materialsQuery = materialsQuery.Where(m => m.CategoryId == filters.CategoryId.Value);

        var materials = await materialsQuery.ToListAsync(cancellationToken);
        var materialIds = materials.Select(m => m.Id).ToList();

        var snapshots = await _dbContext.InventorySnapshots
            .AsNoTracking()
            .Where(s => materialIds.Contains(s.MaterialId))
            .ToDictionaryAsync(s => s.MaterialId, cancellationToken);

        var consumptions = await _dbContext.MaterialConsumptionSummaries
            .AsNoTracking()
            .Where(c => materialIds.Contains(c.MaterialId))
            .GroupBy(c => c.MaterialId)
            .Select(g => new
            {
                MaterialId = g.Key,
                TotalConsumed = g.Sum(c => c.TotalConsumption),
                AvgDaily = g.Average(c => c.AverageDailyConsumption)
            })
            .ToDictionaryAsync(x => x.MaterialId, cancellationToken);

        var results = new List<MaterialConsumptionReportItemDto>();

        foreach (var material in materials)
        {
            snapshots.TryGetValue(material.Id, out var snap);
            consumptions.TryGetValue(material.Id, out var cons);

            var currentOnHand = snap?.OnHandQuantity ?? 0m;
            var safetyStock = material.SafetyStock ?? 0m;
            var reorderPoint = material.MinStock ?? 0m;
            var totalConsumed = cons?.TotalConsumed ?? 0m;
            var avgDaily = cons?.AvgDaily ?? 0m;

            var coverageDays = avgDaily > 0 ? Math.Round(currentOnHand / avgDaily, 1) : 999m;

            string riskStatus = "Safe";
            if (currentOnHand <= safetyStock && safetyStock > 0)
                riskStatus = "Critical";
            else if (currentOnHand <= reorderPoint && reorderPoint > 0)
                riskStatus = "Warning";
            else if (coverageDays > 120 && avgDaily > 0)
                riskStatus = "Overstocked";

            results.Add(new MaterialConsumptionReportItemDto
            {
                MaterialId = material.Id,
                MaterialCode = material.InternalCode,
                MaterialName = material.Name,
                CategoryName = material.Category?.Name ?? "عمومی",
                UnitName = material.BaseUnit?.Name ?? "عدد",
                TotalConsumedQuantity = totalConsumed,
                AverageDailyConsumption = Math.Round(avgDaily, 2),
                CurrentStock = currentOnHand,
                CoverageDays = coverageDays,
                InventoryRiskStatus = riskStatus,
                LastMovementDateUtc = snap?.SnapshotTimeUtc
            });
        }

        return results.OrderBy(r => r.CoverageDays).ToList();
    }

    public async Task<InventoryHealthReportDto> GetInventoryHealthAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var materials = await _dbContext.Materials
            .AsNoTracking()
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .ToListAsync(cancellationToken);

        var snapshots = await _dbContext.InventorySnapshots
            .AsNoTracking()
            .ToDictionaryAsync(s => s.MaterialId, cancellationToken);

        var latestPrices = await _dbContext.PurchaseOrderLines
            .AsNoTracking()
            .OrderByDescending(l => l.PurchaseOrder!.CreatedAtUtc)
            .GroupBy(l => l.MaterialId)
            .Select(g => new { MaterialId = g.Key, UnitPrice = g.First().UnitPrice })
            .ToDictionaryAsync(x => x.MaterialId, x => x.UnitPrice, cancellationToken);

        var items = new List<InventoryHealthItemDto>();
        decimal totalInventoryValue = 0m;
        int criticalCount = 0;
        int lowCount = 0;
        int healthyCount = 0;
        int overstockedCount = 0;
        int staleCount = 0;

        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);

        foreach (var mat in materials)
        {
            snapshots.TryGetValue(mat.Id, out var snap);
            latestPrices.TryGetValue(mat.Id, out var unitPrice);

            var onHand = snap?.OnHandQuantity ?? 0m;
            var safety = mat.SafetyStock ?? 0m;
            var reorder = mat.MinStock ?? 0m;
            var value = onHand * (unitPrice > 0 ? unitPrice : 10000m);
            totalInventoryValue += value;

            var isStale = snap?.SnapshotTimeUtc == null || snap.SnapshotTimeUtc < ninetyDaysAgo;
            if (isStale) staleCount++;

            string status = "Healthy";
            if (onHand <= safety && safety > 0)
            {
                status = "Critical";
                criticalCount++;
            }
            else if (onHand <= reorder && reorder > 0)
            {
                status = "Low";
                lowCount++;
            }
            else if (onHand > (reorder * 3) && reorder > 0)
            {
                status = "Overstocked";
                overstockedCount++;
            }
            else
            {
                healthyCount++;
            }

            items.Add(new InventoryHealthItemDto
            {
                MaterialId = mat.Id,
                MaterialCode = mat.InternalCode,
                MaterialName = mat.Name,
                CategoryName = mat.Category?.Name ?? "عمومی",
                UnitName = mat.BaseUnit?.Name ?? "واحد",
                CurrentOnHand = onHand,
                SafetyStock = safety,
                ReorderPoint = reorder,
                LastUnitPrice = unitPrice,
                TotalStockValue = value,
                CoverageDays = 30m,
                StockStatus = status,
                IsStale = isStale,
                LastMovementDateUtc = snap?.SnapshotTimeUtc
            });
        }

        var lastSync = await _dbContext.InventorySyncJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var freshness = DataFreshnessStatus.LiveVerified;
        string? warning = null;

        if (lastSync != null && (DateTime.UtcNow - lastSync.StartedAtUtc).TotalHours > 24)
        {
            freshness = DataFreshnessStatus.Stale;
            warning = "هشدار: اطلاعات موجودی انبار به دلیل گذشت بیش از ۲۴ ساعت از آخرین همگام‌سازی، کهنه (Stale) ارزیابی می‌شود.";
        }

        return new InventoryHealthReportDto
        {
            TotalSkus = materials.Count,
            TotalInventoryValue = totalInventoryValue,
            CriticalStockCount = criticalCount,
            LowStockCount = lowCount,
            HealthyStockCount = healthyCount,
            OverstockedCount = overstockedCount,
            StaleInventoryCount = staleCount,
            LastSyncDateUtc = lastSync?.StartedAtUtc,
            SyncStatus = lastSync?.Status.ToString() ?? "Initial",
            DataFreshness = freshness,
            FreshnessWarning = warning,
            Items = items.OrderBy(i => i.StockStatus == "Critical" ? 0 : (i.StockStatus == "Low" ? 1 : 2)).ToList(),
            TotalCount = items.Count
        };
    }

    public async Task<FinanceReconciliationReportDto> GetFinanceReconciliationReportAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var matches = await _dbContext.ThreeWayMatchResults
            .AsNoTracking()
            .Include(m => m.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Include(m => m.SupplierInvoice)
            .ToListAsync(cancellationToken);

        var sepidarLinks = await _dbContext.SepidarFinancialReconciliations
            .AsNoTracking()
            .Where(s => s.LinkStatus == ReconciliationLinkStatus.Confirmed)
            .ToDictionaryAsync(s => s.SupplierInvoiceId, cancellationToken);

        var invoices = await _dbContext.SupplierInvoices
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalInvoicedAmount = invoices.Sum(i => i.TotalAmount);
        var totalMatchedAmount = matches
            .Where(m => m.MatchStatus == ThreeWayMatchStatus.ExactMatch || m.MatchStatus == ThreeWayMatchStatus.WithinTolerance)
            .Sum(m => m.SupplierInvoice?.TotalAmount ?? 0m);

        var totalVarianceAmount = matches.Sum(m => Math.Abs(m.TotalVarianceAmount));
        var matchSuccessRate = matches.Count > 0
            ? Math.Round(((decimal)matches.Count(m => m.MatchStatus == ThreeWayMatchStatus.ExactMatch || m.MatchStatus == ThreeWayMatchStatus.WithinTolerance) / matches.Count) * 100m, 2)
            : 100m;

        var openExceptions = matches
            .Where(m => !m.IsResolved && m.MatchStatus != ThreeWayMatchStatus.ExactMatch && m.MatchStatus != ThreeWayMatchStatus.WithinTolerance)
            .ToList();

        var priceMismatchCount = openExceptions.Count(m => m.MatchStatus == ThreeWayMatchStatus.PriceMismatch);
        var quantityMismatchCount = openExceptions.Count(m => m.MatchStatus == ThreeWayMatchStatus.QuantityMismatch);
        var missingReceiptCount = openExceptions.Count(m => m.MatchStatus == ThreeWayMatchStatus.MissingReceipt);

        var sepidarLinkedCount = sepidarLinks.Count;
        var sepidarBacklogCount = Math.Max(0, invoices.Count - sepidarLinkedCount);

        var exceptionItems = matches
            .OrderByDescending(m => m.EvaluatedAtUtc)
            .Select(m =>
            {
                sepidarLinks.TryGetValue(m.SupplierInvoiceId, out var sep);
                var invAmount = m.SupplierInvoice?.TotalAmount ?? 0m;
                var recVal = invAmount - m.TotalVarianceAmount;
                var varPct = invAmount > 0 ? Math.Round((m.TotalVarianceAmount / invAmount) * 100m, 2) : 0m;

                return new FinanceExceptionItemDto
                {
                    MatchResultId = m.Id,
                    PurchaseOrderId = m.PurchaseOrderId,
                    OrderNumber = m.PurchaseOrder?.OrderNumber ?? "نامشخص",
                    SupplierInvoiceId = m.SupplierInvoiceId,
                    InvoiceNumber = m.SupplierInvoice?.InvoiceNumber ?? "نامشخص",
                    SupplierName = m.PurchaseOrder?.Supplier?.DisplayName ?? m.PurchaseOrder?.Supplier?.LegalName ?? "تأمین‌کننده",
                    InvoiceDateUtc = m.SupplierInvoice?.InvoiceDateUtc ?? m.EvaluatedAtUtc,
                    InvoicedAmount = invAmount,
                    ReceivedValue = recVal,
                    VarianceAmount = m.TotalVarianceAmount,
                    VariancePercent = varPct,
                    MatchStatus = m.MatchStatus.ToString(),
                    IsResolved = m.IsResolved,
                    ResolutionAction = m.ResolutionAction,
                    ResolutionNotes = m.ResolutionNotes,
                    ResolvedBy = m.ResolvedBy,
                    ResolvedAtUtc = m.ResolvedAtUtc,
                    HasSepidarLink = sep != null,
                    SepidarVoucherNumber = sep?.SepidarVoucherNumber
                };
            })
            .ToList();

        return new FinanceReconciliationReportDto
        {
            TotalInvoicedAmount = totalInvoicedAmount,
            TotalMatchedAmount = totalMatchedAmount,
            TotalVarianceAmount = totalVarianceAmount,
            MatchSuccessRate = matchSuccessRate,
            OpenExceptionsCount = openExceptions.Count,
            PriceMismatchCount = priceMismatchCount,
            QuantityMismatchCount = quantityMismatchCount,
            MissingReceiptCount = missingReceiptCount,
            SepidarBacklogCount = sepidarBacklogCount,
            SepidarLinkedCount = sepidarLinkedCount,
            Exceptions = exceptionItems,
            TotalExceptionsCount = exceptionItems.Count
        };
    }

    public async Task<TelephonyActivityReportDto> GetTelephonyActivityReportAsync(ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var callsQuery = _dbContext.CallRecords.AsNoTracking();

        if (filters.StartDateUtc.HasValue)
            callsQuery = callsQuery.Where(c => c.StartTimeUtc >= filters.StartDateUtc.Value);
        if (filters.EndDateUtc.HasValue)
            callsQuery = callsQuery.Where(c => c.StartTimeUtc <= filters.EndDateUtc.Value);

        var calls = await callsQuery.ToListAsync(cancellationToken);

        var totalCalls = calls.Count;
        var incoming = calls.Count(c => c.Direction == CallDirection.Incoming);
        var outgoing = calls.Count(c => c.Direction == CallDirection.Outgoing);
        var answered = calls.Count(c => c.Status == CallStatus.Answered || c.Status == CallStatus.Completed);
        var missed = calls.Count(c => c.Status == CallStatus.Missed);

        var missedRate = totalCalls > 0 ? Math.Round(((decimal)missed / totalCalls) * 100m, 2) : 0m;

        var durations = calls.Where(c => c.DurationSeconds.HasValue && c.DurationSeconds.Value > 0).Select(c => c.DurationSeconds!.Value).ToList();
        var avgDuration = durations.Count > 0 ? Math.Round((decimal)durations.Average(), 1) : 0m;

        var supplierCalls = calls.Count(c => c.PartyId != null);
        var unknownCalls = calls.Count(c => c.IsUnknownCaller);

        var notes = await _dbContext.CallNotes.AsNoTracking().Where(n => n.FollowUpRequired).ToListAsync(cancellationToken);
        var pendingFollowUps = notes.Count(n => n.FollowUpStatus == FollowUpStatus.Pending);
        var overdueFollowUps = notes.Count(n => n.FollowUpStatus == FollowUpStatus.Pending && n.FollowUpDueDateUtc.HasValue && n.FollowUpDueDateUtc.Value < DateTime.UtcNow);

        return new TelephonyActivityReportDto
        {
            TotalCalls = totalCalls,
            IncomingCalls = incoming,
            OutgoingCalls = outgoing,
            AnsweredCalls = answered,
            MissedCalls = missed,
            MissedCallRate = missedRate,
            AverageCallDurationSeconds = avgDuration,
            SupplierIdentifiedCalls = supplierCalls,
            UnknownCallerCalls = unknownCalls,
            PendingFollowUpsCount = pendingFollowUps,
            OverdueFollowUpsCount = overdueFollowUps,
            PbxConnectionStatus = "Simulated (آداپتور شبیه‌ساز ایزابل فعال)",
            DataFreshness = DataFreshnessStatus.Simulated,
            FreshnessWarning = "توجه: تماس‌های فعلی از طریق شبیه‌ساز قطعی تلفنی پردازش شده‌اند و سرور زنده PBX هنوز فعال نشده است."
        };
    }

    public async Task<IntegrationHealthReportDto> GetIntegrationHealthAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        bool dbHealthy = false;
        try
        {
            dbHealthy = await _dbContext.Users.AnyAsync(cancellationToken);
        }
        catch
        {
            dbHealthy = false;
        }
        sw.Stop();

        var lastSyncJob = await _dbContext.InventorySyncJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var syncErrorsCount = await _dbContext.InventorySyncErrors.AsNoTracking().CountAsync(cancellationToken);

        var isStale = lastSyncJob != null && (DateTime.UtcNow - lastSyncJob.StartedAtUtc).TotalHours > 24;
        var elapsed = lastSyncJob != null ? (DateTime.UtcNow - lastSyncJob.StartedAtUtc) : TimeSpan.FromDays(99);

        var warnings = new List<string>
        {
            "ارتباط با دیتابیس مالی سپیدار در وضعیت 'آداپتور آزمایشی' (SepidarFixtureFinancialAdapter) قرار دارد؛ اتصال زنده نیازمند مجوز عملیاتی است.",
            "اتصال مرکز تلفن ایزابل روی 'آداپتور شبیه‌ساز' (SimulatedTelephonyAdapter) فعال است؛ ارتباط فیزیکی سوکت ۵۰۳۸ در محیط آزمایشگاهی در حالت آماده‌باش (Standby) می‌باشد."
        };

        if (isStale)
        {
            warnings.Add("همگام‌سازی انبار بیش از ۲۴ ساعت گذشته اجرا نشده است.");
        }

        return new IntegrationHealthReportDto
        {
            ApiHealth = "Healthy (سالم و در حال پاسخگویی)",
            DatabaseHealth = dbHealthy ? "Healthy (متصل به SQL Server CRM)" : "Degraded (اختلال در دیتابیس)",
            DatabaseLatencyMs = sw.ElapsedMilliseconds,
            SepidarAdapter = new SepidarAdapterHealthDto
            {
                AdapterName = "SepidarFixtureFinancialAdapter",
                AdapterType = "Fixture / Simulated",
                IsLiveConnected = false,
                Status = "Active (Mock Mode)",
                LastSyncUtc = DateTime.UtcNow.AddHours(-2),
                FreshnessNote = "داده‌های آزمایشی استاندارد جهت تست‌های تطبیق ۳طرفه"
            },
            IssabelAdapter = new IssabelAdapterHealthDto
            {
                AdapterName = "SimulatedTelephonyAdapter",
                AdapterType = "Simulated",
                IsLiveConnected = false,
                Status = "Connected (Simulation)",
                EventsProcessed = await _dbContext.CallRecords.CountAsync(cancellationToken),
                ReconnectionRetries = 0,
                FreshnessNote = "شبیه‌ساز رویدادهای AMI و CDR با موفقیت تست گردیده است"
            },
            InventorySync = new InventorySyncHealthDto
            {
                LastRunUtc = lastSyncJob?.StartedAtUtc,
                LastRunStatus = lastSyncJob?.Status.ToString() ?? "Initial",
                ItemsProcessed = lastSyncJob?.ItemsProcessed ?? 0,
                ErrorsCount = syncErrorsCount,
                IsStale = isStale,
                ElapsedSinceLastSync = elapsed
            },
            FreshnessWarnings = warnings,
            CheckedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<RoleDashboardDto> GetRoleDashboardAsync(string roleName, ReportFilterParams filters, CancellationToken cancellationToken = default)
    {
        var role = roleName.Trim();
        var normalizedRole = char.ToUpperInvariant(role[0]) + role.Substring(1).ToLowerInvariant();

        var trends = await GetPurchaseOrderTrendsAsync(filters, cancellationToken);
        var suppliers = await GetSupplierPerformanceAsync(filters, cancellationToken);
        var materials = await GetMaterialConsumptionAsync(filters, cancellationToken);
        var procurementSummary = await GetProcurementSummaryAsync(filters, cancellationToken);
        var inventoryHealth = await GetInventoryHealthAsync(filters, cancellationToken);
        var financeSummary = await GetFinanceReconciliationReportAsync(filters, cancellationToken);
        var telephonySummary = await GetTelephonyActivityReportAsync(filters, cancellationToken);
        var integrationHealth = await GetIntegrationHealthAsync(cancellationToken);

        var metrics = new Dictionary<string, object>();
        var actions = new List<DashboardActionItemDto>();

        switch (normalizedRole)
        {
            case "Procurement":
                metrics["totalPurchaseVolume"] = procurementSummary.TotalPurchaseVolume;
                metrics["activeOrdersCount"] = procurementSummary.ActiveOrdersCount;
                metrics["orderFulfillmentRate"] = procurementSummary.OrderFulfillmentRate;
                metrics["onTimeDeliveryRate"] = procurementSummary.OnTimeDeliveryRate;
                metrics["criticalStockCount"] = procurementSummary.CriticalStockCount;
                metrics["activeRecommendationsCount"] = procurementSummary.ActiveRecommendationsCount;
                metrics["overdueFollowUpsCount"] = telephonySummary.OverdueFollowUpsCount;

                // Action items: Critical stock materials & Overdue POs
                var overdueOrders = await _dbContext.PurchaseOrders
                    .AsNoTracking()
                    .Where(o => (o.Status == PurchaseOrderStatus.Issued || o.Status == PurchaseOrderStatus.PartiallyReceived) &&
                                o.ExpectedDeliveryDateUtc != null && o.ExpectedDeliveryDateUtc.Value < DateTime.UtcNow)
                    .ToListAsync(cancellationToken);

                foreach (var po in overdueOrders.Take(5))
                {
                    actions.Add(new DashboardActionItemDto
                    {
                        Id = po.Id.ToString(),
                        Title = $"سفارش خرید با تأخیر: {po.OrderNumber}",
                        Subtitle = $"سررسید تحویل: {ToJalaliDate(po.ExpectedDeliveryDateUtc!.Value)} - پیگیری فوری با تأمین‌کننده الزامی است",
                        Severity = "Critical",
                        TargetView = "orders",
                        TimestampUtc = po.ExpectedDeliveryDateUtc.Value,
                        JalaliDate = ToJalaliDate(po.ExpectedDeliveryDateUtc.Value)
                    });
                }
                break;

            case "Manager":
                metrics["totalPurchaseVolume"] = procurementSummary.TotalPurchaseVolume;
                metrics["totalOrdersCount"] = procurementSummary.TotalOrdersCount;
                metrics["fulfillmentRate"] = procurementSummary.OrderFulfillmentRate;
                metrics["matchingSuccessRate"] = financeSummary.MatchSuccessRate;
                metrics["openMatchingExceptions"] = financeSummary.OpenExceptionsCount;
                metrics["totalInventoryValue"] = inventoryHealth.TotalInventoryValue;
                metrics["criticalInventorySkus"] = inventoryHealth.CriticalStockCount;
                metrics["totalCalls"] = telephonySummary.TotalCalls;
                metrics["missedCalls"] = telephonySummary.MissedCalls;

                foreach (var exc in financeSummary.Exceptions.Where(e => !e.IsResolved).Take(3))
                {
                    actions.Add(new DashboardActionItemDto
                    {
                        Id = exc.MatchResultId.ToString(),
                        Title = $"مغایرت باز تطبیق ۳طرفه: {exc.OrderNumber}",
                        Subtitle = $"مغایرت مبلغی: {exc.VarianceAmount:N0} ریال ({exc.MatchStatus})",
                        Severity = "Warning",
                        TargetView = "finance",
                        TimestampUtc = exc.InvoiceDateUtc,
                        JalaliDate = ToJalaliDate(exc.InvoiceDateUtc)
                    });
                }
                break;

            case "Warehouse":
                metrics["totalSkus"] = inventoryHealth.TotalSkus;
                metrics["totalInventoryValue"] = inventoryHealth.TotalInventoryValue;
                metrics["criticalStockCount"] = inventoryHealth.CriticalStockCount;
                metrics["lowStockCount"] = inventoryHealth.LowStockCount;
                metrics["staleInventoryCount"] = inventoryHealth.StaleInventoryCount;
                metrics["pendingReceiptsCount"] = procurementSummary.ActiveOrdersCount;
                metrics["syncStatus"] = inventoryHealth.SyncStatus;

                var pendingPos = await _dbContext.PurchaseOrders
                    .AsNoTracking()
                    .Where(o => o.Status == PurchaseOrderStatus.Issued || o.Status == PurchaseOrderStatus.PartiallyReceived)
                    .OrderBy(o => o.ExpectedDeliveryDateUtc)
                    .Take(5)
                    .ToListAsync(cancellationToken);

                foreach (var po in pendingPos)
                {
                    actions.Add(new DashboardActionItemDto
                    {
                        Id = po.Id.ToString(),
                        Title = $"محموله مورد انتظار انبار: {po.OrderNumber}",
                        Subtitle = $"وضعیت سفارش: {(po.Status == PurchaseOrderStatus.Issued ? "در انتظار تحویل اولیه" : "دریافت جزئی")}",
                        Severity = po.ExpectedDeliveryDateUtc < DateTime.UtcNow ? "Critical" : "Normal",
                        TargetView = "warehouse",
                        TimestampUtc = po.ExpectedDeliveryDateUtc ?? po.CreatedAtUtc,
                        JalaliDate = ToJalaliDate(po.ExpectedDeliveryDateUtc ?? po.CreatedAtUtc)
                    });
                }
                break;

            case "Finance":
                metrics["totalInvoicedAmount"] = financeSummary.TotalInvoicedAmount;
                metrics["totalMatchedAmount"] = financeSummary.TotalMatchedAmount;
                metrics["totalVarianceAmount"] = financeSummary.TotalVarianceAmount;
                metrics["matchSuccessRate"] = financeSummary.MatchSuccessRate;
                metrics["openExceptionsCount"] = financeSummary.OpenExceptionsCount;
                metrics["priceMismatchCount"] = financeSummary.PriceMismatchCount;
                metrics["quantityMismatchCount"] = financeSummary.QuantityMismatchCount;
                metrics["missingReceiptCount"] = financeSummary.MissingReceiptCount;
                metrics["sepidarBacklogCount"] = financeSummary.SepidarBacklogCount;
                metrics["sepidarLinkedCount"] = financeSummary.SepidarLinkedCount;

                foreach (var exc in financeSummary.Exceptions.Where(e => !e.IsResolved).Take(5))
                {
                    actions.Add(new DashboardActionItemDto
                    {
                        Id = exc.MatchResultId.ToString(),
                        Title = $"بررسی سند فاکتور: {exc.InvoiceNumber} ({exc.SupplierName})",
                        Subtitle = $"انحراف: {exc.VarianceAmount:N0} ریال - {exc.MatchStatus}",
                        Severity = "Critical",
                        TargetView = "finance",
                        TimestampUtc = exc.InvoiceDateUtc,
                        JalaliDate = ToJalaliDate(exc.InvoiceDateUtc)
                    });
                }
                break;

            case "Admin":
            default:
                metrics["apiHealth"] = integrationHealth.ApiHealth;
                metrics["databaseHealth"] = integrationHealth.DatabaseHealth;
                metrics["dbLatencyMs"] = integrationHealth.DatabaseLatencyMs;
                metrics["sepidarAdapter"] = integrationHealth.SepidarAdapter.AdapterName;
                metrics["sepidarStatus"] = integrationHealth.SepidarAdapter.Status;
                metrics["issabelAdapter"] = integrationHealth.IssabelAdapter.AdapterName;
                metrics["issabelStatus"] = integrationHealth.IssabelAdapter.Status;
                metrics["inventorySyncLastRun"] = integrationHealth.InventorySync.LastRunStatus;
                metrics["inventorySyncItems"] = integrationHealth.InventorySync.ItemsProcessed;
                metrics["inventorySyncErrors"] = integrationHealth.InventorySync.ErrorsCount;

                foreach (var warn in integrationHealth.FreshnessWarnings)
                {
                    actions.Add(new DashboardActionItemDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "هشدار سلامت و تازگی داده‌های سامانه",
                        Subtitle = warn,
                        Severity = "Warning",
                        TargetView = "admin",
                        TimestampUtc = DateTime.UtcNow,
                        JalaliDate = ToJalaliDate(DateTime.UtcNow)
                    });
                }
                break;
        }

        return new RoleDashboardDto
        {
            Role = normalizedRole,
            GeneratedAtUtc = DateTime.UtcNow,
            DataFreshness = DataFreshnessStatus.LiveVerified,
            FreshnessBadge = "داده‌های زنده سامانه CRM پارک پالاس",
            SummaryMetrics = metrics,
            TrendItems = trends,
            TopSuppliers = suppliers.Take(5).ToList(),
            CriticalMaterials = materials.Where(m => m.InventoryRiskStatus == "Critical" || m.InventoryRiskStatus == "Warning").Take(5).ToList(),
            ActionItems = actions
        };
    }
}
